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
	private bool _isEmpty = true;
	private bool _anyConnected;

	public MainViewModel(IRasService ras, TimeProvider? time = null, ISettingsStore? store = null) {
		_ras = ras;
		_time = time ?? TimeProvider.System;
		_store = store ?? new MemorySettingsStore();
		_settings = _store.Load();
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

		foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)) {
			var item = Items.FirstOrDefault(x => x.Name == entry.Name);
			if (item == null) {
				item = new VpnItemViewModel(entry.Name, _ras, _time);
				Items.Add(item);
			}

			item.Apply(entry);
		}

		// Привести порядок к сортировке по имени, не пересоздавая элементы.
		var sorted = Items.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		for (var i = 0; i < sorted.Count; i++) {
			var cur = Items.IndexOf(sorted[i]);
			if (cur != i) Items.Move(cur, i);
		}

		IsEmpty = Items.Count == 0;
		AnyConnected = Items.Any(x => x.State == VpnState.Connected);
	}
}