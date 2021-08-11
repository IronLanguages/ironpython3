// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using RegExpMatch = System.Text.RegularExpressions.Match;

[assembly: PythonModule("re", typeof(IronPython.Modules.PythonRegex))]
namespace IronPython.Modules {

    /// <summary>
    /// Python regular expression module.
    /// </summary>
    public static class PythonRegex {
        private static CacheDict<PatternKey, Pattern> _cachedPatterns = new CacheDict<PatternKey, Pattern>(100);

#pragma warning disable IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("reerror", dict, "error", "re");
            var module = context.GetCopyRegModule();
            if (module != null) {
                var pickle = PythonOps.GetBoundAttr(context.SharedContext, module, "pickle");
                PythonOps.CallWithContext(context.SharedContext, pickle, DynamicHelpers.GetPythonTypeFromType(typeof(Pattern)), dict["_pickle"]);
            }
        }
#pragma warning restore IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute

        private static readonly Random r = new Random(DateTime.Now.Millisecond);

        #region CONSTANTS

        [Flags]
        internal enum ReFlags : int {
            TEMPLATE = 0x01,
            IGNORECASE = 0x02,
            LOCALE = 0x04,
            MULTILINE = 0x08,
            DOTALL = 0x10,
            UNICODE = 0x20,
            VERBOSE = 0x40,
            DEBUG = 0x80,
            ASCII = 0x100,
        }

        // short forms
        public const int I = (int)ReFlags.IGNORECASE;
        public const int L = (int)ReFlags.LOCALE;
        public const int M = (int)ReFlags.MULTILINE;
        public const int S = (int)ReFlags.DOTALL;
        public const int U = (int)ReFlags.UNICODE;
        public const int X = (int)ReFlags.VERBOSE;
        public const int A = (int)ReFlags.ASCII;

        // long forms
        public const int IGNORECASE = (int)ReFlags.IGNORECASE;
        public const int LOCALE     = (int)ReFlags.LOCALE;
        public const int MULTILINE  = (int)ReFlags.MULTILINE;
        public const int DOTALL     = (int)ReFlags.DOTALL;
        public const int UNICODE    = (int)ReFlags.UNICODE;
        public const int VERBOSE    = (int)ReFlags.VERBOSE;
        public const int ASCII      = (int)ReFlags.ASCII;

        #endregion

        #region Public API Surface

        public static Pattern compile(CodeContext/*!*/ context, object? pattern, int flags = 0) {
            try {
                return GetPattern(context, pattern, flags, true);
            } catch (ArgumentException e) {
                throw PythonExceptions.CreateThrowable(error(context), e.Message);
            }
        }

        public const string engine = "cli reg ex";

