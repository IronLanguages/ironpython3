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
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_locale", typeof(IronPython.Modules.PythonLocale))]
namespace IronPython.Modules {
    public static class PythonLocale {
        public const string __doc__ = "Provides access for querying and manipulating the current locale settings";

        private static readonly object _localeKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            EnsureLocaleInitialized(context);
            context.EnsureModuleException("_localeerror", dict, "Error", "_locale");
        }

        internal static void EnsureLocaleInitialized(PythonContext context) {
            if (!context.HasModuleState(_localeKey)) {
                context.SetModuleState(_localeKey, new LocaleInfo(context));
            }
        }

        public const int CHAR_MAX = 127;
        public const int LC_ALL = (int)LocaleCategories.All;
        public const int LC_COLLATE = (int)LocaleCategories.Collate;
        public const int LC_CTYPE = (int)LocaleCategories.CType;
        public const int LC_MONETARY = (int)LocaleCategories.Monetary;
        public const int LC_NUMERIC = (int)LocaleCategories.Numeric;
        public const int LC_TIME = (int)LocaleCategories.Time;

        internal static string PreferredEncoding {
            get {
#if FEATURE_ANSICP    // No ANSICodePage in Silverlight
                return "cp" + CultureInfo.CurrentCulture.TextInfo.ANSICodePage.ToString();
#elif NETSTANDARD
                return "utf_8";
#else
                return "";
#endif
            }
        }

        [Documentation("gets the default locale tuple")]
        public static object _getdefaultlocale() {            
            return PythonTuple.MakeTuple(
                GetDefaultLocale(), 
                PreferredEncoding
            );
        }

