namespace VpnManager.ViewModels;

public enum ConnectionEventKind {
	/// <summary>Соединение установлено.</summary>
	Connected,

	/// <summary>Пользователь отключил профиль.</summary>
	Disconnected,

	/// <summary>Соединение оборвалось само.</summary>
	Dropped,

	/// <summary>Не удалось подключиться.</summary>
	Failed,

	/// <summary>Автопереподключение исчерпало попытки.</summary>
	GaveUp
}

/// <param name="Notify">Показывать ли пользователю (в автопереподключении промежуточные неудачи тихие).</param>
public record ConnectionEvent(DateTimeOffset At, string Name, ConnectionEventKind Kind,
	TimeSpan? Duration = null, string? Detail = null, bool Notify = true);
