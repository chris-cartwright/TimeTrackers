using System;
using System.Collections.Generic;

namespace TimeTrackers {
    public static class Helpers {
        public static void BubbleSort<T>(this IList<T> o) where T : IComparable<T> {
            for (var i = o.Count - 1; i >= 0; i--) {
                for (var j = 1; j <= i; j++) {
                    var o1 = o[j - 1];
                    var o2 = o[j];
                    if (((IComparable<T>)o1).CompareTo(o2) > 0) {
                        o.Remove(o1);
                        o.Insert(j, o1);
                    }
                }
            }
        }

        public static DateTime RoundToNearestInterval(DateTime dt, TimeSpan d) {
            var f = 0;
            var m = (double)(dt.Ticks % d.Ticks) / d.Ticks;
            if (m >= 0.5) {
                f = 1;
            }

            return new DateTime(((dt.Ticks / d.Ticks) + f) * d.Ticks);
        }
    }
}
