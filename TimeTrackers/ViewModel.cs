using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Timers;
using LibGit2Sharp;
using Newtonsoft.Json;
using PostSharp.Patterns.Model;
using TimeTrackers.View.ViewModel;

namespace TimeTrackers {
	[NotifyPropertyChanged]
	public class ViewModel {
		public static string AutosaveFile { get; } = Path.Combine(Path.GetTempPath(), "TimeTrackers.autosave.json");
		public static string[] GitPaths { get; } = { @"C:\_Development\OSCIDv4", @"C:\_Development\OSCIDv4_Utilities" };

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
			}
		}

		[NotifyPropertyChanged]
		public class TimeTracker {
			public DateTime Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }

			public TimeTracker() {
				Time = DateTime.Now;
			}
		}

		public class FinalTracker {
			public TimeSpan Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
		}

		public ObservableCollection<TimeTracker> TimeTrackers { get; }
		public ObservableCollection<FinalTracker> FinalTrackers { get; }
		public string Message { get; set; }
		public TimeSpan TotalTime { get; set; }

		public RelayCommand RemoveCommand { get; }
		public RelayCommand GitMessagesCommand { get; }

		private ViewModel() {
			TimeTrackers = new ObservableCollection<TimeTracker>();
			FinalTrackers = new ObservableCollection<FinalTracker>();

			RemoveCommand = new RelayCommand(RemoveCommand_Execute);
			GitMessagesCommand = new RelayCommand(GitMessagesCommand_Execute);

			if (File.Exists(AutosaveFile)) {
				List<TimeTracker> tts = JsonConvert.DeserializeObject<List<TimeTracker>>(File.ReadAllText(AutosaveFile));
				foreach (TimeTracker tt in tts) {
					TimeTrackers.Add(tt);
				}

				Message = "Timers loaded from cache";
			}

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
				from p in GitPaths
				let g = new Repository(p)
				from c in g.Commits
					.SkipWhile(c => c.Author.When > stop)
					.TakeWhile(c => c.Author.When > start)
				where c.Author.Email == "chris.cartwright@dei.ca"
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

		public void CalculateFinals() {
			FinalTrackers.Clear();
			TotalTime = new TimeSpan();

			IEnumerable<TimeTracker> tts = TimeTrackers.Where(t => !String.IsNullOrWhiteSpace(t.Notes));
			List<DifferenceTimeTracker> finals = new List<DifferenceTimeTracker>();
			for (LinkedListNode<TimeTracker> node = new LinkedList<TimeTracker>(tts).First; node != null; node = node.Next) {
				finals.Add(new DifferenceTimeTracker(node.Value, (ToHourMinute(node.Next?.Value?.Time) ?? ToHourMinute(DateTime.Now)) - ToHourMinute(node.Value.Time)));
			}

			IEnumerable<IGrouping<string, DifferenceTimeTracker>> groupedFinals =
				from f in finals
				group f by f.Group
				into g
				select g;

			foreach (IGrouping<string, DifferenceTimeTracker> tg in groupedFinals) {
				if (String.IsNullOrWhiteSpace(tg.Key)) {
					foreach (DifferenceTimeTracker tt in tg) {
						FinalTrackers.Add(new FinalTracker() {
							Time = tt.Difference,
							Group = String.Empty,
							Notes = tt.Notes
						});
					}

					continue;
				}

				FinalTrackers.Add(new FinalTracker() {
					Time = new TimeSpan(tg.Sum(t => t.Difference.Ticks)),
					Group = tg.Key,
					Notes = String.Join(Environment.NewLine, tg.Select(t => t.Notes))
				});
			}

			TotalTime = new TimeSpan(FinalTrackers.Sum(t => t.Time.Ticks));
		}

		public void SaveTimers() {
			File.WriteAllText(AutosaveFile, JsonConvert.SerializeObject(TimeTrackers));
			Message = $"Timers saved to cache at {DateTime.Now}";
		}
	}
}
