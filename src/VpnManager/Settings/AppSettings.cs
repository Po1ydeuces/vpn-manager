using System.IO;
using System.Text.Json;

namespace VpnManager.Settings;

public class AppSettings {
	public List<string> Pinned { get; set; } = new();
	public List<string> AutoReconnect { get; set; } = new();
	public List<string> ConnectOnStart { get; set; } = new();
	public List<string> Hidden { get; set; } = new();

	/// <summary>Ctrl+Alt+V открывает popup.</summary>
	public bool HotkeyEnabled { get; set; } = true;

	/// <summary>Всплывающие уведомления об обрыве и ошибках.</summary>
	public bool NotificationsEnabled { get; set; } = true;

	/// <summary>"light", "dark" или null (по системной).</summary>
	public string? Theme { get; set; }

	/// <summary>"en", "ru" или null (по системному).</summary>
	public string? Language { get; set; }
}

public interface ISettingsStore {
	AppSettings Load();
	void Save(AppSettings settings);
}

/// <summary>Хранит настройки в памяти (тесты, значения по умолчанию).</summary>
public sealed class MemorySettingsStore : ISettingsStore {
	private AppSettings _settings = new();

	public AppSettings Load() {
		return _settings;
	}

	public void Save(AppSettings settings) {
		_settings = settings;
	}
}

/// <summary>JSON-файл в %LOCALAPPDATA%\VpnManager\settings.json.</summary>
public sealed class JsonSettingsStore : ISettingsStore {
	private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
	private readonly string _path;

	public JsonSettingsStore(string? path = null) {
		_path = path ?? Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnManager", "settings.json");
	}

	public AppSettings Load() {
		try {
			if (File.Exists(_path))
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
		} catch {
			// битый файл не должен мешать запуску: начинаем с настроек по умолчанию
		}

		return new AppSettings();
	}

	public void Save(AppSettings settings) {
		try {
			Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
			File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
		} catch {
			// не удалось сохранить: настройка живёт до выхода из приложения
		}
	}
}
