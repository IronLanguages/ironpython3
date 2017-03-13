#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################
from iptest.assert_util import *

import _struct

def pack(f, *v):
    return _struct.Struct(f).pack(*v)

def unpack(f, *v):
    return _struct.Struct(f).unpack(*v)

def calcsize(f):
    return _struct.Struct(f).size

def test_sanity():
    mapping = {
        'c': 'a',
        'b': ord('b'),
        'B': ord('c'),
        'h': -123,
        'H': 123,
        'i': -12345,
        'l': -123456789,
        'I': 12345,
        'L': 123456789,
        'q': -1000000000,
        'Q': 1000000000,
        'f': 3.14,
        'd': -0.3439,
        '6s': 'string',
        '15p': 'another string'
        }
    
    for (k, v) in mapping.items():
        s = pack(k, v)
        v2 = unpack(k, s)
        
        if isinstance(v, float):
            AlmostEqual(v, v2[0])
        else:
            AreEqual(v, v2[0])

    AreEqual(pack(' c\t', 'a'), 'a')
    
def test_padding_len():
    AreEqual(unpack('4xi','\x00\x01\x02\x03\x01\x00\x00\x00'), (1,))

def test_cp3092():
    for format in [ "i", "I", "l", "L"]:
        mem = "\x01\x00\x00\x00" * 8
        AreEqual(len(mem), 32)
    
        fmt = "<8" + format
        AreEqual(calcsize(fmt), 32)
        AreEqual(unpack(fmt, mem), (1,)*8)
    
        fmt = "<2" + format + "4x5" + format
        AreEqual(calcsize(fmt), 32)
        AreEqual(unpack(fmt, mem), (1,)*7)
        
        fmt = "<" + format + format + "4x5" + format
        AreEqual(calcsize(fmt), 32)
        AreEqual(unpack(fmt, mem), (1,)*7)

        fmt = "<32x"
        AreEqual(calcsize(fmt), 32)
        AreEqual(unpack(fmt, mem), ())
    
        fmt = "<28x" + format
        AreEqual(calcsize(fmt), 32)
        AreEqual(unpack(fmt, mem), (1,))
    
def test_cp9347():
    temp_list = [("2xB",    '\x00\x00\xff',             255),
                 ("4s4x",   'AAAA\x00\x00\x00\x00',     "AAAA"),
                 ("x",      '\x00'),
                 ("ix",     '\x01\x00\x00\x00\x00',     1),
                 ("ix",     '\x01\x00\x00\x80\x00',     -(2**(32-1)-1)),
                 ("xI",     '\x00\x00\x00\x00\xff\xff\xff\xff',     2**32-1),
                 ("xlx",    '\x00\x00\x00\x00x\xec\xff\xff\x00',        -5000),
                 ("LxL",    '~\x00\x00\x00\x00\x00\x00\x00~\x00\x00\x00', 126, 126),
                 ("LxxL",   '~\x00\x00\x00\x00\x00\x00\x00~\x00\x00\x00', 126, 126),
                 ("32xLL",  '\x00' *32 + '~\x00\x00\x00~\x00\x00\x00', 126, 126),
                 ("LxL8xLL", '~\x00\x00\x00\x00\x00\x00\x00~\x00\x00\x00' + '\x00'*8 + '~\x00\x00\x00'*2, 126, 126, 126, 126),
    ]
    
    for stuff in temp_list:
        format = stuff[0]
        expected_val = stuff[1]
        params = stuff[2:]
        
        actual = pack(format, *params)
        AreEqual(expected_val, actual)
        AreEqual(unpack(format, actual),
                 params)

def test_negative():
    AssertError(_struct.error, pack, 'x', 1)
    AssertError(_struct.error, unpack, 'hh', pack('h', 1))
    
    AssertError(_struct.error, pack, 'a', 1)
    
    # BUG: 1033
    # such chars should be in the leading position only
    
    for x in '=@<>!':
        AssertError(_struct.error, pack, 'h'+x+'h', 1, 2)

    AssertError(_struct.error, pack, 'c', 300)

