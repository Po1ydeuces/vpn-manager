using System.Collections.ObjectModel;
using VpnManager.Localization;
using VpnManager.Ras;
using VpnManager.Settings;

namespace VpnManager.ViewModels;

public class MainViewModel : ViewModelBase {
	private readonly IRasService _ras;
	private readonly TimeProvider _time;
	private readonly ISettingsStore _store;
	private readonly AppSettings _settings;
	private readonly TimeSpan? _reconnectDelay;
	private bool _isEmpty = true;
	private bool _anyConnected;

	public MainViewModel(IRasService ras, TimeProvider? time = null, ISettingsStore? store = null,
		TimeSpan? reconnectDelay = null) {
		_ras = ras;
		_time = time ?? TimeProvider.System;
		_store = store ?? new MemorySettingsStore();
		_settings = _store.Load();
		_reconnectDelay = reconnectDelay;
	}

	public ObservableCollection<VpnItemViewModel> Items { get; } = new();

	public bool IsEmpty {
		get => _isEmpty;
		private set => Set(ref _isEmpty, value);
	}

	public bool AnyConnected {
		get => _anyConnected;
		private set => Set(ref _anyConnected, value);
	}

	public string EmptyText => Loc.T("NoVpn");

	/// <summary>"en", "ru" или null (по системному).</summary>
	public string? Language => _settings.Language;

	public bool HotkeyEnabled => _settings.HotkeyEnabled;

	public void SetHotkeyEnabled(bool enabled) {
		_settings.HotkeyEnabled = enabled;
		_store.Save(_settings);
	}

	public void SetLanguage(string? language) {
		_settings.Language = language;
		_store.Save(_settings);
		Loc.SetLanguage(language);
		Raise(nameof(EmptyText));
		foreach (var item in Items) item.RefreshStrings();
	}

	/// <summary>Один шаг опроса на вызывающем потоке (для тестов).</summary>
	public void Refresh() {
		Apply(_ras.GetEntries());
	}

	/// <summary>
	/// Опрос RAS в фоне, применение результата на вызывающем (UI) потоке.
	/// RAS-вызовы могут блокироваться, пока идёт RasDial/RasHangUp, поэтому UI-поток их не делает.
	/// </summary>
	public async Task RefreshAsync() {
		var entries = await Task.Run(_ras.GetEntries);
		Apply(entries);
	}

	private void Apply(IReadOnlyList<VpnEntry> entries) {
		for (var i = Items.Count - 1; i >= 0; i--)
			if (!entries.Any(e => e.Name == Items[i].Name))
				Items.RemoveAt(i);

		foreach (var entry in entries) {
			var item = Items.FirstOrDefault(x => x.Name == entry.Name);
			if (item == null) {
				item = new VpnItemViewModel(entry.Name, _ras, _time, _reconnectDelay) {
					IsPinned = _settings.Pinned.Contains(entry.Name),
					AutoReconnect = _settings.AutoReconnect.Contains(entry.Name),
					ConnectOnStart = _settings.ConnectOnStart.Contains(entry.Name),
					IsHidden = _settings.Hidden.Contains(entry.Name)
				};
				item.PreferencesChanged += OnPreferencesChanged;
				Items.Add(item);
			}

			item.Apply(entry);
		}

		Resort();
		IsEmpty = Items.All(x => x.IsHidden);
		AnyConnected = Items.Any(x => x.State == VpnState.Connected);
	}

	/// <summary>При запуске приложения подключить отключённые профили с флагом «подключать при запуске».</summary>
	public Task ConnectOnStartAsync() {
		var targets = Items.Where(x => x.ConnectOnStart && x.State == VpnState.Disconnected).ToList();
		return Task.WhenAll(targets.Select(x => x.ToggleAsync()));
	}

	/// <summary>Имена скрытых профилей (для меню трея).</summary>
	public IReadOnlyList<string> HiddenNames => Items.Where(x => x.IsHidden).Select(x => x.Name).ToList();

	public void Unhide(string name) {
		var item = Items.FirstOrDefault(x => x.Name == name);
		if (item != null) item.IsHidden = false;
	}

	private void OnPreferencesChanged() {
		_settings.Pinned = Items.Where(x => x.IsPinned).Select(x => x.Name).ToList();
		_settings.AutoReconnect = Items.Where(x => x.AutoReconnect).Select(x => x.Name).ToList();
		_settings.ConnectOnStart = Items.Where(x => x.ConnectOnStart).Select(x => x.Name).ToList();
		_settings.Hidden = Items.Where(x => x.IsHidden).Select(x => x.Name).ToList();
		_store.Save(_settings);
		Resort();
		IsEmpty = Items.All(x => x.IsHidden);
	}

	// Закреплённые сверху, внутри групп по имени. Элементы не пересоздаются, только двигаются.
	private void Resort() {
		var sorted = Items
			.OrderByDescending(x => x.IsPinned)
			.ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
		for (var i = 0; i < sorted.Count; i++) {
			var cur = Items.IndexOf(sorted[i]);
			if (cur != i) Items.Move(cur, i);
		}
	}
}
