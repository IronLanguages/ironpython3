// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("re", typeof(IronPython.Modules.PythonRegex))]
namespace IronPython.Modules {

    /// <summary>
    /// Python regular expression module.
    /// </summary>
    public static class PythonRegex {
        private static CacheDict<PatternKey, RE_Pattern> _cachedPatterns = new CacheDict<PatternKey, RE_Pattern>(100);

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("reerror", dict, "error", "re");
            PythonCopyReg.GetDispatchTable(context.SharedContext)[DynamicHelpers.GetPythonTypeFromType(typeof(RE_Pattern))] = dict["_pickle"];
        }

        private static readonly Random r = new Random(DateTime.Now.Millisecond);

        #region CONSTANTS

        // short forms
        public const int I = 0x02;
        public const int L = 0x04;
        public const int M = 0x08;
        public const int S = 0x10;
        public const int U = 0x20;
        public const int X = 0x40;
        public const int A = 0x100;

        // long forms
        public const int IGNORECASE = 0x02;
        public const int LOCALE = 0x04;
        public const int MULTILINE = 0x08;
        public const int DOTALL = 0x10;
        public const int UNICODE = 0x20;
        public const int VERBOSE = 0x40;
        public const int ASCII = 0x100;

        #endregion

        #region Public API Surface

        public static RE_Pattern compile(CodeContext/*!*/ context, object pattern, int flags = 0) {
            try {
                return GetPattern(context, pattern, flags, true);
            } catch (ArgumentException e) {
                throw PythonExceptions.CreateThrowable(error(context), e.Message);
            }
        }

        public const string engine = "cli reg ex";

        public static string escape(string text) {
            if (text == null) throw PythonOps.TypeError("text must not be None");

            for (int i = 0; i < text.Length; i++) {
                if (!char.IsLetterOrDigit(text[i])) {
                    StringBuilder sb = new StringBuilder(text, 0, i, text.Length);

                    char ch = text[i];
                    do {
                        sb.Append('\\');
                        sb.Append(ch);
                        i++;

                        int last = i;
                        while (i < text.Length) {
                            ch = text[i];
                            if (!char.IsLetterOrDigit(ch)) {
                                break;
                            }
                            i++;
                        }
                        sb.Append(text, last, i - last);
                    } while (i < text.Length);

                    return sb.ToString();
                }
            }
            return text;
        }

        public static PythonList findall(CodeContext/*!*/ context, object pattern, string @string, int flags = 0) {
            RE_Pattern pat = GetPattern(context, ValidatePattern(pattern), flags);
            ValidateString(@string, nameof(@string));

            MatchCollection mc = pat.FindAllWorker(context, @string, 0, @string.Length);
            return FixFindAllMatch(pat, mc, null);
        }

        public static PythonList findall(CodeContext context, object pattern, IList<byte> @string, int flags = 0) {
            RE_Pattern pat = GetPattern(context, ValidatePattern(pattern), flags);
            ValidateString(@string, nameof(@string));

            MatchCollection mc = pat.FindAllWorker(context, @string, 0, @string.Count);
            return FixFindAllMatch(pat, mc, FindMaker(@string));
        }

        private static Func<string, object> FindMaker(object input) {
            Func<string, object> maker = null;
            if (input is ByteArray) {
                maker = delegate (string x) { return new ByteArray(x.MakeByteArray()); };
            }
            return maker;
        }

        private static PythonList FixFindAllMatch(RE_Pattern pat, MatchCollection mc, Func<string, object> maker) {
            object[] matches = new object[mc.Count];
            int numgrps = pat._re.GetGroupNumbers().Length;
            for (int i = 0; i < mc.Count; i++) {
                if (numgrps > 2) { // CLR gives us a "bonus" group of 0 - the entire expression
                    //  at this point we have more than one group in the pattern;
                    //  need to return a list of tuples in this case

                    //  for each match item in the matchcollection, create a tuple representing what was matched
                    //  e.g. findall("(\d+)|(\w+)", "x = 99y") == [('', 'x'), ('99', ''), ('', 'y')]
                    //  in the example above, ('', 'x') did not match (\d+) as indicated by '' but did 
                    //  match (\w+) as indicated by 'x' and so on...
                    int k = 0;
                    List<object> tpl = new List<object>();
                    foreach (Group g in mc[i].Groups) {
                        //  here also the CLR gives us a "bonus" match as the first item which is the 
                        //  group that was actually matched in the tuple e.g. we get 'x', '', 'x' for 
                        //  the first match object...so we'll skip the first item when creating the 
                        //  tuple
                        if (k++ != 0) {
                            tpl.Add(maker != null ? maker(g.Value) : g.Value);
                        }
                    }
                    matches[i] = PythonTuple.Make(tpl);
                } else if (numgrps == 2) {
                    //  at this point we have exactly one group in the pattern (including the "bonus" one given 
                    //  by the CLR 
                    //  skip the first match since that contains the entire match and not the group match
                    //  e.g. re.findall(r"(\w+)\s+fish\b", "green fish") will have "green fish" in the 0 
                    //  index and "green" as the (\w+) group match
                    matches[i] = maker != null ? maker(mc[i].Groups[1].Value) : mc[i].Groups[1].Value;
                } else {
                    matches[i] = maker != null ? maker(mc[i].Value) : mc[i].Value;
                }
            }

            return PythonList.FromArrayNoCopy(matches);
        }

