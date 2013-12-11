/* ****************************************************************************
 *
 * Copyright (c) Jeff Hardy 2011.
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

#if FEATURE_COMPRESSION

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("unicodedata", typeof(IronPython.Modules.unicodedata))]

namespace IronPython.Modules
{
    public static class unicodedata
    {
        private const string UnicodedataResourceName = "IronPython.Modules.unicodedata.IPyUnicodeData.txt.gz";
        private const string OtherNotAssigned = "Cn";

        private static Dictionary<int, CharInfo> database;
        private static List<RangeInfo> ranges;
        private static Dictionary<string, int> nameLookup;

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, IDictionary/*!*/ dict)
        {
            EnsureLoaded();
        }

        public static string lookup(string name)
        {
            EnsureLoaded();

            return Convert.ToChar(nameLookup[name]).ToString();
        }

        public static string name(char unichr, string @default = null)
        {
            try
            {
                return GetInfo(unichr).Name;
            }
            catch(KeyNotFoundException)
            {
                if(@default != null)
                    return @default;
                else
                    throw;
            }
        }

        public static int @decimal(char unichr, int @default) 
        {
            try 
            {
                int? d = GetInfo(unichr).Numeric_Value_Decimal;
                if (d.HasValue) 
                {
                    return d.Value;
                } 
                else 
                {
                    return @default;
                }
            } 
            catch (KeyNotFoundException) 
            {
                return @default;
            }
        }

        public static int @decimal(char unichr) 
        {
            try 
            {
                int? d = GetInfo(unichr).Numeric_Value_Decimal;
                if (d.HasValue) 
                {
                    return d.Value;
                }
                else 
                {
                    throw PythonOps.ValueError("not a decimal");
                }
            } 
            catch (KeyNotFoundException) 
            {
                throw PythonOps.ValueError("not a decimal");
            }
        }

        public static object @decimal(char unichr, object @default) 
        {
            try 
            {
                int? d = GetInfo(unichr).Numeric_Value_Decimal;
                if (d.HasValue)
                {
                    return d.Value;
                } 
                else 
                {
                    return @default;
                }
            } 
            catch (KeyNotFoundException) 
            {
                return @default;
            }
        }

        public static int digit(char unichr, int @default)
        {
            try
            {
                int? d = GetInfo(unichr).Numeric_Value_Digit;
                if(d.HasValue)
                {
                    return d.Value;
                }
                else
                {
                    return @default;
                }
            }
            catch(KeyNotFoundException)
            {
                return @default;
            }
        }

        public static object digit(char unichr, object @default) 
        {
            try 
            {
                int? d = GetInfo(unichr).Numeric_Value_Digit;
                if (d.HasValue) 
                {
                    return d.Value;
                }
                else 
                {
                    return @default;
                }
            }
            catch (KeyNotFoundException) 
            {
                return @default;
            }
        }

        public static int digit(char unichr) 
        {
            try 
            {
                int? d = GetInfo(unichr).Numeric_Value_Digit;
                if (d.HasValue) 
                {
                    return d.Value;
                } 
                else 
                {
                    throw PythonOps.ValueError("not a digit");
                }
            } 
            catch (KeyNotFoundException) 
            {
                throw PythonOps.ValueError("not a digit");
            }
        }

        public static double numeric(char unichr, double @default) 
        {
            try 
            {
                double? d = GetInfo(unichr).Numeric_Value_Numeric;
                if (d.HasValue) 
                {
                    return d.Value;
                } 
                else 
                {
                    return @default;
                }
            } 
            catch (KeyNotFoundException) 
            {
                return @default;
            }
        }

        public static double numeric(char unichr) 
        {
            try 
            {
                double? d = GetInfo(unichr).Numeric_Value_Numeric;
                if (d.HasValue) 
                {
                    return d.Value;
                } 
                else 
                {
                    throw PythonOps.ValueError("not a numeric character");
                }
            } 
            catch (KeyNotFoundException) 
            {
                throw PythonOps.ValueError("not a numeric character");
            }
        }

        public static object numeric(char unichr, object @default) 
        {
            try 
            {
                double? d = GetInfo(unichr).Numeric_Value_Numeric;
                if (d.HasValue) 
                {
                    return d.Value;
                } 
                else 
                {
                    return @default;
                }
            } 
            catch (KeyNotFoundException) 
            {
                return @default;
            }
        }

        public static string category(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return OtherNotAssigned;
            return GetInfo(unichr).General_Category;
        }

        public static string bidirectional(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return string.Empty;
            return GetInfo(unichr).Bidi_Class;
        }

        public static int combining(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return 0;
            return GetInfo(unichr).Canonical_Combining_Class;
        }

        public static string east_asian_width(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return string.Empty;
            return GetInfo(unichr).East_Asian_Width;
        }

        public static int mirrored(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return 0;
            return GetInfo(unichr).Bidi_Mirrored;
        }

        public static string decomposition(char unichr)
        {
            if (!database.ContainsKey(unichr))
                return string.Empty;
            return GetInfo(unichr).Decomposition_Type;
        }

        public static string normalize(string form, string unistr)
        {
            NormalizationForm nf;
            switch(form)
            {
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
                    throw new ArgumentException("Invalid normalization form " + form, "form");
            }

            return unistr.Normalize(nf);
        }

        static void BuildDatabase(StreamReader data)
        {
            var sep = new char[] { ';' };

            database = new Dictionary<int, CharInfo>();
            ranges = new List<RangeInfo>();

            foreach(string raw_line in data.ReadLines())
            {
                int n = raw_line.IndexOf('#');
                string line = n == -1 ? raw_line : raw_line.Substring(raw_line.Length - n).Trim();

                if(string.IsNullOrEmpty(line))
                    continue;

                var info = line.Split(sep, 2);
                var m = Regex.Match(info[0], @"([0-9a-fA-F]{4})\.\.([0-9a-fA-F]{4})");
                if(m.Success)
                {
                    // this is a character range
                    int first = Convert.ToInt32(m.Groups[1].Value, 16);
                    int last = Convert.ToInt32(m.Groups[2].Value, 16);

                    ranges.Add(new RangeInfo(first, last, info[1].Split(sep)));
                }
                else
                {
                    // this is a single character
                    database[Convert.ToInt32(info[0], 16)] = new CharInfo(info[1].Split(sep));
                }
            }
        }

        static void BuildNameLookup()
        {
            nameLookup = database.Where(c => !c.Value.Name.StartsWith("<")).ToDictionary(c => c.Value.Name, c => c.Key);
        }

        private static CharInfo GetInfo(char unichr)
        {
            EnsureLoaded();
            
            return database[(int)unichr];
        }

        private static void EnsureLoaded()
        {
            if(database == null || nameLookup == null)
            {
                var rsrc = typeof(unicodedata).Assembly.GetManifestResourceStream(UnicodedataResourceName);
                var gzip = new GZipStream(rsrc, CompressionMode.Decompress);
                var data = new StreamReader(gzip, Encoding.UTF8);

                BuildDatabase(data);
                BuildNameLookup();
            }
        }
    }

    internal static class StreamReaderExtensions
    {
        public static IEnumerable<string> ReadLines(this StreamReader reader)
        {
            string line;
            while((line = reader.ReadLine()) != null)
                yield return line;
        }
    }

    class CharInfo
    {
        internal CharInfo(string[] info)
        {
            this.Name = info[PropertyIndex.Name].ToUpperInvariant();
            this.General_Category = info[PropertyIndex.General_Category];
            this.Canonical_Combining_Class = int.Parse(info[PropertyIndex.Canonical_Combining_Class]);
            this.Bidi_Class = info[PropertyIndex.Bidi_Class];
            this.Decomposition_Type = info[PropertyIndex.Decomposition_Type];

            string nvdes = info[PropertyIndex.Numeric_Value_Decimal];
            this.Numeric_Value_Decimal = nvdes != "" ? (int?)int.Parse(nvdes) : null;

            string nvdis = info[PropertyIndex.Numeric_Value_Digit];
            this.Numeric_Value_Digit = nvdis != "" ? (int?)int.Parse(nvdis) : null;

            string nvns = info[PropertyIndex.Numeric_Value_Numeric];
            if(nvns != "")
            {
                string[] nvna = nvns.Split(new char[] { '/' });
                double num = double.Parse(nvna[0]);
                if(nvna.Length > 1)
                {
                    double den = double.Parse(nvna[1]);
                    num /= den;
                }

                this.Numeric_Value_Numeric = num;
            }
            else
            {
                this.Numeric_Value_Numeric = null;
            }

            this.Bidi_Mirrored = info[PropertyIndex.Bidi_Mirrored] == "Y" ? 1 : 0;
            this.East_Asian_Width = info[PropertyIndex.East_Asian_Width];
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

        static class PropertyIndex
        {
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

    class RangeInfo : CharInfo
    {
        internal RangeInfo(int first, int last, string[] info) : base(info)
        { 
            this.First = first;
            this.Last = last;
        }

        internal readonly int First;
        internal readonly int Last;
    }
}

#endif
