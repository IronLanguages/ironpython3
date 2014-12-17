/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

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
        private const double epochDifference = 62135596800.0; // Difference between CLS epoch and UNIX epoch, == System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).Ticks / TimeSpan.TicksPerSecond
        private const double ticksPerSecond = (double)TimeSpan.TicksPerSecond;

        public static readonly int altzone;
        public static readonly int daylight;
        public static readonly int timezone;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonTuple tzname;
        public const bool accept2dyear = true;

#if FEATURE_STOPWATCH    // System.Diagnostics.Stopwatch
        [MultiRuntimeAware]
        private static Stopwatch sw;
#endif

        public const string __doc__ = "This module provides various functions to manipulate time values.";

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            // we depend on locale, it needs to be initialized
            PythonLocale.EnsureLocaleInitialized(context);
        }

        static PythonTime() {

            // altzone, timezone are offsets from UTC in seconds, so they always fit in the
            // -13*3600 to 13*3600 range and are safe to cast to ints
#if FEATURE_TIMEZONE
            DaylightTime dayTime = TimeZone.CurrentTimeZone.GetDaylightChanges(DateTime.Now.Year);
            daylight = (dayTime.Start == dayTime.End && dayTime.Start == DateTime.MinValue && dayTime.Delta.Ticks == 0) ? 0 : 1;

            tzname = PythonTuple.MakeTuple(TimeZone.CurrentTimeZone.StandardName, TimeZone.CurrentTimeZone.DaylightName);
            altzone = (int)-TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalSeconds;
            timezone = altzone;
            if (daylight != 0) {
                // GetUtcOffset includes adjustments for timezones.  If we're in daylight saving time then
                // we need to adjust timezone so it is the correct time.  Otherwise we need to adjust altzone
                if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now)) {
                    timezone += (int)dayTime.Delta.TotalSeconds;
                } else {
                    altzone -= (int)dayTime.Delta.TotalSeconds;
                }
            }
#else
            daylight = TimeZoneInfo.Local.SupportsDaylightSavingTime ? 1 : 0;
            tzname = PythonTuple.MakeTuple(TimeZoneInfo.Local.StandardName, TimeZoneInfo.Local.DaylightName);
            timezone = (int)-TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds;
            altzone = (int)-TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalSeconds;
