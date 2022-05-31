﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2011.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("unicodedata", typeof(IronPython.Modules.unicodedata))]
namespace IronPython.Modules {
    public static class unicodedata {
        private static UCD ucd_5_2_0 = null;

        // This is supposed to by the Unicode 3.2 database, but since .NET doesn't provide it
        // just use the 5.2 one instead. 
        public static UCD ucd_3_2_0 { 
            get {
                if (_ucd_3_2_0 == null) {
                    Interlocked.CompareExchange(ref _ucd_3_2_0, new UCD("3.2.0"), null);
                }

                return _ucd_3_2_0;
            } 
        }
        private static UCD _ucd_3_2_0 = null;

        public static string unidata_version { get { return ucd_5_2_0.unidata_version; } }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, IDictionary/*!*/ dict) {
            EnsureInitialized();
        }

        /// <summary>
        /// Ensures that the modules is initialized so that static methods don't throw.
        /// </summary>
        [MemberNotNull(nameof(ucd_5_2_0))]
        internal static void EnsureInitialized() {
            if (ucd_5_2_0 == null) {
                // This is a lie. The version of Unicode depends on the .NET version as well as the OS. The
                // version of the database stored internally is 5.2, so just say that.
                Interlocked.CompareExchange(ref ucd_5_2_0, new UCD("5.2.0"), null);
            }
        }

        public static string lookup(string name) {
            return ucd_5_2_0.lookup(name);
        }

#nullable enable

        public static string name([NotNone] string unichr)
            => ucd_5_2_0.name(unichr);

        public static object? name([NotNone] string unichr, object? @default)
            => ucd_5_2_0.name(unichr, @default);

        internal static bool TryGetName(int rune, [NotNullWhen(true)] out string? name)
            => ucd_5_2_0.TryGetName(rune, out name);

#nullable restore

        public static int @decimal(char unichr, int @default) {
            return ucd_5_2_0.@decimal(unichr, @default);
        }

        public static int @decimal(char unichr) {
            return ucd_5_2_0.@decimal(unichr);
        }

        public static object @decimal(char unichr, object @default) {
            return ucd_5_2_0.@decimal(unichr, @default);
        }

        public static int digit(char unichr, int @default) {
            return ucd_5_2_0.digit(unichr, @default);
        }

        public static object digit(char unichr, object @default) {
            return ucd_5_2_0.digit(unichr, @default);
        }

        public static int digit(char unichr) {
            return ucd_5_2_0.digit(unichr);
        }

        public static double numeric(char unichr, double @default) {
            return ucd_5_2_0.numeric(unichr, @default);
        }

        public static double numeric(char unichr) {
            return ucd_5_2_0.numeric(unichr);
        }

        public static object numeric(char unichr, object @default) {
            return ucd_5_2_0.numeric(unichr, @default);
        }

        public static string category(char unichr) {
            return ucd_5_2_0.category(unichr);
        }

        public static string bidirectional(char unichr) {
            return ucd_5_2_0.bidirectional(unichr);
        }

        public static int combining(char unichr) {
            return ucd_5_2_0.combining(unichr);
        }

        public static string east_asian_width(char unichr) {
            return ucd_5_2_0.east_asian_width(unichr);
        }

        public static int mirrored(char unichr) {
            return ucd_5_2_0.mirrored(unichr);
        }

        public static string decomposition(char unichr) {
            return ucd_5_2_0.decomposition(unichr);
        }

        public static string normalize(string form, string unistr) {
            return ucd_5_2_0.normalize(form, unistr);
        }

        [PythonType("unicodedata.UCD")]
        public class UCD {
            private const string UnicodedataResourceName = "IronPython.Modules.unicodedata.IPyUnicodeData.txt.gz";
            private const string OtherNotAssigned = "Cn";

            private Dictionary<int, CharInfo> database;
            private List<RangeInfo> ranges;
            private Dictionary<string, int> nameLookup;

            public UCD(string version) {
                unidata_version = version;
                EnsureLoaded();
            }

            public string unidata_version { get; private set; }

            public string lookup(string name) {
                if (TryLookup(name, out int code))
                    return char.ConvertFromUtf32(code);
                throw PythonOps.KeyError("undefined character name");
            }

