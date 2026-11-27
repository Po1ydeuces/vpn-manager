using Microsoft.Win32;

namespace VpnManager.Tray;

public static class Autostart {
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "VpnManager";

	public static bool IsEnabled() {
		using var key = Registry.CurrentUser.OpenSubKey(RunKey);
		return key?.GetValue(ValueName) != null;
	}

	public static void SetEnabled(bool enabled) {
		using var key = Registry.CurrentUser.CreateSubKey(RunKey);
		if (enabled)
			key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
		else
			key.DeleteValue(ValueName, false);
	}
}