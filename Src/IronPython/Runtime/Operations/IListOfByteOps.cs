// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Operations {
    internal static class IListOfByteOps {
        internal static int Compare(this IList<byte>/*!*/ self, IList<byte>/*!*/ other) {
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

        internal static int Compare(this IList<byte>/*!*/ self, string other) {
            for (int i = 0; i < self.Count && i < other.Length; i++) {
                if ((char)self[i] != other[i]) {
                    if ((char)self[i] > other[i]) {
                        return 1;
                    } else {
                        return -1;
                    }
                }
            }

            if (self.Count == other.Length) {
                return 0;
            }

            return self.Count > other.Length ? +1 : -1;
        }

        internal static bool EndsWith(this IList<byte>/*!*/ self, IList<byte>/*!*/ suffix) {
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

        internal static bool EndsWith(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ suffix, int start) {
            int len = bytes.Count;
            if (start > len) return false;
            // map the negative indice to its positive counterpart
            if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            return bytes.Substring(start).EndsWith(suffix);
        }

        //  With optional start, test beginning at that position (the char at that index is
        //  included in the test). With optional end, stop comparing at that position (the 
        //  char at that index is not included in the test)
        internal static bool EndsWith(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ suffix, int start, int end) {
            int len = bytes.Count;
            if (start > len) return false;
            // map the negative indices to their positive counterparts
            else if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            if (end >= len) {
                return bytes.Substring(start).EndsWith(suffix);
            } else if (end < 0) {
                end += len;
                if (end < 0) {
                    return false;
                }
            }
            if (end < start) {
                return false;
            }
            return bytes.Substring(start, end - start).EndsWith(suffix);
        }

        internal static bool EndsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ suffix) {
            foreach (object obj in suffix) {
                if (bytes.EndsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool EndsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ suffix, int start) {
            int len = bytes.Count;
            if (start > len) return false;
            // map the negative indice to its positive counterpart
            if (start < 0) {
                start += len;
                if (start < 0) {
                    start = 0;
                }
            }
            foreach (object obj in suffix) {
                if (bytes.Substring(start).EndsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool EndsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ suffix, int start, int end) {
            int len = bytes.Count;
            if (start > len) return false;
            // map the negative indices to their positive counterparts
            else if (start < 0) {
                start += len;
                if (start < 0) {
                    start = 0;
                }
            }
            if (end >= len) {
                end = len;
            } else if (end < 0) {
                end += len;
                if (end < 0) {
                    return false;
                }
            }
            if (end < start) {
                return false;
            }

            foreach (object obj in suffix) {
                if (bytes.Substring(start, end - start).EndsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool StartsWith(this IList<byte>/*!*/ self, IList<byte>/*!*/ prefix) {
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

        internal static int IndexOfAny(this IList<byte>/*!*/ str, IList<byte>/*!*/ separators, int i) {
            for (; i < str.Count; i++) {
                for (int j = 0; j < separators.Count; j++) {
                    if (str[i] == separators[j]) {
                        return i;
                    }
                }
            }
            return -1;
        }

        internal static int IndexOf(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sub, int start) {
            return IndexOf(bytes, sub, start, bytes.Count - start);
        }

        internal static int IndexOf(this IList<byte>/*!*/ self, IList<byte>/*!*/ ssub, int start, int length) {
            if (ssub == null) {
                throw PythonOps.TypeError("cannot do None in bytes or bytearray");
            } else if (ssub.Count == 0) {
                return 0;
            }

            byte firstByte = ssub[0];
            for (int i = start; i < start + length; i++) {
                if (self[i] == firstByte) {
                    bool differ = false;

                    for (int j = 1; j < ssub.Count; j++) {
                        if (j + i == start + length || ssub[j] != self[i + j]) {
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

        internal static bool IsTitle(this IList<byte>/*!*/ bytes) {
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

        internal static bool IsUpper(this IList<byte>/*!*/ bytes) {
            bool foundUpper = false;
            foreach (byte b in bytes) {
                foundUpper = foundUpper || b.IsUpper();
                if (b.IsLower()) {
                    return false;
                }
            }
            return foundUpper;
        }

        internal static List<byte> Title(this IList<byte>/*!*/ self) {
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

        internal static int LastIndexOf(this IList<byte>/*!*/ self, IList<byte>/*!*/ sub, int start, int length) {
            byte firstByte = sub[sub.Count - 1];            
            for (int i = start - 1; i >= start - length; i--) {
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

        internal static List<byte>/*!*/[]/*!*/ Split(this IList<byte>/*!*/ str, IList<byte>/*!*/ separators, int maxComponents, StringSplitOptions options) {
            ContractUtils.RequiresNotNull(str, nameof(str));
            bool keep_empty = (options & StringSplitOptions.RemoveEmptyEntries) != StringSplitOptions.RemoveEmptyEntries;
            
            if (separators == null) {
                Debug.Assert(!keep_empty);
                return SplitOnWhiteSpace(str, maxComponents);
            }

            List<List<byte>> result = new List<List<byte>>(maxComponents == Int32.MaxValue ? 1 : maxComponents + 1);

            int i = 0;
            int next;
            while (maxComponents > 1 && i < str.Count && (next = IndexOfAny(str, separators, i)) != -1) {
                if (next > i || keep_empty) {
                    result.Add(Substring(str, i, next - i));
                    maxComponents--;
                }

                i = next + separators.Count;
            }

            if (i < str.Count || keep_empty) {
                /*while (i < str.Count) {
                    if (!separators.Contains(str[i])) {
                        break;
                    }

                    i++;
                }*/
                result.Add(Substring(str, i));
            }

            return result.ToArray();
        }

        internal static List<byte>/*!*/[]/*!*/ SplitOnWhiteSpace(this IList<byte>/*!*/ str, int maxComponents) {
            ContractUtils.RequiresNotNull(str, nameof(str));
            

            List<List<byte>> result = new List<List<byte>>(maxComponents == Int32.MaxValue ? 1 : maxComponents + 1);

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

        internal static bool StartsWith(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ prefix, int start, int end) {
            int len = bytes.Count;
            if (start > len) {
                return false;
            } else if (start < 0) {
                // map the negative indices to their positive counterparts
                start += len;
                if (start < 0) {
                    start = 0;
                }
            }
            if (end >= len) {
                return bytes.Substring(start).StartsWith(prefix);
            } else if (end < 0) {
                end += len;
                if (end < 0) {
                    return false;
                }
            }
            if (end < start) {
                return false;
            }

            return bytes.Substring(start, end - start).StartsWith(prefix);
        }

        internal static List<byte>/*!*/ Replace(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ old, IList<byte>/*!*/ @new, int count) {
            if (@new == null) {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            }

            if (count == -1) {
                count = old.Count + 1;
            } 
            
            if (old.Count == 0) {
                return ReplaceEmpty(bytes, @new, count);
            }

            List<byte> ret = new List<byte>(bytes.Count);

            int index;
            int start = 0;

            while (count > 0 && (index = bytes.IndexOf(old, start)) != -1) {
                ret.AddRange(bytes.Substring(start, index - start));
                ret.AddRange(@new);
                start = index + old.Count;
                count--;
            }
            ret.AddRange(bytes.Substring(start));

            return ret;
        }

        private static List<byte>/*!*/ ReplaceEmpty(this IList<byte>/*!*/ self, IList<byte>/*!*/ @new, int count) {
            int max = count > self.Count ? self.Count : count;
            List<byte> ret = new List<byte>(self.Count * (@new.Count + 1));
            for (int i = 0; i < max; i++) {
                ret.AddRange(@new);
                ret.Add(self[i]);
            }
            for (int i = max; i < self.Count; i++) {
                ret.Add(self[i]);
            }
            if (count > max) {
                ret.AddRange(@new);
            }

            return ret;
        }

        internal static int ReverseFind(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sub, int? start, int? end) {
            if (sub == null) {
                throw PythonOps.TypeError("expected string, got NoneType");
            } else if (start > bytes.Count) {
                return -1;
            }

            int iStart = FixStart(bytes, start);
            int iEnd = FixEnd(bytes, end);

            if (iStart > iEnd) {
                // can't possibly match anything, not even an empty string
                return -1;
            } else if (sub.Count == 0) {
                // match at the end
                return iEnd;
            } else if (end == 0) {
                // can't possibly find anything
                return -1;
            }

            return bytes.LastIndexOf(sub, iEnd, iEnd - iStart);
        }

        internal static PythonList/*!*/ RightSplit(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sep, int maxsplit, Func<IList<byte>/*!*/, IList<byte>>/*!*/ ctor) {
            //  rsplit works like split but needs to split from the right;
            //  reverse the original string (and the sep), split, reverse 
            //  the split list and finally reverse each element of the list
            IList<byte> reversed = bytes.ReverseBytes();
            if (sep != null) {
                sep = sep.ReverseBytes();
            }

            PythonList temp = null, ret = null;
            temp = ctor(reversed).Split(sep, maxsplit, x => ctor(x));
            temp.reverse();
            int resultlen = temp.__len__();
            if (resultlen != 0) {
                ret = new PythonList(resultlen);
                foreach (IList<byte> s in temp)
                    ret.AddNoLock(ctor(s.ReverseBytes()));
            } else {
                ret = temp;
            }
            return ret;
        }

        internal static int IndexOfWhiteSpace(this IList<byte>/*!*/ str, int start) {
            while (start < str.Count && !str[start].IsWhiteSpace()) start++;

            return (start == str.Count) ? -1 : start;
        }


        internal static byte[]/*!*/ ReverseBytes(this IList<byte>/*!*/ s) {
            byte[] rchars = new byte[s.Count];
            for (int i = s.Count - 1, j = 0; i >= 0; i--, j++) {
                rchars[j] = s[i];
            }
            return rchars;
        }

        internal static List<byte>/*!*/ Substring(this IList<byte>/*!*/ bytes, int start) {
            return Substring(bytes, start, bytes.Count - start);
        }

        internal static List<byte>/*!*/ Substring(this IList<byte>/*!*/ bytes, int start, int len) {
            List<byte> substr = new List<byte>();
            for (int i = start; i < start + len; i++) {
                substr.Add(bytes[i]);
            }
            return substr;
        }

        internal static List<byte>/*!*/ Multiply(this IList<byte>/*!*/ self, int count) {
            if (count <= 0) {
                return new List<byte>();
            }
            List<byte> res = new List<byte>(checked(self.Count * count));
            for (int i = 0; i < count; i++) {
                res.AddRange(self);
            }
            return res;
        }

        internal static List<byte>/*!*/ Capitalize(this IList<byte>/*!*/ bytes) {
            List<byte> res = new List<byte>(bytes);
            if (res.Count > 0) {
                res[0] = res[0].ToUpper();

                for (int i = 1; i < res.Count; i++) {
                    res[i] = res[i].ToLower();
                }
            }
            return res;
        }

        internal static List<byte> TryCenter(this IList<byte>/*!*/ bytes, int width, int fillchar) {
            int spaces = width - bytes.Count;
            if (spaces <= 0) {
                return null;
            }

            byte fill = fillchar.ToByteChecked();

            List<byte> newBytes = new List<byte>();
            for (int i = 0; i < spaces / 2; i++) {
                newBytes.Add(fill);
            }

            newBytes.AddRange(bytes);
            for (int i = 0; i < (spaces + 1) / 2; i++) {
                newBytes.Add(fill);
            }
            return newBytes;
        }

        internal static int CountOf(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ ssub, int start, int end) {
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

        internal static List<byte>/*!*/ ExpandTabs(this IList<byte>/*!*/ bytes, int tabsize) {            
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

        internal static int IndexOfByte(this IList<byte>/*!*/ bytes, int item, int start, int stop) {
            start = PythonOps.FixSliceIndex(start, bytes.Count);
            stop = PythonOps.FixSliceIndex(stop, bytes.Count);

            for (int i = start; i < Math.Min(stop, bytes.Count); i++) {
                if (bytes[i] == item) {
                    return i;
                }
            }

            throw PythonOps.ValueError("bytearray.index(item): item not in bytearray");
        }

        internal static string/*!*/ BytesRepr(this IList<byte>/*!*/ bytes) {
            StringBuilder res = new StringBuilder();
            res.Append("b'");
            for (int i = 0; i < bytes.Count; i++) {
                byte ch = bytes[i];

                switch (ch) {
                    case (byte)'\\': res.Append("\\\\"); break;
                    case (byte)'\t': res.Append("\\t"); break;
                    case (byte)'\n': res.Append("\\n"); break;
                    case (byte)'\r': res.Append("\\r"); break;
                    case (byte)'\'':
                        res.Append('\\');
                        res.Append('\'');
                        break;
                    default:
                        if (ch < ' ' || (ch >= 0x7f && ch <= 0xff)) {
                            res.AppendFormat("\\x{0:x2}", ch);
                        } else {
                            res.Append((char)ch);
                        }
                        break;
                }
            }
            res.Append("'");
            return res.ToString();
        }

        internal static List<byte>/*!*/ ZeroFill(this IList<byte>/*!*/ bytes, int width, int spaces) {
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

        internal static List<byte>/*!*/ ToLower(this IList<byte>/*!*/ bytes) {
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                res.Add(bytes[i].ToLower());
            }
            return res;
        }

        internal static List<byte>/*!*/ ToUpper(this IList<byte>/*!*/ bytes) {
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                res.Add(bytes[i].ToUpper());
            }
            return res;
        }

        internal static List<byte>/*!*/ Translate(this IList<byte>/*!*/ bytes, IList<byte> table, IList<byte> deletechars) {
            List<byte> res = new List<byte>();
            for (int i = 0; i < bytes.Count; i++) {
                if (deletechars == null || !deletechars.Contains(bytes[i])) {
                    if (table == null) {
                        res.Add(bytes[i]);
                    } else {
                        res.Add(table[bytes[i]]);
                    }
                }
            }
            return res;
        }

        internal static List<byte> RightStrip(this IList<byte>/*!*/ bytes) {
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

        internal static List<byte> RightStrip(this IList<byte>/*!*/ bytes, IList<byte> chars) {
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

        internal static PythonList/*!*/ SplitLines(this IList<byte>/*!*/ bytes, bool keepends, Func<List<byte>/*!*/, object>/*!*/ ctor) {
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

        internal static List<byte> LeftStrip(this IList<byte>/*!*/ bytes) {
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

        internal static List<byte> LeftStrip(this IList<byte>/*!*/ bytes, IList<byte> chars) {
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
        
        internal static PythonList/*!*/ Split(this IList<byte>/*!*/ bytes, IList<byte> sep, int maxsplit, Func<List<byte>/*!*/, object>/*!*/ ctor) {
            Debug.Assert(ctor != null);

            if (sep == null) {
                if (maxsplit == 0) {
                    // Corner case for CPython compatibility
                    PythonList result = PythonOps.MakeEmptyList(1);
                    result.AddNoLock(ctor(bytes.LeftStrip() ?? bytes as List<byte> ?? new List<byte>(bytes)));
                    return result;
                }

                return SplitInternal(bytes, (byte[])null, maxsplit, ctor);
            }

            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            } else if (sep.Count == 1) {
                return SplitInternal(bytes, new byte[] { sep[0] }, maxsplit, ctor);
            } else {
                return SplitInternal(bytes, sep, maxsplit, ctor);
            }
        }

        internal static PythonList/*!*/ SplitInternal(IList<byte>/*!*/ bytes, byte[] seps, int maxsplit, Func<List<byte>/*!*/, object>/*!*/ ctor) {
            Debug.Assert(ctor != null);

            if (bytes.Count == 0) {
                return SplitEmptyString(seps != null, ctor);
            } else {
                List<byte>[] r = null;
                //  If the optional second argument sep is absent or None, the words are separated 
                //  by arbitrary strings of whitespace characters (space, tab, newline, return, formfeed);

                r = bytes.Split(seps, (maxsplit < 0) ? Int32.MaxValue : maxsplit + 1,
                    GetStringSplitOptions(seps));

                PythonList ret = PythonOps.MakeEmptyList(r.Length);
                foreach (List<byte> s in r) {
                    ret.AddNoLock(ctor(s));
                }
                return ret;
            }
        }

        private static StringSplitOptions GetStringSplitOptions(IList<byte> seps) {
            return (seps == null) ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
        }

        internal static PythonList/*!*/ SplitInternal(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ separator, int maxsplit, Func<List<byte>/*!*/, object>/*!*/ ctor) {
            Debug.Assert(ctor != null);

            if (bytes.Count == 0) {
                return SplitEmptyString(separator != null, ctor);
            }

            List<byte>[] r = bytes.Split(separator, (maxsplit < 0) ? Int32.MaxValue : maxsplit + 1, GetStringSplitOptions(separator));

            PythonList ret = PythonOps.MakeEmptyList(r.Length);
            foreach (List<byte> s in r) {
                ret.AddNoLock(ctor(s));
            }
            return ret;
        }

        private static PythonList/*!*/ SplitEmptyString(bool separators, Func<List<byte>/*!*/, object>/*!*/ ctor) {
            Debug.Assert(ctor != null);

            PythonList ret = PythonOps.MakeEmptyList(1);
            if (separators) {
                ret.AddNoLock(ctor(new List<byte>(0)));
            }
            return ret;
        }

        internal static List<byte> Strip(this IList<byte>/*!*/ bytes) {
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

        internal static List<byte> Strip(this IList<byte>/*!*/ bytes, IList<byte> chars) {
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

        internal static List<byte> Slice(this IList<byte>/*!*/ bytes, Slice/*!*/ slice) {
            if (slice == null) {
                throw PythonOps.TypeError("indices must be slices or integers");
            }

            int start, stop, step;
            slice.indices(bytes.Count, out start, out stop, out step);
            if (step == 1) {
                return stop > start ? bytes.Substring(start, stop - start) : null;
            }

            List<byte> newData;
            if (step > 0) {
                if (start > stop) {
                    return null;
                }

                int icnt = (stop - start + step - 1) / step;
                newData = new List<byte>(icnt);
                for (int i = start; i < stop; i += step) {
                    newData.Add(bytes[i]);
                }
            } else {
                if (start < stop) {
                    return null;
                }

                int icnt = (stop - start + step + 1) / step;
                newData = new List<byte>(icnt);
                for (int i = start; i > stop; i += step) {
                    newData.Add(bytes[i]);
                }
            }

            return newData;
        }

        internal static List<byte>/*!*/ SwapCase(this IList<byte>/*!*/ bytes) {
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

        internal static bool StartsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ prefix, int start, int end) {
            int len = bytes.Count;
            if (start > len) return false;
            // map the negative indices to their positive counterparts
            else if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            if (end >= len) end = len;
            else if (end < 0) {
                end += len;
                if (end < 0) return false;
            }
            if (end < start) return false;

            foreach (object obj in prefix) {
                if (bytes.Substring(start, end - start).StartsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool StartsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ prefix, int start) {
            int len = bytes.Count;
            if (start > len) return false;
            if (start < 0) {
                start += len;
                if (start < 0) {
                    start = 0;
                }
            }
            foreach (object obj in prefix) {
                if (bytes.Substring(start).StartsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool StartsWith(this IList<byte>/*!*/ bytes, PythonTuple/*!*/ prefix) {
            foreach (object obj in prefix) {
                if (bytes.StartsWith(ByteOps.CoerceBytes(obj))) {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsWhiteSpace(this IList<byte>/*!*/ bytes) {
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

        internal static bool IsLower(this IList<byte>/*!*/ bytes) {
            bool foundLower = false;
            foreach (byte b in bytes) {
                foundLower = foundLower || b.IsLower();
                if (b.IsUpper()) {
                    return false;
                }
            }
            return foundLower;
        }

        internal static bool IsDigit(this IList<byte>/*!*/ bytes) {
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

        internal static bool IsLetter(this IList<byte>/*!*/ bytes) {
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

        internal static bool IsAlphaNumeric(this IList<byte>/*!*/ bytes) {
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

        internal static int Find(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sub) {
            if (sub == null) {
                throw PythonOps.TypeError("expected byte or byte array, got NoneType");
            }

            return bytes.IndexOf(sub, 0);
        }


        internal static int Find(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sub, int? start) {
            if (sub == null) {
                throw PythonOps.TypeError("expected byte or byte array, got NoneType");
            } else if (start > bytes.Count) {
                return -1;
            }

            
            int iStart;
            if (start != null) {
                iStart = PythonOps.FixSliceIndex(start.Value, bytes.Count);
            } else {
                iStart = 0;
            }

            return bytes.IndexOf(sub, iStart);
        }


        internal static int Find(this IList<byte>/*!*/ bytes, IList<byte>/*!*/ sub, int? start, int? end) {
            if (sub == null) {
                throw PythonOps.TypeError("expected byte or byte array, got NoneType");
            }

            if (start > bytes.Count) {
                return -1;
            }

            int iStart = FixStart(bytes, start);
            int iEnd = FixEnd(bytes, end);

            if (iEnd < iStart) {
                return -1;
            }

            return bytes.IndexOf(sub, iStart, iEnd - iStart);
        }

        private static int FixEnd(IList<byte> bytes, int? end) {
            int iEnd;
            if (end != null) {
                iEnd = PythonOps.FixSliceIndex(end.Value, bytes.Count);
            } else {
                iEnd = bytes.Count;
            }
            return iEnd;
        }

        private static int FixStart(IList<byte> bytes, int? start) {
            return start != null ? PythonOps.FixSliceIndex(start.Value, bytes.Count) : 0;
        }

        internal static byte ToByte(this string/*!*/ self, string name, int pos) {
            Debug.Assert(self != null);

            if (self.Length != 1 || self[0] >= 256) {
                throw PythonOps.TypeError(name + "() argument " + pos + " must be char < 256, not string");
            }

            return (byte)self[0];
        }

        internal static byte ToByte(this IList<byte>/*!*/ self, string/*!*/ name, int pos) {
            Debug.Assert(name != null);

            if (self == null) {
                throw PythonOps.TypeError(name + "() argument " + pos + " must be char < 256, not None");
            } else if (self.Count != 1) {
                throw PythonOps.TypeError(name + "() argument " + pos + " must be char < 256, not bytearray or bytes");
            }

            return self[0];
        }

        internal static List<byte>/*!*/ FromHex(string/*!*/ @string) {
            if(@string == null) {
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

        [PythonType("bytes_iterator")]
        private class PythonBytesEnumerator<T> : IEnumerable, IEnumerator<T> {
            private readonly IList<byte>/*!*/ _bytes;
            private readonly Func<byte, T>/*!*/ _conversion;
            private int _index;

            public PythonBytesEnumerator(IList<byte> bytes, Func<byte, T> conversion) {
                Assert.NotNull(bytes);
                Assert.NotNull(conversion);

                _bytes = bytes;
                _conversion = conversion;
                _index = -1;
            }

            #region IEnumerator<T> Members

            public T Current {
                get {
                    if (_index < 0) {
                        throw PythonOps.SystemError("Enumeration has not started. Call MoveNext.");
                    } else if (_index >= _bytes.Count) {
                        throw PythonOps.SystemError("Enumeration already finished.");
                    }
                    return _conversion(_bytes[_index]);
                }
            }

            #endregion

            #region IDisposable Members

            public void Dispose() { }

            #endregion

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    return ((IEnumerator<T>)this).Current;
                }
            }

            public bool MoveNext() {
                if (_index >= _bytes.Count) {
                    return false;
                }
                _index++;
                return _index != _bytes.Count;
            }

            public void Reset() {
                _index = -1;
            }

            #endregion

            #region IEnumerable Members

            public IEnumerator GetEnumerator() {
                return this;
            }

            #endregion
        }

        internal static IEnumerable BytesEnumerable(IList<byte> bytes) {
            return new PythonBytesEnumerator<Bytes>(bytes, b => Bytes.Make(new byte[] { b }));
        }

        internal static IEnumerable BytesIntEnumerable(IList<byte> bytes) {
            return new PythonBytesEnumerator<int>(bytes, b => (int)b);
        }

        internal static IEnumerator<Bytes> BytesEnumerator(IList<byte> bytes) {
            return new PythonBytesEnumerator<Bytes>(bytes, b => Bytes.Make(new byte[] { b }));
        }

        internal static IEnumerator<int> BytesIntEnumerator(IList<byte> bytes) {
            return new PythonBytesEnumerator<int>(bytes, b => (int)b);
        }

        #endregion
    }
}
