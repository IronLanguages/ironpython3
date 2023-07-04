// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;

[assembly: PythonModule("time", typeof(IronPython.Modules.PythonTime))]
namespace IronPython.Modules {
    public static class PythonTime {
        private const int YearIndex = 0;
        private const int MonthIndex = 1;
        private const int DayIndex = 2;
        private const int HourIndex = 3;
        private const int MinuteIndex = 4;
        private const int SecondIndex = 5;
        private const int WeekdayIndex = 6;
        private const int DayOfYearIndex = 7;
        private const int IsDaylightSavingsIndex = 8;
        private const int MaxIndex = 9;

        private const double epochDifferenceDouble = 62135596800.0; // Difference between CLS epoch and UNIX epoch, == System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).Ticks / TimeSpan.TicksPerSecond
        private const long epochDifferenceLong = 62135596800;
        private const double ticksPerSecond = (double)TimeSpan.TicksPerSecond;

        public static readonly int altzone;
        public static readonly int daylight;
        public static readonly int timezone;
        public static readonly PythonTuple tzname;
        public const int _STRUCT_TM_ITEMS = 9;

        [MultiRuntimeAware]
        private static Stopwatch? sw;

        public const string __doc__ = "This module provides various functions to manipulate time values.";

#pragma warning disable IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            // we depend on locale, it needs to be initialized
            PythonLocale.EnsureLocaleInitialized(context);
        }
#pragma warning restore IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute

        static PythonTime() {
            // altzone, timezone are offsets from UTC in seconds, so they always fit in the
            // -13*3600 to 13*3600 range and are safe to cast to ints
            daylight = TimeZoneInfo.Local.SupportsDaylightSavingTime ? 1 : 0;
            tzname = PythonTuple.MakeTuple(TimeZoneInfo.Local.StandardName, TimeZoneInfo.Local.DaylightName);
            timezone = (int)-TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds;
            altzone = timezone;
            if (TimeZoneInfo.Local.SupportsDaylightSavingTime) {
                var now = DateTime.Now;
                var rule = TimeZoneInfo.Local.GetAdjustmentRules().Where(x => x.DateStart <= now && x.DateEnd >= now).FirstOrDefault();
                if (rule != null) {
                    altzone = timezone + ((int)-rule.DaylightDelta.TotalSeconds);
                }
            }
        }

        internal static long TimestampToTicks(double seconds) {
            // If everything is converted in one shot, the rounding error surface at
            // microsecond level.
            // e.g: 5399410716.777882 --> 2141-02-06 05:18:36.777881
            //      1399410716.123    --> 2014-05-06 23:11:56.122995
            // To work around it, second and microseconds are converted
            // separately
            return (((long)seconds) + epochDifferenceLong) * TimeSpan.TicksPerSecond + // seconds
                   (long)(Math.Round(seconds % 1, 6) * TimeSpan.TicksPerSecond); // micro seconds
        }

        internal static double TicksToTimestamp(long ticks) {
            return (ticks / ticksPerSecond) - epochDifferenceDouble;
        }

        private static string AscTimeFormat(DateTime dt)
            => $"{dt.ToString("ddd MMM", CultureInfo.InvariantCulture)} {dt.Day,2} {dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {dt.Year}";

        public static string asctime(CodeContext/*!*/ context)
            => AscTimeFormat(DateTime.Now);

        public static string asctime(CodeContext/*!*/ context, [NotNone] PythonTuple time)
            => AscTimeFormat(GetDateTimeFromTupleNoDst(context, time));

        public static double clock() {
            InitStopWatch();
            return ((double)sw.ElapsedTicks) / Stopwatch.Frequency;
        }

        public static double perf_counter() => clock();

        public static string ctime(CodeContext/*!*/ context)
            => asctime(context, localtime());

        public static string ctime(CodeContext/*!*/ context, object? seconds)
            => asctime(context, localtime(seconds));

        public static object get_clock_info([NotNone] string name) {
            // TODO: Fill with correct values
            if (name == "monotonic")
                return new SimpleNamespace(new Dictionary<string, object?> { { "adjustable", false }, { "implementation", "Stopwatch.GetTimestamp" }, { "monotonic", true }, { "resolution", 0.015625 } });

            throw new NotImplementedException();
        }

