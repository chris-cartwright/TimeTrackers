using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PostSharp.Patterns.Model;

namespace TimeTrackers {
	[NotifyPropertyChanged]
	public class ViewModel {
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

		public class FinalTracker
		{
			public TimeSpan Time { get; set; }
			public string Group { get; set; }
			public string Notes { get; set; }
		}

		public ObservableCollection<TimeTracker> TimeTrackers { get; }
		public ObservableCollection<FinalTracker> FinalTrackers { get; }

		private ViewModel() {
			TimeTrackers = new ObservableCollection<TimeTracker>();
			FinalTrackers = new ObservableCollection<FinalTracker>();

			TimeTrackers.Add(new TimeTracker() { Time = DateTime.Now, Group = "A", Notes = "Testing\nTwo" });
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
				FinalTrackers.Add(new FinalTracker() {
					Time = new TimeSpan(tg.Sum(t => t.Difference.Ticks)),
					Group = tg.Key,
					Notes = String.Join(Environment.NewLine, tg.Select(t => t.Notes))
				});
			}
		}
	}
}