def test_calcsize_alignment():
    '''
    TODO: Side by side test?
    '''
    struct_format = "xcbBhHiIlLqQfdspP"
    
    expected = {'lB': 5, 'PQ': 16, 'BB': 2, 'BL': 8, 'lL': 8, 'BH': 4, 'ci': 8,
                'lH': 6, 'lI': 8, 'ch': 4, 'BP': 8, 'BQ': 16, 'lP': 8,
                'lQ': 16, 'PH': 6, 'Bd': 16, 'Bf': 8, 'lb': 5, 'lc': 5,
                'Bb': 2, 'Bc': 2, 'Bl': 8, 'sd': 16, 'll': 8, 'Bh': 4, 'Bi': 8,
                'lh': 6, 'li': 8, 'fb': 5, 'cc': 2, 'Bp': 2, 'Bq': 16, 'lp': 5,
                'cb': 2, 'sI': 8, 'Bx': 2, 'lx': 5, 'qQ': 16, 'qP': 12,
                'dl': 12, 'dh': 10, 'di': 12, 'df': 12, 'dd': 16, 'db': 9,
                'dc': 9, 'BI': 8, 'sB': 2, 'qB': 9, 'dx': 9, 'qI': 12, 'qH':10,
                'qL': 12, 'dp': 9, 'dq': 16, 'qq': 16, 'qp': 9, 'qs': 9,
                'dH': 10, 'dI': 12, 'Bs': 2, 'dB': 9, 'qc': 9, 'qb': 9, 'qd': 16,
                'qx': 9, 'qi': 12, 'qh': 10, 'ph': 4, 'ql': 12, 'dP': 12, 'dQ': 16,
                'fp': 5, 'Pp': 5, 'Pq': 16, 'fq': 16, 'sH': 4, 'HP': 8, 'HQ': 16,
                'Pb': 5, 'Pc': 5, 'HH': 4, 'HI': 8, 'Pf': 8, 'HL': 8, 'HB': 3,
                'pi': 8, 'Ph': 6, 'Pi': 8, 'cq': 16, 'Pl': 8, 'Hx': 3, 'cp': 2,
                'fH': 6, 'Hs': 3, 'Hp': 3, 'Hq': 16, 'PB': 5, 'fx': 5, 'Hh': 4,
                'Hi': 8, 'Hl': 8, 'Qx': 9, 'Hb': 3, 'Hc': 3, 'pH': 4, 'PI': 8,
                'Hf': 8, 'Hd': 16, 'bd': 16, 'lf': 8, 'bf': 8, 'fI': 8, 'pQ': 16,
                'bb': 2, 'bc': 2, 'bl': 8, 'qf': 12, 'bh': 4, 'bi': 8, 'cH': 4,
                'bp': 2, 'bq': 16, 'ld': 16, 'bs': 2, 'pI': 8, 'pP': 8, 'bx': 2,
                'Ps': 5, 'bB': 2, 'bL': 8, 'cI': 8, 'bH': 4, 'bI': 8, 'sx': 2,
                'ds': 9, 'fc': 5, 'bP': 8, 'bQ': 16, 'px': 2, 'Pd': 16, 'Qd': 16,
                'xh': 4, 'xi': 8, 'xl': 8, 'cl': 8, 'xb': 2, 'xc': 2, 'sL': 8,
                'xf': 8, 'cf': 8, 'xd': 16, 'cd': 16, 'pB': 2, 'fh': 6,
                'xx': 2, 'cx': 2, 'pp': 2, 'Px': 5, 'fi': 8, 'cs': 2, 'xs': 2,
                'xp': 2, 'xq': 16, 'pL': 8, 'ps': 2, 'xH': 4, 'xI': 8,
                'lq': 16, 'xL': 8, 'cL': 8, 'xB': 2, 'cB': 2, 'sf': 8, 'PL': 8,
                'pb': 2, 'pc': 2, 'pf': 8, 'pd': 16, 'xP': 8, 'xQ': 16,
                'Ll': 8, 'pl': 8, 'ls': 5, 'fP': 8, 'hx': 3, 'QP': 12, 'hs': 3,
                'hp': 3, 'hq': 16, 'hh': 4, 'hi': 8, 'hl': 8, 'hb': 3, 'hc': 3,
                'hf': 8, 'cQ': 16, 'hd': 16, 'cP': 8, 'sc': 2, 'hP': 8,
                'hQ': 16, 'fQ': 16, 'ss': 2, 'hH': 4, 'hI': 8, 'hL': 8, 'hB': 3,
                'sq': 16, 'Ls': 5, 'Lf': 8, 'ix': 5, 'Ld': 16, 'sb': 2, 'Lb': 5,
                'Lc': 5, 'iq': 16, 'ip': 5, 'is': 5, 'Lh': 6, 'Li': 8, 'ii': 8,
                'ih': 6, 'il': 8, 'Lp': 5, 'Lq': 16, 'ic': 5, 'ib': 5,
                'id': 16, 'Lx': 5, 'if': 8, 'LB': 5, 'iQ': 16, 'iP': 8,
                'LL': 8, 'pq': 16, 'si': 8, 'LH': 6, 'LI': 8, 'iI': 8,
                'iH': 6, 'sh': 4, 'iL': 8, 'LP': 8, 'LQ': 16, 'iB': 5,
                'Qq': 16, 'Qp': 9, 'Qs': 9, 'fs': 5, 'IQ': 16, 'IP': 8,
                'sQ': 16, 'sP': 8, 'PP': 8, 'II': 8, 'IH': 6, 'Qc': 9,
                'Qb': 9, 'fd': 16, 'IL': 8, 'ff': 8, 'Qf': 12, 'Qi': 12,
                'Qh': 10, 'IB': 5, 'fl': 8, 'Ql': 12, 'QQ': 16, 'Ix': 5,
                'dL': 12, 'Iq': 16, 'Ip': 5, 'Is': 5, 'sp': 2, 'QL': 12,
                'Ii': 8, 'Ih': 6, 'fB': 5, 'QB': 9, 'Il': 8, 'sl': 8,
                'QI': 12, 'QH': 10, 'Ic': 5,'Ib': 5, 'fL': 8, 'Id': 16, 'If': 8}
    
    for x in struct_format:
        for y in struct_format:
            temp_str = str(x) + str(y)
            if is_64 and "P" in temp_str: 
                continue #CodePlex 17683 - we need to test against 64-bit CPython
            Assert(expected[temp_str] == calcsize(temp_str),
                     "_struct.Struct(" + temp_str + ").size is broken")


