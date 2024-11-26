// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Threading;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Types {

    [PythonType("ellipsis"), Documentation(null)]
    public sealed class Ellipsis : ICodeFormattable {
        private static Ellipsis? _instance;
        
        private Ellipsis() { }

        internal static Ellipsis Value {
            get {
                if (_instance == null) {
                    Interlocked.CompareExchange(ref _instance, new Ellipsis(), null);
                }
                return _instance;
            }
        }

        public static Ellipsis __new__(CodeContext/*!*/ context, [NotNone] PythonType cls) {
            if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(Ellipsis)))
                return Value;
            throw PythonOps.TypeError("{0} is not a subtype of ellipsis", cls.Name);
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return "Ellipsis";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int __hash__() {
            return 0x1e1a6208;
        }

        #endregion
    }

    [PythonType("NotImplementedType"), Documentation(null)]
    public sealed class NotImplementedType : ICodeFormattable {
        private static NotImplementedType? _instance;
        
        private NotImplementedType() { }
        
        public static NotImplementedType Value {
            get {
                if (_instance == null) {
                    Interlocked.CompareExchange(ref _instance, new NotImplementedType(), null);
                }
                return _instance;
            }
        }

        public static NotImplementedType __new__(CodeContext/*!*/ context, [NotNone] PythonType cls) {
            if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(NotImplementedType)))
                return Value;
            throw PythonOps.TypeError("{0} is not a subtype of NotImplementedType", cls.Name);
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return "NotImplemented";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int __hash__() {
            return 0x1e1a1e98;
        }

        #endregion
    }

    public sealed class NoneTypeOps {
        internal const int NoneHashCode = 0x1e1a2e40;

        [StaticExtensionMethod]
        public static object? __new__(CodeContext/*!*/ context, [NotNone] PythonType cls) {
            if (cls == TypeCache.Null)
                return null;
            throw PythonOps.TypeError("{0} is not a subtype of NoneType", cls.Name);
        }

        public static int __hash__(DynamicNull self) {
            return NoneHashCode;
        }

        public static readonly string? __doc__;

        public static string __repr__(DynamicNull self) {
            return "None";
        }
    }
}
