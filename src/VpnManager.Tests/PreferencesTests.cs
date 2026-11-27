using System.Runtime.CompilerServices;
using VpnManager.Localization;
using VpnManager.Ras;
using VpnManager.Settings;
using VpnManager.ViewModels;
using static VpnManager.Tests.FakeRasService;

namespace VpnManager.Tests;

internal static class TestLanguage {
	// Тесты проверяют русские строки независимо от языка системы.
	[ModuleInitializer]
	internal static void Init() {
		Loc.SetLanguage("ru");
	}
}

public class PreferencesTests {
	private static MainViewModel Create(FakeRasService ras, ISettingsStore? store = null) {
		var vm = new MainViewModel(ras, store: store, reconnectDelay: TimeSpan.Zero);
		vm.Refresh();
		return vm;
	}

	[Fact]
	public void Pin_MovesItemToTop_AndPersists() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService { Entries = { Off("A"), Off("B"), Off("C") } }, store);

		vm.Items[2].TogglePinCommand.Execute(null);

		Assert.Equal(new[] { "C", "A", "B" }, vm.Items.Select(x => x.Name));
		Assert.Equal(new[] { "C" }, store.Load().Pinned);
	}

	[Fact]
	public void Unpin_RestoresNameOrder() {
		var vm = Create(new FakeRasService { Entries = { Off("A"), Off("B") } });
		var b = vm.Items[1];
		b.IsPinned = true;
		Assert.Equal(new[] { "B", "A" }, vm.Items.Select(x => x.Name));

		b.IsPinned = false;
		Assert.Equal(new[] { "A", "B" }, vm.Items.Select(x => x.Name));
	}

	[Fact]
	public void Settings_AreRestoredOnNewViewModel() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		var first = Create(ras, store);
		var b = first.Items[1];
		b.IsPinned = true;
		b.AutoReconnect = true;

		var second = Create(ras, store);

		Assert.Equal("B", second.Items[0].Name);
		Assert.True(second.Items[0].IsPinned);
		Assert.True(second.Items[0].AutoReconnect);
		Assert.False(second.Items[1].AutoReconnect);
	}

	[Fact]
	public void SetLanguage_ChangesTexts_AndPersists() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService { Entries = { Off("A") } }, store);
		try {
			vm.SetLanguage("en");
			Assert.Equal("Disconnected", vm.Items[0].StatusText);
			Assert.Equal("No VPN connections", vm.EmptyText);
			Assert.Equal("en", store.Load().Language);

			vm.SetLanguage("ru");
			Assert.Equal("Отключено", vm.Items[0].StatusText);
		} finally {
			Loc.SetLanguage("ru");
		}
	}

	[Fact]
	public async Task AutoReconnect_ConnectionDropped_Redials() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Equal(new[] { "A" }, ras.ConnectCalls);
		Assert.Equal(VpnState.Connected, vm.Items[0].State);
	}

	[Fact]
	public async Task AutoReconnect_Off_DoesNotRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task AutoReconnect_ManualDisconnect_DoesNotRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		await vm.Items[0].ToggleAsync();
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task AutoReconnect_GivesUpAfterMaxAttempts() {
		var ras = new FakeRasService {
			Entries = { On("A", TimeSpan.Zero) },
			ConnectBehavior = () => Task.FromResult(new RasResult(false, "boom", false))
		};
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(100);

		Assert.Equal(VpnItemViewModel.MaxReconnectAttempts, ras.ConnectCalls.Count);
		Assert.Equal(VpnState.Error, vm.Items[0].State);
	}

	[Fact]
	public async Task AutoReconnect_UserClickCancelsPendingRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = new MainViewModel(ras, reconnectDelay: TimeSpan.FromMilliseconds(200));
		vm.Refresh();
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		vm.Items[0].AutoReconnect = false; // пользователь выключил до истечения паузы
		await Task.Delay(400);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task ConnectOnStart_ConnectsOnlyFlaggedDisconnectedProfiles() {
		var ras = new FakeRasService { Entries = { Off("A"), Off("B"), On("C", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].ConnectOnStart = true; // A
		vm.Items[2].ConnectOnStart = true; // C уже подключён

		await vm.ConnectOnStartAsync();

		Assert.Equal(new[] { "A" }, ras.ConnectCalls);
	}

	[Fact]
	public void ConnectOnStart_IsPersisted() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		Create(ras, store).Items[1].ConnectOnStart = true;

		var second = Create(ras, store);

		Assert.False(second.Items[0].ConnectOnStart);
		Assert.True(second.Items[1].ConnectOnStart);
	}

	[Fact]
	public void Hotkey_EnabledByDefault_AndPersisted() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService(), store);
		Assert.True(vm.HotkeyEnabled);

		vm.SetHotkeyEnabled(false);

		Assert.False(Create(new FakeRasService(), store).HotkeyEnabled);
	}

	[Fact]
	public void Hide_RemovesFromVisibleSet_PersistsAndUnhides() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		var vm = Create(ras, store);

		vm.Items[0].HideCommand.Execute(null);

		Assert.Equal(new[] { "A" }, vm.HiddenNames);
		Assert.False(vm.IsEmpty);
		Assert.Equal(new[] { "A" }, Create(ras, store).HiddenNames);

		vm.Unhide("A");
		Assert.Empty(vm.HiddenNames);
		Assert.Empty(store.Load().Hidden);
	}

	[Fact]
	public void Hide_AllProfiles_IsEmpty_ButConnectedStateStillTracked() {
		var vm = Create(new FakeRasService { Entries = { On("A", TimeSpan.Zero) } });

		vm.Items[0].IsHidden = true;

		Assert.True(vm.IsEmpty);
		Assert.True(vm.AnyConnected);
	}

	[Fact]
	public void Traffic_ShownWhileConnected_ClearedAfterDisconnect() {
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 2048, 5L * 1024 * 1024) }
		};
		var vm = Create(ras);
		Assert.Equal("↓ 5 MB  ↑ 2 KB", vm.Items[0].TrafficText);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();

		Assert.Null(vm.Items[0].TrafficText);
	}
}
