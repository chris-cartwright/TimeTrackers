using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using Newtonsoft.Json;
using TimeTrackers.Annotations;
using TimeTrackers.Properties;
using TimeTrackers.View.ViewModel;

namespace TimeTrackers
{
    public class ViewModel : INotifyPropertyChanged
    {
        public Tuple<string, string>[] InternalIssues => new[]
        {
            new Tuple<string, string>("INT-5", "SME"),
            new Tuple<string, string>("INT-8", "Team Building"),
            new Tuple<string, string>("INT-23", "Holiday"),
            new Tuple<string, string>("INT-19", "Scrum")
        };

        private string _message;
        private TimeSpan _totalTime;
        private DateTime _filterDay;
        private DateTime _smallestSaved;
        public static string AutosaveFile { get; } = Path.Combine(Directory.GetCurrentDirectory(), "TimeTrackers.autosave.json");

        public static ViewModel Instance { get; }

        static ViewModel()
        {
            Instance = new ViewModel();
        }

        private class DifferenceTimeTracker : TimeTracker
        {
            public TimeSpan Difference { get; }

            public DifferenceTimeTracker(TimeTracker tt, TimeSpan difference)
            {
                Difference = difference;
                Time = tt.Time;
                Group = tt.Group;
                UserNotes = tt.UserNotes;
                GitNotes = tt.GitNotes;
                Type = tt.Type;
            }
        }

        public enum TimeTrackerType
        {
            Normal,
            Lunch,
            EndOfDay
        };

        public class TimeTracker : IComparable<TimeTracker>, INotifyPropertyChanged
        {
            private DateTime _time;
            private string _group;
            private string _userNotes;
            private string _gitNotes;
            private TimeTrackerType _type;

            public DateTime Time
            {
                get => _time;
                set
                {
                    if (value.Equals(_time)) return;
                    _time = value;
                    OnPropertyChanged1();
                }
            }

            public string Group
            {
                get => _group;
                set
                {
                    if (value == _group) return;
                    _group = value;
                    OnPropertyChanged1();
                }
            }

            public string UserNotes
            {
                get => _userNotes;
                set
                {
                    if (value == _userNotes) return;
                    _userNotes = value;
                    OnPropertyChanged1();
                    OnPropertyChanged1(nameof(AllNotes));
                }
            }

            public string GitNotes
            {
                get => _gitNotes;
                set
                {
                    if (value == _gitNotes) return;
                    _gitNotes = value;
                    OnPropertyChanged1();
                    OnPropertyChanged1(nameof(AllNotes));
                }
            }

            public TimeTrackerType Type
            {
                get => _type;
                set
                {
                    if (value == _type) return;
                    _type = value;
                    OnPropertyChanged1();
                }
            }

            [JsonIgnore]
            public string AllNotes => (UserNotes + Environment.NewLine + GitNotes).Trim();

            public TimeTracker()
            {
                Time = Helpers.RoundToNearestInterval(DateTime.Now, new TimeSpan(0, 15, 0));
                Type = TimeTrackerType.Normal;
            }

