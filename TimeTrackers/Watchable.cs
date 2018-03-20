using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TimeTrackers.Annotations;

namespace TimeTrackers
{
    [Serializable]
    public class Watchable<T> : INotifyPropertyChanged
    {
        private T _value;

        public static implicit operator T(Watchable<T> watchable)
        {
            return watchable.Value;
        }

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public Watchable()
        {
        }

        public Watchable(T value)
        {
            Value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
