using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Timers;
using Newtonsoft.Json;
using PostSharp.Patterns.Model;
using TimeTrackers.View.ViewModel;

namespace TimeTrackers {
	[NotifyPropertyChanged]
	public class ViewModel {
		public static string AutosaveFile { get; } = Path.Combine(Path.GetTempPath(), "TimeTrackers.autosave.json");

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

		public class TimeTracker {
			public DateTime Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
		}

		public class FinalTracker {
			public TimeSpan Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
		}

		public ObservableCollection<TimeTracker> TimeTrackers { get; }
		public ObservableCollection<FinalTracker> FinalTrackers { get; }
		public string Message { get; set; }

		public RelayCommand RemoveCommand { get; }

		private ViewModel() {
			TimeTrackers = new ObservableCollection<TimeTracker>();
			FinalTrackers = new ObservableCollection<FinalTracker>();

			RemoveCommand = new RelayCommand(RemoveCommand_Execute);

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

		private void RemoveCommand_Execute(object o) {
			TimeTrackers.Remove(o as TimeTracker);
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
			SaveTimers();
		}

		public void CalculateFinals() {
			FinalTrackers.Clear();

			IEnumerable<TimeTracker> tts = TimeTrackers.Where(t => !String.IsNullOrWhiteSpace(t.Notes));
			List<DifferenceTimeTracker> finals = new List<DifferenceTimeTracker>();
			for (LinkedListNode<TimeTracker> node = new LinkedList<TimeTracker>(tts).First; node != null; node = node.Next) {
				finals.Add(new DifferenceTimeTracker(node.Value, (node.Next?.Value?.Time ?? DateTime.Now) - node.Value.Time));
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
		}

		public void SaveTimers() {
			File.WriteAllText(AutosaveFile, JsonConvert.SerializeObject(TimeTrackers));
			Message = $"Timers saved to cache at {DateTime.Now}";
		}
	}
}
