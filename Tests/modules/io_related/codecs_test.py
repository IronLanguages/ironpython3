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

#
# test codecs
#

'''
TODO - essentially all the tests currently here are barebones sanity checks
to ensure a minimal level of functionality exists. In other words, there are
many special cases that are not being covered *yet*.

Disabled Silverlight tests are due to Rowan #304084
'''

from iptest.assert_util import *
from iptest.misc_util import ip_supported_encodings

if is_cli or is_silverlight:
    import _codecs as codecs
else:
    import codecs


#-----------------------
#--GLOBALS

def test_escape_decode():
    '''
    '''
    #sanity checks

    value, length = codecs.escape_decode("ab\a\b\t\n\r\f\vba")
    AreEqual(value, 'ab\x07\x08\t\n\r\x0c\x0bba')
    AreEqual(length, 11)
    
    value, length = codecs.escape_decode("\\a")
    AreEqual(value, '\x07')
    AreEqual(length, 2)
    
    
    value, length = codecs.escape_decode("ab\a\b\t\n\r\f\vbaab\\a\\b\\t\\n\\r\\f\\vbaab\\\a\\\b\\\t\\\n\\\r\\\f\\\vba")
    AreEqual(value, 'ab\x07\x08\t\n\r\x0c\x0bbaab\x07\x08\t\n\r\x0c\x0bbaab\\\x07\\\x08\\\t\\\r\\\x0c\\\x0bba')
    AreEqual(length, 47)
    
    value, length = codecs.escape_decode("\\\a")
    AreEqual(value, '\\\x07')
    AreEqual(length, 2)

    AreEqual("abc", codecs.escape_decode("abc", None)[0])
    AreEqual("?\\", codecs.escape_decode("\\x", 'replace')[0])
    AreEqual("?\\x", codecs.escape_decode("\\x2", 'replace')[0])
    AreEqual("?\\x", codecs.escape_decode("\\xI", 'replace')[0])
    AreEqual("?\\xI", codecs.escape_decode("\\xII", 'replace')[0])
    AreEqual("?\\x1", codecs.escape_decode("\\x1I", 'replace')[0])
    AreEqual("?\\xI", codecs.escape_decode("\\xI1", 'replace')[0])
    
def test_escape_encode():
    '''
    '''
    #sanity checks
    value, length = codecs.escape_encode("abba")
    AreEqual(value, "abba")
    AreEqual(length, 4)

    value, length = codecs.escape_encode("ab\a\b\t\n\r\f\vba")
    AreEqual(value, 'ab\\x07\\x08\\t\\n\\r\\x0c\\x0bba')
    if is_ironpython: #http://ironpython.codeplex.com/workitem/27899
        AreEqual(length, 26)
    else:
        AreEqual(length, 11)

    value, length = codecs.escape_encode("\\a")
    AreEqual(value, "\\\\a")
    if is_ironpython: #http://ironpython.codeplex.com/workitem/27899
        AreEqual(length, 3)
    else:
        AreEqual(length, 2)
    
    l = []
    for i in range(256):
        l.append(chr(i))
        
    value, length = codecs.escape_encode(''.join(l))
    AreEqual(value, '\\x00\\x01\\x02\\x03\\x04\\x05\\x06\\x07\\x08\\t\\n\\x0b\\x0c\\r\\x0e\\x0f\\x10\\x11\\x12\\x13\\x14\\x15\\x16\\x17\\x18\\x19\\x1a\\x1b\\x1c\\x1d\\x1e\\x1f !"#$%&\\\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\\x7f\\x80\\x81\\x82\\x83\\x84\\x85\\x86\\x87\\x88\\x89\\x8a\\x8b\\x8c\\x8d\\x8e\\x8f\\x90\\x91\\x92\\x93\\x94\\x95\\x96\\x97\\x98\\x99\\x9a\\x9b\\x9c\\x9d\\x9e\\x9f\\xa0\\xa1\\xa2\\xa3\\xa4\\xa5\\xa6\\xa7\\xa8\\xa9\\xaa\\xab\\xac\\xad\\xae\\xaf\\xb0\\xb1\\xb2\\xb3\\xb4\\xb5\\xb6\\xb7\\xb8\\xb9\\xba\\xbb\\xbc\\xbd\\xbe\\xbf\\xc0\\xc1\\xc2\\xc3\\xc4\\xc5\\xc6\\xc7\\xc8\\xc9\\xca\\xcb\\xcc\\xcd\\xce\\xcf\\xd0\\xd1\\xd2\\xd3\\xd4\\xd5\\xd6\\xd7\\xd8\\xd9\\xda\\xdb\\xdc\\xdd\\xde\\xdf\\xe0\\xe1\\xe2\\xe3\\xe4\\xe5\\xe6\\xe7\\xe8\\xe9\\xea\\xeb\\xec\\xed\\xee\\xef\\xf0\\xf1\\xf2\\xf3\\xf4\\xf5\\xf6\\xf7\\xf8\\xf9\\xfa\\xfb\\xfc\\xfd\\xfe\\xff')
    
    if is_ironpython: #http://ironpython.codeplex.com/workitem/27899
        AreEqual(length, 735)
    else:
        AreEqual(length, 256)

