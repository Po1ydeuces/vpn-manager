using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace VpnManager.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged {
	public event PropertyChangedEventHandler? PropertyChanged;

	protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) {
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		return true;
	}

	protected void Raise([CallerMemberName] string? name = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

public sealed class RelayCommand(Func<Task> execute) : ICommand {
	public event EventHandler? CanExecuteChanged {
		add { }
		remove { }
	}

	public bool CanExecute(object? parameter) {
		return true;
	}

	public async void Execute(object? parameter) {
		await execute();
	}
}