using System.IO;
using System.Text;
using VpnManager.ViewModels;

namespace VpnManager.Logging;

public interface IConnectionLog {
	void Write(ConnectionEvent e);
}

public sealed class NullConnectionLog : IConnectionLog {
	public void Write(ConnectionEvent e) { }
}

public static class ConnectionLogFormatter {
	/// <summary>"2026-11-27 12:31:05  Work  Connection dropped after 01:02:03". Журнал всегда на английском.</summary>
	public static string Format(ConnectionEvent e) {
		var duration = e.Duration is { } d ? $" after {DurationFormatter.Format(d)}" : "";
		var detail = string.IsNullOrEmpty(e.Detail) ? "" : $": {e.Detail}";
		var what = e.Kind switch {
			ConnectionEventKind.Connected => "Connected",
			ConnectionEventKind.Disconnected => $"Disconnected by user{duration}",
			ConnectionEventKind.Dropped => $"Connection dropped{duration}",
			ConnectionEventKind.Failed => $"Connection failed{detail}",
			ConnectionEventKind.GaveUp => $"Auto-reconnect gave up{detail}",
			_ => e.Kind.ToString()
		};
		return $"{e.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}  {e.Name}  {what}";
	}
}

/// <summary>Журнал в %LOCALAPPDATA%\VpnManager\connections.log; при запуске обрезается, если вырос.</summary>
public sealed class FileConnectionLog : IConnectionLog {
	private const long MaxBytes = 1024 * 1024;
	private const int KeepLines = 2000;
	private readonly object _lock = new();

	public static readonly string DefaultPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnManager", "connections.log");

	private readonly string _path;

	public FileConnectionLog(string? path = null) {
		_path = path ?? DefaultPath;
		Trim();
	}

	/// <summary>Последние записи журнала, новые сверху. Нет файла или ошибка чтения — пустой список.</summary>
	public static IReadOnlyList<string> ReadRecent(string? path = null, int max = KeepLines) {
		try {
			using var stream = new FileStream(path ?? DefaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			var lines = new List<string>();
			while (reader.ReadLine() is { } line)
				if (line.Length > 0) lines.Add(line);
			lines.Reverse();
			return lines.Count > max ? lines.GetRange(0, max) : lines;
		} catch {
			return Array.Empty<string>();
		}
	}

	public void Write(ConnectionEvent e) {
		try {
			lock (_lock) {
				Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
				File.AppendAllText(_path, ConnectionLogFormatter.Format(e) + Environment.NewLine, Encoding.UTF8);
			}
		} catch {
			// журнал не должен ломать приложение
		}
	}

	private void Trim() {
		try {
			if (!File.Exists(_path) || new FileInfo(_path).Length <= MaxBytes) return;
			var lines = File.ReadAllLines(_path);
			File.WriteAllLines(_path, lines.Skip(Math.Max(0, lines.Length - KeepLines)), Encoding.UTF8);
		} catch {
			// не вышло обрезать: обрежется при следующем запуске
		}
	}
}
