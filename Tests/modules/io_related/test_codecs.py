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
'''

import codecs
import os
import shutil
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, is_posix, run_test, skipUnlessIronPython
from iptest.misc_util import ip_supported_encodings


class CodecTest(IronPythonTestCase):

    def test_escape_decode(self):
        #sanity checks

        value, length = codecs.escape_decode("ab\a\b\t\n\r\f\vba")
        self.assertEqual(value, 'ab\x07\x08\t\n\r\x0c\x0bba')
        self.assertEqual(length, 11)
        
        value, length = codecs.escape_decode("\\a")
        self.assertEqual(value, '\x07')
        self.assertEqual(length, 2)
        
        
        value, length = codecs.escape_decode("ab\a\b\t\n\r\f\vbaab\\a\\b\\t\\n\\r\\f\\vbaab\\\a\\\b\\\t\\\n\\\r\\\f\\\vba")
        self.assertEqual(value, 'ab\x07\x08\t\n\r\x0c\x0bbaab\x07\x08\t\n\r\x0c\x0bbaab\\\x07\\\x08\\\t\\\r\\\x0c\\\x0bba')
        self.assertEqual(length, 47)
        
        value, length = codecs.escape_decode("\\\a")
        self.assertEqual(value, '\\\x07')
        self.assertEqual(length, 2)

        self.assertEqual("abc", codecs.escape_decode("abc", None)[0])
        self.assertEqual("?\\", codecs.escape_decode("\\x", 'replace')[0])
        self.assertEqual("?\\x", codecs.escape_decode("\\x2", 'replace')[0])
        self.assertEqual("?\\x", codecs.escape_decode("\\xI", 'replace')[0])
        self.assertEqual("?\\xI", codecs.escape_decode("\\xII", 'replace')[0])
        self.assertEqual("?\\x1", codecs.escape_decode("\\x1I", 'replace')[0])
        self.assertEqual("?\\xI", codecs.escape_decode("\\xI1", 'replace')[0])
    
    def test_escape_encode(self):
        #sanity checks
        value, length = codecs.escape_encode("abba")
        self.assertEqual(value, "abba")
        self.assertEqual(length, 4)

        value, length = codecs.escape_encode("ab\a\b\t\n\r\f\vba")
        self.assertEqual(value, 'ab\\x07\\x08\\t\\n\\r\\x0c\\x0bba')
        if is_cli: #http://ironpython.codeplex.com/workitem/27899
            self.assertEqual(length, 26)
        else:
            self.assertEqual(length, 11)

        value, length = codecs.escape_encode("\\a")
        self.assertEqual(value, "\\\\a")
        if is_cli: #http://ironpython.codeplex.com/workitem/27899
            self.assertEqual(length, 3)
        else:
            self.assertEqual(length, 2)
        
        l = []
        for i in xrange(256):
            l.append(chr(i))
            
        value, length = codecs.escape_encode(''.join(l))
        self.assertEqual(value, '\\x00\\x01\\x02\\x03\\x04\\x05\\x06\\x07\\x08\\t\\n\\x0b\\x0c\\r\\x0e\\x0f\\x10\\x11\\x12\\x13\\x14\\x15\\x16\\x17\\x18\\x19\\x1a\\x1b\\x1c\\x1d\\x1e\\x1f !"#$%&\\\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\\x7f\\x80\\x81\\x82\\x83\\x84\\x85\\x86\\x87\\x88\\x89\\x8a\\x8b\\x8c\\x8d\\x8e\\x8f\\x90\\x91\\x92\\x93\\x94\\x95\\x96\\x97\\x98\\x99\\x9a\\x9b\\x9c\\x9d\\x9e\\x9f\\xa0\\xa1\\xa2\\xa3\\xa4\\xa5\\xa6\\xa7\\xa8\\xa9\\xaa\\xab\\xac\\xad\\xae\\xaf\\xb0\\xb1\\xb2\\xb3\\xb4\\xb5\\xb6\\xb7\\xb8\\xb9\\xba\\xbb\\xbc\\xbd\\xbe\\xbf\\xc0\\xc1\\xc2\\xc3\\xc4\\xc5\\xc6\\xc7\\xc8\\xc9\\xca\\xcb\\xcc\\xcd\\xce\\xcf\\xd0\\xd1\\xd2\\xd3\\xd4\\xd5\\xd6\\xd7\\xd8\\xd9\\xda\\xdb\\xdc\\xdd\\xde\\xdf\\xe0\\xe1\\xe2\\xe3\\xe4\\xe5\\xe6\\xe7\\xe8\\xe9\\xea\\xeb\\xec\\xed\\xee\\xef\\xf0\\xf1\\xf2\\xf3\\xf4\\xf5\\xf6\\xf7\\xf8\\xf9\\xfa\\xfb\\xfc\\xfd\\xfe\\xff')
        
        if is_cli: #http://ironpython.codeplex.com/workitem/27899
            self.assertEqual(length, 735)
        else:
            self.assertEqual(length, 256)

    def test_register_error(self):
            '''
            TODO: test that these are actually used.
            '''
            #Sanity
            def garbage_error0(): print "garbage_error0"
            def garbage_error1(param1): print "garbage_error1:", param1
            def garbage_error2(param1, param2): print "garbage_error2:", param1, "; ", param2
            
            codecs.register_error("garbage0", garbage_error0)
            codecs.register_error("garbage1", garbage_error1)
            codecs.register_error("garbage2", garbage_error2)
            codecs.register_error("garbage1dup", garbage_error1)

    def test_utf_16_ex_decode(self):
        #sanity
        new_str, size, zero = codecs.utf_16_ex_decode("abc")
        self.assertEqual(new_str, u'\u6261')
        self.assertEqual(size, 2)
        self.assertEqual(zero, 0)
    
    def test_charmap_decode(self):
        #Sanity
        new_str, size = codecs.charmap_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)
        self.assertEqual(codecs.charmap_decode("a", 'strict', {ord('a') : u'a'})[0], u'a')
        self.assertEqual(codecs.charmap_decode("a", "replace", {})[0], u'\ufffd')
        self.assertEqual(codecs.charmap_decode("a", "replace", {ord('a'): None})[0], u'\ufffd')
        
        self.assertEqual(codecs.charmap_decode(""),
                (u'', 0))

        #Negative
        self.assertRaises(UnicodeDecodeError, codecs.charmap_decode, "a", "strict", {})
        self.assertRaises(UnicodeDecodeError, codecs.charmap_decode, "a", "strict", {'a': None})
        self.assertRaises(UnicodeEncodeError, codecs.charmap_encode, "a", "strict", {'a': None})
        self.assertRaises(UnicodeEncodeError, codecs.charmap_encode, "a", "replace", {'a': None})
        
        self.assertRaises(TypeError, codecs.charmap_decode, "a", "strict", {ord('a'): 2.0})
    
    def test_decode(self):
        #sanity
        new_str = codecs.decode("abc")
        self.assertEqual(new_str, u'abc')
        
    def test_encode(self):
        #sanity
        new_str = codecs.encode("abc")
        self.assertEqual(new_str, 'abc')

    def test_raw_unicode_escape_decode(self):
        #sanity
        new_str, size = codecs.raw_unicode_escape_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)

    def test_raw_unicode_escape_encode(self):
        #sanity
        new_str, size = codecs.raw_unicode_escape_encode("abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(size, 3)

    def test_utf_7_decode(self):
        #sanity
        new_str, size = codecs.utf_7_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)

    def test_utf_7_encode(self):
        #sanity
        new_str, size = codecs.utf_7_encode("abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(size, 3)

    def test_ascii_decode(self):
        #sanity
        new_str, size = codecs.ascii_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)

    def test_ascii_encode(self):
        #sanity
        new_str, size = codecs.ascii_encode("abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(size, 3)

    def test_latin_1_decode(self):
        #sanity
        new_str, size = codecs.latin_1_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)

    def test_latin_1_encode(self):
        #sanity
        new_str, size = codecs.latin_1_encode("abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(size, 3)
    
        # so many ways to express latin 1...
        for x in ['iso-8859-1', 'iso8859-1', '8859', 'cp819', 'latin', 'latin1', 'L1']:
            self.assertEqual('abc'.encode(x), 'abc')
        

    #TODO: @skip("multiple_execute")
    def test_lookup_error(self):
        #sanity
        self.assertRaises(LookupError, codecs.lookup_error, "blah garbage xyz")
        def garbage_error1(someError): pass
        codecs.register_error("blah garbage xyz", garbage_error1)
        self.assertEqual(codecs.lookup_error("blah garbage xyz"), garbage_error1)
        def garbage_error2(someError): pass
        codecs.register_error("some other", garbage_error2)
        self.assertEqual(codecs.lookup_error("some other"), garbage_error2)

    #TODO: @skip("multiple_execute")
    def test_register(self):
        '''
        TODO: test that functions passed in are actually used
        '''
        #sanity check - basically just ensure that functions can be registered
        def garbage_func0(): pass
        def garbage_func1(param1): pass
        codecs.register(garbage_func0)
        codecs.register(garbage_func1)
        
        #negative cases
        self.assertRaises(TypeError, codecs.register)
        self.assertRaises(TypeError, codecs.register, None)
        self.assertRaises(TypeError, codecs.register, ())
        self.assertRaises(TypeError, codecs.register, [])
        self.assertRaises(TypeError, codecs.register, 1)
        self.assertRaises(TypeError, codecs.register, "abc")
        self.assertRaises(TypeError, codecs.register, 3.14)

    def test_unicode_internal_encode(self):
        # takes one or two parameters, not zero or three
        self.assertRaises(TypeError, codecs.unicode_internal_encode)
        self.assertRaises(TypeError, codecs.unicode_internal_encode, 'abc', 'def', 'qrt')
        if is_cli: #http://ironpython.codeplex.com/workitem/27899
            self.assertEqual(codecs.unicode_internal_encode(u'abc'), ('a\x00b\x00c\x00', 6))
        else:
            self.assertEqual(codecs.unicode_internal_encode(u'abc'), ('a\x00b\x00c\x00', 3))

    def test_unicode_internal_decode(self):
        # takes one or two parameters, not zero or three
        self.assertRaises(TypeError, codecs.unicode_internal_decode)
        self.assertRaises(TypeError, codecs.unicode_internal_decode, 'abc', 'def', 'qrt')
        self.assertEqual(codecs.unicode_internal_decode('ab'), (u'\u6261', 2))

    def test_utf_16_be_decode(self):
        #sanity
        new_str, size = codecs.utf_16_be_decode("abc")
        self.assertEqual(new_str, u'\u6162')
        self.assertEqual(size, 2)

    def test_utf_16_be_encode(self):
        #sanity
        new_str, size = codecs.utf_16_be_encode("abc")
        self.assertEqual(new_str, '\x00a\x00b\x00c')
        self.assertEqual(size, 3)
    
    def test_utf_16_decode(self):
        #sanity
        new_str, size = codecs.utf_16_decode("abc")
        self.assertEqual(new_str, u'\u6261')
        self.assertEqual(size, 2)


    def test_utf_16_le_decode(self):
        #sanity
        new_str, size = codecs.utf_16_le_decode("abc")
        self.assertEqual(new_str, u'\u6261')
        self.assertEqual(size, 2)

    def test_utf_16_le_encode(self):
        #sanity
        new_str, size = codecs.utf_16_le_encode("abc")
        self.assertEqual(new_str, 'a\x00b\x00c\x00')
        self.assertEqual(size, 3)


    def test_utf_16_le_str_encode(self):
        for x in ('utf_16_le', 'UTF-16LE', 'utf-16le'):
            self.assertEqual('abc'.encode(x), 'a\x00b\x00c\x00')

    def test_utf_8_decode(self):
        #sanity
        new_str, size = codecs.utf_8_decode("abc")
        self.assertEqual(new_str, u'abc')
        self.assertEqual(size, 3)


    def test_cp34951(self):
        def internal_cp34951(sample1):
            self.assertEqual(codecs.utf_8_decode(sample1), (u'12\u20ac\x0a', 6))
            sample1 = sample1[:-1] # 12<euro>
            self.assertEqual(codecs.utf_8_decode(sample1), (u'12\u20ac', 5))
            sample1 = sample1[:-1] # 12<uncomplete euro>
            self.assertEqual(codecs.utf_8_decode(sample1), (u'12', 2))

            sample1 = sample1 + 'x7f' # makes it invalid
            try:
                r = codecs.utf_8_decode(sample1)
                self.assertTrue(False, "expected UncodeDecodeError not raised")
            except Exception as e:
                self.assertEqual(type(e), UnicodeDecodeError)

        internal_cp34951(b'\x31\x32\xe2\x82\xac\x0a') # 12<euro><cr>
        internal_cp34951(b'\xef\xbb\xbf\x31\x32\xe2\x82\xac\x0a') # <BOM>12<euro><cr>


    def test_utf_8_encode(self):
        #sanity
        new_str, size = codecs.utf_8_encode("abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(size, 3)

    def test_charbuffer_encode(self):
        if is_cli:
            self.assertRaises(NotImplementedError, codecs.charbuffer_encode, "abc")

    def test_charmap_encode(self):
        #Sanity
        self.assertEqual(codecs.charmap_encode("abc"), 
                ('abc', 3))
        self.assertEqual(codecs.charmap_encode("abc", "strict"), 
                ('abc', 3))
        
        self.assertEqual(codecs.charmap_encode("", "strict", {}),
                ('', 0))

        charmap = dict([ (ord(c), c.upper()) for c in "abcdefgh"])
        self.assertEqual(codecs.charmap_encode(u"abc", "strict", charmap),
                ('ABC', 3))

                    
        #Sanity Negative
        self.assertRaises(UnicodeEncodeError, codecs.charmap_encode, "abc", "strict", {})


    @unittest.skipIf(is_posix, 'only UTF8 on posix - mbcs_decode/encode only exist on windows versions of python')
    def test_mbcs_decode(self):
        for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
            self.assertEqual(codecs.mbcs_decode('foo', mode), ('foo', 3))
            cpyres = u'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\u20ac\x81\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\x8d\u017d\x8f\x90\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\x9d\u017e\u0178\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
            allchars = ''.join([chr(i) for i in xrange(256)])
            self.assertEqual(codecs.mbcs_decode(allchars, mode)[0], cpyres)
            
            # round tripping
            self.assertEqual(codecs.mbcs_encode(codecs.mbcs_decode(allchars, mode)[0])[0], allchars)


    @unittest.skipIf(is_posix, 'only UTF8 on posix - mbcs_decode/encode only exist on windows versions of python')
    def test_mbcs_encode(self):
        for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
            self.assertEqual(codecs.mbcs_encode('foo', mode), ('foo', 3))
            uall = u''.join([unichr(i) for i in xrange(256)])
            cpyres = '\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f?\x81???????????\x8d?\x8f\x90????????????\x9d??\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
            ipyres = codecs.mbcs_encode(uall, mode)[0]
            self.assertEqual(cpyres, ipyres)
            
            # all weird unicode characters that are supported
            chrs = u'\u20ac\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178'
            self.assertEqual(codecs.mbcs_encode(chrs, mode), ('\x80\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8e\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9e\x9f', 27))

    @skipUnlessIronPython()
    def test_unicode_escape_decode(self):
        self.assertRaises(NotImplementedError, codecs.unicode_escape_decode, "abc")

    @skipUnlessIronPython()
    def test_unicode_escape_encode(self):
        self.assertRaises(NotImplementedError, codecs.unicode_escape_encode, "abc")

    def test_utf_16_encode(self):
        #Sanity
        self.assertEqual(codecs.utf_16_encode("abc"), ('\xff\xfea\x00b\x00c\x00', 3))


    def test_misc_encodings(self):
        self.assertEqual('abc'.encode('utf-16'), '\xff\xfea\x00b\x00c\x00')
        self.assertEqual('abc'.encode('utf-16-be'), '\x00a\x00b\x00c')
        for unicode_escape in ['unicode-escape', 'unicode escape']:
            self.assertEqual('abc'.encode('unicode-escape'), 'abc')
            self.assertEqual('abc\u1234'.encode('unicode-escape'), 'abc\\\\u1234')

    def test_file_encodings(self):
        '''
        Once this gets fixed, we should use *.py files in the correct encoding instead
        of dynamically generating ASCII files.  Also, need variations on the encoding
        names.
        '''
        
        sys.path.append(os.path.join(self.temporary_dir, "tmp_encodings"))
        try:
            os.mkdir(os.path.join(self.temporary_dir, "tmp_encodings"))
        except:
            pass
        
        try:
            #positive cases
            for coding in ip_supported_encodings:
                if coding.lower().replace(" ", "-")=="utf-16-be":
                    print "https://github.com/IronLanguages/ironpython2/issues/3"
                    continue
                temp_mod_name = "test_encoding_" + coding.replace("-", "_").replace(" ", "_")
                f = open(os.path.join(self.temporary_dir, "tmp_encodings", temp_mod_name + ".py"), "w")
                f.write("# coding: %s" % (coding))
                f.close()
                if temp_mod_name not in ["test_encoding_uTf!!!8"]: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20302
                    __import__(temp_mod_name)
                os.remove(os.path.join(self.temporary_dir, "tmp_encodings", temp_mod_name + ".py"))
                
        finally:
            #cleanup
            sys.path.remove(os.path.join(self.temporary_dir, "tmp_encodings"))
            shutil.rmtree(os.path.join(self.temporary_dir, "tmp_encodings"), True)


    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    @unittest.skipIf(is_mono, "https://github.com/IronLanguages/main/issues/1608")
    def test_cp11334(self):
        
        #--Test that not using "# coding ..." results in a warning
        t_in, t_out, t_err = os.popen3(sys.executable + " " + os.path.join(self.test_dir, "encoded_files", "cp11334_warn.py"))
        t_err_lines = t_err.readlines()
        t_out_lines = t_out.readlines()
        t_err.close()
        t_out.close()
        t_in.close()
        
        self.assertEqual(len(t_out_lines), 0)
        self.assertTrue(t_err_lines[0].startswith("  File"))
        self.assertTrue(t_err_lines[1].startswith("SyntaxError: Non-ASCII character '\\xb5' in file"))
        
        #--Test that using "# coding ..." is OK
        t_in, t_out, t_err = os.popen3(sys.executable + " " + os.path.join(self.test_dir, "encoded_files", "cp11334_ok.py"))
        t_err_lines = t_err.readlines()
        t_out_lines = t_out.readlines()
        t_err.close()
        t_out.close()
        t_in.close()
        
        self.assertEqual(len(t_err_lines), 0)
        if not is_cli:
            self.assertEqual(t_out_lines[0], "\xb5ble\n")
        else:
            print "CodePlex 11334"
            self.assertEqual(t_out_lines[0], "\xe6ble\n")
        self.assertEqual(len(t_out_lines), 1)


    #TODO:@skip("multiple_execute")
    def test_file_encodings_negative(self):
        '''
        TODO:
        - we should use *.py files in the correct encoding instead
        of dynamically generating ASCII files
        - need variations on the encoding names
        '''
        import sys
        sys.path.append(os.path.join(os.getcwd(), "tmp_encodings"))
        try:
            os.mkdir(os.path.join(os.getcwd(), "tmp_encodings"))
        except:
            pass
                
        try:
            #negative case
            f = open(os.path.join(os.getcwd(), "tmp_encodings", "bad_encoding.py"), "w")
            f.write("# coding: bad")
            f.close()
            self.assertRaises(SyntaxError, __import__, "bad_encoding")
            os.remove(os.path.join(os.getcwd(), "tmp_encodings", "bad_encoding.py"))
        finally:
            #cleanup
            sys.path.remove(os.path.join(os.getcwd(), "tmp_encodings"))
            os.rmdir(os.path.join(os.getcwd(), "tmp_encodings"))

    #@disabled
    def test_cp1214(self):
        """
        TODO: extend this a great deal
        """
        self.assertEqual('7FF80000000000007FF0000000000000'.decode('hex'),
                '\x7f\xf8\x00\x00\x00\x00\x00\x00\x7f\xf0\x00\x00\x00\x00\x00\x00')


    def test_codecs_lookup(self):
        l = []
        def my_func(encoding, cache = l):
            l.append(encoding)
        
        codecs.register(my_func)
        allchars = ''.join([chr(i) for i in xrange(1, 256)])
        try:
            codecs.lookup(allchars)
            AssertUnreachable()
        except LookupError:
            pass
            
        lowerchars = allchars.lower().replace(' ', '-')
        for i in xrange(1, 255):
            if l[0][i] != lowerchars[i]:
                self.assertTrue(False, 'bad chars at index %d: %r %r' % (i, l[0][i], lowerchars[i]))
                
        self.assertRaises(TypeError, codecs.lookup, '\0')
        self.assertRaises(TypeError, codecs.lookup, 'abc\0')
        self.assertEqual(len(l), 1)


    def test_lookup_encodings(self):
        try:
            self.assertEqual('07FF'.decode('hex')  , '\x07\xff')
        except LookupError:
            # if we don't have encodings then this will fail so
            # make sure we're failing because we don't have encodings
            self.assertRaises(ImportError, __import__, 'encodings')

    @unittest.skipIf(is_cli, 'https://github.com/IronLanguages/main/issues/255')
    def test_cp1019(self):
        #--Test that bogus encodings fail properly
        t_in, t_out, t_err = os.popen3(sys.executable + " " + os.path.join(self.test_dir, "encoded_files", "cp1019.py"))
        t_err_lines = t_err.readlines()
        t_out_lines = t_out.readlines()
        t_err.close()
        t_out.close()
        t_in.close()
        
        self.assertEqual(len(t_out_lines), 0)
        self.assertTrue(t_err_lines[0].startswith("  File"))
        self.assertTrue(t_err_lines[1].startswith("SyntaxError: encoding problem: with BOM"))

    def test_cp20302(self):
        import _codecs
        for encoding in ip_supported_encodings:
            if encoding.lower() in ['cp1252']: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20302
                continue
            temp = _codecs.lookup(encoding)

    def test_charmap_build(self):
        decodemap = ''.join([unichr(i).upper() if chr(i).islower() else unichr(i).lower() for i in xrange(256)])
        encodemap = codecs.charmap_build(decodemap)
        self.assertEqual(codecs.charmap_decode(u'Hello World', 'strict', decodemap), ('hELLO wORLD', 11))
        self.assertEqual(codecs.charmap_encode(u'Hello World', 'strict', encodemap), ('hELLO wORLD', 11))

    def test_gh16(self):
        """https://github.com/IronLanguages/ironpython2/issues/16"""
        # test with a standard error handler
        res = u"\xac\u1234\u20ac\u8000".encode("rot_13", "backslashreplace")
        self.assertEqual(res, "\xac\\h1234\\h20np\\h8000")

        # test with a custom error handler
        def handler(ex):
            return (u"", ex.end)
        codecs.register_error("test_unicode_error", handler)
        res = u"\xac\u1234\u20ac\u8000".encode("rot_13", "test_unicode_error")
        self.assertEqual(res, "\xac")
    
run_test(__name__)