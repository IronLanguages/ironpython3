// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime.Types;

namespace IronPython.Modules {
    public static partial class PythonWeakRef {
        public sealed partial class weakproxy {

            #region Generated WeakRef Operators Initialization

            // *** BEGIN GENERATED CODE ***
            // generated by function: weakref_operators from: generate_ops.py

            [SlotField] public static PythonTypeSlot __add__ = new SlotWrapper("__add__", ProxyType);
            [SlotField] public static PythonTypeSlot __radd__ = new SlotWrapper("__radd__", ProxyType);
            [SlotField] public static PythonTypeSlot __iadd__ = new SlotWrapper("__iadd__", ProxyType);
            [SlotField] public static PythonTypeSlot __sub__ = new SlotWrapper("__sub__", ProxyType);
            [SlotField] public static PythonTypeSlot __rsub__ = new SlotWrapper("__rsub__", ProxyType);
            [SlotField] public static PythonTypeSlot __isub__ = new SlotWrapper("__isub__", ProxyType);
            [SlotField] public static PythonTypeSlot __pow__ = new SlotWrapper("__pow__", ProxyType);
            [SlotField] public static PythonTypeSlot __rpow__ = new SlotWrapper("__rpow__", ProxyType);
            [SlotField] public static PythonTypeSlot __ipow__ = new SlotWrapper("__ipow__", ProxyType);
            [SlotField] public static PythonTypeSlot __mul__ = new SlotWrapper("__mul__", ProxyType);
            [SlotField] public static PythonTypeSlot __rmul__ = new SlotWrapper("__rmul__", ProxyType);
            [SlotField] public static PythonTypeSlot __imul__ = new SlotWrapper("__imul__", ProxyType);
            [SlotField] public static PythonTypeSlot __matmul__ = new SlotWrapper("__matmul__", ProxyType);
            [SlotField] public static PythonTypeSlot __rmatmul__ = new SlotWrapper("__rmatmul__", ProxyType);
            [SlotField] public static PythonTypeSlot __imatmul__ = new SlotWrapper("__imatmul__", ProxyType);
            [SlotField] public static PythonTypeSlot __floordiv__ = new SlotWrapper("__floordiv__", ProxyType);
            [SlotField] public static PythonTypeSlot __rfloordiv__ = new SlotWrapper("__rfloordiv__", ProxyType);
            [SlotField] public static PythonTypeSlot __ifloordiv__ = new SlotWrapper("__ifloordiv__", ProxyType);
            [SlotField] public static PythonTypeSlot __truediv__ = new SlotWrapper("__truediv__", ProxyType);
            [SlotField] public static PythonTypeSlot __rtruediv__ = new SlotWrapper("__rtruediv__", ProxyType);
            [SlotField] public static PythonTypeSlot __itruediv__ = new SlotWrapper("__itruediv__", ProxyType);
            [SlotField] public static PythonTypeSlot __mod__ = new SlotWrapper("__mod__", ProxyType);
            [SlotField] public static PythonTypeSlot __rmod__ = new SlotWrapper("__rmod__", ProxyType);
            [SlotField] public static PythonTypeSlot __imod__ = new SlotWrapper("__imod__", ProxyType);
            [SlotField] public static PythonTypeSlot __lshift__ = new SlotWrapper("__lshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __rlshift__ = new SlotWrapper("__rlshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __ilshift__ = new SlotWrapper("__ilshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __rshift__ = new SlotWrapper("__rshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __rrshift__ = new SlotWrapper("__rrshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __irshift__ = new SlotWrapper("__irshift__", ProxyType);
            [SlotField] public static PythonTypeSlot __and__ = new SlotWrapper("__and__", ProxyType);
            [SlotField] public static PythonTypeSlot __rand__ = new SlotWrapper("__rand__", ProxyType);
            [SlotField] public static PythonTypeSlot __iand__ = new SlotWrapper("__iand__", ProxyType);
            [SlotField] public static PythonTypeSlot __or__ = new SlotWrapper("__or__", ProxyType);
            [SlotField] public static PythonTypeSlot __ror__ = new SlotWrapper("__ror__", ProxyType);
            [SlotField] public static PythonTypeSlot __ior__ = new SlotWrapper("__ior__", ProxyType);
            [SlotField] public static PythonTypeSlot __xor__ = new SlotWrapper("__xor__", ProxyType);
            [SlotField] public static PythonTypeSlot __rxor__ = new SlotWrapper("__rxor__", ProxyType);
            [SlotField] public static PythonTypeSlot __ixor__ = new SlotWrapper("__ixor__", ProxyType);

            // *** END GENERATED CODE ***

            #endregion

            [SlotField] public static PythonTypeSlot __eq__ = new SlotWrapper("__eq__", ProxyType);
            [SlotField] public static PythonTypeSlot __ne__ = new SlotWrapper("__ne__", ProxyType);
            [SlotField] public static PythonTypeSlot __lt__ = new SlotWrapper("__lt__", ProxyType);
            [SlotField] public static PythonTypeSlot __gt__ = new SlotWrapper("__gt__", ProxyType);
            [SlotField] public static PythonTypeSlot __le__ = new SlotWrapper("__le__", ProxyType);
            [SlotField] public static PythonTypeSlot __ge__ = new SlotWrapper("__ge__", ProxyType);
            [SlotField] public static PythonTypeSlot __divmod__ = new SlotWrapper("__divmod__", ProxyType);
            [SlotField] public static PythonTypeSlot __float__ = new SlotWrapper("__float__", ProxyType);
            [SlotField] public static PythonTypeSlot __index__ = new SlotWrapper("__index__", ProxyType);
            [SlotField] public static PythonTypeSlot __int__ = new SlotWrapper("__int__", ProxyType);
            [SlotField] public static PythonTypeSlot __iter__ = new SlotWrapper("__iter__", ProxyType);
            [SlotField] public static PythonTypeSlot __rdivmod__ = new SlotWrapper("__rdivmod__", ProxyType);
            [SlotField] public static PythonTypeSlot __next__ = new SlotWrapper("__next__", ProxyType);

            [SlotField] public static PythonTypeSlot __getitem__ = new SlotWrapper("__getitem__", ProxyType);
            [SlotField] public static PythonTypeSlot __setitem__ = new SlotWrapper("__setitem__", ProxyType);
            [SlotField] public static PythonTypeSlot __delitem__ = new SlotWrapper("__delitem__", ProxyType);
            [SlotField] public static PythonTypeSlot __len__ = new SlotWrapper("__len__", ProxyType);
            [SlotField] public static PythonTypeSlot __pos__ = new SlotWrapper("__pos__", ProxyType);
            [SlotField] public static PythonTypeSlot __neg__ = new SlotWrapper("__neg__", ProxyType);
            [SlotField] public static PythonTypeSlot __invert__ = new SlotWrapper("__invert__", ProxyType);
            [SlotField] public static PythonTypeSlot __contains__ = new SlotWrapper("__contains__", ProxyType);
            [SlotField] public static PythonTypeSlot __abs__ = new SlotWrapper("__abs__", ProxyType);
        }


        public sealed partial class weakcallableproxy {
            #region Generated WeakRef Callable Proxy Operators Initialization

            // *** BEGIN GENERATED CODE ***
            // generated by function: weakrefCallabelProxy_operators from: generate_ops.py

            [SlotField] public static PythonTypeSlot __add__ = new SlotWrapper("__add__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __radd__ = new SlotWrapper("__radd__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __iadd__ = new SlotWrapper("__iadd__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __sub__ = new SlotWrapper("__sub__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rsub__ = new SlotWrapper("__rsub__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __isub__ = new SlotWrapper("__isub__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __pow__ = new SlotWrapper("__pow__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rpow__ = new SlotWrapper("__rpow__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ipow__ = new SlotWrapper("__ipow__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __mul__ = new SlotWrapper("__mul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rmul__ = new SlotWrapper("__rmul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __imul__ = new SlotWrapper("__imul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __matmul__ = new SlotWrapper("__matmul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rmatmul__ = new SlotWrapper("__rmatmul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __imatmul__ = new SlotWrapper("__imatmul__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __floordiv__ = new SlotWrapper("__floordiv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rfloordiv__ = new SlotWrapper("__rfloordiv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ifloordiv__ = new SlotWrapper("__ifloordiv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __truediv__ = new SlotWrapper("__truediv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rtruediv__ = new SlotWrapper("__rtruediv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __itruediv__ = new SlotWrapper("__itruediv__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __mod__ = new SlotWrapper("__mod__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rmod__ = new SlotWrapper("__rmod__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __imod__ = new SlotWrapper("__imod__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __lshift__ = new SlotWrapper("__lshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rlshift__ = new SlotWrapper("__rlshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ilshift__ = new SlotWrapper("__ilshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rshift__ = new SlotWrapper("__rshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rrshift__ = new SlotWrapper("__rrshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __irshift__ = new SlotWrapper("__irshift__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __and__ = new SlotWrapper("__and__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rand__ = new SlotWrapper("__rand__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __iand__ = new SlotWrapper("__iand__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __or__ = new SlotWrapper("__or__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ror__ = new SlotWrapper("__ror__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ior__ = new SlotWrapper("__ior__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __xor__ = new SlotWrapper("__xor__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rxor__ = new SlotWrapper("__rxor__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ixor__ = new SlotWrapper("__ixor__", CallableProxyType);

            // *** END GENERATED CODE ***

            #endregion

            [SlotField] public static PythonTypeSlot __eq__ = new SlotWrapper("__eq__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ne__ = new SlotWrapper("__ne__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __lt__ = new SlotWrapper("__lt__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __gt__ = new SlotWrapper("__gt__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __le__ = new SlotWrapper("__le__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __ge__ = new SlotWrapper("__ge__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __divmod__ = new SlotWrapper("__divmod__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __float__ = new SlotWrapper("__float__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __index__ = new SlotWrapper("__index__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __int__ = new SlotWrapper("__int__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __iter__ = new SlotWrapper("__iter__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __rdivmod__ = new SlotWrapper("__rdivmod__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __next__ = new SlotWrapper("__next__", CallableProxyType);

            [SlotField] public static PythonTypeSlot __getitem__ = new SlotWrapper("__getitem__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __setitem__ = new SlotWrapper("__setitem__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __delitem__ = new SlotWrapper("__delitem__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __len__ = new SlotWrapper("__len__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __pos__ = new SlotWrapper("__pos__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __neg__ = new SlotWrapper("__neg__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __invert__ = new SlotWrapper("__invert__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __contains__ = new SlotWrapper("__contains__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __abs__ = new SlotWrapper("__abs__", CallableProxyType);
            [SlotField] public static PythonTypeSlot __call__ = new SlotWrapper("__call__", CallableProxyType);

        }
    }
}
