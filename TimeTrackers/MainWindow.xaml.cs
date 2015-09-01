using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TimeTrackers {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		public MainWindow() {
			InitializeComponent();

			DataContext = ViewModel.Instance;
		}

		private void Add_OnClick(object sender, RoutedEventArgs e) {
			ViewModel.Instance.TimeTrackers.Add(new ViewModel.TimeTracker());
		}

		private void Finals_OnGotFocus(object sender, RoutedEventArgs e) {
			ViewModel.Instance.CalculateFinals();
		}

		protected override void OnClosing(CancelEventArgs e) {
			ViewModel.Instance.SaveTimers();
		}

		private void FinalNotes_OnGotFocus(object sender, RoutedEventArgs e) {
			TextBox tb = sender as TextBox;
			if (tb == null) {
				return;
			}

			// Magic line to highlight text: http://stackoverflow.com/a/7986906
			e.Handled = true;

			Clipboard.SetText(tb.Text);
		}
	}
}
