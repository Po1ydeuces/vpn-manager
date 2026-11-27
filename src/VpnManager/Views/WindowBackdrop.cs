using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VpnManager.Views;

/// <summary>Акрил (размытие фона с тонировкой) и скруглённые углы окна, как у выпадающих панелей Windows 11.</summary>
public static class WindowBackdrop {
	/// <summary>Системные скруглённые углы есть с Windows 11 (build 22000).</summary>
	public static bool IsSupported => Environment.OSVersion.Version.Build >= 22000;

	/// <summary>Окно не должно использовать AllowsTransparency, фон WPF — прозрачный.</summary>
	public static void Enable(Window window) {
		if (!IsSupported) return;
		var hwnd = new WindowInteropHelper(window).EnsureHandle();
		if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
		var round = 2; // DWMWCP_ROUND
		DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE
	}

	/// <summary>Размытие фона под окном с тонировкой цветом tint (альфа задаёт плотность тонировки).</summary>
	public static void SetTint(Window window, Color tint) {
		if (!IsSupported) return;
		var policy = new AccentPolicy {
			AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
			GradientColor = (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R
		};
		var size = Marshal.SizeOf(policy);
		var ptr = Marshal.AllocHGlobal(size);
		try {
			Marshal.StructureToPtr(policy, ptr, false);
			var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size }; // WCA_ACCENT_POLICY
			SetWindowCompositionAttribute(new WindowInteropHelper(window).EnsureHandle(), ref data);
		} finally {
			Marshal.FreeHGlobal(ptr);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct AccentPolicy {
		public int AccentState;
		public int AccentFlags;
		public int GradientColor;
		public int AnimationId;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct WindowCompositionAttributeData {
		public int Attribute;
		public IntPtr Data;
		public int SizeOfData;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	[DllImport("user32.dll")]
	private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
}