        public static object finditer(CodeContext/*!*/ context, object pattern, object @string, int flags = 0) {
            RE_Pattern pat = GetPattern(context, ValidatePattern(pattern), flags);

            string str = ValidateString(@string, nameof(@string));
            return MatchIterator(pat.FindAllWorker(context, str, 0, str.Length), pat, str);
        }

        public static RE_Match match(CodeContext/*!*/ context, object pattern, object @string, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).match(ValidateString(@string, nameof(@string)));

        public static RE_Match fullmatch(CodeContext/*!*/ context, object pattern, object @string, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).fullmatch(context, ValidateString(@string, nameof(@string)));

        public static RE_Match search(CodeContext/*!*/ context, object pattern, object @string, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).search(ValidateString(@string, nameof(@string)));

        [return: SequenceTypeInfo(typeof(string))]
        public static PythonList split(CodeContext/*!*/ context, object pattern, object @string, int maxsplit = 0, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).split(ValidateString(@string, nameof(@string)), maxsplit);

        public static string sub(CodeContext/*!*/ context, object pattern, object repl, object @string, int count = 0, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).sub(context, repl, ValidateString(@string, nameof(@string)), count);

        public static object subn(CodeContext/*!*/ context, object pattern, object repl, object @string, int count = 0, int flags = 0)
            => GetPattern(context, ValidatePattern(pattern), flags).subn(context, repl, ValidateString(@string, nameof(@string)), count);

        public static void purge() {
            _cachedPatterns = new CacheDict<PatternKey, RE_Pattern>(100);
        }

        #endregion

        #region Public classes

        /// <summary>
        /// Compiled reg-ex pattern
        /// </summary>
        [PythonType]
        public class RE_Pattern : IWeakReferenceable {
            internal readonly Regex _re;
            internal readonly ParsedRegex _pre;
            private PythonDictionary _groups;
            private WeakRefTracker _weakRefTracker;

            private static Regex GenRegex(CodeContext/*!*/ context, string pattern, int flags, bool compiled, bool fullmatch) {
                try {
                    RegexOptions opts = FlagsToOption(flags);
                    return new Regex(fullmatch ? $"(?:{pattern})\\Z" : pattern, opts | (compiled ? RegexOptions.Compiled : RegexOptions.None));
                } catch (ArgumentException e) {
                    throw PythonExceptions.CreateThrowable(error(context), e.Message);
                }
            }

            internal RE_Pattern(CodeContext/*!*/ context, object pattern, int flags = 0, bool compiled = false) {
                _pre = PreParseRegex(context, ValidatePatternAsString(pattern));
                flags |= OptionToFlags(_pre.Options);
                _re = GenRegex(context, _pre.Pattern, flags, compiled, false);
                this.flags = flags;
            }

            public RE_Match match(object text) {
                string input = ValidateString(text, nameof(text));
                return RE_Match.makeMatch(_re.Match(input), this, input, 0, input.Length);
            }

            private static int FixPosition(string text, int position) {
                if (position <= 0) return 0;
                if (position > text.Length) return text.Length;

                return position;
            }

            public RE_Match match(object text, int pos) {
                string input = ValidateString(text, nameof(text));
                pos = FixPosition(input, pos);
                return RE_Match.makeMatch(_re.Match(input, pos), this, input, pos, input.Length);
            }

            public RE_Match match(object text, [DefaultParameterValue(0)]int pos, int endpos) {
                string input = ValidateString(text, nameof(text));
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return RE_Match.makeMatch(
                    _re.Match(input.Substring(0, endpos), pos),
                    this,
                    input,
                    pos,
                    endpos);
            }

            private Regex _re_fullmatch;
            private Regex GetRegexFullMatch(CodeContext /*!*/ context) {
                if (_re_fullmatch == null) {
                    lock (_re) {
                        if (_re_fullmatch == null)
                            _re_fullmatch = GenRegex(context, _pre.Pattern, flags, _re.Options.HasFlag(RegexOptions.Compiled), true);
                    }
                }

                return _re_fullmatch;
            }

            public RE_Match fullmatch(CodeContext/*!*/ context, object text, int pos = 0) {
                string input = ValidateString(text, nameof(text));
                pos = FixPosition(input, pos);

                return RE_Match.makeFullMatch(GetRegexFullMatch(context).Match(input, pos), this, input, pos, input.Length);
            }

            public RE_Match fullmatch(CodeContext/*!*/ context, object text, [DefaultParameterValue(0)]int pos, int endpos) {
                string input = ValidateString(text, nameof(text));
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return RE_Match.makeFullMatch(
                    GetRegexFullMatch(context).Match(input.Substring(0, endpos), pos),
                    this,
                    input,
                    pos,
                    endpos);
            }

            public RE_Match search(object text) {
                string input = ValidateString(text, nameof(text));
                return RE_Match.make(_re.Match(input), this, input);
            }

            public RE_Match search(object text, int pos) {
                string input = ValidateString(text, nameof(text));
                if (pos < 0) pos = 0;
                return RE_Match.make(_re.Match(input, pos), this, input);
            }

