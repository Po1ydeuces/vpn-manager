using System.IO;
using System.Runtime.InteropServices;
using static VpnManager.Ras.RasNative;

namespace VpnManager.Ras;

public sealed class RasService : IRasService {
	private const int ERROR_INTERACTIVE_MODE = 703;

	private static readonly string UserPhonebook = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		@"Microsoft\Network\Connections\Pbk\rasphone.pbk");

	public IReadOnlyList<VpnEntry> GetEntries() {
		var connections = EnumConnections();
		var result = new List<VpnEntry>();
		foreach (var (name, _) in EnumProfiles()) {
			var conn = connections.FirstOrDefault(c =>
				string.Equals(c.szEntryName, name, StringComparison.OrdinalIgnoreCase));
			result.Add(conn.hrasconn == IntPtr.Zero
				? new VpnEntry(name, VpnState.Disconnected, null, null)
				: ToEntry(name, conn.hrasconn));
		}

		return result;
	}

	private static readonly RasResult CancelledResult = new(false, null, false, true);

	// Дозвоны к разным профилям идут параллельно, каждый в своём потоке пула.
	public Task<RasResult> ConnectAsync(string entryName, CancellationToken ct) {
		return Task.Run(() => DialAsync(entryName, ct));
	}

	public void Disconnect(string entryName) {
		HangUpByName(entryName);
	}

	private static void HangUpByName(string entryName) {
		foreach (var c in EnumConnections())
			if (string.Equals(c.szEntryName, entryName, StringComparison.OrdinalIgnoreCase))
				RasHangUp(c.hrasconn);
	}

	private static VpnEntry ToEntry(string name, IntPtr hconn) {
		var status = new RASCONNSTATUS { dwSize = Marshal.SizeOf<RASCONNSTATUS>() };
		if (RasGetConnectStatus(hconn, ref status) != 0)
			return new VpnEntry(name, VpnState.Disconnected, null, null);

		if (status.rasconnstate == RASCS_Connected) {
			var stats = new RAS_STATS { dwSize = Marshal.SizeOf<RAS_STATS>() };
			TimeSpan? duration = RasGetConnectionStatistics(hconn, ref stats) == 0
				? TimeSpan.FromMilliseconds(stats.dwConnectDuration)
				: null;
			return new VpnEntry(name, VpnState.Connected, duration, null);
		}

		if (status.rasconnstate == RASCS_Disconnected)
			return new VpnEntry(name, VpnState.Disconnected, null, null);
		return new VpnEntry(name, VpnState.Connecting, null, null);
	}

	private const int CancelCode = -1;
	private static readonly System.Collections.Concurrent.ConcurrentBag<RasDialFunc> PinnedCallbacks = new();

	/// <summary>
	/// Асинхронный RasDial: возвращается сразу с handle соединения, итог приходит в callback.
	/// Отмена рвёт соединение по handle (RasHangUp), не по имени: работает и для дозвона,
	/// который RAS ещё не показывает в RasEnumConnections, и не зависит от других зависших дозвонов.
	/// </summary>
	private static async Task<RasResult> DialAsync(string entryName, CancellationToken ct) {
		if (ct.IsCancellationRequested) return CancelledResult;

		var phonebook = FindPhonebook(entryName);
		var p = new RASDIALPARAMS {
			dwSize = Marshal.SizeOf<RASDIALPARAMS>(),
			szEntryName = entryName
		};
		// Подтягиваем сохранённые в профиле имя пользователя и пароль.
		RasGetEntryDialParams(phonebook, ref p, out _);

		// Результат дозвона: 0 = подключено, CancelCode = отмена, иначе код ошибки RAS.
		var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		RasDialFunc callback = (_, state, error) => {
			if (error != 0) done.TrySetResult(error);
			else if (state == RASCS_Connected) done.TrySetResult(0);
			else if (state == RASCS_Disconnected) done.TrySetResult(ERROR_USER_DISCONNECTION);
		};
		// RAS может вызвать callback и после отмены (при разрыве), когда DialAsync уже вернулся.
		// Если делегата к тому моменту нет, это FailFast «callback on a garbage collected delegate».
		// Поэтому делегат живёт до конца процесса (десятки байт на дозвон).
		PinnedCallbacks.Add(callback);

		var hconn = IntPtr.Zero;
		var code = RasDial(IntPtr.Zero, phonebook, ref p, 0,
			Marshal.GetFunctionPointerForDelegate(callback), ref hconn);
		if (code != 0) {
			if (hconn != IntPtr.Zero) RasHangUp(hconn);
			return Failure(code);
		}

		// RasHangUp синхронный: вызывать из пула, а не из потока, нажавшего «отмена» (UI).
		using var reg = ct.Register(() => {
			done.TrySetResult(CancelCode);
			Task.Run(() => RasHangUp(hconn));
		});

		var result = await done.Task;

		if (ct.IsCancellationRequested || result == CancelCode) return CancelledResult;
		if (result == 0) return new RasResult(true, null, false);

		RasHangUp(hconn); // сбросить недособранное соединение
		return Failure(result);
	}

	private static RasResult Failure(int code) {
		return new RasResult(false, GetErrorString(code), code == ERROR_AUTHENTICATION_FAILURE || code == ERROR_INTERACTIVE_MODE);
	}

	private static string? FindPhonebook(string entryName) {
		foreach (var (name, pbk) in EnumProfiles())
			if (string.Equals(name, entryName, StringComparison.OrdinalIgnoreCase))
				return string.IsNullOrEmpty(pbk) ? null : pbk;
		return null;
	}

	private static List<(string Name, string Phonebook)> EnumProfiles() {
		var list = new List<(string, string)>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var books = new List<string?> { null };
		if (File.Exists(UserPhonebook)) books.Add(UserPhonebook);

		foreach (var book in books) {
			var cb = 0;
			var rc = RasEnumEntries(null, book, null, ref cb, out _);
			if (rc != ERROR_BUFFER_TOO_SMALL || cb == 0) continue;

			var arr = new RASENTRYNAME[cb / Marshal.SizeOf<RASENTRYNAME>()];
			arr[0].dwSize = Marshal.SizeOf<RASENTRYNAME>();
			rc = RasEnumEntries(null, book, arr, ref cb, out var count);
			if (rc != 0) continue;

			for (var i = 0; i < count; i++)
				if (seen.Add(arr[i].szEntryName))
					list.Add((arr[i].szEntryName, arr[i].szPhonebookPath));
		}

		return list;
	}

	private static List<RASCONN> EnumConnections() {
		var cb = 0;
		var rc = RasEnumConnections(null, ref cb, out _);
		if (rc != ERROR_BUFFER_TOO_SMALL || cb == 0) return new List<RASCONN>();

		var arr = new RASCONN[cb / Marshal.SizeOf<RASCONN>()];
		arr[0].dwSize = Marshal.SizeOf<RASCONN>();
		rc = RasEnumConnections(arr, ref cb, out var count);
		return rc == 0 ? arr.Take(count).ToList() : new List<RASCONN>();
	}

	private static string GetErrorString(int code) {
		var buf = new char[512];
		return RasGetErrorString(code, buf, buf.Length) == 0
			? new string(buf).TrimEnd('\0')
			: $"Ошибка RAS {code}";
	}
}