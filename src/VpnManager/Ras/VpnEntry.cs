namespace VpnManager.Ras;

public record VpnEntry(string Name, VpnState State, TimeSpan? Duration, string? ErrorText,
	long BytesSent = 0, long BytesReceived = 0);
