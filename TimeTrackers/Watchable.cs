using System;
using PostSharp.Patterns.Model;

namespace TimeTrackers {
    [Serializable]
    [NotifyPropertyChanged]
    public class Watchable<T> {
        public static implicit operator T(Watchable<T> watchable) {
            return watchable.Value;
        }

        public T Value { get; set; }

        public Watchable() {
        }

        public Watchable(T value) {
            Value = value;
        }
    }
}
