using System.Runtime.CompilerServices;
using VpnManager.Localization;
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
