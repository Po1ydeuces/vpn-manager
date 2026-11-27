using System.Windows;
using Microsoft.Win32;

namespace VpnManager.Views;

public static class ThemeResources {
	/// <summary>Словарь светлой или тёмной темы. theme: "light", "dark" или null (по системной).</summary>
	public static ResourceDictionary Load(string? theme) {
		var systemLight = Registry.GetValue(
			@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
			"AppsUseLightTheme", 1) is not int v || v != 0;
		var light = theme switch {
			"light" => true,
			"dark" => false,
			_ => systemLight
		};

		return new ResourceDictionary {
			Source = new Uri($"/VpnManager;component/Views/Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative)
		};
	}
}
