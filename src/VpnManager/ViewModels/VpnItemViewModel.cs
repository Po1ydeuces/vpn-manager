using System.Windows.Input;
using VpnManager.Localization;
using VpnManager.Ras;

namespace VpnManager.ViewModels;

public class VpnItemViewModel : ViewModelBase {
	private readonly IRasService _ras;
	private int _pending; // локальные операции RAS в процессе (подключение, разрыв)
	private CancellationTokenSource? _cts; // отмена текущей попытки подключения
	private CancellationTokenSource? _reconnectCts; // отмена цикла автопереподключения

	private VpnState _state;
	private string? _durationText;
	private string? _errorText;
	private string? _trafficText;
	private bool _isPinned;
	private bool _autoReconnect;
	private bool _connectOnStart;
	private bool _isHidden;

	/// <summary>Сколько ошибка остаётся видимой.</summary>
	public static readonly TimeSpan ErrorLifetime = TimeSpan.FromSeconds(30);

	/// <summary>Сколько раз пробуем переподключиться после обрыва.</summary>
	public const int MaxReconnectAttempts = 5;

	private readonly TimeProvider _time;
	private readonly TimeSpan _reconnectDelay;
	private DateTimeOffset _errorAt;

	public VpnItemViewModel(string name, IRasService ras, TimeProvider? time = null, TimeSpan? reconnectDelay = null) {
		Name = name;
		_ras = ras;
		_time = time ?? TimeProvider.System;
		_reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(5);
		ToggleCommand = new RelayCommand(ToggleAsync);
		TogglePinCommand = new RelayCommand(() => {
			IsPinned = !IsPinned;
			return Task.CompletedTask;
		});
		HideCommand = new RelayCommand(() => {
			IsHidden = true;
			return Task.CompletedTask;
		});
		ToggleConnectOnStartCommand = new RelayCommand(() => {
			ConnectOnStart = !ConnectOnStart;
			return Task.CompletedTask;
		});
		ToggleAutoReconnectCommand = new RelayCommand(() => {
			AutoReconnect = !AutoReconnect;
			return Task.CompletedTask;
		});
	}

	public string Name { get; }
	public ICommand ToggleCommand { get; }
	public ICommand TogglePinCommand { get; }
	public ICommand ToggleAutoReconnectCommand { get; }
	public ICommand ToggleConnectOnStartCommand { get; }
	public ICommand HideCommand { get; }

	/// <summary>Пользователь изменил закрепление или автопереподключение (нужно сохранить и пересортировать).</summary>
	public event Action? PreferencesChanged;

	public bool IsPinned {
		get => _isPinned;
		set {
			if (!Set(ref _isPinned, value)) return;
			Raise(nameof(PinTip));
			PreferencesChanged?.Invoke();
		}
	}

	public bool AutoReconnect {
		get => _autoReconnect;
		set {
			if (!Set(ref _autoReconnect, value)) return;
			if (!value) CancelReconnect();
			Raise(nameof(AutoReconnectTip));
			PreferencesChanged?.Invoke();
		}
	}

	public string PinTip => Loc.T(IsPinned ? "Unpin" : "Pin");

	/// <summary>Профиль скрыт из списка (опрос и автофункции продолжают работать).</summary>
	public bool IsHidden {
		get => _isHidden;
		set {
			if (!Set(ref _isHidden, value)) return;
			PreferencesChanged?.Invoke();
		}
	}

	public string HideTip => Loc.T("Hide");

	/// <summary>Подключать профиль при запуске приложения.</summary>
	public bool ConnectOnStart {
		get => _connectOnStart;
		set {
			if (!Set(ref _connectOnStart, value)) return;
			Raise(nameof(ConnectOnStartTip));
			PreferencesChanged?.Invoke();
		}
	}