def test_new_init():
    """tests for calling __new__/__init__ directly on the Struct object"""
    for x in (_struct.Struct.__new__(_struct.Struct), _struct.Struct.__new__(_struct.Struct, a = 2)):        
        # state of uninitialized object...
        AreEqual(x.size, -1)
        AreEqual(x.format, None)
        AssertErrorWithMessage(_struct.error, "pack requires exactly -1 arguments", x.pack)
        AssertErrorWithMessage(_struct.error, "unpack requires a string argument of length -1", x.unpack, '')    
    
    # invalid format passed to __init__ - format string is updated but old format info is stored...
    a = _struct.Struct('c')
    try:
        a.__init__('bad')
        AssertUnreachable()
    except _struct.error as e: 
        pass
    
    AreEqual(a.format, 'bad')
    AreEqual(a.pack('1'), '1')
    AreEqual(a.unpack('1'), ('1', ))
    
    # and then back to a valid format
    a.__init__('i')
    AreEqual(a.format, 'i')
    AreEqual(a.pack(0), '\x00\x00\x00\x00')
    AreEqual(a.unpack('\x00\x00\x00\x00'), (0, ))

@skip("silverlight") # no weak refs on Silverlight
def test_weakref():
    """weakrefs to struct objects are supported"""
    x = _struct.Struct('i')
    import _weakref
    AreEqual(_weakref.proxy(x).size, x.size)

def test_cp16476():
    for expected, encoded_val in [(156909,       '\xedd\x02\x00'),
                                  (sys.maxsize,   '\xff\xff\xff\x7f'),
                                  (sys.maxsize-1, '\xfe\xff\xff\x7f'),
                                  (sys.maxsize-2, '\xfd\xff\xff\x7f'),
                                  (sys.maxsize+1, '\x00\x00\x00\x80'),
                                  (sys.maxsize+2, '\x01\x00\x00\x80'),
                                  (sys.maxsize+3, '\x02\x00\x00\x80'),
                                  (2**16,        '\x00\x00\x01\x00'),
                                  (2**16+1,      '\x01\x00\x01\x00'),
                                  (2**16-1,      '\xff\xff\x00\x00'),
                                  (0,            '\x00\x00\x00\x00'),
                                  (1,            '\x01\x00\x00\x00'),
                                    ]:
        actual_val = unpack('I', encoded_val)
        AreEqual((expected,), actual_val)
        AreEqual(type(expected), type(actual_val[0]))


def test_unpack_from():
    '''
    TODO: just a sanity test for now.  Needs far more testing.
    '''
    import array
    _struct.unpack_from("", array.array("c"))
    
    AreEqual(_struct.unpack_from("", array.array("c")),
             ())

#--MAIN------------------------------------------------------------------------    
run_test(__name__)
