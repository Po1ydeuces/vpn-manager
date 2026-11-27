using System.Windows;
using VpnManager.Tray;

namespace VpnManager.Tests;

public class PopupPositionerTests {
	private static readonly Size S = new(300, 200);

	private static void AssertInside(Rect area, Point p) {
		Assert.True(p.X >= area.Left, $"X {p.X} < left {area.Left}");
		Assert.True(p.Y >= area.Top, $"Y {p.Y} < top {area.Top}");
		Assert.True(p.X + S.Width <= area.Right, $"right edge {p.X + S.Width} > {area.Right}");
		Assert.True(p.Y + S.Height <= area.Bottom, $"bottom edge {p.Y + S.Height} > {area.Bottom}");
	}

	[Fact]
	public void TaskbarBottom_PopupAboveAnchor() {
		var wa = new Rect(0, 0, 1920, 1040);
		var p = PopupPositioner.Calculate(wa, new Point(1800, 1045), S);
		AssertInside(wa, p);
		Assert.True(p.Y + S.Height <= 1045);
	}

	[Fact]
	public void TaskbarTop_PopupBelowAnchor() {
		var wa = new Rect(0, 40, 1920, 1040);
		var p = PopupPositioner.Calculate(wa, new Point(1800, 5), S);
		AssertInside(wa, p);
	}

	[Theory]
	[InlineData(5, 500)]
	[InlineData(1915, 500)]
	public void TaskbarSide_PopupInsideWorkArea(double x, double y) {
		var wa = new Rect(0, 0, 1920, 1040);
		AssertInside(wa, PopupPositioner.Calculate(wa, new Point(x, y), S));
	}

	[Fact]
	public void TaskbarBottom_SnapsToNearestHorizontalEdge() {
		var wa = new Rect(12, 12, 1896, 1016);
		Assert.Equal(wa.Right - S.Width, PopupPositioner.Calculate(wa, new Point(1700, 1045), S).X);
		Assert.Equal(wa.Left, PopupPositioner.Calculate(wa, new Point(100, 1045), S).X);
	}

	[Fact]
	public void PopupTallerThanWorkArea_PinnedToTop() {
		var wa = new Rect(0, 0, 1920, 1040);
		var p = PopupPositioner.Calculate(wa, new Point(1800, 1045), new Size(300, 1500));
		Assert.Equal(0, p.Y);
	}

	[Fact]
	public void SecondMonitor_PopupStaysOnThatMonitor() {
		var wa = new Rect(1920, 0, 1920, 1040);
		var p = PopupPositioner.Calculate(wa, new Point(3700, 1045), S);
		AssertInside(wa, p);
	}
}