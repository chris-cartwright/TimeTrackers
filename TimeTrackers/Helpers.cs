using System;
using System.Collections.Generic;

namespace TimeTrackers {
    public static class Helpers {
        public static void BubbleSort<T>(this IList<T> o) where T : IComparable<T> {
            for (int i = o.Count - 1; i >= 0; i--) {
                for (int j = 1; j <= i; j++) {
                    T o1 = o[j - 1];
                    T o2 = o[j];
                    if (((IComparable<T>)o1).CompareTo(o2) > 0) {
                        o.Remove(o1);
                        o.Insert(j, o1);
                    }
                }
            }
        }

        public static DateTime RoundToNearestInterval(DateTime dt, TimeSpan d) {
            int f = 0;
            double m = (double)(dt.Ticks % d.Ticks) / d.Ticks;
            if (m >= 0.5) {
                f = 1;
            }

            return new DateTime(((dt.Ticks / d.Ticks) + f) * d.Ticks);
        }
    }
}
