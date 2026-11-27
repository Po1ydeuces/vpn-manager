using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace VpnManager.Tray;

/// <summary>Иконки трея: щит со скважиной (как у VPN в Windows 11) — серый (нет подключений) и светящийся зелёный (есть активное).</summary>
public static class TrayIcons {
	// Контур щита из системного шрифта значков Windows 11.
	private const string Shield = "";

	/// <summary>Неактивный значок: белый на тёмной панели задач, тёмный на светлой (как системные значки).</summary>
	private static Color OffColor() {
		try {
			using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			if (key?.GetValue("SystemUsesLightTheme") is int light && light == 1) return Color.FromArgb(40, 40, 40);
		} catch { }
		return Color.FromArgb(240, 240, 240);
	}

	public static Icon Create(bool active) {
		const int size = 32;
		using var bmp = new Bitmap(size, size);
		using (var g = Graphics.FromImage(bmp)) {
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			using var font = new Font("Segoe Fluent Icons", 28, FontStyle.Regular, GraphicsUnit.Pixel);
			using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

			void Draw(Brush brush, float dx, float dy) {
				g.DrawString(Shield, font, brush, new RectangleF(dx, dy, size, size), fmt);
				g.FillEllipse(brush, 13.3f + dx, 11.2f + dy, 5.4f, 5.4f);
				g.FillRectangle(brush, 15.2f + dx, 14.9f + dy, 1.6f, 5.6f);
			}

			if (active) {
				// Свечение: ореол из смещённых полупрозрачных копий.
				using var glow = new SolidBrush(Color.FromArgb(55, 60, 255, 120));
				for (var r = 2; r >= 1; r--) {
					for (var a = 0; a < 360; a += 45) {
						Draw(glow, (float) (r * Math.Cos(a * Math.PI / 180)), (float) (r * Math.Sin(a * Math.PI / 180)));
					}
				}
			}
			using var main = new SolidBrush(active ? Color.FromArgb(70, 255, 130) : OffColor());
			Draw(main, 0, 0);
		}

		var hIcon = bmp.GetHicon();
		try {
			using var temp = Icon.FromHandle(hIcon);
			return (Icon) temp.Clone(); // копия не зависит от hIcon
		} finally {
			DestroyIcon(hIcon);
		}
	}

	/// <summary>Та же иконка как ImageSource — для значка окна WPF.</summary>
	public static System.Windows.Media.ImageSource CreateImageSource(bool active) {
		using var icon = Create(active);
		var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
			icon.Handle, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
		source.Freeze();
		return source;
	}

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr hIcon);
}