            public RE_Match search(object text, int pos, int endpos) {
                string input = ValidateString(text, nameof(text));
                if (pos < 0) pos = 0;
                if (endpos < pos) return null;
                if (endpos < input.Length) input = input.Substring(0, endpos);
                return RE_Match.make(_re.Match(input, pos), this, input);
            }

            public object findall(CodeContext/*!*/ context, object @string, int pos = 0, object endpos = null) {
                MatchCollection mc = FindAllWorker(context, ValidateString(@string, nameof(@string)), pos, endpos);
                return FixFindAllMatch(this, mc, FindMaker(@string));
            }

            internal MatchCollection FindAllWorker(CodeContext/*!*/ context, string str, int pos, object endpos) {
                string against = str;
                if (endpos != null) {
                    int end = context.LanguageContext.ConvertToInt32(endpos);
                    against = against.Substring(0, Math.Max(end, 0));
                }
                return _re.Matches(against, pos);
            }

            internal MatchCollection FindAllWorker(CodeContext/*!*/ context, IList<byte> str, int pos, object endpos)
                => FindAllWorker(context, str.MakeString(), pos, endpos);

            public object finditer(CodeContext/*!*/ context, object @string, int pos=0) {
                string input = ValidateString(@string, nameof(@string));
                return MatchIterator(FindAllWorker(context, input, pos, null), this, input);
            }

            public object finditer(CodeContext/*!*/ context, object @string, int pos, int endpos) {
                string input = ValidateString(@string, nameof(@string));
                return MatchIterator(FindAllWorker(context, input, pos, endpos), this, input);
            }

            [return: SequenceTypeInfo(typeof(string))]
            public PythonList split(object @string, int maxsplit = 0) {
                PythonList result = new PythonList();
                // fast path for negative maxSplit ( == "make no splits")
                if (maxsplit < 0) {
                    result.AddNoLock(ValidateString(@string, nameof(@string)));
                } else {
                    // iterate over all matches
                    string theStr = ValidateString(@string, nameof(@string));
                    MatchCollection matches = _re.Matches(theStr);
                    int lastPos = 0; // is either start of the string, or first position *after* the last match
                    int nSplits = 0; // how many splits have occurred?
                    foreach (Match m in matches) {
                        if (m.Length > 0) {
                            // add substring from lastPos to beginning of current match
                            result.AddNoLock(theStr.Substring(lastPos, m.Index - lastPos));
                            // if there are subgroups of the match, add their match or None
                            if (m.Groups.Count > 1)
                                for (int i = 1; i < m.Groups.Count; i++)
                                    if (m.Groups[i].Success)
                                        result.AddNoLock(m.Groups[i].Value);
                                    else
                                        result.AddNoLock(null);
                            // update lastPos, nSplits
                            lastPos = m.Index + m.Length;
                            nSplits++;
                            if (nSplits == maxsplit)
                                break;
                        }
                    }
                    // add tail following last match
                    result.AddNoLock(theStr.Substring(lastPos));
                }
                return result;
            }

            public string sub(CodeContext/*!*/ context, object repl, object @string, int count = 0) {
                if (repl == null) throw PythonOps.TypeError("NoneType is not valid repl");
                //  if 'count' is omitted or 0, all occurrences are replaced
                if (count == 0) count = int.MaxValue;

                string replacement = repl as string;
                if (replacement == null) {
                    if (repl is ExtensibleString) {
                        replacement = ((ExtensibleString)repl).Value;
                    } else if (repl is Bytes) {
                        replacement = ((Bytes)repl).ToString();
                    }
                }

                Match prev = null;
                string input = ValidateString(@string, nameof(@string));
                return _re.Replace(
                    input,
                    delegate (Match match) {
                        //  from the docs: Empty matches for the pattern are replaced 
                        //  only when not adjacent to a previous match
                        if (string.IsNullOrEmpty(match.Value) && prev != null &&
                                        (prev.Index + prev.Length) == match.Index) {
                            return "";
                        };
                        prev = match;

                        if (replacement != null) return UnescapeGroups(match, replacement);
                        return PythonCalls.Call(context, repl, RE_Match.make(match, this, input)) as string;
                    },
                    count);
            }

            public object subn(CodeContext/*!*/ context, object repl, object @string, int count = 0) {
                if (repl == null) throw PythonOps.TypeError("NoneType is not valid repl");
                //  if 'count' is omitted or 0, all occurrences are replaced
                if (count == 0) count = int.MaxValue;

                int totalCount = 0;
                string res;
                string replacement = repl as string;

                if (replacement == null) {
                    if (repl is ExtensibleString) {
                        replacement = ((ExtensibleString)repl).Value;
                    } else if (repl is Bytes) {
                        replacement = ((Bytes)repl).ToString();
                    }
                }

                Match prev = null;
                string input = ValidateString(@string, nameof(@string));
                res = _re.Replace(
                    input,
                    delegate (Match match) {
                        //  from the docs: Empty matches for the pattern are replaced 
                        //  only when not adjacent to a previous match
                        if (string.IsNullOrEmpty(match.Value) && prev != null &&
                            (prev.Index + prev.Length) == match.Index) {
                            return "";
                        };
                        prev = match;

                        totalCount++;
                        if (replacement != null) return UnescapeGroups(match, replacement);

                        return PythonCalls.Call(context, repl, RE_Match.make(match, this, input)) as string;
                    },
                    count);