        public static void sleep(double tm) {
            Thread.Sleep((int)(tm * 1000));
        }

        public static double monotonic()
            => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        // new in Python 3.7
        public static BigInteger monotonic_ns()
            => (BigInteger)Stopwatch.GetTimestamp() * 1000000000 / Stopwatch.Frequency;

        public static double time()
            => TicksToTimestamp(DateTime.Now.ToUniversalTime().Ticks);

        public static PythonTuple localtime()
            => localtime(null);

        public static PythonTuple localtime(object? seconds) {
            DateTime dt = seconds is null ? DateTime.Now : TimestampToDateTime(GetTimestampFromObject(seconds)).AddSeconds(-timezone);
            return GetDateTimeTuple(dt, dt.IsDaylightSavingTime());
        }

        public static PythonTuple gmtime()
            => gmtime(null);

        public static PythonTuple gmtime(object? seconds) {
            DateTime dt = seconds is null ? DateTime.Now.ToUniversalTime() : new DateTime(TimestampToTicks(GetTimestampFromObject(seconds)), DateTimeKind.Unspecified);
            return GetDateTimeTuple(dt, false);
        }

        public static double mktime(CodeContext/*!*/ context, [NotNone] PythonTuple localTime) {
            return TicksToTimestamp(GetDateTimeFromTuple(context, localTime).AddSeconds(timezone).Ticks);
        }

        public static string strftime(CodeContext/*!*/ context, [NotNone] string format) {
            return strftime(context, format, DateTime.Now, null, TimeZoneInfo.Local, errorOnF: true);
        }

        public static string strftime(CodeContext/*!*/ context, [NotNone] string format, [NotNone] PythonTuple dateTime) {
            return strftime(context, format, GetDateTimeFromTupleNoDst(context, dateTime), null, TimeZoneInfo.Local, errorOnF: true);
        }

        public static object? strptime(CodeContext/*!*/ context, [NotNone] string @string) {
            var module = context.LanguageContext.GetStrptimeModule();
            var _strptime_time = PythonOps.GetBoundAttr(context, module, "_strptime_time");
            return PythonOps.CallWithContext(context, _strptime_time, @string);
        }

        public static object? strptime(CodeContext/*!*/ context, [NotNone] string @string, [NotNone] string format) {
            var module = context.LanguageContext.GetStrptimeModule();
            var _strptime_time = PythonOps.GetBoundAttr(context, module, "_strptime_time");
            return PythonOps.CallWithContext(context, _strptime_time, @string, format);
        }

        internal static string strftime(CodeContext/*!*/ context, string format, DateTime dt, int? microseconds, TimeZoneInfo? tzinfo = null, bool errorOnF = false) {
            List<FormatInfo> formatInfoList = PythonFormatToCLIFormat(format);
            StringBuilder res = new StringBuilder();
            var lc_info = PythonLocale.GetLocaleInfo(context);

            // figure out first day of the year...
            DateTime first = new DateTime(dt.Year, 1, 1);
            int weekOneSunday = (7 - (int)first.DayOfWeek) % 7;
            int dayOffset = (8 - (int)first.DayOfWeek) % 7;
            var now = DateTime.Now;

            foreach (FormatInfo formatInfo in formatInfoList) {
                switch (formatInfo.Type) {
                    case FormatInfoType.UserText: res.Append(formatInfo.Text); break;
                    case FormatInfoType.SimpleFormat: res.Append(dt.ToString(formatInfo.Text, lc_info.Time.DateTimeFormat)); break;
                    case FormatInfoType.CustomFormat:
                        switch (formatInfo.Text) {
                            case "f":
                                if (errorOnF) throw PythonOps.ValueError("Invalid format string");
                                res.Append(microseconds != null ? string.Format("{0:D6}", microseconds) : "");
                                break;
                            case "j":
                                res.Append(dt.DayOfYear.ToString("D03"));
                                break;
                            case "U":
                                // week of year (sunday first day, 0-53), all days before Sunday are 0
                                res.Append(((dt.DayOfYear + 6 - weekOneSunday) / 7).ToString("D02"));
                                break;
                            case "W":
                                // week number of year (monday first day, 0-53), all days before Monday are 0
                                res.Append(((dt.DayOfYear + 6 - dayOffset) / 7).ToString("D02"));
                                break;
                            case "w":
                                res.Append(((int)dt.DayOfWeek).ToString());
                                break;
                            case "z":
                                if (tzinfo is null) break;
                                var offset = tzinfo.GetUtcOffset(now);
                                res.Append(((offset < TimeSpan.Zero) ? "-" : "+") + offset.ToString("hhmm"));
                                break;
                            case "Z":
                                if (tzinfo is null) break;
                                res.Append(tzinfo.IsDaylightSavingTime(now) ? tzinfo.DaylightName : tzinfo.StandardName);
                                break;
                            default: throw new InvalidOperationException();
                        }
                        break;
                }
            }
            return res.ToString();
        }

