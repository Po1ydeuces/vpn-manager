using System.Runtime.InteropServices;

namespace VpnManager.Ras;

internal static class RasNative {
	public const int RAS_MaxEntryName = 256;
	public const int RAS_MaxDeviceType = 16;
	public const int RAS_MaxDeviceName = 128;
	public const int RAS_MaxPhoneNumber = 128;
	public const int RAS_MaxCallbackNumber = 128;
	public const int UNLEN = 256;
	public const int PWLEN = 256;
	public const int DNLEN = 15;
	public const int MAX_PATH = 260;

	public const int ERROR_BUFFER_TOO_SMALL = 603;
	public const int ERROR_AUTHENTICATION_FAILURE = 691;
	public const int ERROR_NO_CONNECTION = 668;
	public const int ERROR_USER_DISCONNECTION = 631;

	public const int RASCS_Connected = 0x2000;
	public const int RASCS_Disconnected = 0x2001;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct RASENTRYNAME {
		public int dwSize;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxEntryName + 1)]
		public string szEntryName;

		public int dwFlags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 1)]
		public string szPhonebookPath;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
	public struct RASCONN {
		public int dwSize;
		public IntPtr hrasconn;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxEntryName + 1)]
		public string szEntryName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxDeviceType + 1)]
		public string szDeviceType;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxDeviceName + 1)]
		public string szDeviceName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
		public string szPhonebook;

		public int dwSubEntry;
		public Guid guidEntry;
		public int dwFlags;
		public long luid;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct RASCONNSTATUS {
		public int dwSize;
		public int rasconnstate;
		public int dwError;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxDeviceType + 1)]
		public string szDeviceType;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxDeviceName + 1)]
		public string szDeviceName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxPhoneNumber + 1)]
		public string szPhoneNumber;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RAS_STATS {
		public int dwSize;
		public int dwBytesXmited;
		public int dwBytesRcved;
		public int dwFramesXmited;
		public int dwFramesRcved;
		public int dwCrcErr;
		public int dwTimeoutErr;
		public int dwAlignmentErr;
		public int dwHardwareOverrunErr;
		public int dwFramingErr;
		public int dwBufferOverrunErr;
		public int dwCompressionRatioIn;
		public int dwCompressionRatioOut;
		public int dwBps;
		public int dwConnectDuration; // мс
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
	public struct RASDIALPARAMS {
		public int dwSize;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxEntryName + 1)]
		public string szEntryName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxPhoneNumber + 1)]
		public string szPhoneNumber;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxCallbackNumber + 1)]
		public string szCallbackNumber;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = UNLEN + 1)]
		public string szUserName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = PWLEN + 1)]
		public string szPassword;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = DNLEN + 1)]
		public string szDomain;

		public int dwSubEntry;
		public IntPtr dwCallbackId;
	}

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasEnumEntries(string? reserved, string? lpszPhonebook,
		[In] [Out] RASENTRYNAME[]? lprasentryname, ref int lpcb, out int lpcEntries);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasEnumConnections([In] [Out] RASCONN[]? lprasconn, ref int lpcb, out int lpcConnections);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasGetConnectStatus(IntPtr hrasconn, ref RASCONNSTATUS lprasconnstatus);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasGetConnectionStatistics(IntPtr hRasConn, ref RAS_STATS lpStatistics);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasGetEntryDialParams(string? lpszPhonebook, ref RASDIALPARAMS lprasdialparams, out bool lpfPassword);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasDial(IntPtr lpRasDialExtensions, string? lpszPhonebook,
		ref RASDIALPARAMS lprasdialparams, int dwNotifierType, IntPtr lpvNotifier, ref IntPtr lphRasConn);

	/// <summary>RasDialFunc (dwNotifierType = 0): вызывается RAS при смене состояния дозвона.</summary>
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate void RasDialFunc(uint unMsg, int rasconnstate, int dwError);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasHangUp(IntPtr hrasconn);

	[DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
	public static extern int RasGetErrorString(int uErrorValue, [Out] char[] lpszErrorString, int cBufSize);
}