using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VpnManager.Tray;

/// <summary>Оформление контекстного меню трея под меню Windows 11: скруглённые углы, мягкая подсветка, Segoe UI Variable.</summary>
public static class ModernMenu {
	private const int DwmwaWindowCornerPreference = 33;
	private const int DwmwcpRound = 2;

	/// <summary>Применяет стиль к меню и ко всем вложенным подменю (вызывать при каждом открытии: список пунктов меняется).</summary>
	public static void Apply(ToolStripDropDown menu, bool light) {
		var renderer = new Renderer(light);
		ApplyTo(menu, renderer, light);
	}

	private static void ApplyTo(ToolStripDropDown dd, Renderer renderer, bool light) {
		dd.Renderer = renderer;
		dd.BackColor = renderer.Back;
		dd.ForeColor = renderer.Text;
		dd.Font = MenuFont();
		dd.Padding = new Padding(4);
		if (dd is ToolStripDropDownMenu ddm) {
			ddm.ShowImageMargin = false;
			ddm.ShowCheckMargin = true;
		}

		if (dd.IsHandleCreated) Round(dd.Handle);
		else if (dd.Tag as string != "round") {
			dd.Tag = "round";
			dd.HandleCreated += (_, _) => Round(dd.Handle);
		}

		foreach (ToolStripItem item in dd.Items) {
			if (item is ToolStripMenuItem mi) {
				mi.Padding = new Padding(2, 5, 2, 5);
				if (mi.HasDropDownItems) ApplyTo(mi.DropDown, renderer, light);
			}
		}
	}

	private static Font MenuFont() {
		var font = new Font("Segoe UI Variable Text", 9.5f);
		if (font.Name == "Segoe UI Variable Text") return font;
		font.Dispose();
		return new Font("Segoe UI", 9.5f);
	}

	private static void Round(IntPtr hwnd) {
		var pref = DwmwcpRound;
		DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref pref, sizeof(int));
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	private sealed class Renderer : ToolStripProfessionalRenderer {
		public Color Back { get; }
		public Color Text { get; }
		private readonly Color _muted;
		private readonly Color _hover;
		private readonly Color _line;

		public Renderer(bool light) {
			Back = light ? Color.FromArgb(0xF9, 0xF9, 0xF9) : Color.FromArgb(0x2C, 0x2C, 0x2C);
			Text = light ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.FromArgb(0xF2, 0xF2, 0xF2);
			_muted = light ? Color.FromArgb(0x9A, 0x9A, 0x9A) : Color.FromArgb(0x74, 0x74, 0x74);
			_hover = light ? Color.FromArgb(0xE9, 0xE9, 0xE9) : Color.FromArgb(0x3D, 0x3D, 0x3D);
			_line = light ? Color.FromArgb(0xDD, 0xDD, 0xDD) : Color.FromArgb(0x48, 0x48, 0x48);
			RoundedEdges = false;
		}

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
			using var b = new SolidBrush(Back);
			e.Graphics.FillRectangle(b, e.AffectedBounds);
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

		protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
			if (!e.Item.Selected || !e.Item.Enabled) return;
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			var r = new Rectangle(0, 1, e.Item.Width - 1, e.Item.Height - 3);
			using var path = RoundRect(r, 4);
			using var b = new SolidBrush(_hover);
			g.FillPath(b, path);
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
			e.TextColor = e.Item.Enabled ? Text : _muted;
			base.OnRenderItemText(e);
		}

		protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			var r = e.ImageRectangle;
			float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
			using var pen = new Pen(Text, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
			g.DrawLines(pen, new[] { new PointF(cx - 4, cy), new PointF(cx - 1.5f, cy + 3), new PointF(cx + 4, cy - 3) });
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			var r = e.ArrowRectangle;
			float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
			using var pen = new Pen(e.Item.Enabled ? Text : _muted, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
			g.DrawLines(pen, new[] { new PointF(cx - 1.5f, cy - 3.5f), new PointF(cx + 1.5f, cy), new PointF(cx - 1.5f, cy + 3.5f) });
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
			using var pen = new Pen(_line);
			var y = e.Item.Height / 2;
			e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
		}

		private static GraphicsPath RoundRect(Rectangle r, int radius) {
			var d = radius * 2;
			var p = new GraphicsPath();
			p.AddArc(r.Left, r.Top, d, d, 180, 90);
			p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
			p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
			p.CloseFigure();
			return p;
		}
	}
}
