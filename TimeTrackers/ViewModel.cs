using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using LibGit2Sharp;
using Newtonsoft.Json;
using PostSharp;
using PostSharp.Patterns.Model;
using TimeTrackers.Properties;
using TimeTrackers.View.ViewModel;

// TODO Add checkboxes for EOD and lunch
// TODO Math error on Sept 11 2015
// TODO Skip merges in git messages
namespace TimeTrackers {
	[NotifyPropertyChanged]
	public class ViewModel {
		public static string AutosaveFile { get; } = Path.Combine(Directory.GetCurrentDirectory(), "TimeTrackers.autosave.json");

		public static ViewModel Instance { get; }

		static ViewModel() {
			Instance = new ViewModel();
		}

		private class DifferenceTimeTracker : TimeTracker {
			public TimeSpan Difference { get; }

			public DifferenceTimeTracker(TimeTracker tt, TimeSpan difference) {
				Difference = difference;
				Time = tt.Time;
				Group = tt.Group;
				Notes = tt.Notes;
				Type = tt.Type;
			}
		}

		public enum TimeTrackerType {
			Normal,
			Lunch,
			EndOfDay
		};

		[NotifyPropertyChanged]
		public class TimeTracker {
			public DateTime Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
			public TimeTrackerType Type { get; set; }

			public TimeTracker() {
				Time = DateTime.Now;
				Type = TimeTrackerType.Normal;
			}
		}

		public class FinalTracker {
			public TimeSpan Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
		}

		public ObservableCollection<TimeTracker> TimeTrackers { get; }
		public ObservableCollection<FinalTracker> FinalTrackers { get; }
		public ICollectionView TimeTrackersByDay { get; }
		public string Message { get; set; }
		public TimeSpan TotalTime { get; set; }
		public DateTime FilterDay { get; set; }
		public DateTime SmallestSaved { get; set; }

		public RelayCommand RemoveCommand { get; }
		public RelayCommand GitMessagesCommand { get; }

		private bool FilterByDay(object obj) {
			TimeTracker tt = obj as TimeTracker;
			if (tt == null) {
				throw new ArgumentException("Object is not a time tracker", nameof(obj));
			}

			return tt.Time.Date == FilterDay.Date;
		}

		private ViewModel() {
			TimeTrackers = new ObservableCollection<TimeTracker>();
			FinalTrackers = new ObservableCollection<FinalTracker>();
			FilterDay = DateTime.Now;

			TimeTrackersByDay = CollectionViewSource.GetDefaultView(TimeTrackers);
			TimeTrackersByDay.Filter = FilterByDay;
			INotifyPropertyChanged propChanged = Post.Cast<ViewModel, INotifyPropertyChanged>(this);
			propChanged.PropertyChanged += (src, args) => {
				if (args.PropertyName == nameof(FilterDay)) {
					Application.Current.Dispatcher.Invoke(TimeTrackersByDay.Refresh);
				}
			};

			RemoveCommand = new RelayCommand(RemoveCommand_Execute);
			GitMessagesCommand = new RelayCommand(GitMessagesCommand_Execute);

			if (File.Exists(AutosaveFile)) {
				List<TimeTracker> tts = JsonConvert.DeserializeObject<List<TimeTracker>>(File.ReadAllText(AutosaveFile));
				foreach (TimeTracker tt in tts) {
					TimeTrackers.Add(tt);
				}

				Message = "Timers loaded from cache";
			}

			SetSmallest();

			TimeTrackers.CollectionChanged += (src, args) => {
				Application.Current.Dispatcher.Invoke(SetSmallest);
			};

			Timer timer = new Timer(30000);
			timer.Elapsed += Timer_Elapsed;
			timer.AutoReset = true;
			timer.Enabled = true;
		}

		private void GitMessagesCommand_Execute(object o) {
			TimeTracker tt = o as TimeTracker;
			if (tt == null) {
				return;
			}

			DateTime start = tt.Time;
			DateTime stop = TimeTrackers.SkipWhile(t => t != tt).Skip(1).FirstOrDefault()?.Time ?? DateTime.Now;
			IEnumerable<string> messages =
				from p in Settings.Default.GitPaths
				let g = new Repository(p)
				from c in g.Commits
					.SkipWhile(c => c.Author.When > stop)
					.TakeWhile(c => c.Author.When > start)
				where c.Author.Email == Settings.Default.AuthorEmail
				select $"- {c.Message}".Trim();

			List<string> notes = new List<string>((tt.Notes ?? String.Empty).Split('\r', '\n'));
			notes.AddRange(messages);
			tt.Notes = String.Join(Environment.NewLine, notes.Select(n => n.Trim()).Where(s => !String.IsNullOrWhiteSpace(s)).Distinct()).Trim();
		}

		private void RemoveCommand_Execute(object o) {
			TimeTrackers.Remove(o as TimeTracker);
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
			SaveTimers();
		}

		private TimeSpan? ToHourMinute(DateTime? dateTime) {
			if (dateTime == null) {
				return null;
			}

			return ToHourMinute((DateTime)dateTime);
		}

		private TimeSpan ToHourMinute(DateTime dateTime) {
			return new TimeSpan(dateTime.TimeOfDay.Hours, dateTime.TimeOfDay.Minutes, 0);
		}

		private void SetSmallest() {
			SmallestSaved = (from tt in TimeTrackers let d = tt.Time orderby tt.Time select d).FirstOrDefault();
		}

		public void CalculateFinals() {
			FinalTrackers.Clear();
			TotalTime = new TimeSpan();

			IEnumerable<TimeTracker> tts = TimeTrackersByDay.Cast<TimeTracker>().Where(t => !String.IsNullOrWhiteSpace(t.Notes));
			List<DifferenceTimeTracker> finals = new List<DifferenceTimeTracker>();
			for (LinkedListNode<TimeTracker> node = new LinkedList<TimeTracker>(tts).First; node != null; node = node.Next) {
				// Stop processing on the EOD time tracker
				if (node.Value.Type == TimeTrackerType.EndOfDay) {
					break;
				}

				finals.Add(new DifferenceTimeTracker(node.Value, (ToHourMinute(node.Next?.Value?.Time) ?? ToHourMinute(DateTime.Now)) - ToHourMinute(node.Value.Time)));
			}

			IEnumerable<IGrouping<string, DifferenceTimeTracker>> groupedFinals =
				from f in finals
				where f.Type != TimeTrackerType.Lunch
				group f by f.Group
				into g
				select g;

			foreach (IGrouping<string, DifferenceTimeTracker> tg in groupedFinals) {
				FinalTrackers.Add(new FinalTracker() {
					Time = new TimeSpan(tg.Sum(t => t.Difference.Ticks)),
					Group = tg.Key,
					Notes = String.Join(Environment.NewLine, tg.Select(t => t.Notes))
				});
			}

			TotalTime = new TimeSpan(FinalTrackers.Sum(t => t.Time.Ticks));
		}

		public void SaveTimers() {
			DateTime[] toRemove = (
					from tt in TimeTrackersByDay.Cast<TimeTracker>()
					group tt by tt.Time.Date into g
					orderby g
					select g.Key.Date
				)
				.Skip(Settings.Default.DaysToKeep)
				.ToArray();

			IEnumerable<TimeTracker> toSave =
				from tt in TimeTrackers
				where !toRemove.Contains(tt.Time.Date)
				select tt;

			File.WriteAllText(AutosaveFile, JsonConvert.SerializeObject(toSave));
			Message = $"Timers saved to cache at {DateTime.Now}";
		}
	}
}