            public int CompareTo(TimeTracker other)
            {
                return Comparer<DateTime>.Default.Compare(Time, other.Time);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected virtual void OnPropertyChanged1([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class FinalTracker
        {
            public TimeSpan Time { get; set; }
            public string Group { get; set; }
            public string Notes { get; set; }
        }

        public ObservableCollection<TimeTracker> TimeTrackers { get; }
        public ObservableCollection<FinalTracker> FinalTrackers { get; }
        public ICollectionView TimeTrackersByDay { get; }

        public string Message
        {
            get => _message;
            set
            {
                if (value == _message) return;
                _message = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                if (value.Equals(_totalTime)) return;
                _totalTime = value;
                OnPropertyChanged();
            }
        }

        public DateTime FilterDay
        {
            get => _filterDay;
            set
            {
                if (value.Equals(_filterDay)) return;
                _filterDay = value;
                OnPropertyChanged();
            }
        }

        public DateTime SmallestSaved
        {
            get => _smallestSaved;
            set
            {
                if (value.Equals(_smallestSaved)) return;
                _smallestSaved = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand RemoveCommand { get; }

        private bool FilterByDay(object obj)
        {
            if (!(obj is TimeTracker tt))
            {
                throw new ArgumentException("Object is not a time tracker", nameof(obj));
            }

            return tt.Time.Date == FilterDay.Date;
        }

        private ViewModel()
        {
            TimeTrackers = new ObservableCollection<TimeTracker>();
            FinalTrackers = new ObservableCollection<FinalTracker>();
            FilterDay = DateTime.Now;

            TimeTrackersByDay = CollectionViewSource.GetDefaultView(TimeTrackers);
            TimeTrackersByDay.Filter = FilterByDay;
            PropertyChanged += (src, args) =>
            {
                if (args.PropertyName == nameof(FilterDay))
                {
                    Application.Current.Dispatcher.Invoke(TimeTrackersByDay.Refresh);
                }
            };

            RemoveCommand = new RelayCommand(RemoveCommand_Execute);

            if (File.Exists(AutosaveFile))
            {
                var tts = JsonConvert.DeserializeObject<List<TimeTracker>>(File.ReadAllText(AutosaveFile));
                foreach (var tt in tts)
                {
                    TimeTrackers.Add(tt);
                }

                Message = "Timers loaded from cache";
            }

            if (TimeTrackers.All(t => t.Time.Date != DateTime.Now.Date))
            {
                TimeTrackers.Add(new TimeTracker());
            }

            SetSmallest();

            TimeTrackers.CollectionChanged += (src, args) =>
            {
                Application.Current.Dispatcher.Invoke(SetSmallest);
            };

            var timer = new Timer(30000);
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void RemoveCommand_Execute(object o)
        {
            switch (o)
            {
                case TimeTracker tt:
                    TimeTrackers.Remove(tt);
                    break;
                case Watchable<string> gp:
                    Settings.Default.GitPaths.Remove(gp);
                    break;
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SaveTimers();
        }

        private TimeSpan? ToHourMinute(DateTime? dateTime)
        {
            if (dateTime == null)
            {
                return null;
            }

            return ToHourMinute((DateTime)dateTime);
        }

        private static TimeSpan ToHourMinute(DateTime dateTime)
        {
            return new TimeSpan(dateTime.TimeOfDay.Hours, dateTime.TimeOfDay.Minutes, 0);
        }

        private void SetSmallest()
        {
            SmallestSaved = (from tt in TimeTrackers let d = tt.Time orderby tt.Time select d).FirstOrDefault();
        }

        public void CalculateFinals()
        {
            FinalTrackers.Clear();
            TotalTime = new TimeSpan();

            var tts = TimeTrackersByDay.Cast<TimeTracker>().Where(t => !string.IsNullOrWhiteSpace(t.AllNotes));
            var finals = new List<DifferenceTimeTracker>();
            for (var node = new LinkedList<TimeTracker>(tts).First; node != null; node = node.Next)
            {
                // Stop processing on the EOD time tracker
                if (node.Value.Type == TimeTrackerType.EndOfDay)
                {
                    break;
                }

                finals.Add(new DifferenceTimeTracker(node.Value, (ToHourMinute(node.Next?.Value?.Time) ?? ToHourMinute(DateTime.Now)) - ToHourMinute(node.Value.Time)));
            }

            var groupedFinals =
                from f in finals
                where f.Type != TimeTrackerType.Lunch
                group f by f.Group
                into g
                select g;

            foreach (var tg in groupedFinals)
            {
                FinalTrackers.Add(new FinalTracker()
                {
                    Time = new TimeSpan(tg.Sum(t => t.Difference.Ticks)),
                    Group = tg.Key,
                    Notes = string.Join(Environment.NewLine,
                        from n in tg
                        where n.AllNotes.Length > 2
                        select n.AllNotes.Replace($"{tg.Key}: ", "")
                    )
                });
            }

            TotalTime = new TimeSpan(FinalTrackers.Sum(t => t.Time.Ticks));
        }

        public void SaveTimers()
        {
            var toRemove = (
                    from tt in TimeTrackersByDay.Cast<TimeTracker>()
                    group tt by tt.Time.Date into g
                    orderby g
                    select g.Key.Date
                )
                .Skip(Settings.Default.DaysToKeep)
                .ToArray();

            var toSave =
                from tt in TimeTrackers
                where !toRemove.Contains(tt.Time.Date)
                select tt;

            File.WriteAllText(AutosaveFile, JsonConvert.SerializeObject(toSave));
            Message = $"Timers saved to cache at {DateTime.Now}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
