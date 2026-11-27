using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace VpnManager.Tray;

/// <summary>Иконки трея рисуются кодом: серая (нет подключений) и зелёная (есть активное).</summary>
public static class TrayIcons {
	public static Icon Create(bool active) {
		const int size = 32;
		using var bmp = new Bitmap(size, size);
		using (var g = Graphics.FromImage(bmp)) {
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			using var fill = new SolidBrush(active ? Color.FromArgb(46, 158, 79) : Color.FromArgb(128, 128, 128));
			g.FillEllipse(fill, 1, 1, size - 2, size - 2);
			using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
			using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
			g.DrawString("V", font, Brushes.White, new RectangleF(0, 1, size, size), fmt);
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