using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using log4net;
using LibGit2Sharp;
using Newtonsoft.Json;
using TimeTrackers.Properties;

namespace TimeTrackers {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        private readonly ILog _logger = LogManager.GetLogger(typeof(MainWindow));

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

        private void GetGitMessages_Click(object sender, RoutedEventArgs e) {
            _logger.Warn(JsonConvert.SerializeObject(new {
                Message = "Invalid paths found",
                Paths =
                    from p in Settings.Default.GitPaths
                    where !Directory.Exists(p)
                    select p
            }));

            var messages = (
                from p in Settings.Default.GitPaths
                where Directory.Exists(p)
                let g = new Repository(p)
                from c in g.Commits.QueryBy(new CommitFilter { Since = g.Refs, SortBy = CommitSortStrategies.Time })
                    .SkipWhile(c => c.Author.When > ViewModel.Instance.FilterDay.Date + TimeSpan.FromDays(1))
                    .TakeWhile(c => c.Author.When > ViewModel.Instance.FilterDay.Date)
                where c.Author.Email == Settings.Default.AuthorEmail
                where c.Parents.Count() == 1
                select new { c.Message, When = Helpers.RoundToNearestInterval(c.Author.When.LocalDateTime, TimeSpan.FromMinutes(15)) }
            ).ToArray();

            // Loop backwards over time trackers to calculate time ranges
            DateTime finishTime = DateTime.Now.AddHours(12);
            foreach (var tt in ViewModel.Instance.TimeTrackersByDay.Cast<ViewModel.TimeTracker>().Reverse()) {
                tt.GitNotes = String.Join(Environment.NewLine, (from m in messages where m.When > tt.Time && m.When <= finishTime select $"- {m.Message}".Trim()).Distinct());
                finishTime = tt.Time;
            }
        }
    }
}