@skip('silverlight')
def test_register_error():
        '''
        TODO: test that these are actually used.
        '''
        #Sanity
        def garbage_error0(): print("garbage_error0")
        def garbage_error1(param1): print("garbage_error1:", param1)
        def garbage_error2(param1, param2): print("garbage_error2:", param1, "; ", param2)
        
        codecs.register_error("garbage0", garbage_error0)
        codecs.register_error("garbage1", garbage_error1)
        codecs.register_error("garbage2", garbage_error2)
        codecs.register_error("garbage1dup", garbage_error1)

@skip('silverlight') # different result on Silverlight
def test_utf_16_ex_decode():
    '''
    '''
    #sanity
    new_str, size, zero = codecs.utf_16_ex_decode("abc")
    AreEqual(new_str, '\u6261')
    AreEqual(size, 2)
    AreEqual(zero, 0)
    
def test_charmap_decode():
    '''
    '''
    #Sanity
    new_str, size = codecs.charmap_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)
    AreEqual(codecs.charmap_decode("a", 'strict', {ord('a') : 'a'})[0], 'a')
    AreEqual(codecs.charmap_decode("a", "replace", {})[0], '\ufffd')
    AreEqual(codecs.charmap_decode("a", "replace", {ord('a'): None})[0], '\ufffd')
    
    AreEqual(codecs.charmap_decode(""),
             ('', 0))

    if not is_silverlight:
        #Negative
        AssertError(UnicodeDecodeError, codecs.charmap_decode, "a", "strict", {})
        AssertError(UnicodeDecodeError, codecs.charmap_decode, "a", "strict", {'a': None})
        AssertError(UnicodeEncodeError, codecs.charmap_encode, "a", "strict", {'a': None})
        AssertError(UnicodeEncodeError, codecs.charmap_encode, "a", "replace", {'a': None})
    
    AssertError(TypeError, codecs.charmap_decode, "a", "strict", {ord('a'): 2.0})
    
@skip("silverlight") # no std lib
def test_decode():
    '''
    '''
    #sanity
    new_str = codecs.decode("abc")
    AreEqual(new_str, 'abc')
        
    
@skip("silverlight") # no std lib
def test_encode():
    '''
    '''
    #sanity
    new_str = codecs.encode("abc")
    AreEqual(new_str, 'abc')

def test_raw_unicode_escape_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.raw_unicode_escape_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

def test_raw_unicode_escape_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.raw_unicode_escape_encode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

@skip('silverlight')
def test_utf_7_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_7_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

@skip('silverlight')
def test_utf_7_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_7_encode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

def test_ascii_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.ascii_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

def test_ascii_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.ascii_encode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

@skip('silverlight')
def test_latin_1_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.latin_1_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

@skip('silverlight')
def test_latin_1_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.latin_1_encode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)
    
    # so many ways to express latin 1...
    for x in ['iso-8859-1', 'iso8859-1', '8859', 'cp819', 'latin', 'latin1', 'L1']:
        AreEqual('abc'.encode(x), 'abc')
        

@skip("multiple_execute")
def test_lookup_error():
    '''
    '''
    #sanity
    AssertError(LookupError, codecs.lookup_error, "blah garbage xyz")
    def garbage_error1(someError): pass
    codecs.register_error("blah garbage xyz", garbage_error1)
    AreEqual(codecs.lookup_error("blah garbage xyz"), garbage_error1)
    def garbage_error2(someError): pass
    codecs.register_error("some other", garbage_error2)
    AreEqual(codecs.lookup_error("some other"), garbage_error2)