#endif
        }

        internal static long TimestampToTicks(double seconds) {
            return (long)((seconds + epochDifference) * ticksPerSecond);
        }

        internal static double TicksToTimestamp(long ticks) {
            return (ticks / ticksPerSecond) - epochDifference;
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

            return dt.ToString("ddd MMM dd HH:mm:ss yyyy", null);
        }

        public static double clock() {
#if FEATURE_STOPWATCH    // System.Diagnostics.Stopwatch
            InitStopWatch();
            return ((double)sw.ElapsedTicks) / Stopwatch.Frequency;
#else
            return (double)DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
#endif
        }

        public static string ctime(CodeContext/*!*/ context) {
            return asctime(context, localtime());
        }

        public static string ctime(CodeContext/*!*/ context, object seconds) {
            if (seconds == null)
                return ctime(context);
            return asctime(context, localtime(seconds));
        }

        public static void sleep(double tm) {
            Thread.Sleep((int)(tm * 1000));
        }

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
                    res = res.AddDays(Int32.Parse(@string));
                } else if (dowMIndex != -1 && doyIndex == -1 && dowSIndex == -1) {
                    res = new DateTime(1900, 1, 1);
                    res = res.AddDays(Int32.Parse(@string) * 7);
                } else if (dowSIndex != -1 && doyIndex == -1 && dowMIndex == -1) {
                    res = new DateTime(1900, 1, 1);
                    res = res.AddDays(Int32.Parse(@string) * 7);
                } else {
                    throw PythonOps.ValueError("cannot parse %j, %W, or %U w/ other values");
                }
            } else {
                string[] formats = new string[formatInfo.Count];
                for (int i = 0; i < formatInfo.Count; i++) {
                    switch (formatInfo[i].Type) {
                        case FormatInfoType.UserText: formats[i] = "'" + formatInfo[i].Text + "'"; break;
                        case FormatInfoType.SimpleFormat: formats[i] = formatInfo[i].Text; break;
                        case FormatInfoType.CustomFormat:
                            // include % if we only have one specifier to mark that it's a custom
                            // specifier
                            if (formatInfo.Count == 1 && formatInfo[i].Text.Length == 1) {
                                formats[i] = "%" + formatInfo[i].Text;
                            } else {
                                formats[i] = formatInfo[i].Text;
                            }
                            break;
                    }
                }

                try {
                    if (!StringUtils.TryParseDateTimeExact(@string,
                        String.Join("", formats),
                        PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                        out res)) {
                        // If TryParseExact fails, fall back to DateTime.Parse which does a better job in some cases...
                        res = DateTime.Parse(@string, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat);
                    }
                } catch (FormatException e) {
                    throw PythonOps.ValueError(e.Message + Environment.NewLine + "data=" + @string + ", fmt=" + format + ", to: " + String.Join("", formats));
                }
            }

            DayOfWeek? dayOfWeek = null;
            if ((foundDateComp & FoundDateComponents.DayOfWeek) != 0) {
                dayOfWeek = res.DayOfWeek;
            }

            if ((foundDateComp & FoundDateComponents.Year) == 0) {
                res = new DateTime(1900, res.Month, res.Day, res.Hour, res.Minute, res.Second, res.Millisecond, res.Kind);
            }

            return GetDateTimeTuple(res, dayOfWeek);
        }

        internal static string strftime(CodeContext/*!*/ context, string format, DateTime dt, int? microseconds) {
            bool postProc;
            FoundDateComponents found;
            List<FormatInfo> formatInfo = PythonFormatToCLIFormat(format, false, out postProc, out found);
            StringBuilder res = new StringBuilder();

            for (int i = 0; i < formatInfo.Count; i++) {
                switch (formatInfo[i].Type) {
                    case FormatInfoType.UserText: res.Append(formatInfo[i].Text); break;
                    case FormatInfoType.SimpleFormat: res.Append(dt.ToString(formatInfo[i].Text, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat)); break;
                    case FormatInfoType.CustomFormat:
                        // custom format strings need to be at least 2 characters long                        
                        res.Append(dt.ToString("%" + formatInfo[i].Text, PythonLocale.GetLocaleInfo(context).Time.DateTimeFormat));
                        break;
                }
            }

            if (postProc) {
                res = res.Replace("%f", microseconds != null ? System.String.Format("{0:D6}", microseconds) : "");

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
#if FEATURE_TIMEZONE
            DaylightTime dayTime = TimeZone.CurrentTimeZone.GetDaylightChanges(dt.Year);
            if (always || (dt > dayTime.Start && dt < dayTime.End)) {
                dt -= dayTime.Delta;
            }
#else
            if (always || TimeZoneInfo.Local.IsDaylightSavingTime(dt)) {
                dt = dt - (TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset);
            }
#endif
            return dt;
        }

        private static DateTime AddDst(DateTime dt) {
#if FEATURE_TIMEZONE
            DaylightTime dayTime = TimeZone.CurrentTimeZone.GetDaylightChanges(dt.Year);
            if (dt > dayTime.Start && dt < dayTime.End) {
                dt += dayTime.Delta;
            }
#else
            if (TimeZoneInfo.Local.IsDaylightSavingTime(dt)) {
                dt = dt + (TimeZoneInfo.Local.GetUtcOffset(dt) - TimeZoneInfo.Local.BaseUtcOffset);
            }
#endif
            return dt;
        }

        private static double GetTimestampFromObject(object seconds) {
            int intSeconds;
            if (Converter.TryConvertToInt32(seconds, out intSeconds)) {
                return intSeconds;
            }

            double dblVal;
            if (Converter.TryConvertToDouble(seconds, out dblVal)) {
                if (dblVal > Int64.MaxValue || dblVal < Int64.MinValue) throw PythonOps.ValueError("unreasonable date/time");
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
        enum FoundDateComponents {
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
                            postProcess = true;
                            newFormat.Add(new FormatInfo(FormatInfoType.UserText, "%f")); 
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

            if (tz == null) {
                last = -1;
            } else {
                PythonDateTime.timedelta delta = tz.dst(dt);
                PythonDateTime.ThrowIfInvalid(delta, "dst");
                if (delta == null) {
                    last = -1;
                } else {
                    last = delta.__nonzero__() ? 1 : 0;
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
            int[] dummy;
            return GetDateTimeFromTupleNoDst(context, t, out dummy);
        }

        private static DateTime GetDateTimeFromTupleNoDst(CodeContext context, PythonTuple t, out int[] ints) {
            if (t == null) {
                ints = null;
                return DateTime.Now;
            }

            ints = ValidateDateTimeTuple(context, t);
            return new DateTime(ints[YearIndex], ints[MonthIndex], ints[DayIndex], ints[HourIndex], ints[MinuteIndex], ints[SecondIndex]);
        }

        private static int[] ValidateDateTimeTuple(CodeContext/*!*/ context, PythonTuple t) {
            if (t.__len__() != MaxIndex) throw PythonOps.TypeError("expected tuple of length {0}", MaxIndex);

            int[] ints = new int[MaxIndex];
            for (int i = 0; i < MaxIndex; i++) {
                ints[i] = PythonContext.GetContext(context).ConvertToInt32(t[i]);
            }

            int year = ints[YearIndex];
            if (accept2dyear && (year >= 0 && year <= 99)) {
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

#if FEATURE_STOPWATCH    // Stopwatch
        private static void InitStopWatch() {
            if (sw == null) {
                sw = new Stopwatch();
                sw.Start();
            }
        }
#endif

        [PythonType]
        public class struct_time : PythonTuple {
            private static PythonType _StructTimeType = DynamicHelpers.GetPythonTypeFromType(typeof(struct_time));

            public object tm_year {
                get { return _data[0]; }
            }
            public object tm_mon {
                get { return _data[1]; }
            }
            public object tm_mday {
                get { return _data[2]; }
            }
            public object tm_hour {
                get { return _data[3]; }
            }
            public object tm_min {
                get { return _data[4]; }
            }
            public object tm_sec {
                get { return _data[5]; }
            }
            public object tm_wday {
                get { return _data[6]; }
            }
            public object tm_yday {
                get { return _data[7]; }
            }
            public object tm_isdst {
                get { return _data[8]; }
            }
            public int n_fields {
                get { return _data.Length; }
            }

            public int n_sequence_fields {
                get { return _data.Length; }
            }

            public int n_unnamed_fields {
                get { return 0; }
            }

            internal struct_time(int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst)
                : base(new object[] { year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst }) {
            }

            internal struct_time(PythonTuple sequence)
                : base(sequence) {
            }

            public static struct_time __new__(CodeContext context, PythonType cls, int year, int month, int day, int hour, int minute, int second, int dayOfWeek, int dayOfYear, int isDst) {
                if (cls == _StructTimeType) {
                    return new struct_time(year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst);
                } else {
                    struct_time st = cls.CreateInstance(context, year, month, day, hour, minute, second, dayOfWeek, dayOfYear, isDst) as struct_time;
                    if (st == null)
                        throw PythonOps.TypeError("{0} is not a subclass of time.struct_time", cls);
                    return st;
                }
            }

            public static struct_time __new__(CodeContext context, PythonType cls, [NotNull]PythonTuple sequence) {
                if (sequence.__len__() != 9) {
                    throw PythonOps.TypeError("time.struct_time() takes a 9-sequence ({0}-sequence given)", sequence.__len__());
                }
                if (cls == _StructTimeType) {
                    return new struct_time(sequence);
                } else {
                    struct_time st = cls.CreateInstance(context, sequence) as struct_time;
                    if (st == null) {
                        throw PythonOps.TypeError("{0} is not a subclass of time.struct_time", cls);
                    }
                    return st;
                }
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