	public string ConnectOnStartTip => Loc.T(ConnectOnStart ? "ConnectOnStartOn" : "ConnectOnStartOff");
	public string AutoReconnectTip => Loc.T(AutoReconnect ? "AutoReconnectOn" : "AutoReconnectOff");

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
		Raise(nameof(PinTip));
		Raise(nameof(AutoReconnectTip));
		Raise(nameof(ConnectOnStartTip));
		Raise(nameof(HideTip));
	}

	public string? DurationText {
		get => _durationText;
		private set => Set(ref _durationText, value);
	}

	/// <summary>«↓ принято ↑ отправлено» за текущее соединение; null, если не подключено.</summary>
	public string? TrafficText {
		get => _trafficText;
		private set => Set(ref _trafficText, value);
	}

	public string? ErrorText {
		get => _errorText;
		private set => Set(ref _errorText, value);
	}

	public bool IsBusy => State == VpnState.Connecting;

	private (DateTimeOffset At, long Received, long Sent)? _sample;
	private double _downRate, _upRate;
	private bool _hasRate;

	private void ResetTraffic() {
		_sample = null;
		_hasRate = false;
		TrafficText = null;
	}

	// Скорость = прирост счётчиков RAS между двумя опросами. Счётчики 32-битные: переполнение учитываем.
	private void UpdateTraffic(VpnEntry entry) {
		var now = _time.GetUtcNow();
		if (_sample is { } s) {
			var seconds = (now - s.At).TotalSeconds;
			if (seconds >= 0.9) {
				_downRate = Delta(entry.BytesReceived, s.Received) / seconds;
				_upRate = Delta(entry.BytesSent, s.Sent) / seconds;
				_hasRate = true;
				_sample = (now, entry.BytesReceived, entry.BytesSent);
			}
		} else {
			_sample = (now, entry.BytesReceived, entry.BytesSent);
		}

		var down = ByteFormatter.Format(entry.BytesReceived);
		var up = ByteFormatter.Format(entry.BytesSent);
		TrafficText = _hasRate
			? $"↓ {down} ({ByteFormatter.FormatRate(_downRate)})  ↑ {up} ({ByteFormatter.FormatRate(_upRate)})"
			: $"↓ {down}  ↑ {up}";
	}

	private static long Delta(long current, long previous) {
		return current >= previous ? current - previous : current + (1L << 32) - previous;
	}

	/// <summary>Событие жизненного цикла соединения (обрыв, ошибка): для уведомлений.</summary>
	public event Action<ConnectionEvent>? Happened;

	private bool _reconnecting; // идёт цикл автопереподключения: промежуточные ошибки тихие

	private void Emit(ConnectionEventKind kind, string? detail = null, TimeSpan? duration = null, bool notify = true) {
		Happened?.Invoke(new ConnectionEvent(_time.GetUtcNow(), Name, kind, duration, detail, notify));
	}

	/// <summary>Применить состояние из RAS (вызывается при опросе).</summary>
	public void Apply(VpnEntry entry) {
		if (_pending > 0) return; // локальное состояние главнее данных опроса

		switch (entry.State) {
			case VpnState.Connected:
				ErrorText = null;
				State = VpnState.Connected;
				DurationText = DurationFormatter.Format(entry.Duration ?? TimeSpan.Zero);
				UpdateTraffic(entry);
				break;
			case VpnState.Connecting:
				State = VpnState.Connecting;
				DurationText = null;
				ResetTraffic();
				break;
			default:
				DurationText = null;
				ResetTraffic();
				var dropped = State == VpnState.Connected;
				if (State == VpnState.Error) {
					// Ошибка видна ErrorLifetime после неудачи или до следующей попытки.
					if (_time.GetUtcNow() - _errorAt < ErrorLifetime) return;
					ErrorText = null;
				}

				State = VpnState.Disconnected;
				// Соединение оборвалось само (наш разрыв ставит Disconnected до опроса).
				if (dropped) {
					Emit(ConnectionEventKind.Dropped);
					if (AutoReconnect) StartReconnect();
				}

				break;
		}
	}

	/// <summary>Щелчок пользователя: отменяет автопереподключение и переключает состояние.</summary>
	public async Task ToggleAsync() {
		CancelReconnect();
		await ToggleCoreAsync();
	}

	private void CancelReconnect() {
		var cts = _reconnectCts;
		_reconnectCts = null;
		cts?.Cancel();
	}

	private void StartReconnect() {
		CancelReconnect();
		var cts = new CancellationTokenSource();
		_reconnectCts = cts;
		_ = ReconnectAsync(cts);
	}

	private async Task ReconnectAsync(CancellationTokenSource cts) {
		_reconnecting = true;
		try {
			for (var attempt = 0; attempt < MaxReconnectAttempts; attempt++) {
				await Task.Delay(_reconnectDelay, _time, cts.Token);
				if (!AutoReconnect || State is VpnState.Connected or VpnState.Connecting) return;
				await ToggleCoreAsync();
				if (State == VpnState.Connected) return;
			}

			_reconnecting = false;
			cts.Token.ThrowIfCancellationRequested(); // отмена пользователем на последней попытке — не «сдались»
			Emit(ConnectionEventKind.GaveUp, ErrorText);
		} catch (OperationCanceledException) {
			// пользователь вмешался или выключил автопереподключение
		} finally {
			_reconnecting = false;
			if (ReferenceEquals(_reconnectCts, cts)) _reconnectCts = null;
			cts.Dispose();
		}
	}

	private async Task ToggleCoreAsync() {
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
			ResetTraffic();
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
				Emit(ConnectionEventKind.Failed, ErrorText, notify: !_reconnecting);
			}
		} finally {
			cts.Dispose();
			_pending--;
		}
	}
}