            private static bool IsUnifiedIdeograph(int code) {
                return (0x3400 <= code && code <= 0x4DB5) || // CJK Ideograph Extension A
                    (0x4E00 <= code && code <= 0x9FEF) || // CJK Ideograph
                    (0x20000 <= code && code <= 0x2A6D6) || // CJK Ideograph Extension B
                    (0x2A700 <= code && code <= 0x2B734) || // CJK Ideograph Extension C - 5.2
                    (0x2B740 <= code && code <= 0x2B81D) || // CJK Ideograph Extension D - 6.0
                    (0x2B820 <= code && code <= 0x2CEA1) || // CJK Ideograph Extension E - 8.0
                    (0x2CEB0 <= code && code <= 0x2EBEF) || // CJK Ideograph Extension F - 10.0
                    (0x30000 <= code && code <= 0x3134A);   // CJK Ideograph Extension G - 13.0
            }

            private bool TryLookup(string name, out int code) {
                code = 0;

                if (name.StartsWith("CJK UNIFIED IDEOGRAPH-", StringComparison.Ordinal)) {
                    var val = name.AsSpan(22);
                    if (val.Length != 4 && val.Length != 5) return false;
                    foreach (var c in val) {
                        code *= 16;
                        switch (c) {
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                                code += c - '0';
                                break;
                            case 'A':
                            case 'B':
                            case 'C':
                            case 'D':
                            case 'E':
                            case 'F':
                                code += c - 'A' + 10;
                                break;
                            default:
                                code = 0;
                                return false;
                        }
                    }
                    return IsUnifiedIdeograph(code);

                }

                return nameLookup.TryGetValue(name, out code);
            }

#nullable enable

            public string name([NotNone] string unichr)
                => TryGetName(GetRune(unichr), out var name) ? name : throw PythonOps.ValueError("no such name");

            public object? name([NotNone] string unichr, object? @default)
                => TryGetName(GetRune(unichr), out var name) ? name : @default;

            internal bool TryGetName(int rune, [NotNullWhen(true)] out string? name) {
                if (IsUnifiedIdeograph(rune)) {
                    name = $"CJK UNIFIED IDEOGRAPH-{rune:X}";
                    return true;
                }
                if (TryGetInfo(rune, out CharInfo info, excludeRanges: true)) {
                    name = info.Name;
                    return true;
                }
                name = null;
                return false;
            }

            private int GetRune(string unichr) {
                if (unichr.Length == 1) {
                    return unichr[0];
                } else if (unichr.Length == 2 && char.IsSurrogatePair(unichr, 0)) {
                    return char.ConvertToUtf32(unichr, 0);
                }
                throw PythonOps.TypeError("argument 1 must be a unicode character, not str");
            }

#nullable restore

