using System.Globalization;

namespace VpnManager.Localization;

/// <summary>Минимальная локализация EN/RU. Язык: "en", "ru" или null (по системному).</summary>
public static class Loc {
	private static readonly Dictionary<string, (string En, string Ru)> Table = new() {
		["Connecting"] = ("Connecting…", "Подключение…"),
		["Connected"] = ("Connected", "Подключено"),
		["Error"] = ("Error", "Ошибка"),
		["Disconnected"] = ("Disconnected", "Отключено"),
		["CredentialsRequired"] = ("Credentials required", "Требуется ввод учётных данных"),
		["ConnectFailed"] = ("Failed to connect", "Не удалось подключиться"),
		["NoVpn"] = ("No VPN connections", "Нет VPN-подключений"),
		["Autostart"] = ("Start with Windows", "Запускать при старте Windows"),
		["Exit"] = ("Exit", "Выход"),
		["Language"] = ("Language", "Язык"),
		["LanguageAuto"] = ("Auto", "Авто")
	};

	private static bool _ru;

	public static string? Setting { get; private set; }

	public static void SetLanguage(string? setting) {
		Setting = setting;
		_ru = setting switch {
			"ru" => true,
			"en" => false,
			_ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
		};
	}

	public static string T(string key) {
		return Table.TryGetValue(key, out var v) ? _ru ? v.Ru : v.En : key;
	}
}