@skip("multiple_execute")
def test_register():
    '''
    TODO: test that functions passed in are actually used
    '''
    #sanity check - basically just ensure that functions can be registered
    def garbage_func0(): pass
    def garbage_func1(param1): pass
    codecs.register(garbage_func0)
    codecs.register(garbage_func1)
    
    #negative cases
    AssertError(TypeError, codecs.register)
    AssertError(TypeError, codecs.register, None)
    AssertError(TypeError, codecs.register, ())
    AssertError(TypeError, codecs.register, [])
    AssertError(TypeError, codecs.register, 1)
    AssertError(TypeError, codecs.register, "abc")
    AssertError(TypeError, codecs.register, 3.14)

def test_unicode_internal_encode():
    '''
    '''
    # takes one or two parameters, not zero or three
    AssertError(TypeError, codecs.unicode_internal_encode)
    AssertError(TypeError, codecs.unicode_internal_encode, 'abc', 'def', 'qrt')
    if is_ironpython: #http://ironpython.codeplex.com/workitem/27899
        AreEqual(codecs.unicode_internal_encode('abc'), ('a\x00b\x00c\x00', 6))
    else:
        AreEqual(codecs.unicode_internal_encode('abc'), ('a\x00b\x00c\x00', 3))

def test_unicode_internal_decode():
    '''
    '''
    # takes one or two parameters, not zero or three
    AssertError(TypeError, codecs.unicode_internal_decode)
    AssertError(TypeError, codecs.unicode_internal_decode, 'abc', 'def', 'qrt')
    AreEqual(codecs.unicode_internal_decode('ab'), ('\u6261', 2))

@skip('silverlight')
def test_utf_16_be_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_16_be_decode("abc")
    AreEqual(new_str, '\u6162')
    AreEqual(size, 2)

def test_utf_16_be_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_16_be_encode("abc")
    AreEqual(new_str, '\x00a\x00b\x00c')
    AreEqual(size, 3)
    
@skip('silverlight')
def test_utf_16_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_16_decode("abc")
    AreEqual(new_str, '\u6261')
    AreEqual(size, 2)

@skip('silverlight')
def test_utf_16_le_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_16_le_decode("abc")
    AreEqual(new_str, '\u6261')
    AreEqual(size, 2)

def test_utf_16_le_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_16_le_encode("abc")
    AreEqual(new_str, 'a\x00b\x00c\x00')
    AreEqual(size, 3)

@skip('silverlight')
def test_utf_16_le_str_encode():
    for x in ('utf_16_le', 'UTF-16LE', 'utf-16le'):
        AreEqual('abc'.encode(x), 'a\x00b\x00c\x00')

def test_utf_8_decode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_8_decode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)


def test_cp34951():
    def internal_cp34951(sample1):
        AreEqual(codecs.utf_8_decode(sample1), ('12\u20ac\x0a', 6))
        sample1 = sample1[:-1] # 12<euro>
        AreEqual(codecs.utf_8_decode(sample1), ('12\u20ac', 5))
        sample1 = sample1[:-1] # 12<uncomplete euro>
        AreEqual(codecs.utf_8_decode(sample1), ('12', 2))

        sample1 = sample1 + 'x7f' # makes it invalid
        try:
            r = codecs.utf_8_decode(sample1)
            Assert(False, "expected UncodeDecodeError not raised")
        except Exception as e:
            AreEqual(type(e), UnicodeDecodeError)

    internal_cp34951(b'\x31\x32\xe2\x82\xac\x0a') # 12<euro><cr>
    internal_cp34951(b'\xef\xbb\xbf\x31\x32\xe2\x82\xac\x0a') # <BOM>12<euro><cr>


def test_utf_8_encode():
    '''
    '''
    #sanity
    new_str, size = codecs.utf_8_encode("abc")
    AreEqual(new_str, 'abc')
    AreEqual(size, 3)

def test_charbuffer_encode():
    '''
    '''
    if is_cli:
        AssertError(NotImplementedError, codecs.charbuffer_encode, "abc")

def test_charmap_encode():
    #Sanity
    AreEqual(codecs.charmap_encode("abc"), 
             ('abc', 3))
    AreEqual(codecs.charmap_encode("abc", "strict"), 
             ('abc', 3))
    
    AreEqual(codecs.charmap_encode("", "strict", {}),
             ('', 0))

    charmap = dict([ (ord(c), c.upper()) for c in "abcdefgh"])
    AreEqual(codecs.charmap_encode("abc", "strict", charmap),
             ('ABC', 3))

                 
    if not is_silverlight:
        #Sanity Negative
        AssertError(UnicodeEncodeError, codecs.charmap_encode, "abc", "strict", {})


