using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VpnManager.Tray;

/// <summary>Глобальная горячая клавиша Ctrl+Alt+V через RegisterHotKey и окно-приёмник сообщений.</summary>
public sealed class GlobalHotkey : IDisposable {
	private const int WM_HOTKEY = 0x0312;
	private const uint MOD_ALT = 0x1;
	private const uint MOD_CONTROL = 0x2;
	private const uint MOD_NOREPEAT = 0x4000;
	private const uint VK_V = 0x56;
	private const int HotkeyId = 1;
	private static readonly IntPtr HWND_MESSAGE = new(-3);

	private readonly HwndSource _source;

	public GlobalHotkey() {
		_source = new HwndSource(new HwndSourceParameters("VpnManagerHotkey") {
			ParentWindow = HWND_MESSAGE,
			Width = 0,
			Height = 0,
			WindowStyle = 0
		});
		_source.AddHook(WndProc);
	}

	public event Action? Pressed;

	public bool IsRegistered { get; private set; }

	/// <summary>false, если сочетание занято другим приложением.</summary>
	public bool Register() {
		if (!IsRegistered)
			IsRegistered = RegisterHotKey(_source.Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_V);
		return IsRegistered;
	}

	public void Unregister() {
		if (!IsRegistered) return;
		UnregisterHotKey(_source.Handle, HotkeyId);
		IsRegistered = false;
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId) {
			Pressed?.Invoke();
			handled = true;
		}

		return IntPtr.Zero;
	}

	public void Dispose() {
		Unregister();
		_source.Dispose();
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
