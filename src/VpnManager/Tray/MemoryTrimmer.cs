using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace VpnManager.Tray;

/// <summary>Возвращает память системе: полная сборка мусора с уплотнением и сброс рабочего набора.</summary>
public static class MemoryTrimmer {
	public static void Trim() {
		GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
		GC.WaitForPendingFinalizers();
		using var self = Process.GetCurrentProcess();
		EmptyWorkingSet(self.Handle); // страницы вернутся по мере использования
	}

	[DllImport("psapi.dll")]
	private static extern bool EmptyWorkingSet(IntPtr hProcess);
}