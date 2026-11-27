namespace VpnManager.Ras;

public record VpnEntry(string Name, VpnState State, TimeSpan? Duration, string? ErrorText);