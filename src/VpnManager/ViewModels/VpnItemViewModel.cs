using System.Windows.Input;
using VpnManager.Localization;
using VpnManager.Ras;

namespace VpnManager.ViewModels;

public class VpnItemViewModel : ViewModelBase {
	private readonly IRasService _ras;
	private int _pending; // локальные операции RAS в процессе (подключение, разрыв)
	private CancellationTokenSource? _cts; // отмена текущей попытки подключения

	private VpnState _state;
	private string? _durationText;
	private string? _errorText;

	/// <summary>Сколько ошибка остаётся видимой.</summary>
	public static readonly TimeSpan ErrorLifetime = TimeSpan.FromSeconds(30);

	private readonly TimeProvider _time;
	private DateTimeOffset _errorAt;

	public VpnItemViewModel(string name, IRasService ras, TimeProvider? time = null) {
		Name = name;
		_ras = ras;
		_time = time ?? TimeProvider.System;
		ToggleCommand = new RelayCommand(ToggleAsync);
	}

	public string Name { get; }
	public ICommand ToggleCommand { get; }

	public VpnState State {
		get => _state;
		private set {
			if (!Set(ref _state, value)) return;
			Raise(nameof(StatusText));
			Raise(nameof(IsBusy));
		}
	}

	public string StatusText => State switch {
		VpnState.Connecting => Loc.T("Connecting"),
		VpnState.Connected => Loc.T("Connected"),
		VpnState.Error => Loc.T("Error"),
		_ => Loc.T("Disconnected")
	};

	/// <summary>Язык сменился: обновить локализованные строки.</summary>
	public void RefreshStrings() {
		Raise(nameof(StatusText));
	}

	public string? DurationText {
		get => _durationText;
		private set => Set(ref _durationText, value);
	}

	public string? ErrorText {
		get => _errorText;
		private set => Set(ref _errorText, value);
	}

	public bool IsBusy => State == VpnState.Connecting;

	/// <summary>Применить состояние из RAS (вызывается при опросе).</summary>
	public void Apply(VpnEntry entry) {
		if (_pending > 0) return; // локальное состояние главнее данных опроса

		switch (entry.State) {
			case VpnState.Connected:
				ErrorText = null;
				State = VpnState.Connected;
				DurationText = DurationFormatter.Format(entry.Duration ?? TimeSpan.Zero);
				break;
			case VpnState.Connecting:
				State = VpnState.Connecting;
				DurationText = null;
				break;
			default:
				DurationText = null;
				if (State == VpnState.Error) {
					// Ошибка видна ErrorLifetime после неудачи или до следующей попытки.
					if (_time.GetUtcNow() - _errorAt < ErrorLifetime) return;
					ErrorText = null;
				}

				State = VpnState.Disconnected;
				break;
		}
	}

	public async Task ToggleAsync() {
		if (State == VpnState.Connecting) {
			// Повторный щелчок во время подключения = отмена попытки.
			if (_cts != null) {
				// UI не ждёт бэкенд: попытка снимается с учёта, результат поздней отмены игнорируется.
				var cancelled = _cts;
				_cts = null;
				State = VpnState.Disconnected;
				cancelled.Cancel();
			} else {
				await Task.Run(() => _ras.Disconnect(Name)); // дозвон начат не нами
			}

			return;
		}

		if (State == VpnState.Connected) {
			// RasHangUp синхронный и может занять секунды: в фон, UI сразу показывает «Отключено».
			// _pending не даёт опросу вернуть «Подключено», пока разрыв не завершён.
			_pending++;
			DurationText = null;
			State = VpnState.Disconnected;
			try {
				await Task.Run(() => _ras.Disconnect(Name));
			} finally {
				_pending--;
			}

			return;
		}

		_pending++;
		ErrorText = null;
		State = VpnState.Connecting;
		var cts = new CancellationTokenSource();
		_cts = cts;
		try {
			RasResult result;
			try {
				result = await _ras.ConnectAsync(Name, cts.Token);
			} catch (OperationCanceledException) {
				result = new RasResult(false, null, false, true);
			} catch (Exception ex) {
				result = new RasResult(false, ex.Message, false);
			} // async void в команде не должен ронять приложение

			// Попытку отменили (или заменили новой): результат устарел.
			if (!ReferenceEquals(_cts, cts)) return;
			_cts = null;

			if (result.Cancelled) {
				State = VpnState.Disconnected;
			} else if (result.Success) {
				DurationText = DurationFormatter.Format(TimeSpan.Zero);
				State = VpnState.Connected;
			} else {
				ErrorText = result.CredentialsRequired
					? Loc.T("CredentialsRequired")
					: result.ErrorText ?? Loc.T("ConnectFailed");
				_errorAt = _time.GetUtcNow();
				State = VpnState.Error;
			}
		} finally {
			cts.Dispose();
			_pending--;
		}
	}
}