        public static string escape(string? text) {
            if (text == null) throw PythonOps.TypeError("text must not be None");

            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_') {
                    StringBuilder sb = new StringBuilder(text, 0, i, text.Length);

                    do {
                        sb.Append('\\');
                        sb.Append(ch);
                        i++;

                        int last = i;
                        while (i < text.Length) {
                            ch = text[i];
                            if (!char.IsLetterOrDigit(ch) && ch != '_') {
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

        public static PythonList findall(CodeContext/*!*/ context, object? pattern, object? @string, int flags = 0)
            => GetPattern(context, pattern, flags).findall(context, @string);

        public static object finditer(CodeContext/*!*/ context, object? pattern, object? @string, int flags = 0)
            => GetPattern(context, pattern, flags).finditer(context, @string);

        public static Match? match(CodeContext/*!*/ context, object? pattern, object? @string, int flags = 0)
            => GetPattern(context, pattern, flags).match(@string);

        public static Match? fullmatch(CodeContext/*!*/ context, object? pattern, object? @string, int flags = 0)
            => GetPattern(context, pattern, flags).fullmatch(context, @string);

        public static Match? search(CodeContext/*!*/ context, object? pattern, object? @string, int flags = 0)
            => GetPattern(context, pattern, flags).search(@string);

        public static PythonList split(CodeContext/*!*/ context, object? pattern, object? @string, int maxsplit = 0, int flags = 0)
            => GetPattern(context, pattern, flags).split(@string, maxsplit);

        public static object sub(CodeContext/*!*/ context, object? pattern, object? repl, object? @string, int count = 0, int flags = 0)
            => GetPattern(context, pattern, flags).sub(context, repl, @string, count);

        public static object subn(CodeContext/*!*/ context, object? pattern, object? repl, object? @string, int count = 0, int flags = 0)
            => GetPattern(context, pattern, flags).subn(context, repl, @string, count);

        public static void purge() {
            _cachedPatterns = new CacheDict<PatternKey, Pattern>(100);
        }

        #endregion

        #region Public classes

        /// <summary>
        /// Compiled reg-ex pattern
        /// </summary>
        [PythonType]
        public class Pattern : IWeakReferenceable {
            internal readonly Regex _re;
            internal readonly string _prePattern;
            private PythonDictionary? _groups;
            private WeakRefTracker? _weakRefTracker;

            internal Pattern(CodeContext/*!*/ context, object pattern, ReFlags flags = 0, bool compiled = false) {
                _prePattern = PreParseRegex(context, PatternAsString(pattern, ref flags), (flags & ReFlags.VERBOSE) != 0, out ReFlags options);
                flags |= options;
                _re = GenRegex(context, _prePattern, flags, compiled, false);
                this.pattern = pattern;
                this.flags = (int)flags;

                static string PatternAsString(object pattern, ref ReFlags flags) {
                    switch (pattern) {
                        case Bytes bytes:
                            return bytes.MakeString();
                        case string s:
                            flags |= ReFlags.UNICODE;
                            return s;
                        case ExtensibleString es:
                            flags |= ReFlags.UNICODE;
                            return es.Value;
                        default:
                            throw new ArgumentTypeException();
                    }
                }
            }

            private static Regex GenRegex(CodeContext/*!*/ context, string pattern, ReFlags flags, bool compiled, bool fullmatch) {
                try {
                    RegexOptions opts = FlagsToOption(flags);
                    return new Regex(fullmatch ? $"(?:{pattern})\\Z" : pattern, opts | (compiled ? RegexOptions.Compiled : RegexOptions.None));
                } catch (ArgumentException e) {
                    throw PythonExceptions.CreateThrowable(error(context), e.Message);
                }
            }

            private static int FixPosition(string text, int position) {
                if (position <= 0) return 0;
                if (position > text.Length) return text.Length;
                return position;
            }

            public Match? match(object? @string) {
                string input = ValidateString(@string);
                return Match.MakeMatch(_re.Match(input), this, input, 0, input.Length);
            }

            public Match? match(object? @string, int pos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                return Match.MakeMatch(_re.Match(input, pos), this, input, pos, input.Length);
            }

            public Match? match(object? @string, [DefaultParameterValue(0)] int pos, int endpos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return Match.MakeMatch(_re.Match(input.Substring(0, endpos), pos), this, input, pos, endpos);
            }

            private Regex? _re_fullmatch;
            private Regex GetRegexFullMatch(CodeContext /*!*/ context) {
                if (_re_fullmatch == null) {
                    lock (_re) {
                        if (_re_fullmatch == null)
                            _re_fullmatch = GenRegex(context, _prePattern, (ReFlags)flags, _re.Options.HasFlag(RegexOptions.Compiled), true);
                    }
                }

                return _re_fullmatch;
            }

            public Match? fullmatch(CodeContext/*!*/ context, object? @string) {
                string input = ValidateString(@string);
                return Match.MakeFullMatch(GetRegexFullMatch(context).Match(input, 0), this, input, 0, input.Length);
            }

            public Match? fullmatch(CodeContext/*!*/ context, object? @string, int pos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                return Match.MakeFullMatch(GetRegexFullMatch(context).Match(input, pos), this, input, pos, input.Length);
            }

            public Match? fullmatch(CodeContext/*!*/ context, object? @string, [DefaultParameterValue(0)] int pos, int endpos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return Match.MakeFullMatch(GetRegexFullMatch(context).Match(input.Substring(0, endpos), pos), this, input, pos, endpos);
            }

            public Match? search(object? @string) {
                string input = ValidateString(@string);
                return Match.Make(_re.Match(input), this, input);
            }

            public Match? search(object? @string, int pos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                return Match.Make(_re.Match(input, pos), this, input);
            }

            public Match? search(object? @string, [DefaultParameterValue(0)] int pos, int endpos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return Match.Make(_re.Match(input.Substring(0, endpos), pos), this, input);
            }

            public PythonList findall(CodeContext/*!*/ context, object? @string) {
                string input = ValidateString(@string);
                return FixFindAllMatch(FindAllWorker(context, input, 0, input.Length));
            }

            public PythonList findall(CodeContext/*!*/ context, object? @string, int pos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                return FixFindAllMatch(FindAllWorker(context, input, pos, input.Length));
            }

            public PythonList findall(CodeContext/*!*/ context, object? @string, [DefaultParameterValue(0)] int pos, int endpos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return FixFindAllMatch(FindAllWorker(context, input, pos, endpos));
            }

            private PythonList FixFindAllMatch(MatchCollection mc) {
                object[] matches = new object[mc.Count];
                int numgrps = _re.GetGroupNumbers().Length;
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
                        foreach (Group g in mc[i].Groups.Cast<Group>()) {
                            //  here also the CLR gives us a "bonus" match as the first item which is the
                            //  group that was actually matched in the tuple e.g. we get 'x', '', 'x' for
                            //  the first match object...so we'll skip the first item when creating the
                            //  tuple
                            if (k++ != 0) {
                                tpl.Add(ToPatternType(g.Value));
                            }
                        }
                        matches[i] = PythonTuple.MakeTuple(tpl.ToArray());
                    } else if (numgrps == 2) {
                        //  at this point we have exactly one group in the pattern (including the "bonus" one given
                        //  by the CLR
                        //  skip the first match since that contains the entire match and not the group match
                        //  e.g. re.findall(r"(\w+)\s+fish\b", "green fish") will have "green fish" in the 0
                        //  index and "green" as the (\w+) group match
                        matches[i] = ToPatternType(mc[i].Groups[1].Value);
                    } else {
                        matches[i] = ToPatternType(mc[i].Value);
                    }
                }

                return PythonList.FromArrayNoCopy(matches);
            }

            internal MatchCollection FindAllWorker(CodeContext/*!*/ context, string input, int pos, int endpos) {
                return _re.Matches(input.Substring(0, endpos), pos);
            }

            public object finditer(CodeContext/*!*/ context, object? @string) {
                string input = ValidateString(@string);
                return MatchIterator(FindAllWorker(context, input, 0, input.Length), this, input);
            }

            public object finditer(CodeContext/*!*/ context, object? @string, int pos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                return MatchIterator(FindAllWorker(context, input, pos, input.Length), this, input);
            }

            public object finditer(CodeContext/*!*/ context, object? @string, [DefaultParameterValue(0)] int pos, int endpos) {
                string input = ValidateString(@string);
                pos = FixPosition(input, pos);
                endpos = FixPosition(input, endpos);
                return MatchIterator(FindAllWorker(context, input, pos, endpos), this, input);
            }

            public PythonList split(object? @string, int maxsplit = 0) {
                string input = ValidateString(@string);
                PythonList result = new PythonList();
                // fast path for negative maxSplit ( == "make no splits")
                if (maxsplit < 0) {
                    result.AddNoLock(ToPatternType(input));
                } else {
                    // iterate over all matches
                    MatchCollection matches = _re.Matches(input);
                    int lastPos = 0; // is either start of the string, or first position *after* the last match
                    int nSplits = 0; // how many splits have occurred?
                    foreach (RegExpMatch m in matches.Cast<RegExpMatch>()) {
                        if (m.Length > 0) {
                            // add substring from lastPos to beginning of current match
                            result.AddNoLock(ToPatternType(input.Substring(lastPos, m.Index - lastPos)));
                            // if there are subgroups of the match, add their match or None
                            if (m.Groups.Count > 1)
                                for (int i = 1; i < m.Groups.Count; i++) {
                                    result.AddNoLock(GetGroupValue(m.Groups[i]));
                                }
                            // update lastPos, nSplits
                            lastPos = m.Index + m.Length;
                            nSplits++;
                            if (nSplits == maxsplit)
                                break;
                        }
                    }
                    // add tail following last match
                    result.AddNoLock(ToPatternType(input.Substring(lastPos)));
                }
                return result;
            }

            public object sub(CodeContext/*!*/ context, object? repl, object? @string, int count = 0) {
                if (repl == null) throw PythonOps.TypeError("NoneType is not valid repl");
                //  if 'count' is omitted or 0, all occurrences are replaced
                if (count == 0) count = int.MaxValue;

                string? replacement = ValidateReplacement(repl);

                int prevEnd = -1;
                string input = ValidateString(@string);
                return ToPatternType(_re.Replace(
                    input,
                    delegate (RegExpMatch match) {
                        //  from the docs: Empty matches for the pattern are replaced 
                        //  only when not adjacent to a previous match
                        if (string.IsNullOrEmpty(match.Value) && match.Index == prevEnd) {
                            return "";
                        };
                        prevEnd = match.Index + match.Length;

                        if (replacement != null) return UnescapeGroups(match, replacement);
                        return ValidateString(PythonCalls.Call(context, repl, Match.Make(match, this, input)));
                    },
                    count));
            }

            public PythonTuple subn(CodeContext/*!*/ context, object? repl, object? @string, int count = 0) {
                if (repl == null) throw PythonOps.TypeError("NoneType is not valid repl");
                //  if 'count' is omitted or 0, all occurrences are replaced
                if (count == 0) count = int.MaxValue;

                int totalCount = 0;
                string res;
                string? replacement = ValidateReplacement(repl);

                int prevEnd = -1;
                string input = ValidateString(@string);
                res = _re.Replace(
                    input,
                    delegate (RegExpMatch match) {
                        //  from the docs: Empty matches for the pattern are replaced 
                        //  only when not adjacent to a previous match
                        if (string.IsNullOrEmpty(match.Value) && match.Index == prevEnd) {
                            return "";
                        };
                        prevEnd = match.Index + match.Length;

                        totalCount++;
                        if (replacement != null) return UnescapeGroups(match, replacement);

                        return ValidateString(PythonCalls.Call(context, repl, Match.Make(match, this, input)));
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

            public object pattern { get; }

            public override bool Equals(object? obj)
                => obj is Pattern other && other.pattern == pattern && other.flags == flags;

            public override int GetHashCode() => pattern.GetHashCode() ^ flags;

            #region IWeakReferenceable Members

            WeakRefTracker? IWeakReferenceable.GetWeakRef() => _weakRefTracker;

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _weakRefTracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) => ((IWeakReferenceable)this).SetWeakRef(value);

            #endregion

            private string ValidateString(object? @string) {
                string str;
                if (pattern is Bytes) {
                    switch (@string) {
                        case IBufferProtocol bufferProtocol:
                            using (IPythonBuffer buf = bufferProtocol.GetBuffer()) {
                                str = buf.AsReadOnlySpan().MakeString();
                            }
                            break;
                        case IList<byte> b:
                            str = b.MakeString();
                            break;
#if FEATURE_MMAP
                        case MmapModule.MmapDefault mmapFile:
                            str = mmapFile.GetSearchString().MakeString();
                            break;
#endif
                        case string _:
                        case ExtensibleString _:
                            throw PythonOps.TypeError("cannot use a bytes pattern on a string-like object");
                        default:
                            throw PythonOps.TypeError("expected string or bytes-like object");
                    }
                } else if (pattern is string) {
                    switch (@string) {
                        case string s:
                            str = s;
                            break;
                        case ExtensibleString es:
                            str = es;
                            break;
                        case IBufferProtocol _:
                        case IList<byte> _:
#if FEATURE_MMAP
                        case MmapModule.MmapDefault _:
#endif
                            throw PythonOps.TypeError("cannot use a string pattern on a bytes-like object");
                        default:
                            throw PythonOps.TypeError("expected string or bytes-like object");
                    }
                } else {
                    throw PythonOps.TypeError("pattern must be a string or compiled pattern");
                }
                return str;
            }

            private string? ValidateReplacement(object repl) {
                string? str = null;
                if (pattern is Bytes) {
                    switch (repl) {
                        case IBufferProtocol bufferProtocol:
                            using (IPythonBuffer buf = bufferProtocol.GetBuffer()) {
                                str = buf.AsReadOnlySpan().MakeString();
                            }
                            break;
                        case IList<byte> b:
                            str = b.MakeString();
                            break;
                        case string _:
                        case ExtensibleString _:
                            throw PythonOps.TypeError($"expected a bytes-like object, {PythonTypeOps.GetName(repl)} found");
                        default:
                            break;
                    }
                } else if (pattern is string) {
                    switch (repl) {
                        case string s:
                            str = s;
                            break;
                        case ExtensibleString es:
                            str = es;
                            break;
                        case IBufferProtocol _:
                        case IList<byte> _:
                            throw PythonOps.TypeError($"expected str instance, {PythonTypeOps.GetName(repl)} found");
                        default:
                            break;
                    }
                }
                return str;
            }

            internal object ToPatternType(string value)
                => pattern is Bytes ? Bytes.Make(value.MakeByteArray()) : (object)value;

            internal object? GetGroupValue(Group g, object? @default = null)
                => g.Success ? ToPatternType(g.Value) : @default;
        }

        public static PythonTuple _pickle(CodeContext/*!*/ context, [NotNull] Pattern pattern) {
            object scope = Importer.ImportModule(context, new PythonDictionary(), "re", false, 0);
            if (scope is PythonModule module && module.__dict__.TryGetValue("compile", out object compile)) {
                return PythonTuple.MakeTuple(compile, PythonTuple.MakeTuple(pattern.pattern, pattern.flags));
            }
            throw new InvalidOperationException("couldn't find compile method");
        }

        [PythonType]
        public sealed class Match {
            private readonly RegExpMatch _m;
            private int _lastindex = -1;

            #region Internal makers

            internal static Match? Make(RegExpMatch m, Pattern pattern, string input) {
                if (m.Success) return new Match(m, pattern, input, 0, input.Length);
                return null;
            }

            internal static Match? Make(RegExpMatch m, Pattern pattern, string input, int offset, int endpos) {
                if (m.Success) return new Match(m, pattern, input, offset, endpos);
                return null;
            }

            internal static Match? MakeMatch(RegExpMatch m, Pattern pattern, string input, int offset, int endpos) {
                if (m.Success && m.Index == offset) return new Match(m, pattern, input, offset, endpos);
                return null;
            }

            internal static Match? MakeFullMatch(RegExpMatch m, Pattern pattern, string input, int offset, int endpos) {
                if (m.Success && m.Index == offset && m.Length == endpos - offset) return new Match(m, pattern, input, offset, endpos);
                return null;
            }

            #endregion

            #region Private ctors

            private Match(RegExpMatch m, Pattern pattern, string text, int pos, int endpos) {
                _m = m;
                re = pattern;
                @string = text;
                this.pos = pos;
                this.endpos = endpos;
            }

            #endregion

            #region Public API Surface

            public string __repr__(CodeContext context)
                => $"<re.Match object; span=({start()}, {end()}), match={PythonOps.Repr(context, group(0))}>";

            public int end() => _m.Index + _m.Length;

            public int start() => _m.Index;

            public int start(object? group) {
                var g = GetGroup(group);
                return g.Success ? g.Index : -1;
            }

            public int end(object? group) {
                var g = GetGroup(group);
                return g.Success ? g.Index + g.Length : -1;
            }

            public object? group(object? index, [NotNull] params object?[] additional) {
                if (additional.Length == 0) {
                    return group(index);
                }

                object?[] res = new object[additional.Length + 1];
                res[0] = re.GetGroupValue(GetGroup(index));
                for (int i = 1; i < res.Length; i++) {
                    res[i] = re.GetGroupValue(GetGroup(additional[i - 1]));
                }
                return PythonTuple.MakeTuple(res);
            }

            public object? group(object? index)
                => re.GetGroupValue(GetGroup(index));

            public object? group() => group(0);

            public PythonTuple groups() => groups(null);

            public PythonTuple groups(object? @default) {
                object?[] ret = new object[_m.Groups.Count - 1];
                for (int i = 0; i < ret.Length; i++) {
                    ret[i] = re.GetGroupValue(_m.Groups[i + 1], @default);
                }
                return PythonTuple.MakeTuple(ret);
            }

            public string expand(object? template) {
                string strTmp = ValidateString(template);

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

                static string ValidateString(object? str) {
                    switch (str) {
                        case string s:
                            return s;
                        case ExtensibleString es:
                            return es.Value;
                        case Bytes bytes:
                            return bytes.MakeString();
                        case ByteArray byteArray:
                            return byteArray.MakeString();
                        case ArrayModule.array array:
                            return Bytes.Make(array.ToByteArray()).MakeString();
#if FEATURE_MMAP
                        case MmapModule.MmapDefault mmapFile:
                            return mmapFile.GetSearchString().MakeString();
#endif
                        default:
                            throw PythonOps.TypeError($"expected string or bytes-like object");
                    }
                }

                void AppendGroup(StringBuilder sb, int index) => sb.Append(_m.Groups[index].Value);
            }

            private static bool IsGroupNumber(string name) {
                foreach (char c in name) {
                    if (!char.IsNumber(c)) return false;
                }
                return true;
            }

            [return: DictionaryTypeInfo(typeof(string), typeof(object))]
            public PythonDictionary groupdict(object? @default = null) {
                string[] groupNames = this.re._re.GetGroupNames();
                Debug.Assert(groupNames.Length == this._m.Groups.Count);
                PythonDictionary d = new PythonDictionary();
                for (int i = 0; i < groupNames.Length; i++) {
                    if (IsGroupNumber(groupNames[i])) continue; // python doesn't report group numbers
                    d[groupNames[i]] = re.GetGroupValue(_m.Groups[i], @default);
                }
                return d;
            }

            [return: SequenceTypeInfo(typeof(int))]
            public PythonTuple span() => PythonTuple.MakeTuple(start(), end());

            [return: SequenceTypeInfo(typeof(int))]
            public PythonTuple span(object? group) => PythonTuple.MakeTuple(start(group), end(group));

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

            public Pattern re { get; }

            public object? lastindex {
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

            public string? lastgroup {
                get {
                    if (lastindex == null) return null;

                    // when group was not explicitly named, RegEx assigns the number as name
                    // This is different from C-Python, which returns None in such cases

                    return this.re._re.GroupNameFromNumber((int)lastindex);
                }
            }

            #endregion

            #region Private helper functions

            private Group GetGroup(object? group) {
                return _m.Groups[GetGroupIndex(group)];

                int GetGroupIndex(object? group) {
                    int grpIndex;
                    if (!Converter.TryConvertToInt32(group, out grpIndex)) {
                        if (group is string s) {
                            grpIndex = re._re.GroupNumberFromName(s);
                        } else if (group is ExtensibleString es) {
                            grpIndex = re._re.GroupNumberFromName(es);
                        } else {
                            grpIndex = -1;
                        }
                    }
                    if (grpIndex < 0 || grpIndex >= _m.Groups.Count) {
                        throw PythonOps.IndexError("no such group");
                    }
                    return grpIndex;
                }
            }

            #endregion
        }

        #endregion

        #region Private helper functions

        private static Pattern GetPattern(CodeContext/*!*/ context, object? pattern, int flags, bool compiled = false) {
            switch (pattern) {
                case Pattern p:
                    return p;
                case Bytes _:
                case string _:
                case ExtensibleString _:
                    break;
                default:
                    throw PythonOps.TypeError("pattern must be a string or compiled pattern");
            }

            PatternKey key = new PatternKey(pattern.GetType(), pattern, flags);
            lock (_cachedPatterns) {
                if (_cachedPatterns.TryGetValue(key, out Pattern res)) {
                    if (!compiled || res._re.Options.HasFlag(RegexOptions.Compiled)) {
                        return res;
                    }
                }
                res = new Pattern(context, pattern, (ReFlags)flags, compiled);
                _cachedPatterns[key] = res;
                return res;
            }
        }

        private static IEnumerator MatchIterator(MatchCollection matches, Pattern pattern, string input) {
            for (int i = 0; i < matches.Count; i++) {
                yield return Match.Make(matches[i], pattern, input, 0, input.Length);
            }
        }

        private static RegexOptions FlagsToOption(ReFlags flags) {
            RegexOptions opts = RegexOptions.None;
            if ((flags & ReFlags.ASCII) != 0) opts |= RegexOptions.ECMAScript;
            if ((flags & ReFlags.IGNORECASE) != 0) opts |= RegexOptions.IgnoreCase;
            if ((flags & ReFlags.MULTILINE) != 0) opts |= RegexOptions.Multiline;
            if ((flags & ReFlags.LOCALE) == 0) opts &= ~RegexOptions.CultureInvariant;
            if ((flags & ReFlags.DOTALL) != 0) opts |= RegexOptions.Singleline;

            return opts;
        }

        private static readonly char[] _endOfLineChars = new[] { '\r', '\n' };
        private static readonly char[] _preParsedChars = new[] { '(', '{', '[', ']' };
        private const string _mangledNamedGroup = "___PyRegexNameMangled";

        /// <summary>
        /// Preparses a regular expression text returning a ParsedRegex class
        /// that can be used for further regular expressions.
        /// </summary>
        private static string PreParseRegex(CodeContext/*!*/ context, string pattern, bool verbose, out ReFlags options) {
            var userPattern = pattern;
            options = default;
            if (verbose) options |= ReFlags.VERBOSE;

            int cur = 0, nameIndex;
            int curGroup = 0;
            bool isCharList = false;
            bool containsNamedGroup = false;

            int groupCount = 0;
            var namedGroups = new Dictionary<string, int>();

            if (verbose) {
                pattern = ApplyVerbose(pattern);
            }

            static string ApplyVerbose(string pattern) {
                var builder = new StringBuilder();

                bool isCharList = false;
                bool isEscaped = false;

                for (int i = 0; i < pattern.Length; i++) {
                    var c = pattern[i];
                    if (isEscaped) {
                        isEscaped = false;
                    } else {
                        switch (c) {
                            case ' ':
                            case '\t':
                            case '\n':
                            case '\r':
                            case '\f':
                            case '\v':
                                if (!isCharList) continue;
                                break;
                            case '\\':
                                isEscaped = true;
                                break;
                            case '[':
                                isCharList = true;
                                break;
                            case ']':
                                isCharList = false;
                                break;
                            case '#':
                                if (!isCharList) {
                                    // skip to end of line
                                    i = pattern.IndexOfAny(_endOfLineChars, i);
                                    if (i < 0) i = pattern.Length;
                                    continue;
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    builder.Append(c);
                }

                return builder.ToString();
            }

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
                        cur = ++nameIndex;
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
                                        case 'a':
                                            options |= ReFlags.ASCII;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'i':
                                            options |= ReFlags.IGNORECASE;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'L':
                                            options |= ReFlags.LOCALE;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'm':
                                            options |= ReFlags.MULTILINE;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 's':
                                            options |= ReFlags.DOTALL;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'u':
                                            options |= ReFlags.UNICODE;
                                            RemoveOption(ref pattern, ref nameIndex);
                                            break;
                                        case 'x':
                                            if (!verbose) return PreParseRegex(context, userPattern, true, out options);
                                            options |= ReFlags.VERBOSE;
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
                        UnicodeCategory charClass = CharUnicodeInfo.GetUnicodeCategory(curChar);
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

            return pattern;
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

        private static string UnescapeGroups(RegExpMatch m, string text) {
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
                                case 'a': sb.Append('\a'); break;
                                case 'b': sb.Append('\b'); break;
                                case 'f': sb.Append('\f'); break;
                                case 'v': sb.Append('\v'); break;
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

                                            if (int.TryParse(grp, out int num)) {
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

        private static PythonType error(CodeContext/*!*/ context) => (PythonType)context.LanguageContext.GetModuleState("reerror");

        private readonly struct PatternKey : IEquatable<PatternKey> {
            public readonly Type Type;
            public readonly object Pattern;
            public readonly int Flags;

            public PatternKey(Type type, object pattern, int flags) {
                Type = type;
                Pattern = pattern;
                Flags = flags;
            }

            public override bool Equals(object? obj) => obj is PatternKey key && Equals(key);

            public override int GetHashCode() => Type.GetHashCode() ^ Pattern.GetHashCode() ^ Flags;

            #region IEquatable<PatternKey> Members

            public bool Equals(PatternKey other) => other.Type == Type && Equals(other.Pattern, Pattern) && other.Flags == Flags;

            #endregion
        }

        #endregion
    }
}
