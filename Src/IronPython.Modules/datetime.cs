/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

[assembly: PythonModule("datetime", typeof(IronPython.Modules.PythonDateTime))]
namespace IronPython.Modules {
    public class PythonDateTime {
        public static readonly int MAXYEAR = DateTime.MaxValue.Year;
        public static readonly int MINYEAR = DateTime.MinValue.Year;
        public const string __doc__ = "Provides functions and types for working with dates and times.";

        [PythonType]
        public class timedelta : ICodeFormattable {
            internal int _days;
            internal int _seconds;
            internal int _microseconds;

            private TimeSpan _tsWithDaysAndSeconds, _tsWithSeconds; // value type
            private bool _fWithDaysAndSeconds = false; // whether _tsWithDaysAndSeconds initialized
            private bool _fWithSeconds = false;

            internal static readonly timedelta _DayResolution = new timedelta(1, 0, 0);
            // class attributes:
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly timedelta resolution = new timedelta(0, 0, 1);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly timedelta min = new timedelta(-MAXDAYS, 0, 0);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly timedelta max = new timedelta(MAXDAYS, 86399, 999999);

            private const int MAXDAYS = 999999999;
            private const double SECONDSPERDAY = 24 * 60 * 60;

            internal timedelta(double days, double seconds, double microsecond)
                : this(days, seconds, microsecond, 0, 0, 0, 0) {
            }

            internal timedelta(TimeSpan ts, double microsecond)
                : this(ts.Days, ts.Seconds, microsecond, ts.Milliseconds, ts.Minutes, ts.Hours, 0) {
            }

            public timedelta(double days, double seconds, double microseconds, double milliseconds, double minutes, double hours, double weeks) {
                double totalDays = weeks * 7 + days;
                double totalSeconds = ((totalDays * 24 + hours) * 60 + minutes) * 60 + seconds;

                double totalSecondsSharp = Math.Floor(totalSeconds);
                double totalSecondsFloat = totalSeconds - totalSecondsSharp;

                double totalMicroseconds = Math.Round(totalSecondsFloat * 1e6 + milliseconds * 1000 + microseconds);
                double otherSecondsFromMicroseconds = Math.Floor(totalMicroseconds / 1e6);

                totalSecondsSharp += otherSecondsFromMicroseconds;
                totalMicroseconds -= otherSecondsFromMicroseconds * 1e6;

                if (totalSecondsSharp > 0 && totalMicroseconds < 0) {
                    totalSecondsSharp -= 1;
                    totalMicroseconds += 1e6;
                }

                _days = (int)(totalSecondsSharp / SECONDSPERDAY);
                _seconds = (int)(totalSecondsSharp - _days * SECONDSPERDAY);

                if (_seconds < 0) {
                    _days--;
                    _seconds += (int)SECONDSPERDAY;
                }
                _microseconds = (int)(totalMicroseconds);

                if (Math.Abs(_days) > MAXDAYS) {
                    throw PythonOps.OverflowError("days={0}; must have magnitude <= 999999999", _days);
                }
            }

            public static timedelta __new__(CodeContext context, PythonType cls,
                [DefaultParameterValue(0D)] double days,
                [DefaultParameterValue(0D)] double seconds,
                [DefaultParameterValue(0D)] double microseconds,
                [DefaultParameterValue(0D)] double milliseconds,
                [DefaultParameterValue(0D)] double minutes,
                [DefaultParameterValue(0D)] double hours,
                [DefaultParameterValue(0D)] double weeks) {
                if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(timedelta))) {
                    return new timedelta(days, seconds, microseconds, milliseconds, minutes, hours, weeks);
                } else {
                    timedelta delta = cls.CreateInstance(context, days, seconds, microseconds, milliseconds, minutes, hours, weeks) as timedelta;
                    if (delta == null) throw PythonOps.TypeError("{0} is not a subclass of datetime.timedelta", cls);
                    return delta;
                }
            }

            // instance attributes:
            public int days {
                get { return _days; }
            }

            public int seconds {
                get { return _seconds; }
            }

            public int microseconds {
                get { return _microseconds; }
            }

            internal TimeSpan TimeSpanWithDaysAndSeconds {
                get {
                    if (!_fWithDaysAndSeconds) {
                        _tsWithDaysAndSeconds = new TimeSpan(_days, 0, 0, _seconds);
                        _fWithDaysAndSeconds = true;
                    }
                    return _tsWithDaysAndSeconds;
                }
            }

            internal TimeSpan TimeSpanWithSeconds {
                get {
                    if (!_fWithSeconds) {
                        _tsWithSeconds = TimeSpan.FromSeconds(_seconds);
                        _fWithSeconds = true;
                    }
                    return _tsWithSeconds;
                }
            }

            // supported operations:
            public static timedelta operator +(timedelta self, timedelta other) {
                return new timedelta(self._days + other._days, self._seconds + other._seconds, self._microseconds + other._microseconds);
            }

            public static timedelta operator -(timedelta self, timedelta other) {
                return new timedelta(self._days - other._days, self._seconds - other._seconds, self._microseconds - other._microseconds);
            }

            public static timedelta operator -(timedelta self) {
                return new timedelta(-self._days, -self._seconds, -self._microseconds);
            }

            public static timedelta operator +(timedelta self) {
                return new timedelta(self._days, self._seconds, self._microseconds);
            }

            public static timedelta operator *(timedelta self, int other) {
                return new timedelta(self._days * other, self._seconds * other, self._microseconds * other);
            }

            public static timedelta operator *(int other, timedelta self) {
                return new timedelta(self._days * other, self._seconds * other, self._microseconds * other);
            }

            public static timedelta operator /(timedelta self, int other) {
                return new timedelta((double)self._days / other, (double)self._seconds / other, (double)self._microseconds / other);
            }

            public static timedelta operator *(timedelta self, BigInteger other) {
                return self * (int)other;
            }

            public static timedelta operator *(BigInteger other, timedelta self) {
                return (int)other * self;
            }

            public static timedelta operator /(timedelta self, BigInteger other) {
                return self / (int)other;
            }