                return PythonTuple.MakeTuple(res, totalCount);
            }

            public int flags { get; }

            public PythonDictionary groupindex {
                get {
                    if (_groups == null) {
                        PythonDictionary d = new PythonDictionary();
                        string[] names = _re.GetGroupNames();
                        int[] nums = _re.GetGroupNumbers();
                        for (int i = 1; i < names.Length; i++) {
                            if (char.IsDigit(names[i][0]) || names[i].StartsWith(_mangledNamedGroup)) {
                                // skip numeric names and our mangling for unnamed groups mixed w/ named groups.
                                continue;
                            }

                            d[names[i]] = nums[i];
                        }
                        _groups = d;
                    }
                    return _groups;
                }
            }

            public int groups => _re.GetGroupNumbers().Length - 1;

            public string pattern => _pre.UserPattern;

            public override bool Equals(object obj)
                => obj is RE_Pattern other && other.pattern == pattern && other.flags == flags;

            public override int GetHashCode() => pattern.GetHashCode() ^ flags;

            #region IWeakReferenceable Members

            WeakRefTracker IWeakReferenceable.GetWeakRef() => _weakRefTracker;

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _weakRefTracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) => ((IWeakReferenceable)this).SetWeakRef(value);

            #endregion
        }

        public static PythonTuple _pickle(CodeContext/*!*/ context, RE_Pattern pattern) {
            object scope = Importer.ImportModule(context, new PythonDictionary(), "re", false, 0);
            if (scope is PythonModule module && module.__dict__.TryGetValue("compile", out object compile)) {
                return PythonTuple.MakeTuple(compile, PythonTuple.MakeTuple(pattern.pattern, pattern.flags));
            }
            throw new InvalidOperationException("couldn't find compile method");
        }

        [PythonType]
        public class RE_Match {
            private readonly Match _m;
            private int _lastindex = -1;

            #region Internal makers

            internal static RE_Match make(Match m, RE_Pattern pattern, string input) {
                if (m.Success) return new RE_Match(m, pattern, input, 0, input.Length);
                return null;
            }

            internal static RE_Match make(Match m, RE_Pattern pattern, string input, int offset, int endpos) {
                if (m.Success) return new RE_Match(m, pattern, input, offset, endpos);
                return null;
            }

            internal static RE_Match makeMatch(Match m, RE_Pattern pattern, string input, int offset, int endpos) {
                if (m.Success && m.Index == offset) return new RE_Match(m, pattern, input, offset, endpos);
                return null;
            }

            internal static RE_Match makeFullMatch(Match m, RE_Pattern pattern, string input, int offset, int endpos) {
                if (m.Success && m.Index == offset && m.Length == endpos - offset) return new RE_Match(m, pattern, input, offset, endpos);
                return null;
            }

            #endregion

            #region Public ctors

            public RE_Match(Match m, RE_Pattern pattern, string text) {
                _m = m;
                re = pattern;
                @string = text;
            }

            public RE_Match(Match m, RE_Pattern pattern, string text, int pos, int endpos) {
                _m = m;
                re = pattern;
                @string = text;
                this.pos = pos;
                this.endpos = endpos;
            }

            #endregion

            #region Public API Surface

            public int end() => _m.Index + _m.Length;

            public int start() => _m.Index;

            public int start(object group) {
                int grpIndex = GetGroupIndex(group);
                if (!_m.Groups[grpIndex].Success) {
                    return -1;
                }
                return _m.Groups[grpIndex].Index;
            }

            public int end(object group) {
                int grpIndex = GetGroupIndex(group);
                if (!_m.Groups[grpIndex].Success) {
                    return -1;
                }
                return _m.Groups[grpIndex].Index + _m.Groups[grpIndex].Length;
            }

            public object group(object index, params object[] additional) {
                if (additional.Length == 0) {
                    return group(index);
                }

                object[] res = new object[additional.Length + 1];
                res[0] = _m.Groups[GetGroupIndex(index)].Success ? _m.Groups[GetGroupIndex(index)].Value : null;
                for (int i = 1; i < res.Length; i++) {
                    int grpIndex = GetGroupIndex(additional[i - 1]);
                    res[i] = _m.Groups[grpIndex].Success ? _m.Groups[grpIndex].Value : null;
                }
                return PythonTuple.MakeTuple(res);
            }

            public string group(object index) {
                int pos = GetGroupIndex(index);
                Group g = _m.Groups[pos];
                return g.Success ? g.Value : null;
            }

            public string group() => group(0);

            [return: SequenceTypeInfo(typeof(string))]
            public PythonTuple groups() => groups(null);

            public PythonTuple groups(object @default) {
                object[] ret = new object[_m.Groups.Count - 1];
                for (int i = 1; i < _m.Groups.Count; i++) {
                    if (!_m.Groups[i].Success) {
                        ret[i - 1] = @default;
                    } else {
                        ret[i - 1] = _m.Groups[i].Value;
                    }
                }
                return PythonTuple.MakeTuple(ret);
            }

            public string expand(object template) {
                string strTmp = ValidateString(template, nameof(template));

                StringBuilder res = new StringBuilder();
                for (int i = 0; i < strTmp.Length; i++) {
                    if (strTmp[i] != '\\') { res.Append(strTmp[i]); continue; }
                    if (++i == strTmp.Length) { res.Append(strTmp[i - 1]); continue; }

                    if (char.IsDigit(strTmp[i])) {
                        AppendGroup(res, (int)(strTmp[i] - '0'));
                    } else if (strTmp[i] == 'g') {
                        if (++i == strTmp.Length) { res.Append("\\g"); return res.ToString(); }
                        if (strTmp[i] != '<') {
                            res.Append("\\g<"); continue;
                        } else { // '<'
                            StringBuilder name = new StringBuilder();
                            i++;
                            while (strTmp[i] != '>' && i < strTmp.Length) {
                                name.Append(strTmp[i++]);
                            }
                            AppendGroup(res, re._re.GroupNumberFromName(name.ToString()));
                        }
                    } else {
                        switch (strTmp[i]) {
                            case 'n': res.Append('\n'); break;
                            case 'r': res.Append('\r'); break;
                            case 't': res.Append('\t'); break;
                            case '\\': res.Append('\\'); break;
                        }
                    }

                }
                return res.ToString();
            }

            [return: DictionaryTypeInfo(typeof(string), typeof(string))]
            public PythonDictionary groupdict() => groupdict(null);

            private static bool IsGroupNumber(string name) {
                foreach (char c in name) {
                    if (!char.IsNumber(c)) return false;
                }
                return true;
            }

            [return: DictionaryTypeInfo(typeof(string), typeof(string))]
            public PythonDictionary groupdict([NotNull]string value) => groupdict((object)value);

            [return: DictionaryTypeInfo(typeof(string), typeof(object))]
            public PythonDictionary groupdict(object value) {
                string[] groupNames = this.re._re.GetGroupNames();
                Debug.Assert(groupNames.Length == this._m.Groups.Count);
                PythonDictionary d = new PythonDictionary();
                for (int i = 0; i < groupNames.Length; i++) {
                    if (IsGroupNumber(groupNames[i])) continue; // python doesn't report group numbers

                    if (_m.Groups[i].Captures.Count != 0) {
                        d[groupNames[i]] = _m.Groups[i].Value;
                    } else {
                        d[groupNames[i]] = value;
                    }
                }
                return d;
            }

            [return: SequenceTypeInfo(typeof(int))]
            public PythonTuple span() => PythonTuple.MakeTuple(start(), end());

            [return: SequenceTypeInfo(typeof(int))]
            public PythonTuple span(object group) => PythonTuple.MakeTuple(start(group), end(group));

            public int pos { get; }

            public int endpos { get; }

            public string @string { get; }

            public PythonTuple regs {
                get {
                    object[] res = new object[_m.Groups.Count];
                    for (int i = 0; i < res.Length; i++) {
                        res[i] = PythonTuple.MakeTuple(start(i), end(i));
                    }

                    return PythonTuple.MakeTuple(res);
                }
            }

            public RE_Pattern re { get; }

            public object lastindex {
                get {
                    //   -1 : initial value of lastindex
                    //    0 : no match found
                    //other : the true lastindex

                    // Match.Groups contains "lower" level matched groups, which has to be removed
                    if (_lastindex == -1) {
                        int i = 1;
                        while (i < _m.Groups.Count) {
                            if (_m.Groups[i].Success) {
                                _lastindex = i;
                                int start = _m.Groups[i].Index;
                                int end = start + _m.Groups[i].Length;
                                i++;

                                // skip any group which fall into the range [start, end], 
                                // no matter match succeed or fail
                                while (i < _m.Groups.Count && (_m.Groups[i].Index < end)) {
                                    i++;
                                }
                            } else {
                                i++;
                            }
                        }

                        if (_lastindex == -1) {
                            _lastindex = 0;
                        }
                    }

                    if (_lastindex == 0) {
                        return null;
                    } else {
                        return _lastindex;
                    }
                }
            }

            public string lastgroup {
                get {
                    if (lastindex == null) return null;

                    // when group was not explicitly named, RegEx assigns the number as name
                    // This is different from C-Python, which returns None in such cases

                    return this.re._re.GroupNameFromNumber((int)lastindex);
                }
            }

            #endregion

            #region Private helper functions

            private void AppendGroup(StringBuilder sb, int index) => sb.Append(_m.Groups[index].Value);

            private int GetGroupIndex(object group) {
                if (!Converter.TryConvertToInt32(group, out int grpIndex)) {
                    grpIndex = re._re.GroupNumberFromName(ValidateString(group, nameof(group)));
                }
                if (grpIndex < 0 || grpIndex >= _m.Groups.Count) {
                    throw PythonOps.IndexError("no such group");
                }
                return grpIndex;
            }

            #endregion
        }

        #endregion

        #region Private helper functions

        private static RE_Pattern GetPattern(CodeContext/*!*/ context, object pattern, int flags, bool compiled = false) {
            if (pattern is RE_Pattern res) {
                return res;
            }

            string strPattern = ValidatePatternAsString(pattern);
            PatternKey key = new PatternKey(strPattern, flags);
            lock (_cachedPatterns) {
                if (_cachedPatterns.TryGetValue(new PatternKey(strPattern, flags), out res)) {
                    if (!compiled || res._re.Options.HasFlag(RegexOptions.Compiled)) {
                        return res;
                    }
                }
                res = new RE_Pattern(context, strPattern, flags, compiled);
                _cachedPatterns[key] = res;
                return res;
            }
        }

        private static IEnumerator MatchIterator(MatchCollection matches, RE_Pattern pattern, string input) {
            for (int i = 0; i < matches.Count; i++) {
                yield return RE_Match.make(matches[i], pattern, input, 0, input.Length);
            }
        }

        private static RegexOptions FlagsToOption(int flags) {
            RegexOptions opts = RegexOptions.None;
            if ((flags & (int)IGNORECASE) != 0) opts |= RegexOptions.IgnoreCase;
            if ((flags & (int)MULTILINE) != 0) opts |= RegexOptions.Multiline;
            if (((flags & (int)LOCALE)) == 0) opts &= (~RegexOptions.CultureInvariant);
            if ((flags & (int)DOTALL) != 0) opts |= RegexOptions.Singleline;
            if ((flags & (int)VERBOSE) != 0) opts |= RegexOptions.IgnorePatternWhitespace;

            return opts;
        }

        private static int OptionToFlags(RegexOptions options) {
            int flags = 0;
            if ((options & RegexOptions.IgnoreCase) != 0) {
                flags |= IGNORECASE;
            }
            if ((options & RegexOptions.Multiline) != 0) {
                flags |= MULTILINE;
            }
            if ((options & RegexOptions.CultureInvariant) == 0) {
                flags |= LOCALE;
            }
            if ((options & RegexOptions.Singleline) != 0) {
                flags |= DOTALL;
            }
            if ((options & RegexOptions.IgnorePatternWhitespace) != 0) {
                flags |= VERBOSE;
            }
            return flags;
        }

        internal class ParsedRegex {
            public ParsedRegex(string pattern) {
                this.UserPattern = pattern;
            }

            public string UserPattern;
            public string Pattern;
            public RegexOptions Options = RegexOptions.CultureInvariant;
        }

        private static readonly char[] _preParsedChars = new[] { '(', '{', '[', ']' };
        private const string _mangledNamedGroup = "___PyRegexNameMangled";

        /// <summary>
        /// Preparses a regular expression text returning a ParsedRegex class
        /// that can be used for further regular expressions.
        /// </summary>
        private static ParsedRegex PreParseRegex(CodeContext/*!*/ context, string pattern) {
            ParsedRegex res = new ParsedRegex(pattern);

            //string newPattern;
            int cur = 0, nameIndex;
            int curGroup = 0;
            bool isCharList = false;
            bool containsNamedGroup = false;

            int groupCount = 0;
            var namedGroups = new Dictionary<string, int>();

            for (; ; ) {
                nameIndex = pattern.IndexOfAny(_preParsedChars, cur);
                if (nameIndex > 0 && pattern[nameIndex - 1] == '\\') {
                    int curIndex = nameIndex - 2;
                    int backslashCount = 1;
                    while (curIndex >= 0 && pattern[curIndex] == '\\') {
                        backslashCount++;
                        curIndex--;
                    }
                    // odd number of back slashes, this is an optional
                    // paren that we should ignore.
                    if ((backslashCount & 0x01) != 0) {
                        cur++;
                        continue;
                    }
                }

                if (nameIndex == -1) break;
                if (nameIndex == pattern.Length - 1) break;

                switch (pattern[nameIndex]) {
                    case '{':
                        if (pattern[++nameIndex] == ',') {
                            // no beginning specified for the n-m quntifier, add the
                            // default 0 value.
                            pattern = pattern.Insert(nameIndex, "0");
                        }
                        break;
                    case '[':
                        nameIndex++;
                        isCharList = true;
                        break;
                    case ']':
                        nameIndex++;
                        isCharList = false;
                        break;
                    case '(':
                        // make sure we're not dealing with [(]
                        if (!isCharList) {
                            groupCount++;
                            switch (pattern[++nameIndex]) {
                                case '?':
                                    // extension syntax
                                    if (nameIndex == pattern.Length - 1) throw PythonExceptions.CreateThrowable(error(context), "unexpected end of regex");
                                    switch (pattern[++nameIndex]) {
                                        case 'P':
                                            //  named regex, .NET doesn't expect the P so we'll remove it;
                                            //  also, once we see a named group i.e. ?P then we need to start artificially 
                                            //  naming all unnamed groups from then on---this is to get around the fact that 
                                            //  the CLR RegEx support orders all the unnamed groups before all the named 
                                            //  groups, even if the named groups are before the unnamed ones in the pattern;
                                            //  the artificial naming preserves the order of the groups and thus the order of
                                            //  the matches
                                            if (nameIndex + 1 < pattern.Length && pattern[nameIndex + 1] == '=') {
                                                // match whatever was previously matched by the named group

                                                // remove the (?P=
                                                pattern = pattern.Remove(nameIndex - 2, 4);
                                                pattern = pattern.Insert(nameIndex - 2, "\\k<");
                                                int tmpIndex = pattern.IndexOf(')', nameIndex);

                                                if (tmpIndex == -1) throw PythonExceptions.CreateThrowable(error(context), "unexpected end of regex");

                                                pattern = pattern.Substring(0, tmpIndex) + ">" + pattern.Substring(tmpIndex + 1);
                                            } else {
                                                containsNamedGroup = true;
                                                // we need to look and see if the named group was already seen and throw an error if it was
                                                if (nameIndex + 1 < pattern.Length && pattern[nameIndex + 1] == '<') {
                                                    int tmpIndex = pattern.IndexOf('>', nameIndex);

                                                    if (tmpIndex == -1) throw PythonExceptions.CreateThrowable(error(context), "unexpected end of regex");

                                                    var namedGroup = pattern.Substring(nameIndex + 2, tmpIndex - (nameIndex + 2));
                                                    if (namedGroups.ContainsKey(namedGroup)) {
                                                        throw PythonExceptions.CreateThrowable(error(context), $"redefinition of group name '{namedGroup}' as group {groupCount}; was group {namedGroups[namedGroup]}");
                                                    }

                                                    namedGroups[namedGroup] = groupCount;
                                                }

                                                pattern = pattern.Remove(nameIndex, 1);
                                            }

                                            break;
                                        case 'i':
                                            res.Options |= RegexOptions.IgnoreCase;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'L':
                                            res.Options &= ~(RegexOptions.CultureInvariant);
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'm':
                                            res.Options |= RegexOptions.Multiline;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 's':
                                            res.Options |= RegexOptions.Singleline;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'u':
                                            // specify unicode; not relevant and not valid under .NET as we're always unicode
                                            // -- so the option needs to be removed
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'x':
                                            res.Options |= RegexOptions.IgnorePatternWhitespace;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case ':': break; // non-capturing
                                        case '=': break; // look ahead assertion
                                        case '<': break; // positive look behind assertion
                                        case '!': break; // negative look ahead assertion
                                        case '#': break; // inline comment
                                        case '(':
                                            // conditional match alternation (?(id/name)yes-pattern|no-pattern)
                                            // move past ?( so we don't preparse the name.
                                            nameIndex++;
                                            break;
                                        default: throw PythonExceptions.CreateThrowable(error(context), "Unrecognized extension " + pattern[nameIndex]);
                                    }
                                    break;
                                default:
                                    // just another group
                                    curGroup++;
                                    if (containsNamedGroup) {
                                        // need to name this unnamed group
                                        pattern = pattern.Insert(nameIndex, "?<" + _mangledNamedGroup + GetRandomString() + ">");
                                    }
                                    break;
                            }
                        } else {
                            nameIndex++;
                        }
                        break;
                }

                cur = nameIndex;
            }

            cur = 0;
            for (; ; ) {
                nameIndex = pattern.IndexOf('\\', cur);

                if (nameIndex == -1 || nameIndex == pattern.Length - 1) break;
                cur = ++nameIndex;
                char curChar = pattern[cur];
                switch (curChar) {
                    case 'x':
                    case 'u':
                    case 'a':
                    case 'b':
                    case 'e':
                    case 'f':
                    case 'k':
                    case 'n':
                    case 'r':
                    case 't':
                    case 'v':
                    case 'c':
                    case 's':
                    case 'W':
                    case 'w':
                    case 'p':
                    case 'P':
                    case 'S':
                    case 'd':
                    case 'D':
                    case 'A':
                    case 'B':
                    case '\\':
                        // known escape sequences, leave escaped.
                        break;
                    case 'Z':
                        // /Z matches "end of string" in Python, replace with /z which is the .NET equivalent
                        pattern = pattern.Remove(cur, 1).Insert(cur, "z");
                        break;
                    default:
                        System.Globalization.UnicodeCategory charClass = CharUnicodeInfo.GetUnicodeCategory(curChar);
                        switch (charClass) {
                            // recognized word characters, always unescape.
                            case System.Globalization.UnicodeCategory.ModifierLetter:
                            case System.Globalization.UnicodeCategory.LowercaseLetter:
                            case System.Globalization.UnicodeCategory.UppercaseLetter:
                            case System.Globalization.UnicodeCategory.TitlecaseLetter:
                            case System.Globalization.UnicodeCategory.OtherLetter:
                            case System.Globalization.UnicodeCategory.LetterNumber:
                            case System.Globalization.UnicodeCategory.OtherNumber:
                            case System.Globalization.UnicodeCategory.ConnectorPunctuation:
                                pattern = pattern.Remove(nameIndex - 1, 1);
                                cur--;
                                break;
                            case System.Globalization.UnicodeCategory.DecimalDigitNumber:
                                //  actually don't want to unescape '\1', '\2' etc. which are references to groups
                                break;
                        }
                        break;
                }
                if (++cur >= pattern.Length) {
                    break;
                }
            }

            res.Pattern = pattern;
            return res;
        }

        private static void RemoveOption(ref string pattern, ref int nameIndex) {
            if (pattern[nameIndex - 1] == '?' && nameIndex < (pattern.Length - 1) && pattern[nameIndex + 1] == ')') {
                pattern = pattern.Remove(nameIndex - 2, 4);
                nameIndex -= 2;
            } else {
                pattern = pattern.Remove(nameIndex, 1);
                nameIndex -= 2;
            }
        }

        private static string GetRandomString() => r.Next(int.MaxValue / 2, int.MaxValue).ToString();

        private static string UnescapeGroups(Match m, string text) {
            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '\\') {
                    StringBuilder sb = new StringBuilder(text, 0, i, text.Length);

                    do {
                        if (text[i] == '\\') {
                            i++;
                            if (i == text.Length) { sb.Append('\\'); break; }

                            switch (text[i]) {
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case '\\': sb.Append('\\'); break;
                                case '\'': sb.Append('\''); break;
                                case 'b': sb.Append('\b'); break;
                                case 'g':
                                    //  \g<#>, \g<name> need to be substituted by the groups they 
                                    //  matched
                                    if (text[i + 1] == '<') {
                                        int anglebrkStart = i + 1;
                                        int anglebrkEnd = text.IndexOf('>', i + 2);
                                        if (anglebrkEnd != -1) {
                                            //  grab the # or 'name' of the group between '< >'
                                            int lengrp = anglebrkEnd - (anglebrkStart + 1);
                                            string grp = text.Substring(anglebrkStart + 1, lengrp);
                                            Group g;

                                            if (StringUtils.TryParseInt32(grp, out int num)) {
                                                g = m.Groups[num];
                                                if (string.IsNullOrEmpty(g.Value)) {
                                                    throw PythonOps.IndexError("unknown group reference");
                                                }
                                                sb.Append(g.Value);
                                            } else {
                                                g = m.Groups[grp];
                                                if (string.IsNullOrEmpty(g.Value)) {
                                                    throw PythonOps.IndexError("unknown group reference");
                                                }
                                                sb.Append(g.Value);
                                            }
                                            i = anglebrkEnd;
                                        }
                                        break;
                                    }
                                    sb.Append('\\');
                                    sb.Append((char)text[i]);
                                    break;
                                default:
                                    if (char.IsDigit(text[i]) && text[i] <= '7') {
                                        int val = 0;
                                        int digitCount = 0;
                                        while (i < text.Length && char.IsDigit(text[i]) && text[i] <= '7') {
                                            digitCount++;
                                            val += val * 8 + (text[i] - '0');
                                            i++;
                                        }
                                        i--;

                                        if (digitCount == 1 && val > 0 && val < m.Groups.Count) {
                                            sb.Append(m.Groups[val].Value);
                                        } else {
                                            sb.Append((char)val);
                                        }
                                    } else {
                                        sb.Append('\\');
                                        sb.Append((char)text[i]);
                                    }
                                    break;
                            }
                        } else {
                            sb.Append(text[i]);
                        }
                    } while (++i < text.Length);
                    return sb.ToString();
                }
            }
            return text;
        }

        private static object ValidatePattern(object pattern) {
            switch (pattern)
            {
                case string s:
                    return s;
                case ExtensibleString es:
                    return es.Value;
                case Bytes bytes:
                    return bytes.ToString();
                case RE_Pattern rep:
                    return rep;
                default:
                    throw PythonOps.TypeError("pattern must be a string or compiled pattern");
            }
        }

        private static string ValidatePatternAsString(object pattern) {
            switch (pattern)
            {
                case string s:
                    return s;
                case ExtensibleString es:
                    return es.Value;
                case Bytes bytes:
                    return bytes.ToString();
                case RE_Pattern rep:
                    return rep._pre.UserPattern;
                default:
                    throw PythonOps.TypeError("pattern must be a string or compiled pattern");
            }
        }

        private static string ValidateString(object str, string param) {
            switch (str)
            {
                case string s:
                    return s;
                case ExtensibleString es:
                    return es.Value;
                case PythonBuffer buf:
                    return buf.ToString();
                case Bytes bytes:
                    return bytes.ToString();
                case ByteArray byteArray:
                    return byteArray.MakeString();
                case ArrayModule.array array:
                    return Bytes.Make(array.ToByteArray()).ToString();
#if FEATURE_MMAP
                case MmapModule.mmap mmapFile:
                    return mmapFile.GetSearchString();
#endif
                default:
                    throw PythonOps.TypeError($"expected string for parameter '{param}' but got '{PythonOps.GetPythonTypeName(str)}'");
            }

        }

        private static PythonType error(CodeContext/*!*/ context) => (PythonType)context.LanguageContext.GetModuleState("reerror");

        private readonly struct PatternKey : IEquatable<PatternKey> {
            public readonly string Pattern;
            public readonly int Flags;

            public PatternKey(string pattern, int flags) {
                Pattern = pattern;
                Flags = flags;
            }

            public override bool Equals(object obj) => obj is PatternKey key && Equals(key);

            public override int GetHashCode() => Pattern.GetHashCode() ^ Flags;

            #region IEquatable<PatternKey> Members

            public bool Equals(PatternKey other) => other.Pattern == Pattern && other.Flags == Flags;

            #endregion
        }

        #endregion
    }
}
