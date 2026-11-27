using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace VpnManager.Views;

public static class ThemeResources {
	/// <summary>Тёмная ли тема после последнего Load.</summary>
	public static bool IsDark { get; private set; }

	/// <summary>Цвет фона панели после последнего Load.</summary>
	public static Color PanelColor { get; private set; }

	/// <summary>
	/// Словарь светлой или тёмной темы. theme: "light", "dark" или null (по системной).
	/// acrylic: фон делается полупрозрачным, чтобы под ним был виден системный акрил окна.
	/// </summary>
	public static ResourceDictionary Load(string? theme, bool acrylic = false) {
		var systemLight = Registry.GetValue(
			@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
			"AppsUseLightTheme", 1) is not int v || v != 0;
		var light = theme switch {
			"light" => true,
			"dark" => false,
			_ => systemLight
		};

		var baseTheme = new ResourceDictionary {
			Source = new Uri($"/VpnManager;component/Views/Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative)
		};
		IsDark = !light;
		PanelColor = ((SolidColorBrush)baseTheme["PanelBg"]).Color;

		// Цвет панели задач, если Windows красит её акцентом: фон окна, рамка и подсветка строки.
		var taskbar = TaskbarColor();
		if (taskbar == null && !acrylic) return baseTheme;
		var bg = taskbar ?? ((SolidColorBrush)baseTheme["PanelBg"]).Color;
		var result = new ResourceDictionary();
		result.MergedDictionaries.Add(baseTheme);
		var dark = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) < 140;
		IsDark = dark;
		var edge = dark ? Colors.White : Colors.Black;
		PanelColor = bg;
		// В режиме акрила тонировку рисует система (WindowBackdrop.SetTint), WPF-фон почти прозрачный.
		result["PanelBg"] = Frozen(bg, acrylic ? (byte)0x01 : (byte)0xFF);
		result["PanelBorder"] = Frozen(Blend(bg, edge, 0.2));
		result["RowHover"] = Frozen(Blend(bg, edge, 0.12));
		if (taskbar != null) {
			result["TextMain"] = Frozen(dark ? Color.FromRgb(0xF2, 0xF2, 0xF2) : Color.FromRgb(0x1A, 0x1A, 0x1A));
			result["TextMuted"] = Frozen(Blend(bg, edge, 0.65));
		}
		return result;
	}

	/// <summary>Цвет панели задач при включённом «Цвет элементов на панели задач»; иначе null.</summary>
	private static Color? TaskbarColor() {
		const string personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
		const string accent = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
		if (Registry.GetValue(personalize, "ColorPrevalence", 0) is not int p || p == 0) return null;

		var taskbarLight = Registry.GetValue(personalize, "SystemUsesLightTheme", 0) is int l && l != 0;
		if (!taskbarLight && Registry.GetValue(accent, "AccentPalette", null) is byte[] { Length: >= 24 } pal)
			return Color.FromRgb(pal[20], pal[21], pal[22]); // 6-й оттенок палитры: тёмная панель задач

		if (Registry.GetValue(accent, "AccentColorMenu", null) is int abgr)
			return Color.FromRgb((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16));
		return null;
	}

	private static Color Blend(Color bg, Color fg, double k) => Color.FromRgb(
		(byte)(bg.R + (fg.R - bg.R) * k),
		(byte)(bg.G + (fg.G - bg.G) * k),
		(byte)(bg.B + (fg.B - bg.B) * k));

	private static SolidColorBrush Frozen(Color c, byte alpha = 0xFF) {
		var brush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
		brush.Freeze();
		return brush;
	}
}
