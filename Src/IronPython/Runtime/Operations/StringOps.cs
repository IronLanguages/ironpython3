// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;
using DisallowNullAttribute = System.Diagnostics.CodeAnalysis.DisallowNullAttribute;

namespace IronPython.Runtime.Operations {
    /// <summary>
    /// ExtensibleString is the base class that is used for types the user defines
    /// that derive from string.  It carries along with it the string's value and
    /// our converter recognizes it as a string.
    /// </summary>
    public class ExtensibleString : Extensible<string>, ICodeFormattable, IStructuralEquatable {
        public ExtensibleString() : base(String.Empty) { }
        public ExtensibleString(string self) : base(self) { }

        public override string ToString() {
            return Value;
        }

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return StringOps.Quote(Value);
        }

        #endregion        

        [return: MaybeNotImplemented]
        public object __eq__(object other) {
            if (other is string || other is ExtensibleString) {
                return ScriptingRuntimeHelpers.BooleanToObject(EqualsWorker(other));
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public object __ne__(object other) {
            if (other is string || other is ExtensibleString) {
                return ScriptingRuntimeHelpers.BooleanToObject(!EqualsWorker(other));
            }

            return NotImplementedType.Value;
        }

        #region IStructuralEquatable Members

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
            if (comparer is PythonContext.PythonEqualityComparer) {
                return GetHashCode();
            }

            return ((IStructuralEquatable)PythonTuple.MakeTuple(Value.ToCharArray())).GetHashCode(comparer);
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
            if (comparer is PythonContext.PythonEqualityComparer) {
                return EqualsWorker(other);
            }

            if (other is ExtensibleString es) return EqualsWorker(es.Value, comparer);
            if (other is string os) return EqualsWorker(os, comparer);

            return false;
        }

        private bool EqualsWorker(object other) {
            if (other == null) return false;

            if (other is ExtensibleString es) return Value == es.Value;
            if (other is string os) return Value == os;

            return false;
        }

