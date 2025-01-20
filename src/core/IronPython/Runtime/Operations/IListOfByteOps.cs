// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace IronPython.Runtime.Operations {
    internal static class IListOfByteOps {
        internal static int Compare(this IList<byte> self, IList<byte> other) {
            for (int i = 0; i < self.Count && i < other.Count; i++) {
                if (self[i] != other[i]) {
                    if (self[i] > other[i]) {
                        return 1;
                    } else {
                        return -1;
                    }
                }
            }

            if (self.Count == other.Count) {
                return 0;
            }

            return self.Count > other.Count ? +1 : -1;
        }

        internal static bool EndsWith(this IList<byte> self, IList<byte> suffix) {
            if (self.Count < suffix.Count) {
                return false;
            }

            int offset = self.Count - suffix.Count;
            for (int i = 0; i < suffix.Count; i++) {
                if (suffix[i] != self[i + offset]) {
                    return false;
                }
            }

            return true;
        }

        //  With optional start, test beginning at that position (the char at that index is
        //  included in the test). With optional end, stop comparing at that position (the 
        //  char at that index is not included in the test)
        internal static bool EndsWith(this IList<byte> self, IList<byte> suffix, int start, int end) {
            int len = self.Count;

            if(!PythonOps.TryFixSubsequenceIndices(len, ref start, ref end)) {
                return false;
            }

            if (end - start < suffix.Count) {
                return false;
            }

            for (int i = suffix.Count - 1, j = end - 1; i >= 0; i--, j--) {
                if (suffix[i] != self[j]) {
                    return false;
                }
            }

            return true;
        }

        internal static bool EndsWith(this IList<byte> bytes, PythonTuple suffix) {
            foreach (object? obj in suffix) {
                if (bytes.EndsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool EndsWith(this IList<byte> bytes, PythonTuple suffix, int start, int end) {
            foreach (object? obj in suffix) {
                if (bytes.EndsWith(ByteOps.CoerceBytes(obj), start, end)) {
                    return true;
                }
            }
            return false;
        }

        internal static int IndexOfAny(this IList<byte> str, IList<byte> separators, int i) {
            for (; i < str.Count; i++) {
                for (int j = 0; j < separators.Count; j++) {
                    if (str[i] == separators[j]) {
                        return i;
                    }
                }
            }
            return -1;
        }

        internal static int IndexOf(this IList<byte> bytes, IList<byte> sub, int start = 0) {
            return IndexOf(bytes, sub, start, bytes.Count - start);
        }

        internal static int IndexOf(this IList<byte> self, IList<byte> sub, int start, int count) {
            // same preconditions as for System.String.IndexOf
            Debug.Assert(0 <= start && start <= self.Count);
            Debug.Assert(0 <= count && start + count <= self.Count);

            if (sub.Count == 0) return start;

            byte firstByte = sub[0];
            int end = start + count - (sub.Count - 1);
            for (int i = start; i < end; i++) {
                if (self[i] == firstByte) {
                    bool differ = false;

                    for (int j = 1, ij = i + j; j < sub.Count; j++, ij++) {
                        if (sub[j] != self[ij]) {
                            differ = true;
                            break;
                        }
                    }

                    if (!differ) {
                        return i;
                    }
                }
            }

            return -1;
        }

        internal static bool IsTitle(this IList<byte> bytes) {
            if (bytes.Count == 0) {
                return false;
            }

            bool prevCharCased = false, currCharCased = false, containsUpper = false;
            for (int i = 0; i < bytes.Count; i++) {
                if (bytes[i].IsUpper()) {
                    containsUpper = true;
                    if (prevCharCased)
                        return false;
                    else
                        currCharCased = true;
                } else if (bytes[i].IsLower())
                    if (!prevCharCased)
                        return false;
                    else
                        currCharCased = true;
                else
                    currCharCased = false;
                prevCharCased = currCharCased;
            }

            //  if we've gone through the whole string and haven't encountered any rule 
            //  violations but also haven't seen an Uppercased char, then this is not a 
            //  title e.g. '\n', all whitespace etc.
            return containsUpper;
        }

        internal static bool IsUpper(this IList<byte> bytes) {
            bool foundUpper = false;
            foreach (byte b in bytes) {
                foundUpper = foundUpper || b.IsUpper();
                if (b.IsLower()) {
                    return false;
                }
            }
            return foundUpper;
        }

        internal static List<byte>? Title(this IList<byte> self) {
            if (self.Count == 0) {
                return null;
            }

            List<byte> retchars = new List<byte>(self);
            bool prevCharCased = false;
            bool currCharCased = false;
            int i = 0;
            do {
                if (retchars[i].IsUpper() || retchars[i].IsLower()) {
                    if (!prevCharCased)
                        retchars[i] = retchars[i].ToUpper();
                    else
                        retchars[i] = retchars[i].ToLower();
                    currCharCased = true;
                } else {
                    currCharCased = false;
                }
                i++;
                prevCharCased = currCharCased;
            } while (i < retchars.Count);
            return retchars;
        }

        // NOTE: The start parameter is exclusive, unlike
        // in System.Globalization.CompareInfo.LastIndexOf
        internal static int LastIndexOf(this IList<byte> self, IList<byte> sub, int start, int count) {
            Debug.Assert(0 <= start && start <= self.Count);
            Debug.Assert(0 <= count && count <= start);

            if (sub.Count == 0) return start;

            byte firstByte = sub[sub.Count - 1];
            var end = start - count + sub.Count - 1;
            for (int i = start - 1; i >= end; i--) {
                if (self[i] == firstByte) {
                    bool differ = false;

                    if (sub.Count != 1) {
                        for (int j = sub.Count - 2, selfIndex = 1; j >= 0; j--, selfIndex++) {
                            if (sub[j] != self[i - selfIndex]) {
                                differ = true;
                                break;
                            }
                        }
                    }

                    if (!differ) {
                        return i - sub.Count + 1;
                    }
                }
            }

            return -1;
        }

        internal static List<byte>[] Split(this IList<byte> str, IList<byte>? separator, int maxComponents, StringSplitOptions options) {
            bool keep_empty = (options & StringSplitOptions.RemoveEmptyEntries) != StringSplitOptions.RemoveEmptyEntries;

            if (separator == null) {
                Debug.Assert(!keep_empty);
                return SplitOnWhiteSpace(str, maxComponents);
            }

            List<List<byte>> result = new List<List<byte>>(maxComponents + 1);

            int i = 0;
            int next;
            while (maxComponents > 1 && i < str.Count && (next = str.IndexOf(separator, i)) != -1) {
                if (next > i || keep_empty) {
                    result.Add(Substring(str, i, next - i));
                    maxComponents--;
                }

                i = next + separator.Count;
            }

            if (i < str.Count || keep_empty) {
                result.Add(Substring(str, i));
            }

            return result.ToArray();
        }

        internal static List<byte>[] SplitOnWhiteSpace(this IList<byte> str, int maxComponents) {
            var result = new List<List<byte>>(maxComponents + 1);

            int i = 0;
            int next;
            while (maxComponents > 1 && i < str.Count && (next = IndexOfWhiteSpace(str, i)) != -1) {
                if (next > i) {
                    result.Add(Substring(str, i, next - i));
                    maxComponents--;
                }

                i = next + 1;
            }

            if (i < str.Count) {
                // check if we only have white space remaining
                while (i < str.Count) {
                    if (!str[i].IsWhiteSpace()) {
                        break;
                    }

                    i++;
                }

                if (i < str.Count) {
                    result.Add(Substring(str, i));
                }
            }

            return result.ToArray();
        }

        internal static bool StartsWith(this IList<byte> self, IList<byte> prefix) {
            if (self.Count < prefix.Count) {
                return false;
            }

            for (int i = 0; i < prefix.Count; i++) {
                if (prefix[i] != self[i]) {
                    return false;
                }
            }

            return true;
        }

        internal static bool StartsWith(this IList<byte> self, IList<byte> prefix, int start, int end) {
            int len = self.Count;

            if(!PythonOps.TryFixSubsequenceIndices(len, ref start, ref end)) {
                return false;
            }

            if (end - start < prefix.Count) {
                return false;
            }

            for (int i = 0, j = start; i < prefix.Count; i++, j++) {
                if (prefix[i] != self[j]) {
                    return false;
                }
            }

            return true;
        }

        internal static List<byte> Replace(this IList<byte> bytes, IList<byte> old, IList<byte> @new, ref int count) {
            if (@new == null) {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            }

            if (count < 0) {
                count = bytes.Count + 1;
            }

            if (old.Count == 0) {
                return ReplaceEmpty(bytes, @new, ref count);
            }

            List<byte> ret = new List<byte>(bytes.Count);

            int index;
            int start = 0;
            int actualCnt = 0;

            while (count > 0 && (index = bytes.IndexOf(old, start)) != -1) {
                ret.AddRange(bytes.Substring(start, index - start));
                ret.AddRange(@new);
                actualCnt++;
                start = index + old.Count;
                count--;
            }
            ret.AddRange(bytes.Substring(start));

            count = actualCnt;
            return ret;
        }

        private static List<byte> ReplaceEmpty(this IList<byte> self, IList<byte> @new, ref int count) {
            int max = count > self.Count ? self.Count : count;
            List<byte> ret = new List<byte>(self.Count * (@new.Count + 1));
            int actualCnt = 0;
            for (int i = 0; i < max; i++) {
                ret.AddRange(@new);
                actualCnt++;
                ret.Add(self[i]);
            }
            for (int i = max; i < self.Count; i++) {
                ret.Add(self[i]);
            }
            if (count > max) {
                ret.AddRange(@new);
                actualCnt++;
            }

            count = actualCnt;
            return ret;
        }

        internal static int ReverseFind(this IList<byte> bytes, IList<byte> sub, int start, int end) {
            if (!PythonOps.TryFixSubsequenceIndices(bytes.Count, ref start, ref end)) {
                return -1;
            }
            return bytes.LastIndexOf(sub, end, end - start);
        }

        internal static PythonList RightSplit(this IList<byte> bytes, IList<byte>? sep, int maxsplit, Func<IList<byte>, IList<byte>> ctor) {
            if (sep == null && maxsplit < 0) {
                // in this case RightSplit becomes equivalent of Split
                return SplitInternal(bytes, null, -1, ctor);
            }
            //  rsplit works like split but needs to split from the right;
            //  reverse the original string (and the sep), split, reverse 
            //  the split list and finally reverse each element of the list
            IList<byte> reversed = bytes.ReverseBytes();
            sep = sep?.ReverseBytes();

            PythonList temp, ret;
            temp = ctor(reversed).Split(sep, maxsplit, x => ctor(x));
            temp.reverse();
            int resultlen = temp.__len__();
            if (resultlen != 0) {
                ret = new PythonList(resultlen);
                foreach (IList<byte>? s in temp)
                    ret.AddNoLock(ctor(s!.ReverseBytes()));
            } else {
                ret = temp;
            }
            return ret;
        }

        internal static int IndexOfWhiteSpace(this IList<byte> str, int start) {
            while (start < str.Count && !str[start].IsWhiteSpace()) start++;

            return (start == str.Count) ? -1 : start;
        }


        internal static byte[] ReverseBytes(this IList<byte> s) {
            byte[] rchars = new byte[s.Count];
            for (int i = s.Count - 1, j = 0; i >= 0; i--, j++) {
                rchars[j] = s[i];
            }
            return rchars;
        }

        internal static List<byte> Substring(this IList<byte> bytes, int start) {
            return Substring(bytes, start, bytes.Count - start);
        }

        internal static List<byte> Substring(this IList<byte> bytes, int start, int len) {
            List<byte> substr = new List<byte>();
            for (int i = start; i < start + len; i++) {
                substr.Add(bytes[i]);
            }
            return substr;
        }

        internal static List<byte> Multiply(this IList<byte> self, int count) {
            if (count <= 0) {
                return new List<byte>();
            }
            List<byte> res = new List<byte>(checked(self.Count * count));
            for (int i = 0; i < count; i++) {
                res.AddRange(self);
            }
            return res;
        }

        internal static List<byte> Capitalize(this IList<byte> bytes) {
            List<byte> res = new List<byte>(bytes);
            if (res.Count > 0) {
                res[0] = res[0].ToUpper();

                for (int i = 1; i < res.Count; i++) {
                    res[i] = res[i].ToLower();
                }
            }
            return res;
        }

        internal static List<byte>? TryCenter(this IList<byte> bytes, int width, int fillchar) {
            int spaces = width - bytes.Count;
            if (spaces <= 0) {
                return null;
            }

            byte fill = fillchar.ToByteChecked();

            List<byte> newBytes = new List<byte>();
            if ((width & 1) == 0) {
                for (int i = 0; i < spaces / 2; i++) {
                    newBytes.Add(fill);
                }
                newBytes.AddRange(bytes);
                for (int i = 0; i < (spaces + 1) / 2; i++) {
                    newBytes.Add(fill);
                }
            } else {
                for (int i = 0; i < (spaces + 1) / 2; i++) {
                    newBytes.Add(fill);
                }
                newBytes.AddRange(bytes);
                for (int i = 0; i < spaces / 2; i++) {
                    newBytes.Add(fill);
                }
            }
            return newBytes;
        }

        internal static int CountOf(this IList<byte> bytes, IList<byte> ssub, int start, int end) {
            if (ssub == null) {
                throw PythonOps.TypeError("expected bytes or byte array, got NoneType");
            }

            if (start > bytes.Count) {
                return 0;
            }
            start = PythonOps.FixSliceIndex(start, bytes.Count);
            end = PythonOps.FixSliceIndex(end, bytes.Count);

            if (ssub.Count == 0) {
                return Math.Max((end - start) + 1, 0);
            }

            int count = 0;
            while (true) {
                if (end <= start) break;

                int index = bytes.IndexOf(ssub, start, end - start);
                if (index == -1) break;
                count++;
                start = index + ssub.Count;
            }
            return count;
        }

        internal static List<byte> ExpandTabs(this IList<byte> bytes, int tabsize) {
            List<byte> ret = new List<byte>(bytes.Count * 2);

            int col = 0;
            for (int i = 0; i < bytes.Count; i++) {
                byte ch = bytes[i];
                switch (ch) {
                    case (byte)'\n':
                    case (byte)'\r': col = 0; ret.Add(ch); break;
                    case (byte)'\t':
                        if (tabsize > 0) {
                            int tabs = tabsize - (col % tabsize);
                            int existingSize = ret.Capacity;
                            ret.Capacity = checked(existingSize + tabs);
                            for (int j = 0; j < tabs; j++) {
                                ret.Add((byte)' ');
                            }
                            col = 0;
                        }
                        break;
                    default:
                        col++;
                        ret.Add(ch);
                        break;
                }
            }
            return ret;
        }

        internal static int IndexOfByte(this IList<byte> bytes, byte item, int start, int stop) {
            start = PythonOps.FixSliceIndex(start, bytes.Count);
            stop = PythonOps.FixSliceIndex(stop, bytes.Count);

            for (int i = start; i < Math.Min(stop, bytes.Count); i++) {
                if (bytes[i] == item) {
                    return i;
                }
            }

            return -1;
        }

        internal static string BytesRepr(this IReadOnlyList<byte> bytes) {
            StringBuilder res = new StringBuilder();
            res.Append('b');
            char quote = '\'';
            if (bytes.Any(b => b == '\'') && bytes.All(b => b != '\"')) {
                quote = '\"';
            }
            res.Append(quote);
            for (int i = 0; i < bytes.Count; i++) {
                byte ch = bytes[i];

                switch (ch) {
                    case (byte)'\\': res.Append("\\\\"); break;
                    case (byte)'\t': res.Append("\\t"); break;
                    case (byte)'\n': res.Append("\\n"); break;
                    case (byte)'\r': res.Append("\\r"); break;
                    default:
                        if (ch == quote) {
                            res.Append('\\');
                            res.Append((char)ch);
                        } else if (ch < ' ' || (ch >= 0x7f && ch <= 0xff)) {
                            res.AppendFormat("\\x{0:x2}", ch);
                        } else {
                            res.Append((char)ch);
                        }
                        break;
                }
            }
            res.Append(quote);
            return res.ToString();
        }

        internal static List<byte> ZeroFill(this IList<byte> bytes, int width, int spaces) {
            List<byte> ret = new List<byte>(width);
            if (bytes.Count > 0 && bytes[0].IsSign()) {
                ret.Add(bytes[0]);
                for (int i = 0; i < spaces; i++) {
                    ret.Add((byte)'0');
                }
                for (int i = 1; i < bytes.Count; i++) {
                    ret.Add(bytes[i]);
                }
            } else {
                for (int i = 0; i < spaces; i++) {
                    ret.Add((byte)'0');
                }
                ret.AddRange(bytes);
            }
            return ret;
        }

        internal static List<byte> ToLower(this IList<byte> bytes) {
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                res.Add(bytes[i].ToLower());
            }
            return res;
        }

        internal static List<byte> ToUpper(this IList<byte> bytes) {
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                res.Add(bytes[i].ToUpper());
            }
            return res;
        }

        internal static List<byte> Translate(this IList<byte> bytes, IList<byte>? table, IList<byte>? deletechars)
            => Translate(bytes, table, deletechars, out _);

        internal static List<byte> Translate(this IList<byte> bytes, IList<byte>? table, IList<byte>? deletechars, out bool changed) {
            changed = false;
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                var b = bytes[i];
                if (deletechars == null || !deletechars.Contains(b)) {
                    if (table == null) {
                        res.Add(b);
                    } else {
                        var t = table[b];
                        if (b != t) changed = true;
                        res.Add(t);
                    }
                }
                else {
                    changed = true;
                }
            }
            return res;
        }

        internal static List<byte>? RightStrip(this IList<byte> bytes) {
            int i;
            for (i = bytes.Count - 1; i >= 0; i--) {
                if (!bytes[i].IsWhiteSpace()) {
                    break;
                }
            }

            if (i == bytes.Count - 1) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (int j = 0; j <= i; j++) {
                res.Add(bytes[j]);
            }

            return res;
        }

        internal static List<byte>? RightStrip(this IList<byte> bytes, IList<byte> chars) {
            int i;
            for (i = bytes.Count - 1; i >= 0; i--) {
                if (!chars.Contains(bytes[i])) {
                    break;
                }
            }

            if (i == bytes.Count - 1) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (int j = 0; j <= i; j++) {
                res.Add(bytes[j]);
            }

            return res;
        }

        internal static PythonList SplitLines(this IList<byte> bytes, bool keepends, Func<List<byte>, IList<byte>> ctor) {
            PythonList ret = new PythonList();
            int i, linestart;
            for (i = 0, linestart = 0; i < bytes.Count; i++) {
                if (bytes[i] == '\n' || bytes[i] == '\r') {
                    //  special case of "\r\n" as end of line marker
                    if (i < bytes.Count - 1 && bytes[i] == '\r' && bytes[i + 1] == '\n') {
                        if (keepends)
                            ret.AddNoLock(ctor(bytes.Substring(linestart, i - linestart + 2)));
                        else
                            ret.AddNoLock(ctor(bytes.Substring(linestart, i - linestart)));
                        linestart = i + 2;
                        i++;
                    } else { //'\r', '\n', or unicode new line as end of line marker
                        if (keepends)
                            ret.AddNoLock(ctor(bytes.Substring(linestart, i - linestart + 1)));
                        else
                            ret.AddNoLock(ctor(bytes.Substring(linestart, i - linestart)));
                        linestart = i + 1;
                    }
                }
            }
            //  the last line needs to be accounted for if it is not empty
            if (i - linestart != 0) {
                ret.AddNoLock(ctor(bytes.Substring(linestart, i - linestart)));
            }
            return ret;
        }

        internal static List<byte>? LeftStrip(this IList<byte> bytes) {
            int i;
            for (i = 0; i < bytes.Count; i++) {
                if (!bytes[i].IsWhiteSpace()) {
                    break;
                }
            }

            if (i == 0) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (; i < bytes.Count; i++) {
                res.Add(bytes[i]);
            }
            return res;
        }

        internal static List<byte>? LeftStrip(this IList<byte> bytes, IList<byte> chars) {
            int i;
            for (i = 0; i < bytes.Count; i++) {
                if (!chars.Contains(bytes[i])) {
                    break;
                }
            }

            if (i == 0) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (; i < bytes.Count; i++) {
                res.Add(bytes[i]);
            }
            return res;
        }

        internal static PythonList Split(this IList<byte> bytes, IList<byte>? sep, int maxsplit, Func<List<byte>, IList<byte>> ctor) {
            if (sep == null) {
                if (maxsplit == 0) {
                    // Corner case for CPython compatibility
                    PythonList result = new PythonList(1);
                    result.AddNoLock(ctor(bytes.LeftStrip() ?? bytes as List<byte> ?? new List<byte>(bytes)));
                    return result;
                }

                return SplitInternal(bytes, null, maxsplit, ctor);
            }

            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            } else {
                return SplitInternal(bytes, sep, maxsplit, ctor);
            }
        }

        private static StringSplitOptions GetStringSplitOptions(IList<byte>? seps) {
            return (seps == null) ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
        }

        internal static PythonList SplitInternal(this IList<byte> bytes, IList<byte>? separator, int maxsplit, Func<List<byte>, IList<byte>> ctor) {
            if (bytes.Count == 0) {
                return SplitEmptyString(separator != null, ctor);
            }

            //  If the optional second argument sep is absent or None, the words are separated 
            //  by arbitrary strings of whitespace characters (space, tab, newline, return, formfeed);

            List<byte>[] r = bytes.Split(separator, (maxsplit < 0 || maxsplit > bytes.Count) ? bytes.Count + 1 : maxsplit + 1, GetStringSplitOptions(separator));

            PythonList ret = new PythonList(r.Length);
            foreach (List<byte> s in r) {
                ret.AddNoLock(ctor(s));
            }
            return ret;

            static PythonList SplitEmptyString(bool separators, Func<List<byte>, object> ctor) {
                PythonList ret = new PythonList(1);
                if (separators) {
                    ret.AddNoLock(ctor(new List<byte>(0)));
                }
                return ret;
            }
        }

        internal static List<byte>? Strip(this IList<byte> bytes) {
            int start;
            for (start = 0; start < bytes.Count; start++) {
                if (!bytes[start].IsWhiteSpace()) {
                    break;
                }
            }

            int end;
            for (end = bytes.Count - 1; end >= 0; end--) {
                if (!bytes[end].IsWhiteSpace()) {
                    break;
                }
            }

            if (start == 0 && end == bytes.Count - 1) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (int j = start; j <= end; j++) {
                res.Add(bytes[j]);
            }

            return res;
        }

        internal static List<byte>? Strip(this IList<byte> bytes, IList<byte> chars) {
            int start;
            for (start = 0; start < bytes.Count; start++) {
                if (!chars.Contains(bytes[start])) {
                    break;
                }
            }

            int end;
            for (end = bytes.Count - 1; end >= 0; end--) {
                if (!chars.Contains(bytes[end])) {
                    break;
                }
            }

            if (start == 0 && end == bytes.Count - 1) {
                return null;
            }

            List<byte> res = new List<byte>();
            for (int j = start; j <= end; j++) {
                res.Add(bytes[j]);
            }

            return res;
        }

        internal static List<byte>? Slice(this IList<byte> bytes, Slice? slice) {
            if (slice == null) {
                throw PythonOps.TypeError("indices must be slices or integers");
            }

            int start, stop, step, icnt;
            slice.GetIndicesAndCount(bytes.Count, out start, out stop, out step, out icnt);
            if (step == 1) {
                return stop > start ? bytes.Substring(start, stop - start) : null;
            }

            List<byte> newData;
            if (step > 0) {
                if (start > stop) {
                    return null;
                }

                newData = new List<byte>(icnt);
                for (long i = start; i < stop; i += step) {
                    newData.Add(bytes[(int)i]);
                }
            } else {
                if (start < stop) {
                    return null;
                }

                newData = new List<byte>(icnt);
                for (long i = start; i > stop; i += step) {
                    newData.Add(bytes[(int)i]);
                }
            }

            return newData;
        }

        internal static List<byte> SwapCase(this IList<byte> bytes) {
            List<byte> ret = new List<byte>(bytes);
            for (int i = 0; i < bytes.Count; i++) {
                byte ch = ret[i];
                if (ch.IsUpper()) {
                    ret[i] = ch.ToLower();
                } else if (ch.IsLower()) {
                    ret[i] = ch.ToUpper();
                }
            }
            return ret;
        }

        internal static bool StartsWith(this IList<byte> bytes, PythonTuple prefix) {
            foreach (object? obj in prefix) {
                if (bytes.StartsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool StartsWith(this IList<byte> bytes, PythonTuple prefix, int start, int end) {
            foreach (object? obj in prefix) {
                if (bytes.StartsWith(ByteOps.CoerceBytes(obj), start, end)) {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsWhiteSpace(this IList<byte> bytes) {
            if (bytes.Count == 0) {
                return false;
            }

            foreach (byte b in bytes) {
                if (!b.IsWhiteSpace()) {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsLower(this IList<byte> bytes) {
            bool foundLower = false;
            foreach (byte b in bytes) {
                foundLower = foundLower || b.IsLower();
                if (b.IsUpper()) {
                    return false;
                }
            }
            return foundLower;
        }

        internal static bool IsDigit(this IList<byte> bytes) {
            if (bytes.Count == 0) {
                return false;
            }

            foreach (byte b in bytes) {
                if (!b.IsDigit()) {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsLetter(this IList<byte> bytes) {
            if (bytes.Count == 0) {
                return false;
            }

            foreach (byte b in bytes) {
                if (!b.IsLetter()) {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsAlphaNumeric(this IList<byte> bytes) {
            if (bytes.Count == 0) {
                return false;
            }

            foreach (byte b in bytes) {
                if (!b.IsDigit() && !b.IsLetter()) {
                    return false;
                }
            }
            return true;
        }

        internal static int Find(this IList<byte> bytes, IList<byte> sub, int start, int end) {
            if (!PythonOps.TryFixSubsequenceIndices(bytes.Count, ref start, ref end)) {
                return -1;
            }
            return bytes.IndexOf(sub, start, end - start);
        }

        internal static byte ToByte(this IList<byte> self, string name, int pos) {
            if (self is null || self.Count != 1) {
                throw PythonOps.TypeError("{0} () argument {1} must a byte string of length 1, not {2}", name, pos, PythonOps.GetPythonTypeName(self));
            }

            return self[0];
        }

        internal static List<byte> FromHex(string @string) {
            if (@string == null) {
                throw PythonOps.TypeError("expected str, got NoneType");
            }

            List<byte> res = new List<byte>();

            for (int i = 0; i < @string.Length; i++) {
                char c = @string[i];

                int iVal = 0;

                if (Char.IsDigit(c)) {
                    iVal = (c - '0') * 16;
                } else if (c >= 'A' && c <= 'F') {
                    iVal = (c - 'A' + 10) * 16;
                } else if (c >= 'a' && c <= 'f') {
                    iVal = (c - 'a' + 10) * 16;
                } else if (c == ' ') {
                    continue;
                } else {
                    throw PythonOps.ValueError("non-hexadecimal number found in fromhex() arg at position {0}", i);
                }

                i++;
                if (i == @string.Length) {
                    throw PythonOps.ValueError("non-hexadecimal number found in fromhex() arg at position {0}", i - 1);
                }

                c = @string[i];
                if (Char.IsDigit(c)) {
                    iVal += c - '0';
                } else if (c >= 'A' && c <= 'F') {
                    iVal += c - 'A' + 10;
                } else if (c >= 'a' && c <= 'f') {
                    iVal += c - 'a' + 10;
                } else {
                    throw PythonOps.ValueError("non-hexadecimal number found in fromhex() arg at position {0}", i);
                }
                res.Add((byte)iVal);
            }
            return res;
        }

        #region Conversion and Enumeration

        internal static IEnumerable BytesEnumerable(IList<byte> bytes) {
            return new BytesIterator(bytes);
        }

        internal static IEnumerator<int> BytesEnumerator(IList<byte> bytes) {
            return new BytesIterator(bytes);
        }

        #endregion
    }
}