        internal static double DateTimeToTimestamp(DateTime dateTime) {
            return TicksToTimestamp(RemoveDst(dateTime).Ticks);
        }

        internal static DateTime TimestampToDateTime(double timeStamp) {
            return AddDst(new DateTime(TimestampToTicks(timeStamp)));
        }

        private static DateTime RemoveDst(DateTime dt) {
            return RemoveDst(dt, false);
        }

        private static DateTime RemoveDst(DateTime dt, bool always) {
            if (always || TimeZoneInfo.Local.IsDaylightSavingTime(dt)) {
                dt -= TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset;
            }

            return dt;
        }

        private static DateTime AddDst(DateTime dt) {
            if (TimeZoneInfo.Local.IsDaylightSavingTime(dt)) {
                dt += TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset;
            }

            return dt;
        }

        private static double GetTimestampFromObject(object seconds) {
            if (Converter.TryConvertToInt32(seconds, out int intSeconds)) {
                return intSeconds;
            }

            if (Converter.TryConvertToDouble(seconds, out double dblVal)) {
                if (dblVal > long.MaxValue || dblVal < long.MinValue) throw PythonOps.ValueError("unreasonable date/time");
                return dblVal;
            }

            throw PythonOps.TypeError("expected int, got {0}", DynamicHelpers.GetPythonType(seconds));
        }

        private enum FormatInfoType {
            UserText,
            SimpleFormat,
            CustomFormat,
        }

        private class FormatInfo {
            public FormatInfo(string text) {
                Type = FormatInfoType.SimpleFormat;
                Text = text;
            }

            public FormatInfo(FormatInfoType type, string text) {
                Type = type;
                Text = text;
            }

            public FormatInfoType Type;
            public string Text;

            public override string ToString() {
                return string.Format("{0}:{1}", Type, Text);
            }
        }

        // temporary solution
        private static void AddTime(List<FormatInfo> newFormat) {
            newFormat.Add(new FormatInfo("HH"));
            newFormat.Add(new FormatInfo(FormatInfoType.UserText, ":"));
            newFormat.Add(new FormatInfo("mm"));
            newFormat.Add(new FormatInfo(FormatInfoType.UserText, ":"));
            newFormat.Add(new FormatInfo("ss"));
        }

        private static void AddDate(List<FormatInfo> newFormat) {
            newFormat.Add(new FormatInfo("MM"));
            newFormat.Add(new FormatInfo(FormatInfoType.UserText, "/"));
            newFormat.Add(new FormatInfo("dd"));
            newFormat.Add(new FormatInfo(FormatInfoType.UserText, "/"));
            newFormat.Add(new FormatInfo("yy"));
        }

        /// <summary>
        /// Represents the date components that we found while parsing the date.  Used for zeroing out values
        /// which have different defaults from CPython.  Currently we only know that we need to do this for
        /// the year.
        /// </summary>
        [Flags]
        private enum FoundDateComponents {
            None,
            Year = 0x01,
            Date = (Year),
            DayOfWeek = 0x02,
        }