        private bool EqualsWorker(string/*!*/ other, IEqualityComparer comparer) {
            Debug.Assert(other != null);

            if (Value.Length != other.Length) {
                return false;
            } else if (Value.Length == 0) {
                // 2 empty strings are equal
                return true;
            }

            for (int i = 0; i < Value.Length; i++) {
                if (!comparer.Equals(Value[i], other[i])) {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region ISequence Members

        public virtual object this[int index] {
            get { return ScriptingRuntimeHelpers.CharToString(Value[index]); }
        }

        public object this[Slice slice] {
            get { return StringOps.GetItem(Value, slice); }
        }

        #endregion

        #region IPythonContainer Members

        public virtual int __len__() {
            return Value.Length;
        }

        public virtual bool __contains__(object value) {
            if (value is ExtensibleString es) return Value.Contains(es.Value);
            if (value is string s) return Value.Contains(s);

            throw PythonOps.TypeErrorForBadInstance("expected string, got {0}", value);
        }

        #endregion

    }

    /// <summary>
    /// StringOps is the static class that contains the methods defined on strings, i.e. 'abc'
    /// 
    /// Here we define all of the methods that a Python user would see when doing dir('abc').
    /// If the user is running in a CLS aware context they will also see all of the methods
    /// defined in the CLS System.String type.
    /// </summary>
    public static class StringOps {
        internal static Encoding Latin1Encoding => _latin1 ??= Encoding.GetEncoding(28591, new EncoderExceptionFallback(), new DecoderExceptionFallback()); // ISO-8859-1
        [DisallowNull] private static Encoding _latin1;


        internal static object FastNew(CodeContext/*!*/ context, object x) {
            if (x == null) {
                return "None";
            }
            if (x is string) {
                // check ascii
                return x;
            }

            // we don't invoke PythonOps.StringRepr here because we want to return the 
            // Extensible<string> directly back if that's what we received from __str__.
            object value = PythonContext.InvokeUnaryOperator(context, UnaryOperators.String, x);
            if (value is string || value is Extensible<string>) {
                return value;
            }

            throw PythonOps.TypeError("expected str, got {0} from __str__", DynamicHelpers.GetPythonType(value).Name);
        }

        #region Python Constructors

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.String) {
                return string.Empty;
            } else {
                return cls.CreateInstance(context);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, object @object) {
            if (cls == TypeCache.String) {
                return FastNew(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull]string @object) {
            if (cls == TypeCache.String) {
                return @object;
            } else {
                return cls.CreateInstance(context, @object);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull]ExtensibleString @object) {
            if (cls == TypeCache.String) {
                return FastNew(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, char @object) {
            if (cls == TypeCache.String) {
                return ScriptingRuntimeHelpers.CharToString(@object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull]BigInteger @object) {
            if (cls == TypeCache.String) {
                return @object.ToString();
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull]Extensible<BigInteger> @object) {
            if (cls == TypeCache.String) {
                return FastNew(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, int @object) {
            if (cls == TypeCache.String) {
                return @object.ToString();
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, bool @object) {
            if (cls == TypeCache.String) {
                return @object.ToString();
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, double @object) {
            if (cls == TypeCache.String) {
                return DoubleOps.__str__(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, Extensible<double> @object) {
            if (cls == TypeCache.String) {
                return FastNew(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, float @object) {
            if (cls == TypeCache.String) {
                return SingleOps.__str__(context, @object);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [BytesLike, NotNull]IList<byte> @object, [NotNull]string encoding, [NotNull, DisallowNull]string errors = null) {
            if (cls == TypeCache.String) {
                if (@object is Bytes) return ((Bytes)@object).decode(context, encoding, errors);
                return new Bytes(@object).decode(context, encoding, errors);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.String, @object, encoding, errors));
            }
        }

        #endregion

        #region Python __ methods

        public static bool __contains__(string s, string item) {
            return s.Contains(item);
        }

        public static bool __contains__(string s, char item) {
            return s.IndexOf(item) != -1;
        }

        public static string __format__(CodeContext/*!*/ context, string self, string formatSpec) {
            return ObjectOps.__format__(context, self, formatSpec);
        }

        public static int __len__(string s) {
            return s.Length;
        }

        [SpecialName]
        public static string GetItem(string s, int index) {
            return ScriptingRuntimeHelpers.CharToString(s[PythonOps.FixIndex(index, s.Length)]);
        }

        [SpecialName]
        public static string GetItem(string s, object index) {
            return GetItem(s, Converter.ConvertToIndex(index));
        }

        [SpecialName]
        public static string GetItem(string s, Slice slice) {
            if (slice == null) throw PythonOps.TypeError("string indices must be slices or integers");
            int start, stop, step;
            slice.indices(s.Length, out start, out stop, out step);
            if (step == 1) {
                return stop > start ? s.Substring(start, stop - start) : String.Empty;
            } else {
                int index = 0;
                char[] newData;
                if (step > 0) {
                    if (start > stop) return String.Empty;

                    int icnt = (stop - start + step - 1) / step;
                    newData = new char[icnt];
                    for (int i = start; i < stop; i += step) {
                        newData[index++] = s[i];
                    }
                } else {
                    if (start < stop) return String.Empty;

                    int icnt = (stop - start + step + 1) / step;
                    newData = new char[icnt];
                    for (int i = start; i > stop; i += step) {
                        newData[index++] = s[i];
                    }
                }
                return new string(newData);
            }
        }

        #endregion

        #region Public Python methods

        /// <summary>
        /// Returns a copy of this string converted to uppercase
        /// </summary>
        public static string capitalize(this string self) {
            if (self.Length == 0) return self;
            return Char.ToUpper(self[0], CultureInfo.InvariantCulture) + self.Substring(1).ToLower(CultureInfo.InvariantCulture);
        }

        //  default fillchar (padding char) is a space
        public static string center(this string self, int width) {
            return center(self, width, ' ');
        }

        public static string center(this string self, int width, char fillchar) {
            int spaces = width - self.Length;
            if (spaces <= 0) return self;

            StringBuilder ret = new StringBuilder(width);
            ret.Append(fillchar, spaces / 2);
            ret.Append(self);
            ret.Append(fillchar, (spaces + 1) / 2);
            return ret.ToString();
        }

        public static int count(this string self, string sub) {
            return count(self, sub, 0, self.Length);
        }

        public static int count(this string self, string sub, int start) {
            return count(self, sub, start, self.Length);
        }

        public static int count(this string self, string ssub, int start, int end) {
            if (ssub == null) throw PythonOps.TypeError("expected string for 'sub' argument, got NoneType");

            if (start > self.Length) {
                return 0;
            }

            start = PythonOps.FixSliceIndex(start, self.Length);
            end = PythonOps.FixSliceIndex(end, self.Length);

            if (ssub.Length == 0) {
                return Math.Max((end - start) + 1, 0);
            }

            int count = 0;
            CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
            while (true) {
                if (end <= start) break;
                int index = c.IndexOf(self, ssub, start, end - start, CompareOptions.Ordinal);
                if (index == -1) break;
                count++;
                start = index + ssub.Length;
            }
            return count;
        }

        public static Bytes encode(CodeContext/*!*/ context, [NotNull]string s, [NotNull]string encoding = "utf-8", [NotNull]string errors = "strict") {
            return RawEncode(context, s, encoding, errors);
        }

        public static Bytes encode(CodeContext/*!*/ context, [NotNull]string s, [NotNull]Encoding encoding, [NotNull]string errors = "strict") {
            return DoEncode(context, s, errors, GetEncodingName(encoding, normalize: false), encoding, includePreamble: true);
        }

        private static string CastString(object o) {
            if (o is string res) {
                return res;
            }

            return ((Extensible<string>)o).Value;
        }

        internal static string AsString(object o) {
            if (o is string res) {
                return res;
            }

            if (o is Extensible<string> es) {
                return es.Value;
            }

            return null;
        }

        public static bool endswith(this string self, object suffix) {
            TryStringOrTuple(suffix);
            if (suffix is PythonTuple)
                return endswith(self, (PythonTuple)suffix);
            else
                return endswith(self, CastString(suffix));
        }

        public static bool endswith(this string self, object suffix, int start) {
            TryStringOrTuple(suffix);
            if (suffix is PythonTuple)
                return endswith(self, (PythonTuple)suffix, start);
            else
                return endswith(self, CastString(suffix), start);
        }

        public static bool endswith(this string self, object suffix, int start, int end) {
            TryStringOrTuple(suffix);
            if (suffix is PythonTuple)
                return endswith(self, (PythonTuple)suffix, start, end);
            else
                return endswith(self, CastString(suffix), start, end);
        }

        public static string expandtabs(string self) {
            return expandtabs(self, 8);
        }

        public static string expandtabs(this string self, int tabsize) {
            StringBuilder ret = new StringBuilder(self.Length * 2);
            string v = self;
            int col = 0;
            for (int i = 0; i < v.Length; i++) {
                char ch = v[i];
                switch (ch) {
                    case '\n':
                    case '\r': col = 0; ret.Append(ch); break;
                    case '\t':
                        if (tabsize > 0) {
                            int tabs = tabsize - (col % tabsize);
                            int existingSize = ret.Capacity;
                            ret.Capacity = checked(existingSize + tabs);
                            ret.Append(' ', tabs);
                            col = 0;
                        }
                        break;
                    default:
                        col++;
                        ret.Append(ch);
                        break;
                }
            }
            return ret.ToString();
        }

        public static int find(this string self, string sub) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (sub.Length == 1) return self.IndexOf(sub[0]);

            CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
            return c.IndexOf(self, sub, CompareOptions.Ordinal);
        }

        public static int find(this string self, string sub, int start) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            start = PythonOps.FixSliceIndex(start, self.Length);

            CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
            return c.IndexOf(self, sub, start, CompareOptions.Ordinal);
        }

        public static int find(this string self, string sub, BigInteger start) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            return find(self, sub, (int)start);
        }

        public static int find(this string self, string sub, int start, int end) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            start = PythonOps.FixSliceIndex(start, self.Length);
            end = PythonOps.FixSliceIndex(end, self.Length);
            if (end < start) return -1;

            CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
            return c.IndexOf(self, sub, start, end - start, CompareOptions.Ordinal);
        }

        public static int find(this string self, string sub, BigInteger start, BigInteger end) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            return find(self, sub, (int)start, (int)end);
        }

        public static int find(this string self, string sub, object start, object end = null) {
            return find(self, sub, CheckIndex(start, 0), CheckIndex(end, self.Length));
        }

        public static int index(this string self, string sub) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            return index(self, sub, 0, self.Length);
        }

        public static int index(this string self, string sub, int start) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            return index(self, sub, start, self.Length);
        }

        public static int index(this string self, string sub, int start, int end) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            int ret = find(self, sub, start, end);
            if (ret == -1) throw PythonOps.ValueError("substring {0} not found in {1}", sub, self);
            return ret;
        }

        public static int index(this string self, string sub, object start, object end = null) {
            return index(self, sub, CheckIndex(start, 0), CheckIndex(end, self.Length));
        }

        public static bool isalnum(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            for (int i = v.Length - 1; i >= 0; i--) {
                if (!Char.IsLetterOrDigit(v, i)) return false;
            }
            return true;
        }

        public static bool isalpha(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            for (int i = v.Length - 1; i >= 0; i--) {
                if (!Char.IsLetter(v, i)) return false;
            }
            return true;
        }

        public static bool isdigit(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            for (int i = v.Length - 1; i >= 0; i--) {
                // CPython considers the circled digits to be digits
                if (!Char.IsDigit(v, i) && (v[i] < '\u2460' || v[i] > '\u2468')) return false;
            }
            return true;
        }

        // non-ASCII characters are valid under Python 3 (PEP 3131)
        // see https://docs.python.org/3/reference/lexical_analysis.html#identifiers for the list of valid identifiers
        public static bool isidentifier(this string self) {
            if (self.Length == 0) return false;
            char c = self[0];
            if (!char.IsLetter(c) && c != '_') return false;
            for (int i = 1; i < self.Length; i++) {
                c = self[i];
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        internal static bool IsPrintable(char c) {
            // fast path for latin characters
            if (0x1f < c && c < 0x7f) return true;
            if (c <= 0xa0 || c == 0xad) return false;
            if (c <= 0xff) return true;

            switch (CharUnicodeInfo.GetUnicodeCategory(c)) {
                case UnicodeCategory.Control: // Cc
                case UnicodeCategory.Format: // Cf
                case UnicodeCategory.Surrogate: // Cs
                case UnicodeCategory.OtherNotAssigned: // Cn
                case UnicodeCategory.LineSeparator: // Zl
                case UnicodeCategory.ParagraphSeparator: // Zp
                case UnicodeCategory.SpaceSeparator: // Zs
                    return false;
                default:
                    return true;
            }
        }

        public static bool isprintable(this string self) {
            foreach (char c in self) {
                if (!IsPrintable(c)) return false;
            }
            return true;
        }

        public static bool isspace(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            for (int i = v.Length - 1; i >= 0; i--) {
                if (!Char.IsWhiteSpace(v, i)) return false;
            }
            return true;
        }

        public static bool isdecimal(this string self) {
            return isnumeric(self);
        }

        public static bool isnumeric(this string self) {
            if (String.IsNullOrEmpty(self)) return false;

            foreach (char c in self) {
                if (!Char.IsDigit(c)) return false;
            }
            return true;
        }

        public static bool islower(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            bool hasLower = false;
            for (int i = v.Length - 1; i >= 0; i--) {
                if (!hasLower && Char.IsLower(v, i)) hasLower = true;
                if (Char.IsUpper(v, i)) return false;
            }
            return hasLower;
        }

        public static bool isupper(this string self) {
            if (self.Length == 0) return false;
            string v = self;
            bool hasUpper = false;
            for (int i = v.Length - 1; i >= 0; i--) {
                if (!hasUpper && Char.IsUpper(v, i)) hasUpper = true;
                if (Char.IsLower(v, i)) return false;
            }
            return hasUpper;
        }

        /// <summary>
        /// return true if self is a titlecased string and there is at least one
        /// character in self; also, uppercase characters may only follow uncased
        /// characters (e.g. whitespace) and lowercase characters only cased ones.
        /// return false otherwise.
        /// </summary>
        public static bool istitle(this string self) {
            if (self == null || self.Length == 0) return false;

            string v = self;
            bool prevCharCased = false, currCharCased = false, containsUpper = false;
            for (int i = 0; i < v.Length; i++) {
                if (Char.IsUpper(v, i) || Char.GetUnicodeCategory(v, i) == UnicodeCategory.TitlecaseLetter) {
                    containsUpper = true;
                    if (prevCharCased)
                        return false;
                    else
                        currCharCased = true;
                } else if (Char.IsLower(v, i))
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

        /// <summary>
        /// Return a string which is the concatenation of the strings 
        /// in the sequence seq. The separator between elements is the 
        /// string providing this method
        /// </summary>
        public static string join(this string self, object sequence) {
            IEnumerator seq = PythonOps.GetEnumerator(sequence);
            if (!seq.MoveNext()) return "";

            // check if we have just a sequence of just one value - if so just
            // return that value.
            object curVal = seq.Current;
            if (!seq.MoveNext()) return Converter.ConvertToString(curVal);

            StringBuilder ret = new StringBuilder();
            AppendJoin(curVal, 0, ret);

            int index = 1;
            do {
                ret.Append(self);

                AppendJoin(seq.Current, index, ret);

                index++;
            } while (seq.MoveNext());

            return ret.ToString();
        }

        public static string join(this string/*!*/ self, [NotNull]PythonList/*!*/ sequence) {
            if (sequence.__len__() == 0) return String.Empty;

            lock (sequence) {
                if (sequence.__len__() == 1) {
                    return Converter.ConvertToString(sequence[0]);
                }

                StringBuilder ret = new StringBuilder();

                AppendJoin(sequence._data[0], 0, ret);
                for (int i = 1; i < sequence._size; i++) {
                    if (!String.IsNullOrEmpty(self)) {
                        ret.Append(self);
                    }
                    AppendJoin(sequence._data[i], i, ret);
                }

                return ret.ToString();
            }
        }

        public static string ljust(this string self, int width) {
            return ljust(self, width, ' ');
        }

        public static string ljust(this string self, int width, char fillchar) {
            if (width < 0) return self;
            int spaces = width - self.Length;
            if (spaces <= 0) return self;

            StringBuilder ret = new StringBuilder(width);
            ret.Append(self);
            ret.Append(fillchar, spaces);
            return ret.ToString();
        }

        // required for better match with cpython upper/lower
        private static CultureInfo CasingCultureInfo = new CultureInfo("en");

        public static string lower(this string self) {
            return self.ToLower(CasingCultureInfo);
        }

        internal static string ToLowerAsciiTriggered(this string self) {
            for (int i = 0; i < self.Length; i++) {
                if (self[i] >= 'A' && self[i] <= 'Z') {
                    return self.ToLower(CultureInfo.InvariantCulture);
                }
            }
            return self;
        }

        public static string lstrip(this string self) {
            return self.TrimStart();
        }

        public static string lstrip(this string self, string chars) {
            if (chars == null) return lstrip(self);
            return self.TrimStart(chars.ToCharArray());
        }

        [StaticExtensionMethod]
        public static PythonDictionary maketrans([NotNull]string from, [NotNull]string to) {
            if (from.Length != to.Length) throw PythonOps.ValueError("maketrans arguments must have same length");

            var res = new PythonDictionary();
            for (var i = 0; i < from.Length; i++) {
                res[(int)from[i]] = (int)to[i];
            }
            return res;
        }

        [return: SequenceTypeInfo(typeof(string))]
        public static PythonTuple partition(this string self, string sep) {
            if (sep == null)
                throw PythonOps.TypeError("expected string, got NoneType");
            if (sep.Length == 0)
                throw PythonOps.ValueError("empty separator");

            object[] obj = new object[3] { "", "", "" };

            if (self.Length != 0) {
                int index = find(self, sep);
                if (index == -1) {
                    obj[0] = self;
                } else {
                    obj[0] = self.Substring(0, index);
                    obj[1] = sep;
                    obj[2] = self.Substring(index + sep.Length, self.Length - index - sep.Length);
                }
            }
            return new PythonTuple(obj);
        }

        public static string replace(this string self, string old, string @new,
            int count = -1) {

            if (old == null) {
                throw PythonOps.TypeError("expected a character buffer object"); // cpython message
            }
            if (old.Length == 0) return ReplaceEmpty(self, @new, count);

            string v = self;
            int replacements = StringOps.count(v, old);
            replacements = (count < 0 || count > replacements) ? replacements : count;
            int newLength = v.Length;
            newLength -= replacements * old.Length;
            newLength = checked(newLength + replacements * @new.Length);
            StringBuilder ret = new StringBuilder(newLength);

            int index;
            int start = 0;
            while (count != 0 && (index = v.IndexOf(old, start, StringComparison.Ordinal)) != -1) {
                ret.Append(v, start, index - start);
                ret.Append(@new);
                start = index + old.Length;
                count--;
            }
            ret.Append(v.Substring(start));

            return ret.ToString();
        }

        public static int rfind(this string self, string sub) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            return rfind(self, sub, 0, self.Length);
        }

        public static int rfind(this string self, string sub, int start) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            return rfind(self, sub, start, self.Length);
        }

        public static int rfind(this string self, string sub, BigInteger start) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            return rfind(self, sub, (int)start, self.Length);
        }

        public static int rfind(this string self, string sub, int start, int end) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;

            start = PythonOps.FixSliceIndex(start, self.Length);
            end = PythonOps.FixSliceIndex(end, self.Length);

            if (start > end) return -1;     // can't possibly match anything, not even an empty string
            if (sub.Length == 0) return end;    // match at the end
            if (end == 0) return -1;    // can't possibly find anything

            CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
            return c.LastIndexOf(self, sub, end - 1, end - start, CompareOptions.Ordinal);
        }

        public static int rfind(this string self, string sub, BigInteger start, BigInteger end) {
            if (sub == null) throw PythonOps.TypeError("expected string, got NoneType");
            if (start > self.Length) return -1;
            return rfind(self, sub, (int)start, (int)end);
        }

        public static int rfind(this string self, string sub, object start, object end = null) {
            return rfind(self, sub, CheckIndex(start, 0), CheckIndex(end, self.Length));
        }

        public static int rindex(this string self, string sub) {
            return rindex(self, sub, 0, self.Length);
        }

        public static int rindex(this string self, string sub, int start) {
            return rindex(self, sub, start, self.Length);
        }

        public static int rindex(this string self, string sub, int start, int end) {
            int ret = rfind(self, sub, start, end);
            if (ret == -1) throw PythonOps.ValueError("substring {0} not found in {1}", sub, self);
            return ret;
        }

        public static int rindex(this string self, string sub, object start, object end = null) {
            return rindex(self, sub, CheckIndex(start, 0), CheckIndex(end, self.Length));
        }

        public static string rjust(this string self, int width) {
            return rjust(self, width, ' ');
        }

        public static string rjust(this string self, int width, char fillchar) {
            int spaces = width - self.Length;
            if (spaces <= 0) return self;

            StringBuilder ret = new StringBuilder(width);
            ret.Append(fillchar, spaces);
            ret.Append(self);
            return ret.ToString();
        }

        [return: SequenceTypeInfo(typeof(string))]
        public static PythonTuple rpartition(this string self, string sep) {
            if (sep == null)
                throw PythonOps.TypeError("expected string, got NoneType");
            if (sep.Length == 0)
                throw PythonOps.ValueError("empty separator");

            object[] obj = new object[3] { "", "", "" };
            if (self.Length != 0) {
                int index = rfind(self, sep);
                if (index == -1) {
                    obj[2] = self;
                } else {
                    obj[0] = self.Substring(0, index);
                    obj[1] = sep;
                    obj[2] = self.Substring(index + sep.Length, self.Length - index - sep.Length);
                }
            }
            return new PythonTuple(obj);
        }

        //  when no maxsplit arg is given then just use split
        public static PythonList rsplit(this string self) {
            return SplitInternal(self, (char[])null, -1);
        }

        public static PythonList rsplit(this string self, string sep) {
            return rsplit(self, sep, -1);
        }

        public static PythonList rsplit(this string self, string sep, int maxsplit) {
            //  rsplit works like split but needs to split from the right;
            //  reverse the original string (and the sep), split, reverse 
            //  the split list and finally reverse each element of the list
            string reversed = Reverse(self);
            if (sep != null) sep = Reverse(sep);
            PythonList temp = null, ret = null;
            temp = split(reversed, sep, maxsplit);
            temp.reverse();
            int resultlen = temp.__len__();
            if (resultlen != 0) {
                ret = new PythonList(resultlen);
                foreach (string s in temp)
                    ret.AddNoLock(Reverse(s));
            } else {
                ret = temp;
            }
            return ret;
        }

        public static string rstrip(this string self) {
            return self.TrimEnd();
        }

        public static string rstrip(this string self, string chars) {
            if (chars == null) return rstrip(self);
            return self.TrimEnd(chars.ToCharArray());
        }

        public static PythonList split(this string self) {
            return SplitInternal(self, (char[])null, -1);
        }

        public static PythonList split(this string self, string sep) {
            return split(self, sep, -1);
        }

        public static PythonList split(this string self, string sep, int maxsplit) {
            if (sep == null) {
                if (maxsplit == 0) {
                    // Corner case for CPython compatibility
                    PythonList result = PythonOps.MakeEmptyList(1);
                    result.AddNoLock(self.TrimStart());
                    return result;

                } else {
                    return SplitInternal(self, (char[])null, maxsplit);
                }
            }

            if (sep.Length == 0) {
                throw PythonOps.ValueError("empty separator");
            } else if (sep.Length == 1) {
                return SplitInternal(self, new char[] { sep[0] }, maxsplit);
            } else {
                return SplitInternal(self, sep, maxsplit);
            }
        }

        public static PythonList splitlines(this string self) {
            return splitlines(self, false);
        }

        public static PythonList splitlines(this string self, bool keepends) {
            PythonList ret = new PythonList();
            int i, linestart;
            for (i = 0, linestart = 0; i < self.Length; i++) {
                if (self[i] == '\n' || self[i] == '\r' || self[i] == '\x2028') {
                    //  special case of "\r\n" as end of line marker
                    if (i < self.Length - 1 && self[i] == '\r' && self[i + 1] == '\n') {
                        if (keepends)
                            ret.AddNoLock(self.Substring(linestart, i - linestart + 2));
                        else
                            ret.AddNoLock(self.Substring(linestart, i - linestart));
                        linestart = i + 2;
                        i++;
                    } else { //'\r', '\n', or unicode new line as end of line marker
                        if (keepends)
                            ret.AddNoLock(self.Substring(linestart, i - linestart + 1));
                        else
                            ret.AddNoLock(self.Substring(linestart, i - linestart));
                        linestart = i + 1;
                    }
                }
            }
            //  the last line needs to be accounted for if it is not empty
            if (i - linestart != 0)
                ret.AddNoLock(self.Substring(linestart, i - linestart));
            return ret;
        }
        public static bool startswith(this string self, object prefix) {
            TryStringOrTuple(prefix);
            if (prefix is PythonTuple)
                return startswith(self, (PythonTuple)prefix);
            else
                return startswith(self, CastString(prefix));
        }

        public static bool startswith(this string self, object prefix, int start) {
            TryStringOrTuple(prefix);
            if (prefix is PythonTuple)
                return startswith(self, (PythonTuple)prefix, start);
            else
                return startswith(self, CastString(prefix), start);
        }

        public static bool startswith(this string self, object prefix, int start, int end) {
            TryStringOrTuple(prefix);
            if (prefix is PythonTuple)
                return startswith(self, (PythonTuple)prefix, start, end);
            else
                return startswith(self, CastString(prefix), start, end);
        }

        public static string strip(this string self) {
            return self.Trim();
        }

        public static string strip(this string self, string chars) {
            if (chars == null) return strip(self);
            return self.Trim(chars.ToCharArray());
        }

        public static string swapcase(this string self) {
            StringBuilder ret = new StringBuilder(self);
            for (int i = 0; i < ret.Length; i++) {
                char ch = ret[i];
                if (Char.IsUpper(ch)) ret[i] = Char.ToLower(ch, CultureInfo.InvariantCulture);
                else if (Char.IsLower(ch)) ret[i] = Char.ToUpper(ch, CultureInfo.InvariantCulture);
            }
            return ret.ToString();
        }

        public static string title(this string self) {
            if (self == null || self.Length == 0) return self;

            char[] retchars = self.ToCharArray();
            bool prevCharCased = false;
            bool currCharCased = false;
            int i = 0;
            do {
                if (Char.IsUpper(retchars[i]) || Char.IsLower(retchars[i])) {
                    if (!prevCharCased)
                        retchars[i] = Char.ToUpper(retchars[i], CultureInfo.InvariantCulture);
                    else
                        retchars[i] = Char.ToLower(retchars[i], CultureInfo.InvariantCulture);
                    currCharCased = true;
                } else {
                    currCharCased = false;
                }
                i++;
                prevCharCased = currCharCased;
            }
            while (i < retchars.Length);
            return new string(retchars);
        }

        //translate on a unicode string differs from that on an ascii
        //for unicode, the table argument is actually a dictionary with
        //character ordinals as keys and the replacement strings as values
        public static string translate(this string self, [NotNull]PythonDictionary table) {
            if (table == null || self.Length == 0) {
                return self;
            }

            StringBuilder ret = new StringBuilder();
            for (int i = 0, idx = 0; i < self.Length; i++) {
                idx = (int)self[i];
                if (table.__contains__(idx)) {
                    var mapped = table[idx];
                    if (mapped == null) {
                        continue;
                    }
                    if (mapped is int) {
                        var mappedInt = (int)mapped;
                        if (mappedInt > 0xFFFF) {
                            throw PythonOps.TypeError("character mapping must be in range(0x%lx)");
                        }
                        ret.Append((char)(int)mapped);
                    } else if (mapped is String) {
                        ret.Append(mapped);
                    } else {
                        throw PythonOps.TypeError("character mapping must return integer, None or unicode");
                    }
                } else {
                    ret.Append(self[i]);
                }
            }
            return ret.ToString();
        }

        public static string translate(this string self, string table) {
            if (table != null && table.Length != 256) {
                throw PythonOps.ValueError("translation table must be 256 characters long");
            } else if (self.Length == 0) {
                return self;
            }

            // List<char> is about 2/3rds as expensive as StringBuilder appending individual 
            // char's so we use that instead of a StringBuilder
            List<char> res = new List<char>();
            for (int i = 0; i < self.Length; i++) {
                if (table != null) {
                    int idx = (int)self[i];
                    if (idx >= 0 && idx < 256) {
                        res.Add(table[idx]);
                    }
                } else {
                    res.Add(self[i]);
                }
            }
            return new String(res.ToArray());
        }

        public static string upper(this string self) {
            return self.ToUpper(CasingCultureInfo);
        }

        public static string zfill(this string self, int width) {
            int spaces = width - self.Length;
            if (spaces <= 0) return self;

            StringBuilder ret = new StringBuilder(width);
            if (self.Length > 0 && IsSign(self[0])) {
                ret.Append(self[0]);
                ret.Append('0', spaces);
                ret.Append(self.Substring(1));
            } else {
                ret.Append('0', spaces);
                ret.Append(self);
            }
            return ret.ToString();
        }

        /// <summary>
        /// Replaces each replacement field in the string with the provided arguments.
        /// 
        /// replacement_field =  "{" field_name ["!" conversion] [":" format_spec] "}"
        /// field_name        =  (identifier | integer) ("." identifier | "[" element_index "]")*
        /// 
        /// format_spec: [[fill]align][sign][#][0][width][,][.precision][type]
        /// 
        /// Conversion can be 'r' for repr or 's' for string.
        /// </summary>
        public static string/*!*/ format(CodeContext/*!*/ context, string format_string, [NotNull]params object[] args) {
            return NewStringFormatter.FormatString(
                context.LanguageContext,
                format_string,
                PythonTuple.MakeTuple(args),
                new PythonDictionary()
            );
        }

        /// <summary>
        /// Replaces each replacement field in the string with the provided arguments.
        /// 
        /// replacement_field =  "{" field_name ["!" conversion] [":" format_spec] "}"
        /// field_name        =  (identifier | integer) ("." identifier | "[" element_index "]")*
        /// 
        /// format_spec: [[fill]align][sign][#][0][width][.precision][type]
        /// 
        /// Conversion can be 'r' for repr or 's' for string.
        /// </summary>
        public static string/*!*/ format(CodeContext/*!*/ context, string format_string\u00F8, [ParamDictionary]IDictionary<object, object> kwargs\u00F8, params object[] args\u00F8) {
            return NewStringFormatter.FormatString(
                context.LanguageContext,
                format_string\u00F8,
                PythonTuple.MakeTuple(args\u00F8),
                kwargs\u00F8
            );
        }


        public static IEnumerable<PythonTuple>/*!*/ _formatter_parser(this string/*!*/ self) {
            return NewStringFormatter.GetFormatInfo(self);
        }

        public static PythonTuple/*!*/ _formatter_field_name_split(this string/*!*/ self) {
            return NewStringFormatter.GetFieldNameInfo(self);
        }

        #endregion

        #region operators
        [SpecialName]
        public static string Add([NotNull]string self, [NotNull]string other) {
            return self + other;
        }

        [SpecialName]
        public static string Add([NotNull]string self, char other) {
            return self + other;
        }

        [SpecialName]
        public static string Add(char self, [NotNull]string other) {
            return self + other;
        }

        [SpecialName]
        public static string Mod(CodeContext/*!*/ context, string self, object other) {
            return new StringFormatter(context, self, other).Format();
        }

        [SpecialName]
        [return: MaybeNotImplemented]
        public static object Mod(CodeContext/*!*/ context, object other, string self) {
            if (other is string str) {
                return new StringFormatter(context, str, self).Format();
            }

            if (other is Extensible<string> es) {
                return new StringFormatter(context, es.Value, self).Format();
            }

            return NotImplementedType.Value;
        }

        [SpecialName]
        public static string Multiply(string s, int count) {
            if (count <= 0) return String.Empty;
            if (count == 1) return s;

            long size = (long)s.Length * (long)count;
            if (size > Int32.MaxValue) throw PythonOps.OverflowError("repeated string is too long");

            int sz = s.Length;
            if (sz == 1) return new string(s[0], count);

            StringBuilder ret = new StringBuilder(sz * count);
            ret.Insert(0, s, count);
            // the above code is MUCH faster than the simple loop
            //for (int i=0; i < count; i++) ret.Append(s);
            return ret.ToString();
        }

        [SpecialName]
        public static string Multiply(int other, string self) {
            return Multiply(self, other);
        }

        [SpecialName]
        public static object Multiply(string self, [NotNull]Index count) {
            return PythonOps.MultiplySequence<string>(Multiply, self, count, true);
        }

        [SpecialName]
        public static object Multiply([NotNull]Index count, string self) {
            return PythonOps.MultiplySequence<string>(Multiply, self, count, false);
        }

        [SpecialName]
        public static object Multiply(string self, object count) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return Multiply(self, index);
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        [SpecialName]
        public static object Multiply(object count, string self) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return Multiply(index, self);
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        [SpecialName]
        public static bool GreaterThan(string x, string y) {
            return string.CompareOrdinal(x, y) > 0;
        }
        [SpecialName]
        public static bool LessThan(string x, string y) {
            return string.CompareOrdinal(x, y) < 0;
        }
        [SpecialName]
        public static bool LessThanOrEqual(string x, string y) {
            return string.CompareOrdinal(x, y) <= 0;
        }
        [SpecialName]
        public static bool GreaterThanOrEqual(string x, string y) {
            return string.CompareOrdinal(x, y) >= 0;
        }
        [SpecialName]
        public static bool Equals(string x, string y) {
            return string.Equals(x, y);
        }
        [SpecialName]
        public static bool NotEquals(string x, string y) {
            return !string.Equals(x, y);
        }

        #endregion

        [SpecialName, ImplicitConversionMethod]
        public static string ConvertFromChar(char c) {
            return ScriptingRuntimeHelpers.CharToString(c);
        }

        [SpecialName, ExplicitConversionMethod]
        public static char ConvertToChar(string s) {
            if (s.Length == 1) return s[0];
            throw PythonOps.TypeErrorForTypeMismatch("char", s);
        }

        [SpecialName, ImplicitConversionMethod]
        public static IEnumerable ConvertToIEnumerable(string s) {
            // make an enumerator that produces strings instead of chars
            return new PythonStrIterator(s);
        }

        internal static int Compare(string self, string obj) {
            int ret = string.CompareOrdinal(self, obj);
            return ret == 0 ? 0 : (ret < 0 ? -1 : +1);
        }

        public static object __getnewargs__(CodeContext/*!*/ context, string self) {
            if (!Object.ReferenceEquals(self, null)) {
                // Cast self to object to avoid exception caused by trying to access SystemState on DefaultContext
                return PythonTuple.MakeTuple(StringOps.__new__(context, TypeCache.String, (object)self));
            }
            throw PythonOps.TypeErrorForBadInstance("__getnewargs__ requires a 'str' object but received a '{0}'", self);
        }

        public static string __str__(string self) {
            return self;
        }

        public static Extensible<string> __str__(ExtensibleString self) {
            return self;
        }

        public static string/*!*/ __repr__(string/*!*/ self) {
            return StringOps.Quote(self);
        }

        #region Internal implementation details

        internal static string Quote(string s) {
            StringBuilder b = new StringBuilder(s.Length + 5);
            char quote = '\'';
            if (s.IndexOf('\'') != -1 && s.IndexOf('\"') == -1) {
                quote = '\"';
            }
            b.Append(quote);
            b.Append(ReprEncode(s, quote));
            b.Append(quote);
            return b.ToString();
        }

        internal static bool TryGetEncoding(string name, out Encoding encoding) {
#if FEATURE_ENCODING
            encoding = null;

            if (string.IsNullOrWhiteSpace(name)) return false;

            string normName = NormalizeEncodingName(name);

            if (CodecsInfo.Codecs.TryGetValue(normName, out Lazy<Encoding> proxy)) {
                encoding = proxy.Value;
#if NETCOREAPP || NETSTANDARD
            } else {
                try {
                    Encoding enc;
                    if (name.StartsWith("cp") && int.TryParse(name.Substring(2), out int codepage)) {
                        if (codepage < 0 || 65535 < codepage) return false;
                        enc = Encoding.GetEncoding(codepage);
                        CodecsInfo.Codecs[normName] = new Lazy<Encoding>(() => enc, isThreadSafe: false);
                    } else {
                        enc = Encoding.GetEncoding(name);
                        var fac = new Lazy<Encoding>(() => enc, isThreadSafe: false);
                        CodecsInfo.Codecs[normName] = fac;
                        if (enc.CodePage != 0) CodecsInfo.Codecs["cp" + enc.CodePage.ToString()] = fac;
                    }
                    encoding = enc;
                } catch (Exception ex) when (ex is NotSupportedException || ex is ArgumentException) {
                    CodecsInfo.Codecs[normName] = new Lazy<Encoding>(() => null, isThreadSafe: false);
                    return false;
                }
#endif // NETCOREAPP || NETSTANDARD
            }
            return encoding != null;

#else // !FEATURE_ENCODING
            switch (NormalizeEncodingName(name)) {
                case "us_ascii":
                case "ascii": encoding = PythonAsciiEncoding.Instance; return true;

                case "latin1":
                case "latin_1":
                case "iso_8859_1": encoding = Encoding.GetEncoding(28591); return true;

                case "utf_8": encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); return true;

                case "utf_16le":
                case "utf_16_le": encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false); return true;

                case "utf_16be":
                case "utf_16_be": encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false); return true;

                case "utf_8_sig": encoding = Encoding.UTF8; return true;

                default: encoding = null; return false;
            }
#endif
        }

        #endregion

        #region Private implementation details

        private static int CheckIndex(object index, int defaultValue) {
            int res;

            if (index == null) {
                res = defaultValue;
            } else if (!Converter.TryConvertToIndex(index, out res)) {
                throw PythonOps.TypeError("slice indices must be integers or None or have an __index__ method");
            }

            return res;
        }

        private static void AppendJoin(object value, int index, StringBuilder sb) {
            string strVal;

            if ((strVal = value as string) != null) {
                sb.Append(strVal);
            } else if (Converter.TryConvertToString(value, out strVal) && strVal != null) {
                sb.Append(strVal);
            } else {
                throw PythonOps.TypeError("sequence item {0}: expected string, {1} found", index.ToString(), PythonOps.GetPythonTypeName(value));
            }
        }

        private static string ReplaceEmpty(string self, string @new, int count) {
            string v = self;

            if (count == 0) return v;
            else if (count < 0) count = v.Length + 1;
            else if (count > v.Length + 1) count = checked(v.Length + 1);

            int newLength = checked(v.Length + @new.Length * count);
            int max = Math.Min(v.Length, count);
            StringBuilder ret = new StringBuilder(newLength);
            for (int i = 0; i < max; i++) {
                ret.Append(@new);
                ret.Append(v[i]);
            }
            if (count > max) {
                ret.Append(@new);
            } else {
                ret.Append(v, max, v.Length - max);
            }

            return ret.ToString();
        }

        private static string Reverse(string s) {
            if (s.Length == 0 || s.Length == 1) return s;
            char[] rchars = new char[s.Length];
            for (int i = s.Length - 1, j = 0; i >= 0; i--, j++) {
                rchars[j] = s[i];
            }
            return new string(rchars);
        }

        internal static string AsciiEncode(string s) {
            // in the common case we don't need to encode anything, so we
            // lazily create the StringBuilder only if necessary.
            StringBuilder b = null;
            int start = 0;
            int end = s.Length;
            int i = start;
            while (i < end) {
                char ch = s[i++];
                if (ch <= 0x1F || ch == 0x7F) {
                    StringBuilderInit(ref b, s, start, i - 1);
                    b.AppendFormat("\\x{0:x2}", (int)ch);
                } else if (ch > 0x7F) {
                    StringBuilderInit(ref b, s, 0, i - 1);
                    if ((ch & 0xFC00) == 0xD800 && i < end && (s[i] & 0xFC00) == 0xDC00) {
                        b.AppendFormat("\\U{0:x8}", char.ConvertToUtf32(ch, s[i++]));
                    } else if (ch > 0xFF) {
                        b.AppendFormat("\\u{0:x4}", (int)ch);
                    } else {
                        b.AppendFormat("\\x{0:x2}", (int)ch);
                    }
                } else {
                    b?.Append(ch);
                }
            }

            return b?.ToString() ?? s;
        }

        internal static string ReprEncode(string s, char quote) {
            return ReprEncode(s, 0, s.Length, isUniEscape: false, quote);
        }

        private static string ReprEncode(string s, int start, int count, bool isUniEscape, char quote = default) {
            // in the common case we don't need to encode anything, so we
            // lazily create the StringBuilder only if necessary.
            StringBuilder b = null;
            int i = start;
            int end = start + count;
            while (i < end) {
                char ch = s[i++];
                switch (ch) {
                    case '\\': StringBuilderInit(ref b, s, start, i - 1); b.Append("\\\\"); break;
                    case '\t': StringBuilderInit(ref b, s, start, i - 1); b.Append("\\t"); break;
                    case '\n': StringBuilderInit(ref b, s, start, i - 1); b.Append("\\n"); break;
                    case '\r': StringBuilderInit(ref b, s, start, i - 1); b.Append("\\r"); break;
                    default:
                        if (quote != default && ch == quote) {
                            StringBuilderInit(ref b, s, start, i - 1);
                            b.Append('\\'); b.Append(ch);
                        } else if (ch <= 0x1F || ch == 0x7F) {
                            StringBuilderInit(ref b, s, start, i - 1);
                            b.AppendFormat("\\x{0:x2}", (int)ch);
                        } else if (ch > 0x7F && (isUniEscape || !IsPrintable(ch))) {
                            StringBuilderInit(ref b, s, start, i - 1);
                            if ((ch & 0xFC00) == 0xD800 && i < end && (s[i] & 0xFC00) == 0xDC00) {
                                b.AppendFormat("\\U{0:x8}", char.ConvertToUtf32(ch, s[i++]));
                            } else if (ch > 0xFF) {
                                b.AppendFormat("\\u{0:x4}", (int)ch);
                            } else {
                                b.AppendFormat("\\x{0:x2}", (int)ch);
                            }
                        } else {
                            b?.Append(ch);
                        }

                        break;
                }
            }

            return b?.ToString() ?? s.Substring(start, count);
        }

        private static string RawUnicodeEscapeEncode(string s, int start, int count, bool escapeAscii = false) {
            // in the common case we don't need to encode anything, so we
            // lazily create the StringBuilder only if necessary.
            StringBuilder b = null;
            int i = start;
            int end = start + count;
            while (i < end) {
                char ch = s[i++];
                if ((ch & 0xFC00) == 0xD800 && i < end && (s[i] & 0xFC00) == 0xDC00) {
                    StringBuilderInit(ref b, s, start, i - 1);
                    b.AppendFormat("\\U{0:x8}", char.ConvertToUtf32(ch, s[i++]));
                } else if (ch > 0xFF) {
                    StringBuilderInit(ref b, s, start, i - 1);
                    b.AppendFormat("\\u{0:x4}", (int)ch);
                } else if (escapeAscii) {
                    StringBuilderInit(ref b, s, start, i - 1);
                    b.AppendFormat("\\x{0:x2}", (int)ch);
                } else {
                    b?.Append(ch);
                }
            }

            return b?.ToString() ?? s.Substring(start, count);
        }

        private static void StringBuilderInit(ref StringBuilder sb, string s, int start, int end) {
            if (sb != null) return;

            sb = new StringBuilder(s.Length);
            sb.Append(s, start, end - start);
        }

        private static bool IsSign(char ch) {
            return ch == '+' || ch == '-';
        }

        internal static string GetEncodingName(Encoding encoding, bool normalize = true) {
#if FEATURE_ENCODING
            string name = null;

            // if we have a valid code page try and get a reasonable name.  The
            // web names / mail displays tend to match CPython's terse names
            if (encoding.CodePage != 0) {
#if !NETCOREAPP && !NETSTANDARD
                if (encoding.IsBrowserDisplay) {
                    name = encoding.WebName;
                }

                if (name == null && encoding.IsMailNewsDisplay) {
                    name = encoding.HeaderName;
                }
#endif

                if (name == null) {
                    switch (encoding.CodePage) {

                        // recognize a few common cases
                        case 1200: name = "utf-16LE"; break;
                        case 1201: name = "utf-16BE"; break;

                        case 12000: name = "utf-32LE"; break;
                        case 12001: name = "utf-32BE"; break;

                        case 20127: name = "us-ascii"; break;
                        case 28591: name = "iso-8859-1"; break;

                        case 65000: name = "utf-7"; break;
                        case 65001: name = "utf-8"; break;

                        // otherwise use a code page number which also matches CPython
                        default: name = "cp" + encoding.CodePage; break;
                    }
                }
            }

            if (name == null) {
                // otherwise just finally fall back to the human readable name
                name = encoding.EncodingName;
            }
#else
            // only has web names
            string name = encoding.WebName;
#endif

            return normalize ? NormalizeEncodingName(name) : name;
        }

        internal static string NormalizeEncodingName(string name) =>
            name?.ToLower(CultureInfo.InvariantCulture).Replace('-', '_').Replace(' ', '_');

        internal static string RawDecode(CodeContext/*!*/ context, IBufferProtocol data, string encoding, string errors) {
            if (TryGetEncoding(encoding, out Encoding e)) {
                return DoDecode(context, data.ToMemory().Span, errors, encoding, e);
            }

            // look for user-registered codecs
            PythonTuple codecTuple = PythonOps.LookupTextEncoding(context, encoding, "codecs.decode()");
            if (codecTuple != null) {
                return UserDecode(codecTuple, data, errors);
            }

            throw PythonOps.LookupError("unknown encoding: {0}", encoding);
        }

#if FEATURE_ENCODING
        private static DecoderFallback ReplacementFallback = new DecoderReplacementFallback("\ufffd");
#endif

        internal static string DoDecode(CodeContext context, ReadOnlySpan<byte> data, string errors, string encoding, Encoding e) {
            int start = GetStartingOffset(e, data);
            int length = data.Length - start;

#if FEATURE_ENCODING
            // CLR's encoder exceptions have a 1-1 mapping w/ Python's encoder exceptions
            // so we just clone the encoding & set the fallback to throw in strict mode.
            Encoding setFallback(Encoding enc, DecoderFallback fb) {
                enc = (Encoding)enc.Clone();
                enc.DecoderFallback = fb;
                return enc;
            }
            switch (errors) {
                case null:
                case "backslashreplace":
                case "xmlcharrefreplace":
                case "strict": e = setFallback(e, new ExceptionFallback(e is UTF8Encoding)); break;
                case "replace": e = setFallback(e, ReplacementFallback); break;
                case "ignore": e = setFallback(e, new PythonDecoderFallback()); break;
                case "surrogateescape": e =  new PythonSurrogateEscapeEncoding(e); break;
                case "surrogatepass": e =  new PythonSurrogatePassEncoding(e); break;
                default:
                    e = setFallback(e, new PythonDecoderFallback(encoding,
                        //data.Slice(start),          // COMPILE ERROR: fallback should be given non-ref srtuct or object
                        data.Slice(start).ToArray(),  // data copy
                        () => LightExceptions.CheckAndThrow(PythonOps.LookupEncodingError(context, errors))));
                    break;
            }
#endif

            string decoded = string.Empty;
            try {
                unsafe {
                    fixed (byte* bp = data.Slice(start)) {
                        if (bp != null) {
#if FEATURE_ENCODING
                            if (e is UnicodeEscapeEncoding ue) {
                                // This overload is not virtual, but the base implementation is inefficient for this encoding
                                decoded = ue.GetString(bp, length);
                            } else
#endif
                            {
                                decoded = e.GetString(bp, length);
                            }
                        }
                    }
                }
            } catch (DecoderFallbackException ex) {
                // augmenting the caught exception instead of creating UnicodeDecodeError to preserve the stack trace
                ex.Data["encoding"] = encoding;
                ex.Data["object"] = Bytes.Make(data.Slice(start).ToArray());
                throw;
            }

            return decoded;
        }

        /// <summary>
        /// Gets the starting offset checking to see if the incoming bytes already include a preamble.
        /// </summary>
        private static int GetStartingOffset(Encoding e, in ReadOnlySpan<byte> bytes) {
            byte[] preamble = e.GetPreamble();
            return bytes.StartsWith(preamble) ? preamble.Length : 0;
        }

        internal static Bytes RawEncode(CodeContext/*!*/ context, string s, string encoding, string errors) {
            if (TryGetEncoding(encoding, out Encoding e)) {
                return DoEncode(context, s, errors, encoding, e, true);
            }

            // look for user-registered codecs
            PythonTuple codecTuple = PythonOps.LookupTextEncoding(context, encoding, "codecs.encode()");
            if (codecTuple != null) {
                return UserEncode(encoding, codecTuple, s, errors);
            }

            throw PythonOps.LookupError("unknown encoding: {0}", encoding);
        }

        internal static Bytes DoEncodeUtf8(CodeContext context, string s)
            => DoEncode(context, s, "strict", "utf-8", Encoding.UTF8, includePreamble: false);

        internal static Bytes DoEncode(CodeContext context, string s, string errors, string encoding, Encoding e, bool includePreamble) {
#if FEATURE_ENCODING
            // CLR's encoder exceptions have a 1-1 mapping w/ Python's encoder exceptions
            // so we just clone the encoding & set the fallback to throw in strict mode
            Encoding setFallback(Encoding enc, EncoderFallback fb) {
                enc = (Encoding)enc.Clone();
                enc.EncoderFallback = fb;
                return enc;
            }
            switch (errors) {
                case null:
                case "strict": e = setFallback(e, EncoderFallback.ExceptionFallback); break;
                case "replace": e = setFallback(e, EncoderFallback.ReplacementFallback); break;
                case "backslashreplace": e = setFallback(e, new BackslashEncoderReplaceFallback()); break;
                case "xmlcharrefreplace": e = setFallback(e, new XmlCharRefEncoderReplaceFallback()); break;
                case "ignore": e = setFallback(e, new PythonEncoderFallback()); break;
                case "surrogateescape": e = new PythonSurrogateEscapeEncoding(e); break;
                case "surrogatepass": e = new PythonSurrogatePassEncoding(e); break;
                default:
                    e = setFallback(e, new PythonEncoderFallback(encoding, s,
                        () => LightExceptions.CheckAndThrow(PythonOps.LookupEncodingError(context, errors))));
                    break;
            }
#endif

            byte[] preamble = includePreamble ? e.GetPreamble() : null;
            int preambleLen = preamble?.Length ?? 0;
            byte[] bytes;
            try {
                bytes = new byte[preambleLen + e.GetByteCount(s)];
                if (preambleLen > 0) {
                    Array.Copy(preamble, 0, bytes, 0, preambleLen);
                }
                e.GetBytes(s, 0, s.Length, bytes, preambleLen);
            } catch (EncoderFallbackException ex) {
                ex.Data["encoding"] = encoding;
                ex.Data["object"] = s;
                throw;
            }

            return Bytes.Make(bytes);
        }

        private static PythonTuple CallUserDecodeOrEncode(object function, object data, string errors) {
            object res;
            if (errors != null) {
                res = PythonCalls.Call(function, data, errors);
            } else {
                res = PythonCalls.Call(function, data);
            }

            if (res is PythonTuple t && t.Count == 2) {
                return t;
            }

            throw PythonOps.TypeError("encoder/decoder must return a tuple (object, integer)");
        }

        private static Bytes UserEncode(string encoding, PythonTuple codecInfo, string data, string errors) {
            var res = CallUserDecodeOrEncode(codecInfo[0], data, errors);
            if (res[0] is Bytes b) return b;
            throw PythonOps.TypeError("'{0}' encoder returned '{1}' instead of 'bytes'; use codecs.encode() to encode to arbitrary types", encoding, DynamicHelpers.GetPythonType(res[0]).Name);
        }

        private static string UserDecode(PythonTuple codecInfo, object data, string errors) {
            var res = CallUserDecodeOrEncode(codecInfo[1], data, errors);
            return Converter.ConvertToString(res[0]);
        }

#if FEATURE_ENCODING
        internal static class CodecsInfo {
            internal static readonly Encoding RawUnicodeEscapeEncoding = new UnicodeEscapeEncoding(raw: true);
            internal static readonly Encoding UnicodeEscapeEncoding = new UnicodeEscapeEncoding(raw: false);
            internal static readonly Dictionary<string, Lazy<Encoding>> Codecs = MakeCodecsDict();

            private static Dictionary<string, Lazy<Encoding>> MakeCodecsDict() {
                Dictionary<string, Lazy<Encoding>> d = new Dictionary<string, Lazy<Encoding>>();
                Lazy<Encoding> makeEncodingProxy(Func<Encoding> factory) => new Lazy<Encoding>(factory, isThreadSafe: false);

#if NETCOREAPP || NETSTANDARD
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                // TODO: add more encodings
                d["cp1252"] = d["windows_1252"] = makeEncodingProxy(() => Encoding.GetEncoding(1252));
                d["iso8859_15"] = d["iso_8859_15"] = d["latin9"] = d["l9"] = makeEncodingProxy(() => Encoding.GetEncoding(28605));
#endif
                EncodingInfo[] encs = Encoding.GetEncodings();

                foreach (EncodingInfo encInfo in encs) {
                    string normalizedName = NormalizeEncodingName(encInfo.Name);

                    // setup well-known mappings, for everything else we'll store as lower case w/ _
                    // for the common types cp* are not actual Python aliases, but GetEncodingName may return them
                    switch (normalizedName) {
                        case "us_ascii":
                            d["cp" + encInfo.CodePage.ToString()] = d["us_ascii"] = d["us"] = d["ascii"] = d["646"] = makeEncodingProxy(() => PythonAsciiEncoding.Instance);
                            continue;
                        case "utf_7":
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_7"] = d["u7"] = d["unicode_1_1_utf_7"] = makeEncodingProxy(() => new UTF7Encoding(allowOptionals: true));
                            continue;
                        case "utf_8":
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_8"] = d["utf8"] = d["u8"] = makeEncodingProxy(() => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                            d["utf_8_sig"] = makeEncodingProxy(encInfo.GetEncoding); ;
                            continue;
                        case "utf_16":
                            d["utf_16le"] = d["utf_16_le"] = makeEncodingProxy(() => new UnicodeEncoding(bigEndian: false, byteOrderMark: false));
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_16"] = d["utf16"] = d["u16"] = makeEncodingProxy(encInfo.GetEncoding); ;
                            continue;
                        case "utf_16be":
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_16be"] = d["utf_16_be"] = makeEncodingProxy(() => new UnicodeEncoding(bigEndian: true, byteOrderMark: false));
                            continue;
                        case "utf_32":
                            d["utf_32le"] = d["utf_32_le"] = makeEncodingProxy(() => new UTF32Encoding(bigEndian: false, byteOrderMark: false));
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_32"] = d["utf32"] = d["u32"] = makeEncodingProxy(encInfo.GetEncoding); ;
                            continue;
                        case "utf_32be":
                            d["cp" + encInfo.CodePage.ToString()] = d["utf_32be"] = d["utf_32_be"] = makeEncodingProxy(() => new UTF32Encoding(bigEndian: true, byteOrderMark: false));
                            continue;

                        case "iso_8859_1":
                            d["iso_8859_1"] = d["iso8859_1"] = d["8859"] = d["cp28591"] = d["28591"] =
                                d["latin_1"] = d["latin1"] = d["latin"] = d["l1"] = d["cp819"] = d["819"] = makeEncodingProxy(() => Latin1Encoding);
                            continue;
                    }

                    // publish under normalized name (all lower cases, -s replaced with _s)
                    d[normalizedName] = //...
                        // publish under code page number as well...
                        d["cp" + encInfo.CodePage.ToString()] = d[encInfo.CodePage.ToString()] = makeEncodingProxy(encInfo.GetEncoding);
                }

                d["raw_unicode_escape"] = makeEncodingProxy(() => RawUnicodeEscapeEncoding);
                d["unicode_escape"] = makeEncodingProxy(() => UnicodeEscapeEncoding);

#if DEBUG
                foreach (KeyValuePair<string, Lazy<Encoding>> kvp in d) {
                    // all codecs should be stored in lowercase because we only look up from lowercase strings
                    Debug.Assert(kvp.Key.ToLower(CultureInfo.InvariantCulture) == kvp.Key);
                    // all codec names should use underscores instead of dashes to match lookup values
                    Debug.Assert(kvp.Key.IndexOf('-') < 0);
                }
#endif
                return d;
            }

            internal static Dictionary<string, object> MakeErrorHandlersDict() {
                var d = new Dictionary<string, object>();

                d["strict"] = BuiltinFunction.MakeFunction(
                    "strict_errors", 
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(StrictErrors), BindingFlags.Static | BindingFlags.NonPublic)), 
                    typeof(StringOps));

                d["ignore"] = BuiltinFunction.MakeFunction(
                    "ignore_errors", 
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(IgnoreErrors), BindingFlags.Static | BindingFlags.NonPublic)), 
                    typeof(StringOps));

