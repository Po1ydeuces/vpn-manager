using System.Windows;
using System.Windows.Input;
using VpnManager.Localization;
using VpnManager.Logging;

namespace VpnManager.Views;

/// <summary>Просмотр журнала подключений внутри приложения: новые записи сверху.</summary>
public partial class LogWindow : Window {
	public LogWindow(string? theme) {
		Resources.MergedDictionaries.Add(ThemeResources.Load(theme));
		InitializeComponent();
		Title = Loc.T("ConnectionLog");
		Icon = VpnManager.Tray.TrayIcons.CreateImageSource(true);
		RefreshBtn.Content = Loc.T("Refresh");
		CopyBtn.Content = Loc.T("Copy");
		Reload();
	}

	private void Reload() {
		var lines = FileConnectionLog.ReadRecent();
		LogBox.Text = lines.Count == 0 ? Loc.T("LogEmpty") : string.Join(Environment.NewLine, lines);
		LogBox.ScrollToHome();
	}

	private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

	private void Copy_Click(object sender, RoutedEventArgs e) {
		try {
			Clipboard.SetText(LogBox.Text);
		} catch {
			// буфер обмена занят другим приложением
		}
	}

	private void Window_KeyDown(object sender, KeyEventArgs e) {
		if (e.Key == Key.Escape) Close();
		else if (e.Key == Key.F5) Reload();
	}
}