        private static List<FormatInfo> PythonFormatToCLIFormat(string format) {
            List<FormatInfo> newFormat = new List<FormatInfo>();

            for (int i = 0; i < format.Length; i++) {
                var ch = format[i];
                if (ch == '%') {
                    if (i + 1 == format.Length) throw PythonOps.ValueError("Invalid format string");

                    switch (format[++i]) {
                        case '\0': throw PythonOps.ValueError("embedded null character");
                        case 'a': newFormat.Add(new FormatInfo("ddd")); break;
                        case 'A': newFormat.Add(new FormatInfo("dddd")); break;
                        case 'b': newFormat.Add(new FormatInfo("MMM")); break;
                        case 'B': newFormat.Add(new FormatInfo("MMMM")); break;
                        case 'c':
                            AddDate(newFormat);
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, " "));
                            AddTime(newFormat);
                            break;
                        case 'd': newFormat.Add(new FormatInfo("dd")); break;
                        case 'H': newFormat.Add(new FormatInfo("HH")); break;
                        case 'I': newFormat.Add(new FormatInfo("hh")); break;
                        case 'm': newFormat.Add(new FormatInfo("MM")); break;
                        case 'M': newFormat.Add(new FormatInfo("mm")); break;
                        case 'p': newFormat.Add(new FormatInfo("tt")); break;
                        case 'S': newFormat.Add(new FormatInfo("ss")); break;
                        case 'x': AddDate(newFormat); break;
                        case 'X': AddTime(newFormat); break;
                        case 'y': newFormat.Add(new FormatInfo("yy")); break;
                        case 'Y': newFormat.Add(new FormatInfo("yyyy")); break;
                        case '%': newFormat.Add(new FormatInfo("\\%")); break;

                        // format conversions not defined by the CLR, we will need to do special handling
                        case 'j': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "j")); break; // day of year
                        case 'f': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "f")); break; // microseconds
                        case 'W': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "W")); break; // week number (Monday-based)
                        case 'U': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "U")); break; // week number (Sunday-based)
                        case 'w': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "w")); break; // weekday number
                        case 'z': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "z")); break; // UTC offset
                        case 'Z': newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "Z")); break; // time zone name

                        default:
                            throw PythonOps.ValueError("Invalid format string");
                    }
                } else {
                    if (ch == '\0') throw PythonOps.ValueError("embedded null character");
                    if (newFormat.Count == 0 || newFormat[newFormat.Count - 1].Type != FormatInfoType.UserText)
                        newFormat.Add(new FormatInfo(FormatInfoType.UserText, ch.ToString()));
                    else
                        newFormat[newFormat.Count - 1].Text += ch;
                }
            }

            return newFormat;
        }

        // weekday: Monday is 0, Sunday is 6
        internal static int Weekday(DateTime dt) {
            return Weekday(dt.DayOfWeek);
        }

        internal static int Weekday(DayOfWeek dayOfWeek) {
            if (dayOfWeek == DayOfWeek.Sunday) return 6;
            else return (int)dayOfWeek - 1;
        }

        // isoweekday: Monday is 1, Sunday is 7
        internal static int IsoWeekday(DateTime dt) {
            if (dt.DayOfWeek == DayOfWeek.Sunday) return 7;
            else return (int)dt.DayOfWeek;
        }

        internal static PythonTuple GetDateTimeTuple(DateTime dt, DayOfWeek? dayOfWeek = null, PythonDateTime.tzinfo? tz = null) {
            int last = -1;

            if (tz != null) {
                PythonDateTime.timedelta? delta = tz.dst(new PythonDateTime.datetime(dt));
                PythonDateTime.ThrowIfInvalid(delta, "dst");
                if (delta == null) {
                    last = -1;
                } else {
                    last = delta.__bool__() ? 1 : 0;
                }
            }
            return new struct_time(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, Weekday(dayOfWeek ?? dt.DayOfWeek), dt.DayOfYear, last);
        }

        internal static struct_time GetDateTimeTuple(DateTime dt, bool dstMode) {
            return new struct_time(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, Weekday(dt), dt.DayOfYear, dstMode ? 1 : 0);
        }

        private static DateTime GetDateTimeFromTuple(CodeContext/*!*/ context, PythonTuple t) {
            DateTime res = GetDateTimeFromTupleNoDst(context, t, out int[]? ints);

            if (ints != null) {
                switch (ints[IsDaylightSavingsIndex]) {
                    // automatic detection
                    case -1: res = RemoveDst(res); break;
                    // is daylight savings time, force adjustment
                    case 1: res = RemoveDst(res, true); break;
                }
            }
            return res;
        }

        private static DateTime GetDateTimeFromTupleNoDst(CodeContext context, PythonTuple t) {
            return GetDateTimeFromTupleNoDst(context, t, out _);
        }

        private static DateTime GetDateTimeFromTupleNoDst(CodeContext context, PythonTuple t, out int[]? ints) {
            if (t == null) {
                ints = null;
                return DateTime.Now;
            }

            ints = ValidateDateTimeTuple(context, t);

            var month = ints[MonthIndex];
            if (month == 0) month = 1;
            var day = ints[DayIndex];
            if (day == 0) day = 1;

            return new DateTime(ints[YearIndex], month, day, ints[HourIndex], ints[MinuteIndex], ints[SecondIndex]);
        }

        private static int[] ValidateDateTimeTuple(CodeContext/*!*/ context, PythonTuple t) {
            if (t.__len__() != MaxIndex) throw PythonOps.TypeError("expected tuple of length {0}", MaxIndex);

            int[] ints = new int[MaxIndex];
            for (int i = 0; i < MaxIndex; i++) {
                ints[i] = context.LanguageContext.ConvertToInt32(t[i]);
            }

            int year = ints[YearIndex];
            if (year >= 0 && year <= 99) {
                if (year > 68) {
                    year += 1900;
                } else {
                    year += 2000;
                }
            }
            if (year < DateTime.MinValue.Year) throw PythonOps.ValueError("year is too low");
            if (year > DateTime.MaxValue.Year) throw PythonOps.ValueError("year is too high");
            if (ints[WeekdayIndex] < 0 || ints[WeekdayIndex] >= 7) throw PythonOps.ValueError("day of week is outside of 0-6 range");
            return ints;
        }

        [MemberNotNull(nameof(sw))]
        private static void InitStopWatch() {
            if (sw == null) {
                sw = new Stopwatch();
                sw.Start();
            }
        }

        [PythonType]
        public class struct_time : PythonTuple {
            private static readonly PythonType _StructTimeType = DynamicHelpers.GetPythonTypeFromType(typeof(struct_time));

            public object? tm_year => _data[0];
            public object? tm_mon => _data[1];
            public object? tm_mday => _data[2];
            public object? tm_hour => _data[3];
            public object? tm_min => _data[4];
            public object? tm_sec => _data[5];
            public object? tm_wday => _data[6];
            public object? tm_yday => _data[7];
            public object? tm_isdst => _data[8];

            public int n_fields => _data.Length;
            public int n_sequence_fields => _data.Length;
            public int n_unnamed_fields => 0;

            internal struct_time(int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst)
                : base(new object[] { year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst }) {
            }

            internal struct_time(PythonTuple sequence)
                : base(sequence) {
            }

            public static struct_time __new__(CodeContext context, [NotNone] PythonType cls, int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst) {
                if (cls == _StructTimeType) {
                    return new struct_time(year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst);
                }
                if (cls.CreateInstance(context, year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst) is struct_time st) {
                    return st;
                }
                throw PythonOps.TypeError("{0} is not a subclass of time.struct_time", cls);
            }

            public static struct_time __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] PythonTuple sequence) {
                if (sequence.__len__() != 9) {
                    throw PythonOps.TypeError("time.struct_time() takes a 9-sequence ({0}-sequence given)", sequence.__len__());
                }
                if (cls == _StructTimeType) {
                    return new struct_time(sequence);
                }
                if (cls.CreateInstance(context, sequence) is struct_time st) {
                    return st;
                }
                throw PythonOps.TypeError("{0} is not a subclass of time.struct_time", cls);
            }

            public static struct_time __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] IEnumerable sequence) {
                return __new__(context, cls, PythonTuple.Make(sequence));
            }

            public PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(_StructTimeType, PythonTuple.MakeTuple(tm_year, tm_mon, tm_mday, tm_hour, tm_min, tm_sec, tm_wday, tm_yday, tm_isdst));
            }

            public static object __getnewargs__(CodeContext context, int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst) {
                return PythonTuple.MakeTuple(struct_time.__new__(context, _StructTimeType, year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst));
            }

            public override string ToString() {
                return string.Format(
                    "time.struct_time(tm_year={0}, tm_mon={1}, tm_mday={2}, tm_hour={3}, tm_min={4}, tm_sec={5}, tm_wday={6}, tm_yday={7}, tm_isdst={8})",
                    //this.tm_year, this.tm_mon, this.tm_mday, this.tm_hour, this.tm_min, this.tm_sec, this.tm_wday, this.tm_yday, this.tm_isdst
                    _data);
            }

            public override string __repr__(CodeContext context) {
                return this.ToString();
            }
        }
    }
}