                d["replace"] = BuiltinFunction.MakeFunction(
                    "replace_errors",
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(ReplaceErrors), BindingFlags.Static | BindingFlags.NonPublic)),
                    typeof(StringOps));

                d["xmlcharrefreplace"] = BuiltinFunction.MakeFunction(
                    "xmlcharrefreplace_errors",
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(XmlCharRefReplaceErrors), BindingFlags.Static | BindingFlags.NonPublic)),
                    typeof(StringOps));

                d["backslashreplace"] = BuiltinFunction.MakeFunction(
                    "backslashreplace_errors",
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(BackslashReplaceErrors), BindingFlags.Static | BindingFlags.NonPublic)),
                    typeof(StringOps));

                d["surrogateescape"] = BuiltinFunction.MakeFunction(
                    "surrogateescape_errors",
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(SurrogateEscapeErrors), BindingFlags.Static | BindingFlags.NonPublic)),
                    typeof(StringOps));

                d["surrogatepass"] = BuiltinFunction.MakeFunction(
                    "surrogatepass_errors",
                    ReflectionUtils.GetMethodInfos(typeof(StringOps).GetMember(nameof(SurrogatePassErrors), BindingFlags.Static | BindingFlags.NonPublic)),
                    typeof(StringOps));

                return d;
            }
        }
