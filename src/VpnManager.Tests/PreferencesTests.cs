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
		var vm = new MainViewModel(ras, store: store);
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

		var second = Create(ras, store);

		Assert.Equal("B", second.Items[0].Name);
		Assert.True(second.Items[0].IsPinned);
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
}