@skip("silverlight") # only UTF8 on Silverlight
def test_mbcs_decode():
    '''
    '''
    for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
        AreEqual(codecs.mbcs_decode('foo', mode), ('foo', 3))
        cpyres = '\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\u20ac\x81\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\x8d\u017d\x8f\x90\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\x9d\u017e\u0178\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        allchars = ''.join([chr(i) for i in range(256)])
        AreEqual(codecs.mbcs_decode(allchars, mode)[0], cpyres)
        
        # round tripping
        AreEqual(codecs.mbcs_encode(codecs.mbcs_decode(allchars, mode)[0])[0], allchars)


@skip("silverlight") # only UTF8 on Silverlight
def test_mbcs_encode():
    '''
    '''
    for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
        AreEqual(codecs.mbcs_encode('foo', mode), ('foo', 3))
        uall = ''.join([chr(i) for i in range(256)])
        cpyres = '\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f?\x81???????????\x8d?\x8f\x90????????????\x9d??\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        ipyres = codecs.mbcs_encode(uall, mode)[0]
        AreEqual(cpyres, ipyres)
        
        # all weird unicode characters that are supported
        chrs = '\u20ac\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178'
        AreEqual(codecs.mbcs_encode(chrs, mode), ('\x80\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8e\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9e\x9f', 27))

def test_readbuffer_encode():
    '''
    '''
    if is_cli:
        AssertError(NotImplementedError, codecs.readbuffer_encode, "abc")

def test_unicode_escape_decode():
    '''
    '''
    if is_cli:
        AssertError(NotImplementedError, codecs.unicode_escape_decode, "abc")

def test_unicode_escape_encode():
    '''
    '''
    if is_cli:
        AssertError(NotImplementedError, codecs.unicode_escape_encode, "abc")

def test_utf_16_encode():
    #Sanity
    AreEqual(codecs.utf_16_encode("abc"), ('\xff\xfea\x00b\x00c\x00', 3))


def test_misc_encodings():
    if not is_silverlight:
        # codec not available on silverlight
        AreEqual('abc'.encode('utf-16'), '\xff\xfea\x00b\x00c\x00')
        AreEqual('abc'.encode('utf-16-be'), '\x00a\x00b\x00c')
    for unicode_escape in ['unicode-escape', 'unicode escape']:
        AreEqual('abc'.encode('unicode-escape'), 'abc')
        AreEqual('abc\\u1234'.encode('unicode-escape'), 'abc\\\\u1234')

@skip("silverlight")
def test_file_encodings():
    '''
    Once this gets fixed, we should use *.py files in the correct encoding instead
    of dynamically generating ASCII files.  Also, need variations on the encoding
    names.
    '''
    
    sys.path.append(path_combine(os.getcwd(), "tmp_encodings"))
    try:
        os.mkdir(path_combine(os.getcwd(), "tmp_encodings"))
    except:
        pass
    
    try:
        #positive cases
        for coding in ip_supported_encodings:
            if is_net40 and coding.lower().replace(" ", "-")=="utf-16-be":
                print("http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24082")
                continue
            temp_mod_name = "test_encoding_" + coding.replace("-", "_").replace(" ", "_")
            f = open(path_combine(os.getcwd(), "tmp_encodings", temp_mod_name + ".py"),
                    "w")
            f.write("# coding: %s" % (coding))
            f.close()
            if temp_mod_name not in ["test_encoding_uTf!!!8"]: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20302
                __import__(temp_mod_name)
            os.remove(path_combine(os.getcwd(), "tmp_encodings", temp_mod_name + ".py"))
            
    finally:
        #cleanup
        sys.path.remove(path_combine(os.getcwd(), "tmp_encodings"))
        os.rmdir(path_combine(os.getcwd(), "tmp_encodings"))

