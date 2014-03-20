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
if is_silverlight==False:
    from iptest.file_util import *

import marshal

if is_silverlight==False:
    tfn = path_combine(testpath.temporary_dir, 'tempfile.bin')

# a couple of lines are disabled due to 1032

def test_functionality():
    objects = [ None,
                True, False,
                '', 'a', 'abc',
                -3, 0, 10,
                254, -255, 256, 257,
                65534, 65535, -65536,
                3.1415926,
                
                0L,
                -1234567890123456789,
                2**33,
                [],
                [ [] ], [ [], [] ],
                ['abc'], [1, 2],
                tuple(),
                (), ( (), (), ),
                (1,), (1,2,3),
                {},
                { 'abc' : {} },
                {1:2}, {1:2, 4:'4', 5:None},
                0+1j, 2-3.23j,
                set(),
                set(['abc', -5]),
                set([1, (2.1, 3L), frozenset([5]), 'x']),
                frozenset(),
                frozenset(['abc', -5]),
                frozenset([1, (2.1, 3L), frozenset([5]), 'x'])
            ]
    
    if is_cli or is_silverlight:
        import System
        objects.extend(
            [
            System.Single.Parse('-2345678'),
            System.Int64.Parse('2345678'),
            
            ])

    # dumps / loads
    for x in objects:
        s = marshal.dumps(x)
        x2 = marshal.loads(s)
        AreEqual(x, x2)
        
        s2 = marshal.dumps(x2)
        AreEqual(s, s2)

    # dump / load
    for x in objects:
        if is_silverlight:
            break
            
        f = file(tfn, 'wb')
        marshal.dump(x, f)
        f.close()
        
        f = file(tfn, 'rb')
        x2 = marshal.load(f)
        f.close()
        AreEqual(x, x2)
    
def test_buffer():
    for s in ['', ' ', 'abc ', 'abcdef']:
        x = marshal.dumps(buffer(s))
        AreEqual(marshal.loads(x), s)

    for s in ['', ' ', 'abc ', 'abcdef']:
        if is_silverlight:
            break
            
        f = file(tfn, 'wb')
        marshal.dump(buffer(s), f)
        f.close()
        
        f = file(tfn, 'rb')
        x2 = marshal.load(f)
        f.close()
        AreEqual(s, x2)

def test_negative():
    AssertError(TypeError, marshal.dump, 2, None)
    AssertError(TypeError, marshal.load, '-1', None)
    
    l = [1, 2]
    l.append(l)
    AssertError(ValueError, marshal.dumps, l) ## infinite loop
    
    class my: pass
    AssertError(ValueError, marshal.dumps, my())  ## unmarshallable object

@skip("silverlight") # file IO    
def test_file_multiple_reads():
    """calling load w/ a file should only advance the length of the file"""
    l = []
    for i in xrange(10):
        l.append(marshal.dumps({i:i}))
    
    data = ''.join(l)
    f = file('tempfile.txt', 'w')
    f.write(data)
    f.close()
    
    f = file('tempfile.txt')
    
    for i in xrange(10):
        obj = marshal.load(f)
        AreEqual(obj, {i:i})

def test_string_interning():
    AreEqual(marshal.dumps(['abc', 'abc'], 1), '[\x02\x00\x00\x00t\x03\x00\x00\x00abcR\x00\x00\x00\x00')
    AreEqual(marshal.dumps(['abc', 'abc']), '[\x02\x00\x00\x00t\x03\x00\x00\x00abcR\x00\x00\x00\x00')
    AreEqual(marshal.dumps(['abc', 'abc'], 0), '[\x02\x00\x00\x00s\x03\x00\x00\x00abcs\x03\x00\x00\x00abc')
    AreEqual(marshal.dumps(['abc', 'abc', 'abc', 'def', 'def'], 1), '[\x05\x00\x00\x00t\x03\x00\x00\x00abcR\x00\x00\x00\x00R\x00\x00\x00\x00t\x03\x00\x00\x00defR\x01\x00\x00\x00')
    AreEqual(marshal.loads(marshal.dumps(['abc', 'abc'], 1)), ['abc', 'abc'])
    AreEqual(marshal.loads(marshal.dumps(['abc', 'abc'], 0)), ['abc', 'abc'])
    AreEqual(marshal.loads(marshal.dumps(['abc', 'abc', 'abc', 'def', 'def'], 1)), ['abc', 'abc', 'abc', 'def', 'def'])
    
def test_binary_floats():
    AreEqual(marshal.dumps(2.0, 2), 'g\x00\x00\x00\x00\x00\x00\x00@')
    AreEqual(marshal.dumps(2.0), 'g\x00\x00\x00\x00\x00\x00\x00@')
    if not is_cpython: #http://ironpython.codeplex.com/workitem/28195
        AreEqual(marshal.dumps(2.0, 1), 'f\x032.0')
    else:
        AreEqual(marshal.dumps(2.0, 1), 'f\x012')
    AreEqual(marshal.loads(marshal.dumps(2.0, 2)), 2.0)

def test_cp24547():
    AreEqual(marshal.dumps(2**33), "l\x03\x00\x00\x00\x00\x00\x00\x00\x08\x00")


#--MAIN------------------------------------------------------------------------
run_test(__name__)