        [Documentation(@"gets the locale's conventions table.  

The conventions table is a dictionary that contains information on how to use 
the locale for numeric and monetary formatting")]
        public static object localeconv(CodeContext/*!*/ context) {
            return GetLocaleInfo(context).GetConventionsTable();
        }

        [Documentation(@"Sets the current locale for the given category.

LC_ALL:       sets locale for all options below
LC_COLLATE:   sets locale for collation (strcoll and strxfrm) only
LC_CTYPE:     sets locale for CType [unused]
LC_MONETARY:  sets locale for the monetary functions (localeconv())
LC_NUMERIC:   sets the locale for numeric functions (slocaleconv())
LC_TIME:      sets the locale for time functions [unused]

If locale is None then the current setting is returned.
")]
        public static object setlocale(CodeContext/*!*/ context, int category, [DefaultParameterValue(null)]string locale) {
            LocaleInfo li = GetLocaleInfo(context);
            if (locale == null) {
                return li.GetLocale(context, category);
            }
            //  An empty string specifies the user’s default settings.
            if (locale == "") {
                locale = GetDefaultLocale();
            }

            return li.SetLocale(context, category, locale);
        }

        [Documentation("compares two strings using the current locale")]
        public static int strcoll(CodeContext/*!*/ context, string string1, string string2) {
            return GetLocaleInfo(context).Collate.CompareInfo.Compare(string1, string2, CompareOptions.None);
        }

        [Documentation(@"returns a System.Globalization.SortKey that can be compared using the built-in cmp.

Note: Return value differs from CPython - it is not a string.")]
        public static object strxfrm(CodeContext/*!*/ context, string @string) {
#if FEATURE_SORTKEY
            return GetLocaleInfo(context).Collate.CompareInfo.GetSortKey(@string);
#else
            return @string;
#endif
        }

        private enum LocaleCategories {
            All = 0,
            Collate = 1,
            CType = 2,
            Monetary = 3,
            Numeric = 4,
            Time = 5,
        }

        private static string GetDefaultLocale() {
            return CultureInfo.CurrentCulture.Name.Replace('-', '_').Replace(' ', '_');
        }

        internal class LocaleInfo {
            private readonly PythonContext _context;
            private PythonDictionary conv;

            public LocaleInfo(PythonContext context) {
                _context = context;
            }

            public CultureInfo Collate {
                get { return _context.CollateCulture; }
                set { _context.CollateCulture = value; }
            }

            public CultureInfo CType {
                get { return _context.CTypeCulture; }
                set { _context.CTypeCulture= value; }
            }
            
            public CultureInfo Time {
                get { return _context.TimeCulture; }
                set { _context.TimeCulture = value; }
            }

            public CultureInfo Monetary {
                get { return _context.MonetaryCulture; }
                set { _context.MonetaryCulture = value; }
            }
            
            public CultureInfo Numeric {
                get { return _context.NumericCulture; }
                set { _context.NumericCulture = value; }
            }

            public override string ToString() {
                return base.ToString();
            }

            public PythonDictionary GetConventionsTable() {
                CreateConventionsDict();

                return conv;
            }

            public string SetLocale(CodeContext/*!*/ context, int category, string locale) {
                switch ((LocaleCategories)category) {
                    case LocaleCategories.All:
                        SetLocale(context, LC_COLLATE, locale);
                        SetLocale(context, LC_CTYPE, locale);
                        SetLocale(context, LC_MONETARY, locale);
                        SetLocale(context, LC_NUMERIC, locale);
                        return SetLocale(context, LC_TIME, locale);
                    case LocaleCategories.Collate:
                        return CultureToName(Collate = LocaleToCulture(context, locale));
                    case LocaleCategories.CType:
                        return CultureToName(CType = LocaleToCulture(context, locale));                        
                    case LocaleCategories.Time:
                        return CultureToName(Time = LocaleToCulture(context, locale));                        
                    case LocaleCategories.Monetary:
                        Monetary = LocaleToCulture(context, locale);
                        conv = null;
                        return CultureToName(Monetary);
                    case LocaleCategories.Numeric:
                        Numeric = LocaleToCulture(context, locale);
                        conv = null;
                        return CultureToName(Numeric);
                    default:
                        throw PythonExceptions.CreateThrowable(_localeerror(context), "unknown locale category");
                }

            }

            public string GetLocale(CodeContext/*!*/ context, int category) {
                switch ((LocaleCategories)category) {
                    case LocaleCategories.All:
                        if (Collate == CType &&
                            Collate == Time &&
                            Collate == Monetary &&
                            Collate == Numeric) {
                            // they're all the same, return only 1 name
                            goto case LocaleCategories.Collate;
                        }

                        // return them all...
                        return String.Format("LC_COLLATE={0};LC_CTYPE={1};LC_MONETARY={2};LC_NUMERIC={3};LC_TIME={4}",
                            GetLocale(context, LC_COLLATE),
                            GetLocale(context, LC_CTYPE),
                            GetLocale(context, LC_MONETARY),
                            GetLocale(context, LC_NUMERIC),
                            GetLocale(context, LC_TIME));
                    case LocaleCategories.Collate: return CultureToName(Collate);
                    case LocaleCategories.CType: return CultureToName(CType);
                    case LocaleCategories.Time: return CultureToName(Time);
                    case LocaleCategories.Monetary: return CultureToName(Monetary);
                    case LocaleCategories.Numeric: return CultureToName(Numeric);
                    default:
                        throw PythonExceptions.CreateThrowable(_localeerror(context), "unknown locale category");
                }
            }

            public string CultureToName(CultureInfo culture) {
                if (culture == PythonContext.CCulture) {
                    return "C";
                }
                
                return culture.Name.Replace('-', '_');
            }

            private CultureInfo LocaleToCulture(CodeContext/*!*/ context, string locale) {
                if (locale == "C") {
                    return PythonContext.CCulture;
                }

                locale = locale.Replace('_', '-');

                try {
                    return StringUtils.GetCultureInfo(locale);
                } catch (ArgumentException) {
                    throw PythonExceptions.CreateThrowable(_localeerror(context), String.Format("unknown locale: {0}", locale));
                }
            }

            /// <summary>
            /// Populates the given directory w/ the locale information from the given
            /// CultureInfo.
            /// </summary>
            private void CreateConventionsDict() {
                conv = new PythonDictionary();

                conv["decimal_point"] = Numeric.NumberFormat.NumberDecimalSeparator;
                conv["grouping"] = GroupsToList(Numeric.NumberFormat.NumberGroupSizes);
                conv["thousands_sep"] = Numeric.NumberFormat.NumberGroupSeparator;

                conv["mon_decimal_point"] = Monetary.NumberFormat.CurrencyDecimalSeparator;
                conv["mon_thousands_sep"] = Monetary.NumberFormat.CurrencyGroupSeparator;
                conv["mon_grouping"] = GroupsToList(Monetary.NumberFormat.CurrencyGroupSizes);
                conv["int_curr_symbol"] = Monetary.NumberFormat.CurrencySymbol;
                conv["currency_symbol"] = Monetary.NumberFormat.CurrencySymbol;
                conv["frac_digits"] = Monetary.NumberFormat.CurrencyDecimalDigits;
                conv["int_frac_digits"] = Monetary.NumberFormat.CurrencyDecimalDigits;
                conv["positive_sign"] = Monetary.NumberFormat.PositiveSign;
                conv["negative_sign"] = Monetary.NumberFormat.NegativeSign;

                conv["p_sign_posn"] = Monetary.NumberFormat.CurrencyPositivePattern;
                conv["n_sign_posn"] = Monetary.NumberFormat.CurrencyNegativePattern;
            }

            private static List GroupsToList(int[] groups) {
                // .NET: values from 0-9, if the last digit is zero, remaining digits
                // go ungrouped, otherwise they're grouped based upon the last value.

                // locale: ends in CHAR_MAX if no further grouping is performed, ends in
                // zero if the last group size is repeatedly used.
                List res = new List(groups);
                if (groups.Length > 0 && groups[groups.Length - 1] == 0) {
                    // replace zero w/ CHAR_MAX, no further grouping is performed
                    res[res.__len__() - 1] = CHAR_MAX;
                } else {
                    // append 0 to indicate we should repeatedly use the last one
                    res.AddNoLock(0);
                }

                return res;
            }
        }

        internal static LocaleInfo/*!*/ GetLocaleInfo(CodeContext/*!*/ context) {
            EnsureLocaleInitialized(PythonContext.GetContext(context));

            return (LocaleInfo)PythonContext.GetContext(context).GetModuleState(_localeKey);
        }        

        private static PythonType _localeerror(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("_localeerror");
        }
    }
}
