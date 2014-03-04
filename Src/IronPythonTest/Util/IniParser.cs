using System;
using System.Collections.Generic;
using System.IO;

namespace IronPythonTest.Util {
    using Section = Dictionary<string, string>;
    using OptionStore = Dictionary<string, Dictionary<string, string>>;

    class IniParser {
        OptionStore options;

        public IniParser(Stream source) {
            this.options = Parse(new StreamReader(source).ReadLines());
        }

        public string GetValue(string sectionName, string key) {
            return GetValue(sectionName, key, null);
        }

        public string GetValue(string sectionName, string key, string @default) {
            sectionName = string.IsNullOrEmpty(sectionName) ? "DEFAULT" : sectionName;

            Section section;
            if (!this.options.TryGetValue(sectionName, out section)) {
                section = this.options["DEFAULT"];
            }

            string value;
            if (!section.TryGetValue(key, out value)) {
                if (!this.options["DEFAULT"].TryGetValue(key, out value)) {
                    return @default;
                }
            }

            return value;
        }

        public bool GetBool(string sectionName, string key) {
            return GetValue(sectionName, key).AsBool();
        }

        private static OptionStore Parse(IEnumerable<string> lines) {
            Section currentSection = new Section();
            OptionStore options = new OptionStore(StringComparer.OrdinalIgnoreCase) { { "DEFAULT", currentSection } };
            foreach (var rawline in lines) {
                string line = rawline.Split(new [] { ';', '#' }, 2)[0].Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) {
                    var sectionName = line.Substring(1, line.Length - 2);
                    if (!options.TryGetValue(sectionName, out currentSection)) {
                        currentSection = new Section();
                        options.Add(sectionName, currentSection);
                    } 
                } else {
                    var result = line.Split(new [] {'='}, 2);
                    var key = result[0].Trim();
                    var value = result.Length > 1 ? result[1] : "1";

                    currentSection.Add(key, value);
                }

            }

            return options;
        }
    }

    static class TextReaderExtensions {
        public static IEnumerable<string> ReadLines(this TextReader tr) {
            string line;
            while ((line = tr.ReadLine()) != null) {
                yield return line;
            }
        }
    }

    static class StringExtensions {
        static HashSet<string> Truthy = new HashSet<string> { "1", "t", "true", "y", "yes" };
        static HashSet<string> Falsey = new HashSet<string> { "0", "f", "false", "n", "no" };

        public static bool AsBool(this string s) {
            var l = s.ToLowerInvariant();
            if (Truthy.Contains(l)) {
                return true;
            } else if (Falsey.Contains(l)) {
                return false;
            } else {
                throw new ArgumentException(string.Format("'{0}' is neither true nor false.", s));
            }
        }
    }
}