#endif

        private static PythonList SplitEmptyString(bool separators) {
            PythonList ret = PythonOps.MakeEmptyList(1);
            if (separators) {
                ret.AddNoLock(String.Empty);
            }
            return ret;
        }

        private static PythonList SplitInternal(string self, char[] seps, int maxsplit) {
            if (String.IsNullOrEmpty(self)) {
                return SplitEmptyString(seps != null);
            }

            //  If the optional second argument sep is absent or None, the words are separated 
            //  by arbitrary strings of whitespace characters (space, tab, newline, return, formfeed);
            string[] r = StringUtils.Split(
                self,
                seps,
                (maxsplit < 0) ? Int32.MaxValue : maxsplit + 1,
                (seps == null) ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);

            PythonList ret = PythonOps.MakeEmptyList(r.Length);
            foreach (string s in r) ret.AddNoLock(s);
            return ret;
        }

        private static PythonList SplitInternal(string self, string separator, int maxsplit) {
            if (String.IsNullOrEmpty(self)) {
                return SplitEmptyString(separator != null);
            } else {
                string[] r = StringUtils.Split(self, separator, (maxsplit < 0) ? Int32.MaxValue : maxsplit + 1, StringSplitOptions.None);

                PythonList ret = PythonOps.MakeEmptyList(r.Length);
                foreach (string s in r) ret.AddNoLock(s);
                return ret;
            }
        }

        private static void TryStringOrTuple(object prefix) {
            if (prefix == null) {
                throw PythonOps.TypeError("expected string or tuple, got NoneType");
            }
            if (!(prefix is string) && !(prefix is PythonTuple) && !(prefix is Extensible<string>)) {
                throw PythonOps.TypeError("expected string or tuple, got {0}", DynamicHelpers.GetPythonType(prefix).Name);
            }
        }

        private static string GetString(object obj) {
            string ret = AsString(obj);
            if (ret == null) {
                throw PythonOps.TypeError("expected string, got {0}", DynamicHelpers.GetPythonType(obj).Name);
            }
            return ret;
        }

        public static bool endswith(string self, string suffix) {
            return self.EndsWith(suffix, StringComparison.Ordinal);
        }

        //  Indexing is 0-based. Need to deal with negative indices
        //  (which mean count backwards from end of sequence)
        //  +---+---+---+---+---+
        //  | a | b | c | d | e |
        //  +---+---+---+---+---+
        //    0   1   2   3   4
        //   -5  -4  -3  -2  -1

        public static bool endswith(string self, string suffix, int start) {
            int len = self.Length;
            if (start > len) return false;
            // map the negative indice to its positive counterpart
            if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            return self.Substring(start).EndsWith(suffix, StringComparison.Ordinal);
        }

        //  With optional start, test beginning at that position (the char at that index is
        //  included in the test). With optional end, stop comparing at that position (the
        //  char at that index is not included in the test)
        public static bool endswith(string self, string suffix, int start, int end) {
            int len = self.Length;
            if (start > len) return false;
            // map the negative indices to their positive counterparts
            else if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            if (end >= len) return self.Substring(start).EndsWith(suffix, StringComparison.Ordinal);
            else if (end < 0) {
                end += len;
                if (end < 0) return false;
            }
            if (end < start) return false;
            return self.Substring(start, end - start).EndsWith(suffix, StringComparison.Ordinal);
        }

        private static bool endswith(string self, PythonTuple suffix) {
            foreach (object obj in suffix) {
                if (self.EndsWith(GetString(obj), StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        private static bool endswith(string self, PythonTuple suffix, int start) {
            foreach (object obj in suffix) {
                if (endswith(self, GetString(obj), start)) {
                    return true;
                }
            }
            return false;
        }

        private static bool endswith(string self, PythonTuple suffix, int start, int end) {
            foreach (object obj in suffix) {
                if (endswith(self, GetString(obj), start, end)) {
                    return true;
                }
            }
            return false;
        }

        public static bool startswith(string self, string prefix) {
            return self.StartsWith(prefix, StringComparison.Ordinal);
        }

        public static bool startswith(string self, string prefix, int start) {
            int len = self.Length;
            if (start > len) return false;
            if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            return self.Substring(start).StartsWith(prefix, StringComparison.Ordinal);
        }

        public static bool startswith(string self, string prefix, int start, int end) {
            int len = self.Length;
            if (start > len) return false;
            // map the negative indices to their positive counterparts
            else if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            if (end >= len) return self.Substring(start).StartsWith(prefix, StringComparison.Ordinal);
            else if (end < 0) {
                end += len;
                if (end < 0) return false;
            }
            if (end < start) return false;
            return self.Substring(start, end - start).StartsWith(prefix, StringComparison.Ordinal);
        }

        private static bool startswith(string self, PythonTuple prefix) {
            foreach (object obj in prefix) {
                if (self.StartsWith(GetString(obj), StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        private static bool startswith(string self, PythonTuple prefix, int start) {
            foreach (object obj in prefix) {
                if (startswith(self, GetString(obj), start)) {
                    return true;
                }
            }
            return false;
        }

        private static bool startswith(string self, PythonTuple prefix, int start, int end) {
            foreach (object obj in prefix) {
                if (startswith(self, GetString(obj), start, end)) {
                    return true;
                }
            }
            return false;
        }

        internal static IEnumerable StringEnumerable(string str) {
            return new PythonStrIterator(str);
        }

        internal static IEnumerator<string> StringEnumerator(string str) {
            return new PythonStrIterator(str);
        }

        #endregion

        #region UnicodeEscapeEncoding

#if FEATURE_ENCODING

        private class UnicodeEscapeEncoding : Encoding {
            private readonly bool _raw;

            public UnicodeEscapeEncoding(bool raw) {
                _raw = raw;
            }

            private string EscapeEncode(string s, int index, int count) {
                return _raw ?
                    RawUnicodeEscapeEncode(s, index, count)
                :
                    ReprEncode(s, index, count, isUniEscape: true);
            }

            public override int GetByteCount(string s)
                => EscapeEncode(s, 0, s.Length).Length;

            public override int GetByteCount(char[] chars, int index, int count)
                => EscapeEncode(new string(chars), index, count).Length;

            public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex) {
                string res = EscapeEncode(s, charIndex, charCount);

                for (int i = 0; i < res.Length; i++) {
                    bytes[i + byteIndex] = (byte)res[i];
                }
                return res.Length;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
                => GetBytes(new string(chars), charIndex, charCount, bytes, byteIndex);

            public override string GetString(byte[] bytes, int index, int count)
                => LiteralParser.ParseString(bytes, index, count, _raw, GetErrorHandler());

            public new unsafe string GetString(byte* bytes, int byteCount) {
                var data = new ReadOnlySpan<byte>(bytes, byteCount);
                return LiteralParser.ParseString(data, _raw, GetErrorHandler());
            }

            public override unsafe int GetCharCount(byte* bytes, int count)
                => LiteralParser.ParseString(new ReadOnlySpan<byte>(bytes, count), _raw, GetErrorHandler()).Length;

            public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount) {
                var data = new ReadOnlySpan<byte>(bytes, byteCount);
                var dest = new Span<char>(chars, charCount);

                string res = LiteralParser.ParseString(data, _raw, GetErrorHandler());

                if (res.Length < charCount) charCount = res.Length; 
                res.AsSpan().Slice(0, charCount).CopyTo(dest);
                return charCount;
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
                => LiteralParser.ParseString(bytes, index, count, _raw, GetErrorHandler()).Length;

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
                string res = LiteralParser.ParseString(bytes, byteIndex, byteCount, _raw, GetErrorHandler());
                res.AsSpan().CopyTo(chars.AsSpan(charIndex));
                return res.Length;
            }

            public override int GetMaxByteCount(int charCount) => charCount * 5;

            public override int GetMaxCharCount(int byteCount) => byteCount;

            private LiteralParser.ParseStringErrorHandler<byte> GetErrorHandler() {
                // For 'strict' handler, the default error handler of LiteralParser.DoParseString
                // offers better error reporting, so use that one.
                if (DecoderFallback is ExceptionFallback) return default;

                DecoderFallbackBuffer fbuf = null;

                return delegate (in ReadOnlySpan<byte> data, int start, int end, string reason) {
                    fbuf ??= DecoderFallback.CreateFallbackBuffer();

                    byte[] bytesUnknown = new byte[end - start];
                    for (int i = start, j = 0; i < end; i++, j++) {
                        bytesUnknown[j] = data[i];
                    }

                    fbuf.Fallback(bytesUnknown, start);
                    if (fbuf.Remaining == 0) return null;

                    var fallback = new List<char>(fbuf.Remaining);
                    while (fbuf.Remaining > 0) {
                        fallback.Add(fbuf.GetNextChar());
                    }
                    return fallback;
                };
            }
        }

#endif

        #endregion

        #region  Unicode Encode/Decode Fallback Support

#if FEATURE_ENCODING

        /// When encoding or decoding strings if an error occurs CPython supports several different
        /// behaviors, in addition it supports user-extensible behaviors as well.  For the default
        /// behavior we're ok - both of us support throwing and replacing.  For custom behaviors
        /// we define a single fallback for decoding and encoding that calls the python function to do
        /// the replacement.
        /// 
        /// When we do the replacement we call the provided handler w/ a UnicodeEncodeError or UnicodeDecodeError
        /// object which contains:
        ///         encoding    (string, the encoding the user requested)
        ///         object      (the original string or bytes being encoded/decoded)
        ///         start       (the start of the invalid sequence)
        ///         end         (the exclusive end of the invalid sequence)
        ///         reason      (the error message, e.g. 'unexpected byte code', not sure of others)
        /// 
        /// The decoder returns a tuple of (str, int) where str is the replacement string
        /// and int is an index where encoding/decoding should continue.
        /// TODO: returned int is currently ignored, assumed to be equal to end (i.e. the index is not adjusted).

        private class PythonEncoderFallbackBuffer : EncoderFallbackBuffer {
            private readonly string _encoding;
            private readonly string _strData;
            private readonly object _function;
            private string _buffer;
            private int _bufferIndex;

            public PythonEncoderFallbackBuffer(string encoding, string str, object function) {
                _encoding = encoding;
                _strData = str;
                _function = function;
            }

            public override bool Fallback(char charUnknown, int index) {
                return DoPythonFallback(index, 1);
            }

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
                return DoPythonFallback(index, 2);
            }

            public override char GetNextChar() {
                if (_buffer == null || _bufferIndex >= _buffer.Length) return Char.MinValue;

                return _buffer[_bufferIndex++];
            }

            public override bool MovePrevious() {
                if (_bufferIndex > 0) {
                    _bufferIndex--;
                    return true;
                }
                return false;
            }

            public override int Remaining {
                get {
                    if (_buffer == null) return 0;
                    return _buffer.Length - _bufferIndex;
                }
            }

            public override void Reset() {
                _buffer = null;
                _bufferIndex = 0;
                base.Reset();
            }

            private bool DoPythonFallback(int index, int length) {
                if (_function != null) {
                    // create the exception object to hand to the user-function...
                    var exObj = PythonExceptions.CreatePythonThrowable(PythonExceptions.UnicodeEncodeError, _encoding, _strData, index, index + length, "unexpected code byte");

                    // call the user function...
                    object res = PythonCalls.Call(_function, exObj);

                    string replacement = CheckReplacementTuple(res, "encoding", index + length);

                    // finally process the user's request.
                    _buffer = replacement;
                    _bufferIndex = 0;
                    return true;
                }

                return false;
            }
        }

        private class PythonEncoderFallback : EncoderFallback {
            private readonly string encoding;
            private readonly string data;
            private readonly Func<object> lookup;
            private object function;

            public PythonEncoderFallback() { }

            public PythonEncoderFallback(string encoding, string data, Func<object> lookup) {
                this.encoding = encoding;
                this.data = data;
                this.lookup = lookup;
            }

            public override EncoderFallbackBuffer CreateFallbackBuffer() {
                if (function == null && lookup != null) {
                    function = lookup.Invoke();
                }
                return new PythonEncoderFallbackBuffer(encoding, data, function);
            }

            public override int MaxCharCount {
                get { return Int32.MaxValue; }
            }
        }

        private class PythonDecoderFallbackBuffer : DecoderFallbackBuffer {
            private readonly object _function;
            private readonly string _encoding;
            private readonly IList<byte> _strData;
            private string _buffer;
            private int _bufferIndex;

            public PythonDecoderFallbackBuffer(string encoding, IList<byte> str, object callable) {
                _encoding = encoding;
                _strData = str;
                _function = callable;
            }

            public override int Remaining {
                get {
                    if (_buffer == null) return 0;
                    return _buffer.Length - _bufferIndex;
                }
            }

            public override char GetNextChar() {
                if (_buffer == null || _bufferIndex >= _buffer.Length) return Char.MinValue;

                return _buffer[_bufferIndex++];
            }

            public override bool MovePrevious() {
                if (_bufferIndex > 0) {
                    _bufferIndex--;
                    return true;
                }
                return false;
            }

            public override void Reset() {
                _buffer = null;
                _bufferIndex = 0;
                base.Reset();
            }

            public override bool Fallback(byte[] bytesUnknown, int index) {
                if (_function != null) {
                    // create the exception object to hand to the user-function...
                    var exObj = PythonExceptions.CreatePythonThrowable(PythonExceptions.UnicodeDecodeError, _encoding, _strData, index, index + bytesUnknown.Length, "unexpected code byte");

                    // call the user function...
                    object res = PythonCalls.Call(_function, exObj);

                    string replacement = CheckReplacementTuple(res, "decoding", index + bytesUnknown.Length);

                    // finally process the user's request.
                    _buffer = replacement;
                    _bufferIndex = 0;
                    return true;
                }

                return false;
            }

        }

        private class PythonDecoderFallback : DecoderFallback {
            private readonly string encoding;
            private readonly IList<byte> data;
            private readonly Func<object> lookup;
            private object function;

            public PythonDecoderFallback() { }

            public PythonDecoderFallback(string encoding, IList<byte> data, Func<object> lookup) {
                this.encoding = encoding;
                this.data = data;
                this.lookup = lookup;
            }

            public override DecoderFallbackBuffer CreateFallbackBuffer() {
                if (function == null && lookup != null) {
                    function = lookup.Invoke();
                }
                return new PythonDecoderFallbackBuffer(encoding, data, function);
            }

            public override int MaxCharCount {
                get { throw new NotImplementedException(); }
            }
        }

        private static string CheckReplacementTuple(object res, string encodeOrDecode, int cursorPos) {
            // verify the result is sane...
            if (res is PythonTuple tres && tres.__len__() == 2
                    && Converter.TryConvertToString(tres[0], out string replacement)
                    && Converter.TryConvertToInt32(tres[1], out int newPos)) {
                if (newPos != cursorPos) throw new NotImplementedException($"Moving {encodeOrDecode} cursor not implemented yet");
                return replacement;
            }

            throw PythonOps.TypeError("{1} error handler must return tuple containing (str, int), got {0}", PythonOps.GetPythonTypeName(res), encodeOrDecode);
        }

        private class BackslashEncoderReplaceFallback : EncoderFallback {
            private class BackslashReplaceFallbackBuffer : EncoderFallbackBuffer {
                private List<char> _buffer = new List<char>();
                private int _index;

                public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
                    return false;
                }

                public override bool Fallback(char charUnknown, int index) {
                    _buffer.Add('\\');
                    int val = charUnknown;
                    if (val > 0xFF) {
                        _buffer.Add('u');
                        AddCharacter(val >> 8);
                        AddCharacter(val & 0xFF);
                    } else {
                        _buffer.Add('x');
                        AddCharacter(charUnknown);
                    }
                    return true;
                }

                private void AddCharacter(int val) {
                    AddOneDigit(((val) & 0xF0) >> 4);
                    AddOneDigit(val & 0x0F);
                }

                private void AddOneDigit(int val) {
                    if (val > 9) {
                        _buffer.Add((char)('a' + val - 0x0A));
                    } else {
                        _buffer.Add((char)('0' + val));
                    }
                }

                public override char GetNextChar() {
                    if (_index == _buffer.Count) return Char.MinValue;

                    return _buffer[_index++];
                }

                public override bool MovePrevious() {
                    if (_index > 0) {
                        _index--;
                        return true;
                    }
                    return false;
                }

                public override int Remaining {
                    get { return _buffer.Count - _index; }
                }
            }

            public override EncoderFallbackBuffer CreateFallbackBuffer() {
                return new BackslashReplaceFallbackBuffer();
            }

            public override int MaxCharCount {
                get { throw new NotImplementedException(); }
            }
        }

        private class XmlCharRefEncoderReplaceFallback : EncoderFallback {
            private class XmlCharRefEncoderReplaceFallbackBuffer : EncoderFallbackBuffer {
                private List<char> _buffer = new List<char>();
                private int _index;

                public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
                    return false;
                }

                public override bool Fallback(char charUnknown, int index) {
                    _buffer.Add('&');
                    _buffer.Add('#');
                    int val = (int)charUnknown;
                    foreach (char c in val.ToString()) {
                        _buffer.Add(c);
                    }
                    _buffer.Add(';');
                    return true;
                }

                public override char GetNextChar() {
                    if (_index == _buffer.Count) return Char.MinValue;

                    return _buffer[_index++];
                }

                public override bool MovePrevious() {
                    if (_index > 0) {
                        _index--;
                        return true;
                    }
                    return false;
                }

                public override int Remaining {
                    get { return _buffer.Count - _index; }
                }
            }

            public override EncoderFallbackBuffer CreateFallbackBuffer() {
                return new XmlCharRefEncoderReplaceFallbackBuffer();
            }

            public override int MaxCharCount {
                get { throw new NotImplementedException(); }
            }
        }

        // This is an equivalent of System.Text.DecoderExceptionFallback
        // except for the custom error message and a workaround for a UTF-8 bug.
        // It is **not** suitable for use if the following Encoding methods are being employed:
        // * GetChars(Byte[], Int32, Int32, Char[], Int32)
        // * GetChars(Byte*, Int32, Char*, Int32)
        private class ExceptionFallback : DecoderFallback {
            private bool _isUtf8;

            public ExceptionFallback(bool isUtf8 = false) {
                _isUtf8 = isUtf8;
            }

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => _isUtf8 && PythonEncoding.HasBugCorefx29898 ? new ExceptionFallbackBufferUtf8DotNet() : new ExceptionFallbackBuffer();

            public override int MaxCharCount => 0;
        }

        private class ExceptionFallbackBuffer : DecoderFallbackBuffer {

            public override bool Fallback(byte[] bytesUnknown, int index) {
                throw PythonOps.UnicodeDecodeError($"invalid bytes at index: {index}", bytesUnknown, index);
            }

            public override char GetNextChar() => '\0';

            public override bool MovePrevious() => false;

            public override int Remaining => 0;
        }

        // This class can be removed as soon as workaround for utf8 encoding in .net is
        // no longer necessary.
        private class ExceptionFallbackBufferUtf8DotNet : ExceptionFallbackBuffer {

            public override bool Fallback(byte[] bytesUnknown, int index) {
                // In case of .NET and utf-8 value of index does not conform to documentation provided by
                // Microsoft http://msdn.microsoft.com/en-us/library/bdftay9c%28v=vs.100%29.aspx
                // The value of index is mysteriously decreased by the size of bytesUnknown
                // This only happends for GetCharCount calls, the actual decoding by GetChars works fine,
                // however many GetChar overloads call GetCharCount implictly before starting decoding.
                // Tested on Windows 7 64, .NET 4.0.30319.18408, all recommended patches as of 06.02.2014
                // Bug also present in .NET Core 2.1, 2.2, but fixed in 3.0

                index = index + bytesUnknown.Length;
                return base.Fallback(bytesUnknown, index);
            }
        }

        private static object StrictErrors(object unicodeError) {
            if (unicodeError is PythonExceptions._UnicodeTranslateError ute) {
                throw ute.GetClrException();
            }
            if (unicodeError is PythonExceptions.BaseException be) {
                unicodeError = be.GetClrException();
            }
            switch (unicodeError) {
                case DecoderFallbackException dfe: throw dfe;
                case EncoderFallbackException efe: throw efe;
                default: throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        private static object IgnoreErrors(object unicodeError) {
            switch (unicodeError) {
                case PythonExceptions._UnicodeDecodeError ude:
                    return PythonTuple.MakeTuple(string.Empty, ude.end);
                case PythonExceptions._UnicodeEncodeError uee:
                    return PythonTuple.MakeTuple(string.Empty, uee.end);
                case PythonExceptions._UnicodeTranslateError ute:
                    return PythonTuple.MakeTuple(string.Empty, ute.end);
                case DecoderFallbackException dfe: 
                    return PythonTuple.MakeTuple(string.Empty, dfe.Index + dfe.BytesUnknown?.Length ?? 0);
                case EncoderFallbackException efe: 
                    return PythonTuple.MakeTuple(string.Empty, efe.Index + (efe.CharUnknownHigh != '\0' ? 2 : 1));
                default:
                    throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        private static object ReplaceErrors(object unicodeError) {
            switch (unicodeError) {
                case PythonExceptions._UnicodeDecodeError ude:
                    return PythonTuple.MakeTuple("\ufffd", ude.end);

                case PythonExceptions._UnicodeEncodeError uee: {
                        if (uee.@object is string text && uee.start is int start && uee.end is int end) {
                            start = Math.Max(0, Math.Min(start, text.Length - 1));
                            end = Math.Max(start, Math.Min(end, text.Length));
                            return PythonTuple.MakeTuple(new string('?', end - start), end);
                        }
                    }
                    goto default;

                case PythonExceptions._UnicodeTranslateError ute: {
                        if (ute.@object is string text && ute.start is int start && ute.end is int end) {
                            start = Math.Max(0, Math.Min(start, text.Length - 1));
                            end = Math.Max(start, Math.Min(end, text.Length));
                            return PythonTuple.MakeTuple(new string('\ufffd', end - start), end);
                        }
                    }
                    goto default;

                case DecoderFallbackException dfe:
                    return PythonTuple.MakeTuple("\ufffd", dfe.Index + dfe.BytesUnknown?.Length ?? 0);

                case EncoderFallbackException efe:
                    return PythonTuple.MakeTuple("?", efe.Index + (efe.CharUnknownHigh != '\0' ? 2 : 1));

                default:
                    throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        private static object XmlCharRefReplaceErrors(object unicodeError) {
            switch (unicodeError) {
                case PythonExceptions._UnicodeDecodeError ude:
                    throw PythonOps.TypeError("don't know how to handle UnicodeDecodeError in error callback");

                case PythonExceptions._UnicodeEncodeError uee:
                    if (uee.@object is string text && uee.start is int start && uee.end is int end) {
                        start = Math.Max(0, Math.Min(start, text.Length - 1));
                        end = Math.Max(start, Math.Min(end, text.Length));
                        var sb = new StringBuilder(10 * (end - start));
                        int i = start;
                        while (i < end) {
                            sb.Append("&#");
                            char ch = text[i++];
                            if (char.IsHighSurrogate(ch) && i < end && char.IsLowSurrogate(text[i])) {
                                sb.Append(char.ConvertToUtf32(ch, text[i++]));
                            } else {
                                sb.Append((uint)ch);
                            }
                            sb.Append(';');
                        }
                        return PythonTuple.MakeTuple(sb.ToString(), end);
                    }
                    goto default;

                case PythonExceptions._UnicodeTranslateError ute:
                    throw PythonOps.TypeError("don't know how to handle UnicodeTranslateError in error callback");

                case DecoderFallbackException dfe:
                    throw PythonOps.TypeError("don't know how to handle DecoderFallbackException in error callback");

                case EncoderFallbackException efe:
                    string chars = (efe.CharUnknownHigh != '\0') ? $"&#{char.ConvertToUtf32(efe.CharUnknownHigh, efe.CharUnknownLow)}" : $"&#{(int)efe.CharUnknown};";
                    return PythonTuple.MakeTuple(chars, efe.Index + (efe.CharUnknownHigh != '\0' ? 2 : 1));

                default:
                    throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        private static object BackslashReplaceErrors(object unicodeError) {
            switch (unicodeError) {
                case PythonExceptions._UnicodeDecodeError ude:
                    throw PythonOps.TypeError("don't know how to handle UnicodeDecodeError in error callback");

                case PythonExceptions._UnicodeEncodeError uee:
                    if (uee.@object is string text && uee.start is int start && uee.end is int end) {
                        start = Math.Max(0, Math.Min(start, text.Length - 1));
                        end = Math.Max(start, Math.Min(end, text.Length));
                        return PythonTuple.MakeTuple(RawUnicodeEscapeEncode(text, start, end - start, escapeAscii: true), end);
                    }
                    goto default;

                case PythonExceptions._UnicodeTranslateError ute:
                    throw PythonOps.TypeError("don't know how to handle UnicodeTranslateError in error callback");

                case DecoderFallbackException dfe:
                    throw PythonOps.TypeError("don't know how to handle DecoderFallbackException in error callback");

                case EncoderFallbackException efe:
                    string chars = (efe.CharUnknownHigh != '\0') ? new string(new[] { efe.CharUnknownHigh, efe.CharUnknownLow }) : new string(efe.CharUnknown, 1);
                    return PythonTuple.MakeTuple(RawUnicodeEscapeEncode(chars, 0, chars.Length, escapeAscii: true), efe.Index + chars.Length);

                default:
                    throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        private delegate string DecodeErrorHandler(IList<byte> bytes, int start, ref int end);
        private delegate Bytes  EncodeErrorHandler(string text, int start, ref int end);

        private static object SurrogateEscapeErrors(object unicodeError) {
            return SurrogateErrorsImpl(unicodeError, surrogateEscapeDecode, surrogateEscapeEncode);

            static string surrogateEscapeDecode(IList<byte> bytes, int start, ref int end) {
                var sb = new StringBuilder(end - start);
                for (int i = start; i < end; i++) {
                    byte b = bytes[i];
                    if (b < 0x80) {
                        if (i > start) break;
                        else return null;
                    }
                    sb.Append((char)(b | 0xDC00));
                }
                string res = sb.ToString();
                end = start + res.Length;
                return res;
            }

            static Bytes surrogateEscapeEncode(string text, int start, ref int end) {
                var lst = new List<byte>(end - start);
                for (int i = start; i < end; i++) {
                    char c = text[i];
                    if (!char.IsLowSurrogate(c)) return null;
                    byte b = (byte)(c & 0xFF);
                    if (b < 0x80) return null;
                    lst.Add(b);
                }
                return new Bytes(lst);
            }
        }

        private static object SurrogatePassErrors(object unicodeError) {
            const byte Utf8LeadByte = 0b_1110_0000;
            const byte Utf8LeadBytePayload = 0b_1111;
            const byte Utf8ContByte = 0b_10_000000;
            const byte Utf8ContBytePayload = 0b_111111;

            int charWidth = 1;
            bool isBigEndian = false;
            if (unicodeError is PythonExceptions._UnicodeDecodeError ude) {
                if (ude.encoding is string encodingName) {
                    IdentifyUtfEncoding(encodingName, out charWidth, out isBigEndian);
                }
            } else if (unicodeError is PythonExceptions._UnicodeEncodeError uee) {
                if (uee.encoding is string encodingName) {
                    IdentifyUtfEncoding(encodingName, out charWidth, out isBigEndian);
                }
            }
            return SurrogateErrorsImpl(unicodeError, surrogatePassDecode, surrogatePassEncode);

            string surrogatePassDecode(IList<byte> bytes, int start, ref int end) {
                end = start + (charWidth == 1 ? 3 : charWidth); // UTF-8 uses 3 bytes to encode a surrogate
                if (end > bytes.Count) return null;

                char c;
                // decode one character only
                if (charWidth == 1) {  // UTF-8
                    byte b;
                    int codepoint;

                    b = bytes[start++];
                    if ((b & ~Utf8LeadBytePayload) != Utf8LeadByte) return null;
                    codepoint = b & Utf8LeadBytePayload;

                    b = bytes[start++];
                    if ((b & ~Utf8ContBytePayload) != Utf8ContByte) return null;
                    codepoint = (b & Utf8ContBytePayload) | (codepoint << 6);

                    b = bytes[start++];
                    if ((b & ~Utf8ContBytePayload) != Utf8ContByte) return null;
                    codepoint = (b & Utf8ContBytePayload) | (codepoint << 6);

                    c = (char)codepoint;

                } else if (isBigEndian) {
                    if (charWidth == 4) {  // UTF-32BE
                        if (bytes[start++] != 0) return null;
                        if (bytes[start++] != 0) return null;
                    }
                    c = (char)(bytes[start++] << 8 | bytes[start++]);

                } else {
                    c = (char)(bytes[start++] | bytes[start++] << 8);
                    if (charWidth == 4) {  // UTF-32LE
                        if (bytes[start++] != 0) return null;
                        if (bytes[start++] != 0) return null;
                    }
                }
                Debug.Assert(start == end);

                if (!char.IsSurrogate(c)) return null;
                return new string(c, 1);
            }

            Bytes surrogatePassEncode(string text, int start, ref int end) {
                var lst = new List<byte>((end - start) * (charWidth == 1 ? 3 : charWidth)); // UTF-8 uses 3 bytes to encode a surrogate

                for (int i = start; i < end; i++) {
                    char c = text[i];
                    if (!char.IsSurrogate(c)) return null;

                    if (charWidth == 1) { // UTF-8
                        lst.Add((byte)(Utf8LeadByte | c >> 12));
                        lst.Add((byte)(Utf8ContByte | ((c >> 6) & Utf8ContBytePayload)));
                        lst.Add((byte)(Utf8ContByte | (c & Utf8ContBytePayload)));
                    } else { // UTF-16 or UTF-32
                        if (isBigEndian) {
                            if (charWidth == 4) { // UTF-32
                                lst.Add(0);
                                lst.Add(0);
                            }
                            lst.Add((byte)(c >> 8));
                            lst.Add((byte)(c & 0xFF));
                        } else {
                            lst.Add((byte)(c & 0xFF));
                            lst.Add((byte)(c >> 8));
                            if (charWidth == 4) { // UTF-32
                                lst.Add(0);
                                lst.Add(0);
                            }
                        }
                    }
                }
                return new Bytes(lst);
            }
        }

        private static object SurrogateErrorsImpl(object unicodeError, DecodeErrorHandler decodeFallback, EncodeErrorHandler encodeFallback) {
            switch (unicodeError) {
                case PythonExceptions._UnicodeDecodeError ude:
                    if (ude.@object is IList<byte> bytes && ude.start is int bstart && ude.end is int bend) {
                        bstart = Math.Max(0, Math.Min(bstart, bytes.Count - 1));
                        bend = Math.Max(bstart, Math.Min(bend, bytes.Count));
                        string res = decodeFallback(bytes, bstart, ref bend);
                        if (res == null) throw ude.GetClrException();
                        return PythonTuple.MakeTuple(res, bend);
                    }
                    goto default;

                case PythonExceptions._UnicodeEncodeError uee:
                    if (uee.@object is string text && uee.start is int tstart && uee.end is int tend) {
                        tstart = Math.Max(0, Math.Min(tstart, text.Length - 1));
                        tend = Math.Max(tstart, Math.Min(tend, text.Length));
                        Bytes res = encodeFallback(text, tstart, ref tend);
                        if (res == null) throw uee.GetClrException();
                        return PythonTuple.MakeTuple(res, tend);
                    }
                    goto default;

                case PythonExceptions._UnicodeTranslateError ute:
                    throw PythonOps.TypeError("don't know how to handle UnicodeTranslateError in error callback");

                case DecoderFallbackException dfe: {
                        if (dfe.BytesUnknown == null) throw dfe;
                        int end = dfe.BytesUnknown.Length;
                        string res = decodeFallback(dfe.BytesUnknown, 0, ref end);
                        if (res == null) throw dfe;
                        return PythonTuple.MakeTuple(res,  dfe.Index + end);
                    }

                case EncoderFallbackException efe: {
                        string chars = new string(efe.CharUnknown, 1);
                        int end = chars.Length;
                        Bytes res = encodeFallback(chars, 0, ref end);
                        return PythonTuple.MakeTuple(res, efe.Index + end);
                    }

                default:
                    throw PythonOps.TypeError("codec must pass exception instance");
            }
        }

        internal static void IdentifyUtfEncoding(string encodingName, out int charWidth, out bool isBigEndian) {
            charWidth = 1;
            isBigEndian = false;
            if (encodingName != null && encodingName.StartsWith("utf", StringComparison.OrdinalIgnoreCase)) {
                int idx = 3;
                int end = encodingName.Length;
                if (idx < end && (encodingName[idx] == '-' || encodingName[idx] == '_')) {
                    idx++;
                }
                if (idx + 1 < end) {
                    if (encodingName[idx] == '3' && encodingName[idx + 1] == '2') {
                        charWidth = 4;
                        idx += 2;
                    } else if (encodingName[idx] == '1' && encodingName[idx + 1] == '6') {
                        charWidth = 2;
                        idx += 2;
                    } else {
                        return; // UTF-8, UTF-7, or unrecognized
                    }
                    if (idx < end && (encodingName[idx] == '-' || encodingName[idx] == '_')) {
                        idx++;
                        if (idx + 2 > end) { // missing endianness suffix
                            charWidth = 1; // fall back to single character
                            return;
                        }
                    }
                    if (idx < end) {
                        if (encodingName.Substring(idx).Equals("be", StringComparison.OrdinalIgnoreCase)) {
                            isBigEndian = true;
                        } else if (!encodingName.Substring(idx).Equals("le", StringComparison.OrdinalIgnoreCase)) { // incorect suffix
                            charWidth = 1; // fall back to single character
                            return;
                        }
                    }
                }
            }
        }
#endif

        #endregion
    }
}