            public timedelta __pos__() { return +this; }
            public timedelta __neg__() { return -this; }
            public timedelta __abs__() { return (_days > 0) ? this : -this; }
            [SpecialName]
            public timedelta FloorDivide(int y) {
                return this / y;
            }

            [SpecialName]
            public timedelta ReverseFloorDivide(int y) {
                return this / y;
            }

            public double total_seconds() {
                var total_microseconds = (double) this.microseconds + (this.seconds + this.days * 24.0 * 3600.0) * 1000000.0;
                return total_microseconds / 1000000.0;
            }

            public bool __nonzero__() {
                return this._days != 0 || this._seconds != 0 || this._microseconds != 0;
            }

            public PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(GetType()), 
                    PythonTuple.MakeTuple(_days, _seconds, _microseconds)
                );
            }

            public static object __getnewargs__(int days, int seconds, int microseconds) {
                return PythonTuple.MakeTuple(new timedelta(days, seconds, microseconds, 0, 0, 0, 0));
            }

            public override bool Equals(object obj) {
                timedelta delta = obj as timedelta;
                if (delta == null) return false;

                return this._days == delta._days && this._seconds == delta._seconds && this._microseconds == delta._microseconds;
            }

            public override int GetHashCode() {
                return this._days ^ this._seconds ^ this._microseconds;
            }

            public override string ToString() {
                StringBuilder sb = new StringBuilder();
                if (_days != 0) {
                    sb.Append(_days);
                    if (Math.Abs(_days) == 1)
                        sb.Append(" day, ");
                    else
                        sb.Append(" days, ");
                }

                sb.AppendFormat("{0}:{1:d2}:{2:d2}", TimeSpanWithSeconds.Hours, TimeSpanWithSeconds.Minutes, TimeSpanWithSeconds.Seconds);

                if (_microseconds != 0)
                    sb.AppendFormat(".{0:d6}", _microseconds);

                return sb.ToString();
            }

            #region Rich Comparison Members

            private int CompareTo(object other) {
                timedelta delta = other as timedelta;
                if (delta == null)
                    throw PythonOps.TypeError("can't compare datetime.timedelta to {0}", PythonTypeOps.GetName(other));

                int res = this._days - delta._days;
                if (res != 0) return res;

                res = this._seconds - delta._seconds;
                if (res != 0) return res;

                return this._microseconds - delta._microseconds;
            }

            public static bool operator >(timedelta self, object other) {
                return self.CompareTo(other) > 0;
            }

            public static bool operator <(timedelta self, object other) {
                return self.CompareTo(other) < 0;
            }

            public static bool operator >=(timedelta self, object other) {
                return self.CompareTo(other) >= 0;
            }

            public static bool operator <=(timedelta self, object other) {
                return self.CompareTo(other) <= 0;
            }

            #endregion

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                if (_seconds == 0 && _microseconds == 0) {
                    return String.Format("datetime.timedelta({0})", _days);
                } else if (_microseconds == 0) {
                    return String.Format("datetime.timedelta({0}, {1})", _days, _seconds);
                } else {
                    return String.Format("datetime.timedelta({0}, {1}, {2})", _days, _seconds, _microseconds);
                }
            }

            #endregion
        }

        internal static void ThrowIfInvalid(timedelta delta, string funcname) {
            if (delta != null) {
                if (delta._microseconds != 0 || delta._seconds % 60 != 0) {
                    throw PythonOps.ValueError("tzinfo.{0}() must return a whole number of minutes", funcname);
                }

                int minutes = (int)(delta.TimeSpanWithDaysAndSeconds.TotalSeconds / 60);
                if (Math.Abs(minutes) >= 1440) {
                    throw PythonOps.ValueError("tzinfo.{0}() returned {1}; must be in -1439 .. 1439", funcname, minutes);
                }
            }
        }

        internal enum InputKind { Year, Month, Day, Hour, Minute, Second, Microsecond }

        internal static void ValidateInput(InputKind kind, int value) {
            switch (kind) {
                case InputKind.Year:
                    if (value > DateTime.MaxValue.Year || value < DateTime.MinValue.Year) {
                        throw PythonOps.ValueError("year is out of range");
                    }
                    break;
                case InputKind.Month:
                    if (value > 12 || value < 1) {
                        throw PythonOps.ValueError("month must be in 1..12");
                    }
                    break;
                case InputKind.Day:
                    // TODO: changing upper bound
                    if (value > 31 || value < 1) {
                        throw PythonOps.ValueError("day is out of range for month");
                    }
                    break;
                case InputKind.Hour:
                    if (value > 23 || value < 0) {
                        throw PythonOps.ValueError("hour must be in 0..23");
                    }
                    break;
                case InputKind.Minute:
                    if (value > 59 || value < 0) {
                        throw PythonOps.ValueError("minute must be in 0..59");
                    }
                    break;
                case InputKind.Second:
                    if (value > 59 || value < 0) {
                        throw PythonOps.ValueError("second must be in 0..59");
                    }
                    break;
                case InputKind.Microsecond:
                    if (value > 999999 || value < 0) {
                        throw PythonOps.ValueError("microsecond must be in 0..999999");
                    }
                    break;
            }
        }

        internal static bool IsNaiveTimeZone(tzinfo tz) {
            if (tz == null) return true;
            if (tz.utcoffset(null) == null) return true;
            return false;
        }

        internal static int CastToInt(object o) {
            return o is BigInteger ? (int)(BigInteger)o : (int)o;
        }

        [PythonType]
        public class date : ICodeFormattable {
            internal DateTime _dateTime;
            // class attributes
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly date min = new date(new DateTime(1, 1, 1));
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly date max = new date(new DateTime(9999, 12, 31));
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly timedelta resolution = timedelta._DayResolution;

            protected date() { }

            public date(int year, int month, int day) {
                PythonDateTime.ValidateInput(InputKind.Year, year);
                PythonDateTime.ValidateInput(InputKind.Month, month);
                PythonDateTime.ValidateInput(InputKind.Day, day);

                _dateTime = new DateTime(year, month, day);
            }

            internal date(DateTime value) {
                _dateTime = value.Date; // no hour, minute, second
            }

            // other constructors, all class methods
            public static object today() {
                return new date(DateTime.Today);
            }

            public static date fromordinal(int d) {
                if (d < 1) {
                    throw PythonOps.ValueError("ordinal must be >= 1");
                }
                return new date(min._dateTime.AddDays(d - 1));
            }

            public static date fromtimestamp(double timestamp) {
                DateTime dt = PythonTime.TimestampToDateTime(timestamp);
                dt = dt.AddSeconds(-PythonTime.timezone);

                return new date(dt.Year, dt.Month, dt.Day);
            }

            // instance attributes
            public int year {
                get { return _dateTime.Year; }
            }

            public int month {
                get { return _dateTime.Month; }
            }

            public int day {
                get { return _dateTime.Day; }
            }

            internal DateTime InternalDateTime {
                get { return _dateTime; }
                set { _dateTime = value; }
            }

            // supported operations
            public static date operator +([NotNull]date self, [NotNull]timedelta other) {
                try {
                    return new date(self._dateTime.AddDays(other.days));
                } catch {
                    throw PythonOps.OverflowError("date value out of range");
                }
            }
            public static date operator +([NotNull]timedelta other, [NotNull]date self) {
                try {
                    return new date(self._dateTime.AddDays(other.days));
                } catch {
                    throw PythonOps.OverflowError("date value out of range");
                }
            }

            public static date operator -(date self, timedelta delta) {
                try {
                    return new date(self._dateTime.AddDays(-1 * delta.days));
                } catch {
                    throw PythonOps.OverflowError("date value out of range");
                }
            }

            public static timedelta operator -(date self, date other) {
                TimeSpan ts = self._dateTime - other._dateTime;
                return new timedelta(0, ts.TotalSeconds, ts.Milliseconds * 1000);
            }

            public virtual PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(this.GetType()), PythonTuple.MakeTuple(_dateTime.Year, _dateTime.Month, _dateTime.Day));
            }

            public static object __getnewargs__(CodeContext context, int year, int month, int day) {
                return PythonTuple.MakeTuple(new date(year, month, day));
            }

            public object replace() {
                return this;
            }

            // instance methods
            public virtual date replace(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict) {
                int year2 = _dateTime.Year;
                int month2 = _dateTime.Month;
                int day2 = _dateTime.Day;

                foreach (KeyValuePair<object, object> kvp in (IDictionary<object, object>)dict) {
                    string strVal = kvp.Key as string;
                    if (strVal == null) continue;

                    switch (strVal) {
                        case "year": year2 = CastToInt(kvp.Value); break;
                        case "month": month2 = CastToInt(kvp.Value); break;
                        case "day": day2 = CastToInt(kvp.Value); break;
                        default: throw PythonOps.TypeError("{0} is an invalid keyword argument for this function", kvp.Key);
                    }
                }

                return new date(year2, month2, day2);
            }

            public virtual object timetuple() {
                return PythonTime.GetDateTimeTuple(_dateTime);
            }

            public int toordinal() {
                return (_dateTime - min._dateTime).Days + 1;
            }

            public int weekday() { return PythonTime.Weekday(_dateTime); }

            public int isoweekday() { return PythonTime.IsoWeekday(_dateTime); }

            private DateTime FirstDayOfIsoYear(int year) {
                DateTime firstDay = new DateTime(year, 1, 1);
                DateTime firstIsoDay = firstDay;

                switch (firstDay.DayOfWeek) {
                    case DayOfWeek.Sunday:
                        firstIsoDay = firstDay.AddDays(1);
                        break;
                    case DayOfWeek.Monday:
                    case DayOfWeek.Tuesday:
                    case DayOfWeek.Wednesday:
                    case DayOfWeek.Thursday:
                        firstIsoDay = firstDay.AddDays(-1 * ((int)firstDay.DayOfWeek - 1));
                        break;
                    case DayOfWeek.Friday:
                        firstIsoDay = firstDay.AddDays(3);
                        break;
                    case DayOfWeek.Saturday:
                        firstIsoDay = firstDay.AddDays(2);
                        break;
                }
                return firstIsoDay;
            }

            public PythonTuple isocalendar() {
                DateTime firstDayOfLastIsoYear = FirstDayOfIsoYear(_dateTime.Year - 1);
                DateTime firstDayOfThisIsoYear = FirstDayOfIsoYear(_dateTime.Year);
                DateTime firstDayOfNextIsoYear = FirstDayOfIsoYear(_dateTime.Year + 1);

                int year, days;
                if (firstDayOfThisIsoYear <= _dateTime && _dateTime < firstDayOfNextIsoYear) {
                    year = _dateTime.Year;
                    days = (_dateTime - firstDayOfThisIsoYear).Days;
                } else if (_dateTime < firstDayOfThisIsoYear) {
                    year = _dateTime.Year - 1;
                    days = (_dateTime - firstDayOfLastIsoYear).Days;
                } else {
                    year = _dateTime.Year + 1;
                    days = (_dateTime - firstDayOfNextIsoYear).Days;
                }

                return PythonTuple.MakeTuple(year, days / 7 + 1, days % 7 + 1);
            }

            public string isoformat() {
                return _dateTime.ToString("yyyy-MM-dd");
            }

            public override string ToString() {
                return isoformat();
            }

            public string ctime() {
                return _dateTime.ToString("ddd MMM ", CultureInfo.InvariantCulture) +
                    string.Format(CultureInfo.InvariantCulture, "{0,2}", _dateTime.Day) +
                    _dateTime.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture);
            }

            public virtual string strftime(CodeContext/*!*/ context, string dateFormat) {
                return PythonTime.strftime(context, dateFormat, _dateTime, null);
            }

            public override bool Equals(object obj) {
                if (obj == null) return false;
                
                date other = obj as date;
                if (other != null && !(obj is datetime)) {
                    return this._dateTime == other._dateTime;
                } else {
                    return false;
                }
            }

            public override int GetHashCode() {
                return _dateTime.GetHashCode();
            }

            #region Rich Comparison Members

            internal virtual int CompareTo(object other) {
                date date = other as date;
                return this._dateTime.CompareTo(date._dateTime);
            }

            internal bool CheckType(object other) {
                return CheckType(other, true);
            }

            /// <summary>
            /// Used to check the type to see if we can do a comparison.  Returns true if we can
            /// or false if we should return NotImplemented.  May throw if the type's really wrong.
            /// </summary>
            internal bool CheckType(object other, bool shouldThrow) {
                if (other == null) {
                    return CheckTypeError(other, shouldThrow);
                }

                if (other.GetType() != GetType()) {
                    // if timetuple is defined on the other object go ahead and let it try the compare,
                    // but only if it's a user-defined object
                    if (!(GetType() == typeof(date) && other.GetType() == typeof(datetime) ||
                        GetType() == typeof(datetime) & other.GetType() == typeof(date))) {

                        if (PythonOps.HasAttr(DefaultContext.Default, other, "timetuple")) {
                            return false;
                        }
                    }

                    return CheckTypeError(other, shouldThrow);
                }

                return true;
            }

            private static bool CheckTypeError(object other, bool shouldThrow) {
                if (shouldThrow) {
                    throw PythonOps.TypeError("can't compare datetime.date to {0}", PythonTypeOps.GetName(other));
                } else {
                    return true;
                }
            }

            [return: MaybeNotImplemented]
            public static object operator >(date self, object other) {
                if (!self.CheckType(other)) return NotImplementedType.Value;

                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.BooleanToObject(self.CompareTo(other) > 0);
            }

            [return: MaybeNotImplemented]
            public static object operator <(date self, object other) {
                if (!self.CheckType(other)) return NotImplementedType.Value;

                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.BooleanToObject(self.CompareTo(other) < 0);
            }

            [return: MaybeNotImplemented]
            public static object operator >=(date self, object other) {
                if (!self.CheckType(other)) return NotImplementedType.Value;

                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.BooleanToObject(self.CompareTo(other) >= 0);
            }

            [return: MaybeNotImplemented]
            public static object operator <=(date self, object other) {
                if (!self.CheckType(other)) return NotImplementedType.Value;

                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.BooleanToObject(self.CompareTo(other) <= 0);
            }

            public object __eq__(object other) {
                if (!CheckType(other, false)) return NotImplementedType.Value;

                return Equals(other);
            }

            public object __ne__(object other) {
                if (!CheckType(other, false)) return NotImplementedType.Value;

                return !Equals(other);
            }

            #endregion

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                return string.Format("datetime.date({0}, {1}, {2})", _dateTime.Year, _dateTime.Month, _dateTime.Day);
            }

            public virtual string __format__(CodeContext/*!*/ context, string dateFormat){
                if (string.IsNullOrEmpty(dateFormat)) {
                    return PythonOps.ToString(context, this);
                } else {
                    return this.strftime(context, dateFormat);
                }
            }

            #endregion
        }

        [PythonType]
        public class datetime : date, ICodeFormattable {
            internal int _lostMicroseconds;
            internal tzinfo _tz;

            // class attributes
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public new static readonly datetime max = new datetime(DateTime.MaxValue, 999, null);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public new static readonly datetime min = new datetime(DateTime.MinValue, 0, null);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public new static readonly timedelta resolution = timedelta.resolution;


            private UnifiedDateTime _utcDateTime;

            public datetime(int year,
                int month,
                int day,
               [DefaultParameterValue(0)]int hour,
               [DefaultParameterValue(0)]int minute,
               [DefaultParameterValue(0)]int second,
               [DefaultParameterValue(0)]int microsecond,
               [DefaultParameterValue(null)]tzinfo tzinfo) {

                PythonDateTime.ValidateInput(InputKind.Year, year);
                PythonDateTime.ValidateInput(InputKind.Month, month);
                PythonDateTime.ValidateInput(InputKind.Day, day);
                PythonDateTime.ValidateInput(InputKind.Hour, hour);
                PythonDateTime.ValidateInput(InputKind.Minute, minute);
                PythonDateTime.ValidateInput(InputKind.Second, second);
                PythonDateTime.ValidateInput(InputKind.Microsecond, microsecond);

                InternalDateTime = new DateTime(year, month, day, hour, minute, second, microsecond / 1000);
                _lostMicroseconds = microsecond % 1000;
                _tz = tzinfo;
            }

            public datetime([NotNull]string str) {
                if (str.Length != 10) {
                    throw PythonOps.TypeError("an integer is required");
                }

                int microSeconds = (((int)str[7]) << 16) | ((int)str[8] << 8) | (int)str[9];
                int month = (int)str[2];
                if (month == 0 || month > 12) {
                    throw PythonOps.TypeError("invalid month");
                }
                InternalDateTime = new DateTime(
                    (((int)str[0]) << 8) | (int)str[1], 
                    month, 
                    (int)str[3], 
                    (int)str[4], 
                    (int)str[5], 
                    (int)str[6], 
                    microSeconds  / 1000
                );
                _lostMicroseconds = microsecond % 1000;
            }

            public datetime([NotNull]string str, [NotNull]tzinfo tzinfo)
                : this(str) {
                _tz = tzinfo;
            }

            private void Initialize(int year, int month, int day, int hour, int minute, int second, int microsecond, tzinfo tzinfo) {
            }

            public datetime(DateTime dt)
                : this(dt, 0, null) {
            }

            // just present to match CPython's error messages...
            public datetime(params object[] args) {
                
                if (args.Length < 3) {
                    throw PythonOps.TypeError("function takes at least 3 arguments ({0} given)", args.Length);
                } else if (args.Length > 8) {
                    throw PythonOps.TypeError("function takes at most 8 arguments ({0} given)", args.Length);
                }
                
                for (int i = 0; i < args.Length && i < 7; i++) {    // 8 is offsetof tzinfo
                    if (!(args[i] is int)) {
                        throw PythonOps.TypeError("an integer is required");
                    }
                }

                if (args.Length > 7 && !(args[7] is tzinfo || args[7] == null)) {
                    throw PythonOps.TypeError("tzinfo argument must be None or of a tzinfo subclass, not type '{0}'", PythonTypeOps.GetName(args[7]));
                }

                // the above cases should cover all binding failures...
                throw new InvalidOperationException();
            }

            internal datetime(DateTime dt, int lostMicroseconds, tzinfo tzinfo) {
                this.InternalDateTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
                this._lostMicroseconds = dt.Millisecond * 1000 + lostMicroseconds;
                this._tz = tzinfo;

                // make sure both are positive, and lostMicroseconds < 1000
                if (_lostMicroseconds < 0) {
                    try {
                        InternalDateTime = InternalDateTime.AddMilliseconds(_lostMicroseconds / 1000 - 1);
                    } catch {
                        throw PythonOps.OverflowError("date value out of range");
                    }
                    _lostMicroseconds = _lostMicroseconds % 1000 + 1000;
                }

                if (_lostMicroseconds > 999) {
                    try {
                        InternalDateTime = InternalDateTime.AddMilliseconds(_lostMicroseconds / 1000);
                    } catch {
                        throw PythonOps.OverflowError("date value out of range");
                    }
                    _lostMicroseconds = _lostMicroseconds % 1000;
                }
            }

            // other constructors, all class methods:
            public static object now([DefaultParameterValue(null)]tzinfo tz) {
                if (tz != null) {
                    return tz.fromutc(new datetime(DateTime.UtcNow, 0, tz));
                } else {
                    return new datetime(DateTime.Now, 0, null);
                }
            }

            public static object utcnow() {
                return new datetime(DateTime.UtcNow, 0, null);
            }

            
            public new static object today() {
                return new datetime(DateTime.Now, 0, null);
            }           

            public static object fromtimestamp(double timestamp, [DefaultParameterValue(null)] tzinfo tz) {
                DateTime dt = PythonTime.TimestampToDateTime(timestamp);
                dt = dt.AddSeconds(-PythonTime.timezone);

                if (tz != null) {
                    dt = dt.ToUniversalTime();
                    datetime pdtc = new datetime(dt, 0, tz);
                    return tz.fromutc(pdtc);
                } else {
                    return new datetime(dt, 0, null);
                }
            }

            public static datetime utcfromtimestamp(double timestamp) {
                DateTime dt = new DateTime(PythonTime.TimestampToTicks(timestamp), DateTimeKind.Utc);
                return new datetime(dt, 0, null);
            }

            public new static datetime fromordinal(int d) {
                if (d < 1) {
                    throw PythonOps.ValueError("ordinal must be >= 1");
                }
                return new datetime(DateTime.MinValue + new TimeSpan(d - 1, 0, 0, 0), 0, null);
            }

            public static object combine(date date, time time) {
                return new datetime(date.year, date.month, date.day, time.hour, time.minute, time.second, time.microsecond, time.tzinfo);
            }

            // instance attributes
            public int hour {
                get { return InternalDateTime.Hour; }
            }

            public int minute {
                get { return InternalDateTime.Minute; }
            }

            public int second {
                get { return InternalDateTime.Second; }
            }

            public int microsecond {
                get { return InternalDateTime.Millisecond * 1000 + _lostMicroseconds; }
            }

            public object tzinfo {
                get { return _tz; }
            }

            private UnifiedDateTime UtcDateTime {
                get {
                    if (_utcDateTime == null) {
                        _utcDateTime = new UnifiedDateTime();

                        _utcDateTime.DateTime = InternalDateTime;
                        _utcDateTime.LostMicroseconds = _lostMicroseconds;

                        timedelta delta = this.utcoffset();
                        if (delta != null) {
                            datetime utced = this - delta;
                            _utcDateTime.DateTime = utced.InternalDateTime;
                            _utcDateTime.LostMicroseconds = utced._lostMicroseconds;
                        }
                    }
                    return _utcDateTime;
                }
            }

            // supported operations
            public static datetime operator +([NotNull]datetime date, [NotNull]timedelta delta) {
                try {
                    return new datetime(date.InternalDateTime.Add(delta.TimeSpanWithDaysAndSeconds), delta._microseconds + date._lostMicroseconds, date._tz);
                } catch (ArgumentException) {
                    throw new OverflowException("date value out of range");
                }
            }

            public static datetime operator +([NotNull]timedelta delta, [NotNull]datetime date) {
                try {
                    return new datetime(date.InternalDateTime.Add(delta.TimeSpanWithDaysAndSeconds), delta._microseconds + date._lostMicroseconds, date._tz);
                } catch (ArgumentException) {
                    throw new OverflowException("date value out of range");
                }
            }

            public static datetime operator -(datetime date, timedelta delta) {
                return new datetime(date.InternalDateTime.Subtract(delta.TimeSpanWithDaysAndSeconds), date._lostMicroseconds - delta._microseconds, date._tz);
            }

            public static timedelta operator -(datetime date, datetime other) {
                if (CheckTzInfoBeforeCompare(date, other)) {
                    return new timedelta(date.InternalDateTime - other.InternalDateTime, date._lostMicroseconds - other._lostMicroseconds);
                } else {
                    return new timedelta(date.UtcDateTime.DateTime - other.UtcDateTime.DateTime, date.UtcDateTime.LostMicroseconds - other.UtcDateTime.LostMicroseconds);
                }
            }

            // instance methods
            public date date() {
                return new date(year, month, day);
            }

            [Documentation("gets the datetime w/o the time zone component")]
            public time time() {
                return new time(hour, minute, second, microsecond, null);
            }

            public object timetz() {
                return new time(hour, minute, second, microsecond, _tz);
            }

            [Documentation("gets a new datetime object with the fields provided as keyword arguments replaced.")]
            public override date replace(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict) {
                int lyear = year;
                int lmonth = month;
                int lday = day;
                int lhour = hour;
                int lminute = minute;
                int lsecond = second;
                int lmicrosecond = microsecond;
                tzinfo tz = _tz;

                foreach (KeyValuePair<object, object> kvp in (IDictionary<object, object>)dict) {
                    string key = kvp.Key as string;
                    if (key == null) continue;

                    switch (key) {
                        case "year":
                            lyear = CastToInt(kvp.Value);
                            break;
                        case "month":
                            lmonth = CastToInt(kvp.Value);
                            break;
                        case "day":
                            lday = CastToInt(kvp.Value);
                            break;
                        case "hour":
                            lhour = CastToInt(kvp.Value);
                            break;
                        case "minute":
                            lminute = CastToInt(kvp.Value);
                            break;
                        case "second":
                            lsecond = CastToInt(kvp.Value);
                            break;
                        case "microsecond":
                            lmicrosecond = CastToInt(kvp.Value);
                            break;
                        case "tzinfo":
                            tz = kvp.Value as tzinfo;
                            break;
                        default:
                            throw PythonOps.TypeError("{0} is an invalid keyword argument for this function", kvp.Key);
                    }
                }
                return new datetime(lyear, lmonth, lday, lhour, lminute, lsecond, lmicrosecond, tz);
            }

            public object astimezone(tzinfo tz) {
                if (tz == null)
                    throw PythonOps.TypeError("astimezone() argument 1 must be datetime.tzinfo, not None");

                if (_tz == null)
                    throw PythonOps.ValueError("astimezone() cannot be applied to a naive datetime");

                if (tz == _tz)
                    return this;

                datetime utc = this - utcoffset();
                utc._tz = tz;
                return tz.fromutc(utc);
            }

            public timedelta utcoffset() {
                if (_tz == null) return null;
                timedelta delta = _tz.utcoffset(this);
                PythonDateTime.ThrowIfInvalid(delta, "utcoffset");
                return delta;
            }

            public timedelta dst() {
                if (_tz == null) return null;
                timedelta delta = _tz.dst(this);
                PythonDateTime.ThrowIfInvalid(delta, "dst");
                return delta;
            }

            public object tzname() {
                if (_tz == null) return null;
                return _tz.tzname(this);
            }

            public override object timetuple() {
                return PythonTime.GetDateTimeTuple(InternalDateTime, null, _tz);
            }

            public object utctimetuple() {
                if (_tz == null)
                    return PythonTime.GetDateTimeTuple(InternalDateTime, false);
                else {
                    datetime dtc = this - utcoffset();
                    return PythonTime.GetDateTimeTuple(dtc.InternalDateTime, false);
                }
            }

            public string isoformat([DefaultParameterValue('T')]char sep) {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0:d4}-{1:d2}-{2:d2}{3}{4:d2}:{5:d2}:{6:d2}", year, month, day, sep, hour, minute, second);

                if (microsecond != 0) sb.AppendFormat(".{0:d6}", microsecond);

                timedelta delta = utcoffset();
                if (delta != null) {
                    if (delta.TimeSpanWithDaysAndSeconds >= TimeSpan.Zero) {
                        sb.AppendFormat("+{0:d2}:{1:d2}", delta.TimeSpanWithDaysAndSeconds.Hours, delta.TimeSpanWithDaysAndSeconds.Minutes);
                    } else {
                        sb.AppendFormat("-{0:d2}:{1:d2}", -delta.TimeSpanWithDaysAndSeconds.Hours, -delta.TimeSpanWithDaysAndSeconds.Minutes);
                    }
                }

                return sb.ToString();
            }

            internal static bool CheckTzInfoBeforeCompare(datetime self, datetime other) {
                if (self._tz != other._tz) {
                    timedelta offset1 = self.utcoffset();
                    timedelta offset2 = other.utcoffset();

                    if ((offset1 == null && offset2 != null) || (offset1 != null && offset2 == null))
                        throw PythonOps.TypeError("can't compare offset-naive and offset-aware times");

                    return false;
                } else {
                    return true; // has the same TzInfo, Utcoffset will be skipped
                }
            }

            public override bool Equals(object obj) {
                datetime other = obj as datetime;
                if (other == null) return false;

                if (CheckTzInfoBeforeCompare(this, other)) {
                    return this.InternalDateTime.Equals(other.InternalDateTime) && this._lostMicroseconds == other._lostMicroseconds;
                } else {
                    // hack
                    TimeSpan delta = this.InternalDateTime - other.InternalDateTime;
                    if (Math.Abs(delta.TotalHours) > 24 * 2) {
                        return false;
                    } else {
                        return this.UtcDateTime.Equals(other.UtcDateTime);
                    }
                }
            }

            public override int GetHashCode() {
                return this.UtcDateTime.DateTime.GetHashCode() ^ this.UtcDateTime.LostMicroseconds;
            }

            public override string ToString() {
                return isoformat(' ');
            }

            public override PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(GetType()),
                    PythonTuple.MakeTuple(
                        InternalDateTime.Year, 
                        InternalDateTime.Month, 
                        InternalDateTime.Day,
                        InternalDateTime.Hour,
                        InternalDateTime.Minute,
                        InternalDateTime.Second,
                        this.microsecond,
                        this.tzinfo
                    )
                );
            }

            public override string strftime(CodeContext/*!*/ context, string dateFormat) {
                return PythonTime.strftime(context, dateFormat, _dateTime, microsecond);
            }

            public static datetime strptime(CodeContext/*!*/ context, string date_string, string format) {
                var packed = PythonTime._strptime(context, date_string, format);
                return new datetime((DateTime)packed[0]);
            }

            #region IRichComparable Members

            internal override int CompareTo(object other) {
                if (other == null)
                    throw PythonOps.TypeError("can't compare datetime.datetime to NoneType");

                datetime combo = other as datetime;
                if (combo == null)
                    throw PythonOps.TypeError("can't compare datetime.datetime to {0}", PythonTypeOps.GetName(other));

                if (CheckTzInfoBeforeCompare(this, combo)) {
                    int res = this.InternalDateTime.CompareTo(combo.InternalDateTime);

                    if (res != 0) return res;

                    return this._lostMicroseconds - combo._lostMicroseconds;
                } else {
                    TimeSpan delta = this.InternalDateTime - combo.InternalDateTime;
                    // hack
                    if (Math.Abs(delta.TotalHours) > 24 * 2) {
                        return delta > TimeSpan.Zero ? 1 : -1;
                    } else {
                        return this.UtcDateTime.CompareTo(combo.UtcDateTime);
                    }
                }
            }


            #endregion

            #region ICodeFormattable Members

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                StringBuilder sb = new StringBuilder();
                // TODO: need to determine how to get the actual class name if a derived type (CP21478)
                sb.AppendFormat("datetime.datetime({0}, {1}, {2}, {3}, {4}",
                    InternalDateTime.Year,
                    InternalDateTime.Month,
                    InternalDateTime.Day,
                    InternalDateTime.Hour,
                    InternalDateTime.Minute);

                if (microsecond != 0) {
                    sb.AppendFormat(", {0}, {1}", second, microsecond);
                } else {
                    if (second != 0) {
                        sb.AppendFormat(", {0}", second);
                    }
                }

                if (_tz != null) {
                    sb.AppendFormat(", tzinfo={0}", PythonOps.Repr(context, _tz));
                }
                sb.AppendFormat(")");
                return sb.ToString();
            }
            #endregion

            class UnifiedDateTime {
                public DateTime DateTime;
                public int LostMicroseconds;

                public override bool Equals(object obj) {
                    UnifiedDateTime other = obj as UnifiedDateTime;
                    if (other == null) return false;

                    return this.DateTime == other.DateTime && this.LostMicroseconds == other.LostMicroseconds;
                }

                public override int GetHashCode() {
                    return DateTime.GetHashCode() ^ LostMicroseconds;
                }

                public int CompareTo(UnifiedDateTime other) {
                    int res = this.DateTime.CompareTo(other.DateTime);

                    if (res != 0) return res;

                    return this.LostMicroseconds - other.LostMicroseconds;
                }
            }
        }

        [PythonType]
        public class time : ICodeFormattable {
            internal TimeSpan _timeSpan;
            internal int _lostMicroseconds;
            internal tzinfo _tz;
            private UnifiedTime _utcTime;

            // class attributes:
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly time max = new time(23, 59, 59, 999999, null);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly time min = new time(0, 0, 0, 0, null);
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly timedelta resolution = timedelta.resolution;

            public time([DefaultParameterValue(0)]int hour,
                [DefaultParameterValue(0)]int minute,
                [DefaultParameterValue(0)]int second,
                [DefaultParameterValue(0)]int microsecond,
                [DefaultParameterValue(null)]tzinfo tzinfo) {

                PythonDateTime.ValidateInput(InputKind.Hour, hour);
                PythonDateTime.ValidateInput(InputKind.Minute, minute);
                PythonDateTime.ValidateInput(InputKind.Second, second);
                PythonDateTime.ValidateInput(InputKind.Microsecond, microsecond);

                // all inputs are positive
                this._timeSpan = new TimeSpan(0, hour, minute, second, microsecond / 1000);
                this._lostMicroseconds = microsecond % 1000;
                this._tz = tzinfo;
            }

            internal time(TimeSpan timeSpan, int lostMicroseconds, tzinfo tzinfo) {
                this._timeSpan = timeSpan;
                this._lostMicroseconds = lostMicroseconds;
                this._tz = tzinfo;
            }

            // instance attributes:
            public int hour {
                get { return _timeSpan.Hours; }
            }

            public int minute {
                get { return _timeSpan.Minutes; }
            }

            public int second {
                get { return _timeSpan.Seconds; }
            }

            public int microsecond {
                get { return _timeSpan.Milliseconds * 1000 + _lostMicroseconds; }
            }

            public tzinfo tzinfo {
                get { return _tz; }
            }

            private UnifiedTime UtcTime {
                get {
                    if (_utcTime == null) {
                        _utcTime = new UnifiedTime();

                        _utcTime.TimeSpan = _timeSpan;
                        _utcTime.LostMicroseconds = _lostMicroseconds;

                        timedelta delta = this.utcoffset();
                        if (delta != null) {
                            time utced = Add(this, -delta);
                            _utcTime.TimeSpan = utced._timeSpan;
                            _utcTime.LostMicroseconds = utced._lostMicroseconds;
                        }
                    }
                    return _utcTime;
                }
            }

            // supported operations
            private static time Add(time date, timedelta delta) {
                return new time(date._timeSpan.Add(delta.TimeSpanWithDaysAndSeconds), delta._microseconds + date._lostMicroseconds, date._tz);
            }

            public PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(GetType()),
                    PythonTuple.MakeTuple(
                        this.hour,
                        this.minute,
                        this.second,
                        this.microsecond,
                        this.tzinfo
                    )
                );
            }

            public bool __nonzero__() {
                return this.UtcTime.TimeSpan.Ticks != 0 || this.UtcTime.LostMicroseconds != 0;
            }

            public static explicit operator bool(time time) {
                return time.__nonzero__();
            }

            // instance methods
            public object replace() {
                return this;
            }

            public object replace([ParamDictionary]IDictionary<object, object> dict) {
                int lhour = hour;
                int lminute = minute;
                int lsecond = second;
                int lmicrosecond = microsecond;
                tzinfo tz = tzinfo;

                foreach (KeyValuePair<object, object> kvp in (IDictionary<object, object>)dict) {
                    string key = kvp.Key as string;
                    if (key == null) continue;

                    switch (key) {
                        case "hour":
                            lhour = CastToInt(kvp.Value);
                            break;
                        case "minute":
                            lminute = CastToInt(kvp.Value);
                            break;
                        case "second":
                            lsecond = CastToInt(kvp.Value);
                            break;
                        case "microsecond":
                            lmicrosecond = CastToInt(kvp.Value);
                            break;
                        case "tzinfo":
                            tz = kvp.Value as tzinfo;
                            break;
                    }
                }
                return new time(lhour, lminute, lsecond, lmicrosecond, tz);
            }

            public object isoformat() {
                return ToString();
            }

            public override string ToString() {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0:d2}:{1:d2}:{2:d2}", hour, minute, second);

                if (microsecond != 0) sb.AppendFormat(".{0:d6}", microsecond);

                timedelta delta = utcoffset();
                if (delta != null) {
                    if (delta.TimeSpanWithDaysAndSeconds >= TimeSpan.Zero) {
                        sb.AppendFormat("+{0:d2}:{1:d2}", delta.TimeSpanWithDaysAndSeconds.Hours, delta.TimeSpanWithDaysAndSeconds.Minutes);
                    } else {
                        sb.AppendFormat("-{0:d2}:{1:d2}", -delta.TimeSpanWithDaysAndSeconds.Hours, -delta.TimeSpanWithDaysAndSeconds.Minutes);
                    }
                }

                return sb.ToString();
            }

            public object strftime(CodeContext/*!*/ context, string format) {
                return PythonTime.strftime(context,
                    format,
                    new DateTime(1900, 1, 1, _timeSpan.Hours, _timeSpan.Minutes, _timeSpan.Seconds, _timeSpan.Milliseconds),
                    _lostMicroseconds);
            }

            public timedelta utcoffset() {
                if (_tz == null) return null;
                timedelta delta = _tz.utcoffset(null);
                PythonDateTime.ThrowIfInvalid(delta, "utcoffset");
                return delta;
            }

            public object dst() {
                if (_tz == null) return null;
                timedelta delta = _tz.dst(null);
                PythonDateTime.ThrowIfInvalid(delta, "dst");
                return delta;
            }

            public object tzname() {
                if (_tz == null) return null;
                return _tz.tzname(null);
            }

            public override int GetHashCode() {
                return this.UtcTime.GetHashCode();
            }

            internal static bool CheckTzInfoBeforeCompare(time self, time other) {
                if (self._tz != other._tz) {
                    timedelta offset1 = self.utcoffset();
                    timedelta offset2 = other.utcoffset();

                    if ((offset1 == null && offset2 != null) || (offset1 != null && offset2 == null))
                        throw PythonOps.TypeError("can't compare offset-naive and offset-aware times");

                    return false;
                } else {
                    return true; // has the same TzInfo, Utcoffset will be skipped
                }
            }

            public override bool Equals(object obj) {
                time other = obj as time;
                if (other == null) return false;

                if (CheckTzInfoBeforeCompare(this, other)) {
                    return this._timeSpan == other._timeSpan && this._lostMicroseconds == other._lostMicroseconds;
                } else {
                    return this.UtcTime.Equals(other.UtcTime);
                }
            }

            #region Rich Comparison Members

            /// <summary>
            /// Helper function for doing the comparisons.  time has no __cmp__ method
            /// </summary>
            private int CompareTo(object other) {
                time other2 = other as time;
                if (other2 == null)
                    throw PythonOps.TypeError("can't compare datetime.time to {0}", PythonTypeOps.GetName(other));

                if (CheckTzInfoBeforeCompare(this, other2)) {
                    int res = this._timeSpan.CompareTo(other2._timeSpan);
                    if (res != 0) return res;
                    return this._lostMicroseconds - other2._lostMicroseconds;
                } else {
                    return this.UtcTime.CompareTo(other2.UtcTime);
                }
            }

            public static bool operator >(time self, object other) {
                return self.CompareTo(other) > 0;
            }

            public static bool operator <(time self, object other) {
                return self.CompareTo(other) < 0;
            }

            public static bool operator >=(time self, object other) {
                return self.CompareTo(other) >= 0;
            }

            public static bool operator <=(time self, object other) {
                return self.CompareTo(other) <= 0;
            }

            #endregion

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                StringBuilder sb = new StringBuilder();
                if (microsecond != 0)
                    sb.AppendFormat("datetime.time({0}, {1}, {2}, {3}", hour, minute, second, microsecond);
                else if (second != 0)
                    sb.AppendFormat("datetime.time({0}, {1}, {2}", hour, minute, second);
                else
                    sb.AppendFormat("datetime.time({0}, {1}", hour, minute);

                string ltzname = tzname() as string;
                if (ltzname != null) {
                    // TODO: calling __repr__?
                    sb.AppendFormat(", tzinfo={0}", ltzname.ToLower());
                }

                sb.AppendFormat(")");

                return sb.ToString();
            }

            #endregion

            class UnifiedTime {
                public TimeSpan TimeSpan;
                public int LostMicroseconds;

                public override bool Equals(object obj) {
                    UnifiedTime other = obj as UnifiedTime;
                    if (other == null) return false;
                    return this.TimeSpan == other.TimeSpan && this.LostMicroseconds == other.LostMicroseconds;
                }

                public override int GetHashCode() {
                    return TimeSpan.GetHashCode() ^ LostMicroseconds;
                }

                public int CompareTo(UnifiedTime other) {
                    int res = this.TimeSpan.CompareTo(other.TimeSpan);
                    if (res != 0) return res;
                    return this.LostMicroseconds - other.LostMicroseconds;
                }
            }
        }

        [PythonType]
        public class tzinfo {
            public tzinfo() {
            }

            public tzinfo(params object[] args) {
            }

            public tzinfo([ParamDictionary]PythonDictionary dict, params object[] args) {
            }

            public virtual object fromutc(datetime dt) {
                timedelta dtOffset = utcoffset(dt);
                if (dtOffset == null)
                    throw PythonOps.ValueError("fromutc: non-None utcoffset() result required");

                timedelta dtDst = dst(dt);
                if (dtDst == null)
                    throw PythonOps.ValueError("fromutc: non-None dst() result required");

                timedelta delta = dtOffset - dtDst;
                dt = dt + delta; // convert to standard LOCAL time
                dtDst = dt.dst();

                return dt + dtDst;
            }

            public virtual timedelta dst(object dt) {
                throw new NotImplementedException();
            }

            public virtual string tzname(object dt) {
                throw new NotImplementedException("a tzinfo subclass must implement tzname()");
            }

            public virtual timedelta utcoffset(object dt) {
                throw new NotImplementedException();
            }

            public PythonTuple __reduce__(CodeContext/*!*/ context) {
                object dict;
                if (GetType() == typeof(tzinfo) ||
                    !PythonOps.TryGetBoundAttr(context, this, "__dict__", out dict)) {
                    return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), PythonTuple.EMPTY);
                }

                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), PythonTuple.EMPTY, dict);
            }
        }
    }
}
