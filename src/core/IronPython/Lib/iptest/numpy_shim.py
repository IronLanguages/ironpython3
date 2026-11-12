# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""A minimal stand-in for NumPy, for running tests ported from NumPy against CLR arrays.

Ported tests import it as `from iptest import numpy_shim as np`. The NumPy-like functions that
IronPython implements on `System.Array` are re-exported unchanged; everything else here (array
creation, dtypes, `testing`) is a test helper written in Python, not a part of IronPython's API.

Differences from NumPy that ported tests have to take into account:

* dtypes are CLR types (`System.Type`); `dtype_of(x)` replaces `x.dtype`.
* The default integer dtype is `BigInteger`, the CLR type of Python `int` (as in `Array[int]`).
* There are no 0-d arrays.
* Values of CLR types `Boolean`, `Int32`, `Double` and `Complex` are Python scalars, hence "weak"
  in type promotion, unlike NumPy scalars `np.bool`, `np.int32`, `np.float64` and `np.complex128`.
* Arrays are not views: functions returning arrays always return new arrays.
* All arrays are writeable; there are no read-only arrays (no `flags.writeable`).
* Operators `+` and `*` on arrays are not element-wise (in the default mode); use `add` and `multiply`.
* `float16` (`System.Half`) is only available on .NET 7 and later (`HAS_FLOAT16`). Its values
  are created with `clr.Convert(value, np.float16)`, as `System.Half` cannot be called with a number.
