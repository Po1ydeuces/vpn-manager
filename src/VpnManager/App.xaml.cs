using System.Windows;
using VpnManager.Localization;
using VpnManager.Logging;
using VpnManager.Ras;
using VpnManager.Settings;
using VpnManager.Tray;
using VpnManager.ViewModels;
using VpnManager.Views;

namespace VpnManager;

public partial class App : Application {
	private Mutex? _singleInstance;
	private TrayHost? _tray;

	protected override void OnStartup(StartupEventArgs e) {
		base.OnStartup(e);

		// Исключение в обработчике не должно закрывать приложение из трея: пишем в лог и живём дальше.
		DispatcherUnhandledException += (_, args) => {
			LogError(args.Exception);
			args.Handled = true;
		};
		TaskScheduler.UnobservedTaskException += (_, args) => {
			LogError(args.Exception);
			args.SetObserved();
		};

		_singleInstance = new Mutex(true, @"Local\VpnManager.SingleInstance", out var isFirst);
		if (!isFirst) {
			Shutdown();
			return;
		}

		var store = new JsonSettingsStore();
		Loc.SetLanguage(store.Load().Language);
		var vm = new MainViewModel(new RasService(), store: store, log: new FileConnectionLog());
		var popup = new PopupWindow(vm);
		_tray = new TrayHost(vm, popup);
	}

	private static void LogError(Exception ex) {
		try {
			var dir = System.IO.Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnManager");
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
				$"{DateTime.Now:s} {ex}{Environment.NewLine}{Environment.NewLine}");
		} catch {
			// лог не должен сам ронять приложение
		}
	}

	protected override void OnExit(ExitEventArgs e) {
		_tray?.Dispose();
		_singleInstance?.Dispose();
		base.OnExit(e);
	}
}