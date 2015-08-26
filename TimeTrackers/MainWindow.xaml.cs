using System.Windows;

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
	}
}
