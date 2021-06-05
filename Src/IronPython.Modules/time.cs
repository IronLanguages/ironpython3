// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.Scripting.Runtime;

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

        private const int minYear = 1900;   // minimum year for python dates (CLS dates are bigger)
        private const double epochDifferenceDouble = 62135596800.0; // Difference between CLS epoch and UNIX epoch, == System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).Ticks / TimeSpan.TicksPerSecond
        private const long epochDifferenceLong = 62135596800;
        private const double ticksPerSecond = (double)TimeSpan.TicksPerSecond;

        public static readonly int altzone;
        public static readonly int daylight;
        public static readonly int timezone;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonTuple tzname;
        public const int _STRUCT_TM_ITEMS = 9;

        [MultiRuntimeAware]
        private static Stopwatch sw;

        public const string __doc__ = "This module provides various functions to manipulate time values.";

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            // we depend on locale, it needs to be initialized
            PythonLocale.EnsureLocaleInitialized(context);
        }

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

        public static string asctime(CodeContext/*!*/ context) {
            return asctime(context, null);
        }

        public static string asctime(CodeContext/*!*/ context, object time) {
            DateTime dt;
            if (time is PythonTuple) {
                // docs say locale information is not used by asctime, so ignore DST here
                dt = GetDateTimeFromTupleNoDst(context, (PythonTuple)time);
            } else if (time == null) {
                dt = DateTime.Now;
            } else {
                throw PythonOps.TypeError("expected struct_time or None");
            }

            return $"{dt.ToString("ddd MMM", CultureInfo.InvariantCulture)} {dt.Day,2} {dt.ToString("HH:mm:ss yyyy", CultureInfo.InvariantCulture)}";
        }

        public static double clock() {
            InitStopWatch();
            return ((double)sw.ElapsedTicks) / Stopwatch.Frequency;
        }

        public static double perf_counter() => clock();

        public static string ctime(CodeContext/*!*/ context) {
            return asctime(context, localtime());
        }

        public static string ctime(CodeContext/*!*/ context, object seconds) {
            if (seconds == null)
                return ctime(context);
            return asctime(context, localtime(seconds));
        }

        public static object get_clock_info(string name) {
            // TODO: Fill with correct values
            if (name == "monotonic")
                return new SimpleNamespace(new Dictionary<string, object> { { "adjustable", false }, { "implementation", "Stopwatch.GetTimestamp" }, { "monotonic", true }, { "resolution", 0.015625 } });

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

        public static double time() {
            return TicksToTimestamp(DateTime.Now.ToUniversalTime().Ticks);
        }

        public static PythonTuple localtime() {
            return GetDateTimeTuple(DateTime.Now, DateTime.Now.IsDaylightSavingTime());
        }

        public static PythonTuple localtime(object seconds) {
            if (seconds == null) return localtime();

            DateTime dt = TimestampToDateTime(GetTimestampFromObject(seconds));
            dt = dt.AddSeconds(-timezone);

            return GetDateTimeTuple(dt, dt.IsDaylightSavingTime());
        }

        public static PythonTuple gmtime() {
            return GetDateTimeTuple(DateTime.Now.ToUniversalTime(), false);
        }

        public static PythonTuple gmtime(object seconds) {
            if (seconds == null) return gmtime();

            DateTime dt = new DateTime(TimestampToTicks(GetTimestampFromObject(seconds)), DateTimeKind.Unspecified);
            
            return GetDateTimeTuple(dt, false);
        }

        public static double mktime(CodeContext/*!*/ context, PythonTuple localTime) {
            return TicksToTimestamp(GetDateTimeFromTuple(context, localTime).AddSeconds(timezone).Ticks);
        }

        public static string strftime(CodeContext/*!*/ context, string format) {
            return strftime(context, format, DateTime.Now, null);
        }

        public static string strftime(CodeContext/*!*/ context, string format, PythonTuple dateTime) {
            return strftime(context, format, GetDateTimeFromTupleNoDst(context, dateTime), null);
        }

        public static object strptime(CodeContext/*!*/ context, string @string) {
            return DateTime.Parse(@string, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat);
        }

        public static object strptime(CodeContext/*!*/ context, string @string, string format) {
            var packed = _strptime(context, @string, format);
            return GetDateTimeTuple(packed.Item1, packed.Item2);
        }
        
        // returns object array containing 2 elements: DateTime and DayOfWeek 
        internal static Tuple<DateTime, DayOfWeek?> _strptime(CodeContext/*!*/ context, string @string, string format) {
            bool postProc;
            FoundDateComponents foundDateComp;
            List<FormatInfo> formatInfo = PythonFormatToCLIFormat(format, true, out postProc, out foundDateComp);

            DateTime res;
            if (postProc) {
                int doyIndex = FindFormat(formatInfo, "\\%j");
                int dowMIndex = FindFormat(formatInfo, "\\%W");
                int dowSIndex = FindFormat(formatInfo, "\\%U");

                if (doyIndex != -1 && dowMIndex == -1 && dowSIndex == -1) {
                    res = new DateTime(1900, 1, 1);
                    res = res.AddDays(int.Parse(@string));
                } else if (dowMIndex != -1 && doyIndex == -1 && dowSIndex == -1) {
                    res = new DateTime(1900, 1, 1);
                    res = res.AddDays(int.Parse(@string) * 7);
                } else if (dowSIndex != -1 && doyIndex == -1 && dowMIndex == -1) {
                    res = new DateTime(1900, 1, 1);
                    res = res.AddDays(int.Parse(@string) * 7);
                } else {
                    throw PythonOps.ValueError("cannot parse %j, %W, or %U w/ other values");
                }
            } else {
                var fIdx = -1;
                string[] formatParts = new string[formatInfo.Count];
                for (int i = 0; i < formatInfo.Count; i++) {
                    switch (formatInfo[i].Type) {
                        case FormatInfoType.UserText: formatParts[i] = "'" + formatInfo[i].Text + "'"; break;
                        case FormatInfoType.SimpleFormat: formatParts[i] = formatInfo[i].Text; break;
                        case FormatInfoType.CustomFormat:
                            if (formatInfo[i].Text == "f") {
                                fIdx = i;
                            }
                            // include % if we only have one specifier to mark that it's a custom
                            // specifier
                            if (formatInfo.Count == 1 && formatInfo[i].Text.Length == 1) {
                                formatParts[i] = "%" + formatInfo[i].Text;
                            } else {
                                formatParts[i] = formatInfo[i].Text;
                            }
                            break;
                    }
                }
                var formats =
                    fIdx == -1 ? new [] { string.Join("", formatParts) } : ExpandMicrosecondFormat(fIdx, formatParts);
                try {
                    if (!DateTime.TryParseExact(@string,
                        formats,
                        PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                        out res)) {
                        throw PythonOps.ValueError("time data does not match format" + Environment.NewLine + 
                            "data=" + @string + ", fmt=" + format + ", to: " + formats[0]);
                    }
                } catch (FormatException e) {
                    throw PythonOps.ValueError(e.Message + Environment.NewLine + 
                        "data=" + @string + ", fmt=" + format + ", to: " + formats[0]);
                }
            }

            DayOfWeek? dayOfWeek = null;
            if ((foundDateComp & FoundDateComponents.DayOfWeek) != 0) {
                dayOfWeek = res.DayOfWeek;
            }

            if ((foundDateComp & FoundDateComponents.Year) == 0) {
                res = new DateTime(1900, res.Month, res.Day, res.Hour, res.Minute, res.Second, res.Millisecond, res.Kind);
            }

            return Tuple.Create(res, dayOfWeek);
        }

        private static string[] ExpandMicrosecondFormat(int fIdx, string [] formatParts) {
            // for %f number of digits can be anything between 1 and 6
            string[] formats = new string[6];
            formats[0] = string.Join("", formatParts);
            for (var i = 1; i < 6; i++) {
                formatParts[fIdx] = new string('f', i+1);
                formats[i] = string.Join("", formatParts);
            }
            return formats;
        }

        internal static string strftime(CodeContext/*!*/ context, string format, DateTime dt, int? microseconds) {
            bool postProc;
            List<FormatInfo> formatInfoList = PythonFormatToCLIFormat(format, false, out postProc, out _);
            StringBuilder res = new StringBuilder();

            foreach (FormatInfo formatInfo in formatInfoList) {
                switch (formatInfo.Type) {
                    case FormatInfoType.UserText: res.Append(formatInfo.Text); break;
                    case FormatInfoType.SimpleFormat: res.Append(dt.ToString(formatInfo.Text, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat)); break;
                    case FormatInfoType.CustomFormat:
                        // custom format strings need to be at least 2 characters long                        
                        res.Append(dt.ToString("%" + formatInfo.Text, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat));
                        break;
                }
            }

            if (postProc) {
                res = res.Replace("%f", microseconds != null ? string.Format("{0:D6}", microseconds) : "");

                res = res.Replace("%j", dt.DayOfYear.ToString("D03"));  // day of the year (001 - 366)

                // figure out first day of the year...
                DateTime first = new DateTime(dt.Year, 1, 1);
                int weekOneSunday = (7 - (int)first.DayOfWeek) % 7;
                int dayOffset = (8 - (int)first.DayOfWeek) % 7;

                // week of year  (sunday first day, 0-53), all days before Sunday are 0
                res = res.Replace("%U", (((dt.DayOfYear + 6 - weekOneSunday) / 7)).ToString());
                // week number of year (monday first day, 0-53), all days before Monday are 0
                res = res.Replace("%W", (((dt.DayOfYear + 6 - dayOffset) / 7)).ToString());
                res = res.Replace("%w", ((int)dt.DayOfWeek).ToString());
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
                dt = dt - (TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset);
            }

            return dt;
        }

        private static DateTime AddDst(DateTime dt) {
            if (TimeZoneInfo.Local.IsDaylightSavingTime(dt)) {
                dt = dt + (TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset);
            }

            return dt;
        }

        private static double GetTimestampFromObject(object seconds) {
            if (Converter.TryConvertToInt32(seconds, out int intSeconds)) {
                return intSeconds;
            }

            double dblVal;
            if (Converter.TryConvertToDouble(seconds, out dblVal)) {
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

        private static List<FormatInfo> PythonFormatToCLIFormat(string format, bool forParse, out bool postProcess, out FoundDateComponents found) {
            postProcess = false;
            found = FoundDateComponents.None;
            List<FormatInfo> newFormat = new List<FormatInfo>();

            for (int i = 0; i < format.Length; i++) {
                if (format[i] == '%') {
                    if (i + 1 == format.Length) throw PythonOps.ValueError("badly formatted string");

                    switch (format[++i]) {
                        case 'a':
                            found |= FoundDateComponents.DayOfWeek;
                            newFormat.Add(new FormatInfo("ddd")); break;
                        case 'A':
                            found |= FoundDateComponents.DayOfWeek;
                            newFormat.Add(new FormatInfo("dddd")); break;
                        case 'b': 
                            newFormat.Add(new FormatInfo("MMM")); 
                            break;
                        case 'B':
                            newFormat.Add(new FormatInfo("MMMM")); 
                            break;
                        case 'c':
                            found |= FoundDateComponents.Date;
                            AddDate(newFormat);
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, " "));
                            AddTime(newFormat);
                            break;
                        case 'd':
                            // if we're parsing we want to use the less-strict
                            // d format and which doesn't require both digits.
                            if (forParse) newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "d"));
                            else newFormat.Add(new FormatInfo("dd"));
                            break;
                        case 'H': newFormat.Add(new FormatInfo(forParse ? "H" : "HH")); break;
                        case 'I': newFormat.Add(new FormatInfo(forParse ? "h" : "hh")); break;
                        case 'm':
                            newFormat.Add(new FormatInfo(forParse ? "M" : "MM"));
                            break;
                        case 'M': newFormat.Add(new FormatInfo(forParse ? "m" : "mm")); break;
                        case 'p':
                            newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "t"));
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, "M"));
                            break;
                        case 'S': newFormat.Add(new FormatInfo("ss")); break;
                        case 'x':
                            found |= FoundDateComponents.Date;
                            AddDate(newFormat); break;
                        case 'X':
                            AddTime(newFormat);
                            break;
                        case 'y':
                            found |= FoundDateComponents.Year;
                            newFormat.Add(new FormatInfo("yy")); 
                            break;
                        case 'Y':
                            found |= FoundDateComponents.Year;
                            newFormat.Add(new FormatInfo("yyyy")); 
                            break;
                        case '%': newFormat.Add(new FormatInfo("\\%")); break;

                        // format conversions not defined by the CLR.  We leave
                        // them as \\% and then replace them by hand later
                        case 'j': // day of year
                            newFormat.Add(new FormatInfo("\\%j")); 
                            postProcess = true; 
                            break;
                        case 'f':
                            if (forParse) {
                                newFormat.Add(new FormatInfo(FormatInfoType.CustomFormat, "f")); 
                            } else {
                                postProcess = true;
                                newFormat.Add(new FormatInfo(FormatInfoType.UserText, "%f"));
                            }
                            break;
                        case 'W': newFormat.Add(new FormatInfo("\\%W")); postProcess = true; break;
                        case 'U': newFormat.Add(new FormatInfo("\\%U")); postProcess = true; break; // week number
                        case 'w': newFormat.Add(new FormatInfo("\\%w")); postProcess = true; break; // weekday number
                        case 'z':
                        case 'Z':
                            // !!!TODO: 
                            // 'z' for offset
                            // 'Z' for time zone name; could be from PythonTimeZoneInformation
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, ""));
                            break;
                        default:
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, "")); break;
                    }
                } else {
                    if (newFormat.Count == 0 || newFormat[newFormat.Count - 1].Type != FormatInfoType.UserText)
                        newFormat.Add(new FormatInfo(FormatInfoType.UserText, format[i].ToString()));
                    else
                        newFormat[newFormat.Count - 1].Text = newFormat[newFormat.Count - 1].Text + format[i];
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

        internal static PythonTuple GetDateTimeTuple(DateTime dt) {
            return GetDateTimeTuple(dt, null);
        }

        internal static PythonTuple GetDateTimeTuple(DateTime dt, DayOfWeek? dayOfWeek) {
            return GetDateTimeTuple(dt, dayOfWeek, null);
        }

        internal static PythonTuple GetDateTimeTuple(DateTime dt, DayOfWeek? dayOfWeek, PythonDateTime.tzinfo tz) {
            int last = -1;

            if (tz != null) {
                PythonDateTime.timedelta delta = tz.dst(dt);
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
            int[] ints;
            DateTime res = GetDateTimeFromTupleNoDst(context, t, out ints);

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

        private static DateTime GetDateTimeFromTupleNoDst(CodeContext context, PythonTuple t, out int[] ints) {
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
            if (year < DateTime.MinValue.Year || year <= minYear) throw PythonOps.ValueError("year is too low");
            if (year > DateTime.MaxValue.Year) throw PythonOps.ValueError("year is too high");
            if (ints[WeekdayIndex] < 0 || ints[WeekdayIndex] >= 7) throw PythonOps.ValueError("day of week is outside of 0-6 range");
            return ints;
        }

        private static int FindFormat(List<FormatInfo> formatInfo, string format) {
            for (int i = 0; i < formatInfo.Count; i++) {
                if (formatInfo[i].Text == format) return i;
            }
            return -1;
        }

        private static void InitStopWatch() {
            if (sw == null) {
                sw = new Stopwatch();
                sw.Start();
            }
        }

        [PythonType]
        public class struct_time : PythonTuple {
            private static readonly PythonType _StructTimeType = DynamicHelpers.GetPythonTypeFromType(typeof(struct_time));

            public object tm_year => _data[0];
            public object tm_mon => _data[1];
            public object tm_mday => _data[2];
            public object tm_hour => _data[3];
            public object tm_min => _data[4];
            public object tm_sec => _data[5];
            public object tm_wday => _data[6];
            public object tm_yday => _data[7];
            public object tm_isdst => _data[8];

            public int n_fields => _data.Length;
            public int n_sequence_fields => _data.Length;
            public int n_unnamed_fields => 0;

            internal struct_time(int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst)
                : base(new object[] { year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst }) {
            }

            internal struct_time(PythonTuple sequence)
                : base(sequence) {
            }

            public static struct_time __new__(CodeContext context, PythonType cls, int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst) {
                if (cls == _StructTimeType) {
                    return new struct_time(year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst);
                }
                if (cls.CreateInstance(context, year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst) is struct_time st) {
                    return st;
                }
                throw PythonOps.TypeError("{0} is not a subclass of time.struct_time", cls);
            }

            public static struct_time __new__(CodeContext context, PythonType cls, [NotNull]PythonTuple sequence) {
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

            public static struct_time __new__(CodeContext context, PythonType cls, [NotNull]IEnumerable sequence) {
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