            public int @decimal(char unichr, int @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Decimal;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public int @decimal(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Decimal;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                throw PythonOps.ValueError("not a decimal");
            }

            public object @decimal(char unichr, object @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Decimal;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public int digit(char unichr, int @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Digit;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public object digit(char unichr, object @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Digit;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public int digit(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Digit;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                throw PythonOps.ValueError("not a digit");
            }

            public double numeric(char unichr, double @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Numeric;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public double numeric(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Numeric;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                throw PythonOps.ValueError("not a numeric character");
            }

            public object numeric(char unichr, object @default) {
                if (TryGetInfo(unichr, out CharInfo info)) {
                    var d = info.Numeric_Value_Numeric;
                    if (d.HasValue) {
                        return d.Value;
                    }
                }
                return @default;
            }

            public string category(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.General_Category;
                return OtherNotAssigned;
            }

            public string bidirectional(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.Bidi_Class;
                return string.Empty;
            }

            public int combining(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.Canonical_Combining_Class;
                return 0;
            }

            public string east_asian_width(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.East_Asian_Width;
                return string.Empty;
            }

            public int mirrored(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.Bidi_Mirrored;
                return 0;
            }

            public string decomposition(char unichr) {
                if (TryGetInfo(unichr, out CharInfo info))
                    return info.Decomposition_Type;
                return string.Empty;
            }

            public string normalize(string form, string unistr) {
                NormalizationForm nf;
                switch (form) {
                    case "NFC":
                        nf = NormalizationForm.FormC;
                        break;

                    case "NFD":
                        nf = NormalizationForm.FormD;
                        break;

                    case "NFKC":
                        nf = NormalizationForm.FormKC;
                        break;

                    case "NFKD":
                        nf = NormalizationForm.FormKD;
                        break;

                    default:
                        throw new ArgumentException("Invalid normalization form " + form, nameof(form));
                }

                return unistr.Normalize(nf);
            }

            private void BuildDatabase(StreamReader data) {
                var sep = new char[] { ';' };

                database = new Dictionary<int, CharInfo>();
                ranges = new List<RangeInfo>();

                foreach (string raw_line in data.ReadLines()) {
                    int n = raw_line.IndexOf('#');
                    string line = n == -1 ? raw_line : raw_line.Substring(raw_line.Length - n).Trim();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    var info = line.Split(sep, 2);
                    var m = Regex.Match(info[0], @"([0-9a-fA-F]{4})\.\.([0-9a-fA-F]{4})");
                    if (m.Success) {
                        // this is a character range
                        int first = Convert.ToInt32(m.Groups[1].Value, 16);
                        int last = Convert.ToInt32(m.Groups[2].Value, 16);

                        ranges.Add(new RangeInfo(first, last, info[1].Split(sep)));
                    } else {
                        // this is a single character
                        database[Convert.ToInt32(info[0], 16)] = new CharInfo(info[1].Split(sep));
                    }
                }
            }

            private void BuildNameLookup() {
                nameLookup = database.Where(c => !c.Value.Name.StartsWith("<", StringComparison.Ordinal)).ToDictionary(c => c.Value.Name, c => c.Key, StringComparer.OrdinalIgnoreCase);
            }

            internal bool TryGetInfo(int unichr, out CharInfo charInfo, bool excludeRanges = false) {
                if (database.TryGetValue(unichr, out charInfo)) return true;
                if (!excludeRanges) {
                    foreach (var range in ranges) {
                        if (range.First <= unichr && unichr <= range.Last) {
                            charInfo = range;
                            return true;
                        }
                    }
                }
                return false;
            }

            private void EnsureLoaded() {
                if (database == null || nameLookup == null) {
                    var rsrc = typeof(unicodedata).Assembly.GetManifestResourceStream(UnicodedataResourceName);
                    var gzip = new GZipStream(rsrc, CompressionMode.Decompress);
                    var data = new StreamReader(gzip, Encoding.UTF8);

                    BuildDatabase(data);
                    BuildNameLookup();
                }
            }
        }
    }

    internal static class StreamReaderExtensions {
        public static IEnumerable<string> ReadLines(this StreamReader reader) {
            string line;
            while ((line = reader.ReadLine()) != null)
                yield return line;
        }
    }

    internal class CharInfo {
        internal CharInfo(string[] info) {
            Name = info[PropertyIndex.Name].ToUpperInvariant();
            General_Category = info[PropertyIndex.General_Category];
            Canonical_Combining_Class = int.Parse(info[PropertyIndex.Canonical_Combining_Class]);
            Bidi_Class = info[PropertyIndex.Bidi_Class];
            Decomposition_Type = info[PropertyIndex.Decomposition_Type];

            string nvdes = info[PropertyIndex.Numeric_Value_Decimal];
            Numeric_Value_Decimal = nvdes != "" ? (int?)int.Parse(nvdes) : null;

            string nvdis = info[PropertyIndex.Numeric_Value_Digit];
            Numeric_Value_Digit = nvdis != "" ? (int?)int.Parse(nvdis) : null;

            string nvns = info[PropertyIndex.Numeric_Value_Numeric];
            if (nvns != "") {
                string[] nvna = nvns.Split(new char[] { '/' });
                double num = double.Parse(nvna[0]);
                if (nvna.Length > 1) {
                    double den = double.Parse(nvna[1]);
                    num /= den;
                }

                Numeric_Value_Numeric = num;
            } else {
                Numeric_Value_Numeric = null;
            }

            Bidi_Mirrored = info[PropertyIndex.Bidi_Mirrored] == "Y" ? 1 : 0;
            East_Asian_Width = info[PropertyIndex.East_Asian_Width];
        }

        internal readonly string Name;
        internal readonly string General_Category;
        internal readonly int Canonical_Combining_Class;
        internal readonly string Bidi_Class;
        internal readonly string Decomposition_Type;
        internal readonly int? Numeric_Value_Decimal;
        internal readonly int? Numeric_Value_Digit;
        internal readonly double? Numeric_Value_Numeric;
        internal readonly int Bidi_Mirrored;
        internal readonly string East_Asian_Width;

        private static class PropertyIndex {
            internal const int Name = 0;
            internal const int General_Category = 1;
            internal const int Canonical_Combining_Class = 2;
            internal const int Bidi_Class = 3;
            internal const int Decomposition_Type = 4;
            internal const int Numeric_Value_Decimal = 5;
            internal const int Numeric_Value_Digit = 6;
            internal const int Numeric_Value_Numeric = 7;
            internal const int Bidi_Mirrored = 8;
            internal const int East_Asian_Width = 9;
        }
    }

    internal class RangeInfo : CharInfo {
        internal RangeInfo(int first, int last, string[] info)
            : base(info) {
            this.First = first;
            this.Last = last;
        }

        internal readonly int First;
        internal readonly int Last;
    }
}