@skip("silverlight")
def test_cp11334():
    
    #--Test that not using "# coding ..." results in a warning
    t_in, t_out, t_err = os.popen3(sys.executable + " " + path_combine(os.getcwd(), "encoded_files", "cp11334_warn.py"))
    t_err_lines = t_err.readlines()
    t_out_lines = t_out.readlines()
    t_err.close()
    t_out.close()
    t_in.close()
    
    AreEqual(len(t_out_lines), 0)
    Assert(t_err_lines[0].startswith("  File"))
    Assert(t_err_lines[1].startswith("SyntaxError: Non-ASCII character '\\xb5' in file"))
    
    #--Test that using "# coding ..." is OK
    t_in, t_out, t_err = os.popen3(sys.executable + " " + path_combine(os.getcwd(), "encoded_files", "cp11334_ok.py"))
    t_err_lines = t_err.readlines()
    t_out_lines = t_out.readlines()
    t_err.close()
    t_out.close()
    t_in.close()
    
    AreEqual(len(t_err_lines), 0)
    if not is_cli:
        AreEqual(t_out_lines[0], "\xb5ble\n")
    else:
        print("CodePlex 11334")
        AreEqual(t_out_lines[0], "\xe6ble\n")
    AreEqual(len(t_out_lines), 1)


@skip("silverlight", "multiple_execute")
def test_file_encodings_negative():
    '''
    TODO:
    - we should use *.py files in the correct encoding instead
    of dynamically generating ASCII files
    - need variations on the encoding names
    '''
    import sys
    sys.path.append(path_combine(os.getcwd(), "tmp_encodings"))
    try:
        os.mkdir(path_combine(os.getcwd(), "tmp_encodings"))
    except:
        pass
             
    try:
        #negative case
        f = open(path_combine(os.getcwd(), "tmp_encodings", "bad_encoding.py"), "w")
        f.write("# coding: bad")
        f.close()
        AssertError(SyntaxError, __import__, "bad_encoding")
        os.remove(path_combine(os.getcwd(), "tmp_encodings", "bad_encoding.py"))
    finally:
        #cleanup
        sys.path.remove(path_combine(os.getcwd(), "tmp_encodings"))
        os.rmdir(path_combine(os.getcwd(), "tmp_encodings"))

@disabled
def test_cp1214():
    """
    TODO: extend this a great deal
    """
    AreEqual('7FF80000000000007FF0000000000000'.decode('hex'),
             '\x7f\xf8\x00\x00\x00\x00\x00\x00\x7f\xf0\x00\x00\x00\x00\x00\x00')


def test_codecs_lookup():
    l = []
    def my_func(encoding, cache = l):
        l.append(encoding)
    
    codecs.register(my_func)
    allchars = ''.join([chr(i) for i in range(1, 256)])
    try:
        codecs.lookup(allchars)
        AssertUnreachable()
    except LookupError:
        pass
        
    lowerchars = allchars.lower().replace(' ', '-')
    for i in range(1, 255):
        if l[0][i] != lowerchars[i]:
            Assert(False, 'bad chars at index %d: %r %r' % (i, l[0][i], lowerchars[i]))
            
    AssertError(TypeError, codecs.lookup, '\0')
    AssertError(TypeError, codecs.lookup, 'abc\0')
    AreEqual(len(l), 1)


def test_lookup_encodings():
    try:
        AreEqual('07FF'.decode('hex')  , '\x07\xff')
    except LookupError:
        # if we don't have encodings then this will fail so
        # make sure we're failing because we don't have encodings
        AssertError(ImportError, __import__, 'encodings')

@skip("silverlight cli") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=1019
def test_cp1019():
    #--Test that bogus encodings fail properly
    t_in, t_out, t_err = os.popen3(sys.executable + " " + path_combine(os.getcwd(), "encoded_files", "cp1019.py"))
    t_err_lines = t_err.readlines()
    t_out_lines = t_out.readlines()
    t_err.close()
    t_out.close()
    t_in.close()
    
    AreEqual(len(t_out_lines), 0)
    Assert(t_err_lines[0].startswith("  File"))
    Assert(t_err_lines[1].startswith("SyntaxError: encoding problem: with BOM"))

@skip("silverlight")
def test_cp20302():
    import _codecs
    for encoding in ip_supported_encodings:
        if encoding.lower() in ['cp1252']: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20302
            continue
        temp = _codecs.lookup(encoding)

def test_charmap_build():
    decodemap = ''.join([chr(i).upper() if chr(i).islower() else chr(i).lower() for i in range(256)])    
    encodemap = codecs.charmap_build(decodemap)
    AreEqual(codecs.charmap_decode('Hello World', 'strict', decodemap), ('hELLO wORLD', 11))
    AreEqual(codecs.charmap_encode('Hello World', 'strict', encodemap), ('hELLO wORLD', 11))
    
#--MAIN------------------------------------------------------------------------        
run_test(__name__)