"""

import itertools
import unittest

import clr
import System
from System import Array

# ---- NumPy-like functions implemented by IronPython

add = Array.add
multiply = Array.multiply

# ---- dtypes

bool_ = System.Boolean
int8 = System.SByte
int16 = System.Int16
int32 = System.Int32
int64 = System.Int64
uint8 = System.Byte
uint16 = System.UInt16
uint32 = System.UInt32
uint64 = System.UInt64
float32 = System.Single
float64 = System.Double
complex128 = complex

HAS_FLOAT16 = hasattr(System, 'Half')
if HAS_FLOAT16:
    float16 = System.Half

_typecodes = {
    '?': System.Boolean,
    'b': System.SByte, 'i1': System.SByte,
    'h': System.Int16, 'i2': System.Int16,
    'i': System.Int32, 'i4': System.Int32,
    'l': System.Int64, 'q': System.Int64, 'i8': System.Int64,
    'B': System.Byte, 'u1': System.Byte,
    'H': System.UInt16, 'u2': System.UInt16,
    'I': System.UInt32, 'u4': System.UInt32,
    'L': System.UInt64, 'Q': System.UInt64, 'u8': System.UInt64,
    'f': System.Single, 'f4': System.Single,
    'd': System.Double, 'f8': System.Double,
    'D': complex, 'c16': complex,
    'O': object,
}
if HAS_FLOAT16:
    _typecodes.update({'e': float16, 'f2': float16})

def dtype(obj):
    """Return the CLR type for a dtype-like object: a type or a NumPy type code."""
    if isinstance(obj, System.Type):
        return obj
    if isinstance(obj, str):
        obj = _typecodes[obj]
    return clr.GetClrType(obj)

_dtype = dtype

def dtype_of(x):
    """Return the element type of an array, or the CLR type of a scalar (NumPy: `x.dtype`)."""
    if isinstance(x, Array):
        return x.GetType().GetElementType()
    return x.GetType()

# ---- array creation and inspection

def shape(a):
    if not isinstance(a, Array):
        return ()
    return tuple(a.GetLength(d) for d in range(a.Rank))

_shape = shape

def ndim(a):
    return len(shape(a))

def size(a):
    res = 1
    for n in shape(a):
        res *= n
    return res

def ndindex(*shape):
    if len(shape) == 1 and isinstance(shape[0], tuple):
        shape = shape[0]
    return itertools.product(*(range(n) for n in shape))

def _setitem(a, idx, value):
    # CLR arrays do not accept a tuple key in assignments, only separate indices (as in a[i, j] = value)
    Array.__setitem__(a, *idx, value)

def _normalize_shape(shape):
    return (shape,) if isinstance(shape, int) else tuple(shape)

def _infer_shape(obj):
    if isinstance(obj, Array):
        return _shape(obj)
    if isinstance(obj, (list, tuple)):
        return (len(obj),) + (_infer_shape(obj[0]) if obj else ())
    return ()

def _flatten(obj):
    if isinstance(obj, Array):
        return [obj[idx] for idx in ndindex(_shape(obj))]
    if isinstance(obj, (list, tuple)):
        return [item for sub in obj for item in _flatten(sub)]
    return [obj]

def _infer_dtype(obj):
    """Infer the dtype of a new array from its contents, like NumPy does for Python scalars.

    Nested arrays contribute their element type; Python scalars are typed by their kind.
    """
    kinds = (System.Boolean, int, System.Double, complex)
    category = -1
    clr_types = set()

    def visit(o):
        nonlocal category
        if isinstance(o, Array):
            clr_types.add(dtype_of(o))
        elif isinstance(o, (list, tuple)):
            for item in o:
                visit(item)
        elif isinstance(o, (System.Boolean, int, float, complex)):
            # bool is also an int, so the first match is the narrowest kind
            category = max(category, next(i for i, k in enumerate(kinds) if isinstance(o, k)))
        else:
            clr_types.add(clr.GetClrType(object if o is None else type(o)))

    visit(obj)
    if not clr_types:
        return _dtype(kinds[category]) if category >= 0 else _dtype(float64)
    if len(clr_types) == 1 and category < 0:
        return clr_types.pop()
    return clr.GetClrType(object)

def empty(shape, dtype=float64):
    shape = _normalize_shape(shape)
    if not shape:
        raise NotImplementedError("0-d arrays are not supported")
    return Array.CreateInstance(_dtype(dtype), *shape)

def full(shape, fill_value, dtype=None):
    if dtype is None:
        dtype = _infer_dtype(fill_value)
    res = empty(shape, dtype)
    for idx in ndindex(_shape(res)):
        _setitem(res, idx, fill_value)
    return res

def zeros(shape, dtype=float64):
    return full(shape, 0, dtype)

def ones(shape, dtype=float64):
    return full(shape, 1, dtype)

def array(object, dtype=None):
    """Create a new (0-based) array from an array or nested sequences of scalars."""
    shape = _infer_shape(object)
    if dtype is None:
        dtype = _infer_dtype(object)
    res = empty(shape, dtype)
    for idx, item in zip(ndindex(shape), _flatten(object)):
        _setitem(res, idx, item)
    return res

def asarray(a, dtype=None):
    if isinstance(a, Array) and (dtype is None or _dtype(dtype) == dtype_of(a)):
        return a
    return array(a, dtype)

def arange(stop, dtype=None):
    return array(list(range(stop)), dtype)

def reshape(a, newshape):
    newshape = _normalize_shape(newshape)
    res = empty(newshape, dtype_of(a))
    for idx, item in zip(ndindex(newshape), _flatten(a)):
        _setitem(res, idx, item)
    return res

def broadcast_to(a, shape):
    a = asarray(a)
    shape = _normalize_shape(shape)
    ashape = _shape(a)
    offset = len(shape) - len(ashape)
    if offset < 0 or any(n != 1 and n != shape[d + offset] for d, n in enumerate(ashape)):
        raise ValueError("cannot broadcast shape %r to %r" % (ashape, shape))
    res = empty(shape, dtype_of(a))
    for idx in ndindex(shape):
        _setitem(res, idx, a[tuple(0 if n == 1 else idx[d + offset] for d, n in enumerate(ashape))])
    return res

def tolist(a):
    """Convert an array to nested lists (NumPy: `a.tolist()`)."""
    def sub(prefix, dims):
        if not dims:
            return a[prefix]
        return [sub(prefix + (i,), dims[1:]) for i in range(dims[0])]
    return sub((), _shape(a)) if isinstance(a, Array) else a

# ---- numpy.testing

class testing:
    """The subset of `numpy.testing` used by the ported tests."""

    @staticmethod
    def assert_(val, msg=''):
        if not val:
            raise AssertionError(msg)

    @staticmethod
    def assert_equal(actual, desired, err_msg=''):
        if isinstance(actual, Array) or isinstance(desired, Array):
            testing.assert_array_equal(actual, desired, err_msg)
        elif actual != desired:
            raise AssertionError("%r != %r %s" % (actual, desired, err_msg))

    @staticmethod
    def assert_array_equal(actual, desired, err_msg=''):
        """Compare shapes and elements; a scalar is compared with every element of the other side."""
        ashape, dshape = _infer_shape(actual), _infer_shape(desired)
        if ashape and dshape and ashape != dshape:
            raise AssertionError("shapes %r and %r mismatch %s" % (ashape, dshape, err_msg))
        aitems, ditems = _flatten(actual), _flatten(desired)
        if not ashape:
            aitems = aitems * len(ditems)
        if not dshape:
            ditems = ditems * len(aitems)
        if aitems != ditems:
            raise AssertionError("arrays are not equal: %r != %r %s" % (tolist(actual), tolist(desired), err_msg))

    @staticmethod
    def assert_raises(exception_class, *args, **kwargs):
        """Call `args[0](*args[1:], **kwargs)` expecting an exception, or return a context manager."""
        return unittest.TestCase().assertRaises(exception_class, *args, **kwargs)
