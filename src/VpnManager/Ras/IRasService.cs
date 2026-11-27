namespace VpnManager.Ras;

public record RasResult(bool Success, string? ErrorText, bool CredentialsRequired, bool Cancelled = false);

public interface IRasService {
	/// <summary>Все профили с текущим состоянием.</summary>
	IReadOnlyList<VpnEntry> GetEntries();

	/// <summary>RasDial, не блокирует UI-поток. Отмена токена прерывает ожидание в очереди или идущий дозвон.</summary>
	Task<RasResult> ConnectAsync(string entryName, CancellationToken ct);

	/// <summary>RasHangUp.</summary>
	void Disconnect(string entryName);
}