using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TimeTrackers.Properties;

namespace TimeTrackers {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();

            DataContext = ViewModel.Instance;
        }

        private void AddTimeTracker_Click(object sender, RoutedEventArgs e) {
            ViewModel.Instance.TimeTrackers.Add(new ViewModel.TimeTracker());
        }

        private void Finals_OnGotFocus(object sender, RoutedEventArgs e) {
            ViewModel.Instance.CalculateFinals();
        }

        protected override void OnClosing(CancelEventArgs e) {
            ViewModel.Instance.SaveTimers();
            Settings.Default.Save();
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

        private void Arrange_Click(object sender, RoutedEventArgs e) {
            ViewModel.Instance.TimeTrackers.BubbleSort();
        }

        private void AddGitPath_Click(object sender, RoutedEventArgs e) {
            if (Settings.Default.GitPaths == null) {
                Settings.Default.GitPaths = new ObservableCollection<Watchable<string>>();
            }

            Settings.Default.GitPaths.Add(new Watchable<string>(String.Empty));
        }
    }
}
