using System.Windows;

namespace VpnManager.Tray;

public static class PopupPositioner {
	/// <summary>
	/// Позиция левого верхнего угла popup: по центру anchor по горизонтали,
	/// над anchor, если он в нижней половине рабочей области, иначе под ним; всё внутри workArea.
	/// </summary>
	public static Point Calculate(Rect workArea, Point anchor, Size size) {
		var x = anchor.X - size.Width / 2;
		var y = anchor.Y > workArea.Top + workArea.Height / 2
			? anchor.Y - size.Height
			: anchor.Y;

		// Иконка на панели задач (вне рабочей области): прилипание к ближайшему краю экрана, как у системных панелей.
		var onHorizontalTaskbar = anchor.Y < workArea.Top || anchor.Y > workArea.Bottom;
		var onVerticalTaskbar = anchor.X < workArea.Left || anchor.X > workArea.Right;
		if (onHorizontalTaskbar && !onVerticalTaskbar)
			x = anchor.X > workArea.Left + workArea.Width / 2 ? workArea.Right - size.Width : workArea.Left;
		else if (onVerticalTaskbar && !onHorizontalTaskbar)
			y = anchor.Y > workArea.Top + workArea.Height / 2 ? workArea.Bottom - size.Height : workArea.Top;

		x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - size.Width));
		y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - size.Height));
		return new Point(x, y);
	}
}