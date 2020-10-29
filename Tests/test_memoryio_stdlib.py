# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_memoryio from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_memoryio

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_memoryio.CBytesIOTest('testInit'))
        suite.addTest(test.test_memoryio.CBytesIOTest('testRead'))
        suite.addTest(test.test_memoryio.CBytesIOTest('testReadNoArgs'))
        suite.addTest(test.test_memoryio.CBytesIOTest('testSeek'))
        suite.addTest(test.test_memoryio.CBytesIOTest('testTell'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_bytes_array'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_detach'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_flags'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_flush'))
        #suite.addTest(test.test_memoryio.CBytesIOTest('test_getbuffer')) # https://github.com/IronLanguages/ironpython3/issues/1002
        suite.addTest(test.test_memoryio.CBytesIOTest('test_getstate'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_getvalue'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_init'))
        #suite.addTest(test.test_memoryio.CBytesIOTest('test_instance_dict_leak')) # https://github.com/IronLanguages/ironpython3/issues/1004
        suite.addTest(test.test_memoryio.CBytesIOTest('test_issue5449'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_iterator'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_overseek'))
        #suite.addTest(test.test_memoryio.CBytesIOTest('test_pickling')) # https://github.com/IronLanguages/ironpython3/issues/1003
        suite.addTest(test.test_memoryio.CBytesIOTest('test_read'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_read1'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_readinto'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_readline'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_readlines'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_relative_seek'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_seek'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_setstate'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_sizeof'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_subclassing'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_tell'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_truncate'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_unicode'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_write'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_writelines'))
        suite.addTest(test.test_memoryio.CBytesIOTest('test_writelines_error'))
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_issue5265')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_argument')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_cr')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_crlf')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_default')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_empty')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_lf')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newline_none')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_newlines_property')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_relative_seek')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOPickleTest('test_textio_properties')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('testInit'))
        suite.addTest(test.test_memoryio.CStringIOTest('testRead'))
        suite.addTest(test.test_memoryio.CStringIOTest('testReadNoArgs'))
        suite.addTest(test.test_memoryio.CStringIOTest('testSeek'))
        suite.addTest(test.test_memoryio.CStringIOTest('testTell'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_detach'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_flags'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_flush'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_getstate'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_getvalue'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_init')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOTest('test_instance_dict_leak')) # https://github.com/IronLanguages/ironpython3/issues/1004
        #suite.addTest(test.test_memoryio.CStringIOTest('test_issue5265')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_iterator'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_lone_surrogates')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_newline_argument'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_newline_cr'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_newline_crlf'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_newline_default')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_newline_empty'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_newline_lf')) # https://github.com/IronLanguages/ironpython3/issues/1001
        #suite.addTest(test.test_memoryio.CStringIOTest('test_newline_none')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_newlines_property'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_overseek'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_pickling')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_read'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_readline'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_readlines'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_relative_seek'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_seek'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_setstate'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_subclassing'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_tell'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_textio_properties')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_truncate'))
        #suite.addTest(test.test_memoryio.CStringIOTest('test_widechar')) # https://github.com/IronLanguages/ironpython3/issues/1001
        suite.addTest(test.test_memoryio.CStringIOTest('test_write'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_writelines'))
        suite.addTest(test.test_memoryio.CStringIOTest('test_writelines_error'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('testInit'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('testRead'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('testReadNoArgs'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('testSeek'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('testTell'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_bytes_array'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_detach'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_flags'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_flush'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_getbuffer'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_getvalue'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_init'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_instance_dict_leak'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_issue5449'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_iterator'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_overseek'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_pickling'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_read'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_read1'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_readinto'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_readline'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_readlines'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_relative_seek'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_seek'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_subclassing'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_tell'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_truncate'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_unicode'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_write'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_writelines'))
        suite.addTest(test.test_memoryio.PyBytesIOTest('test_writelines_error'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_issue5265'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_argument'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_cr'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_crlf'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_default'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_empty'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_lf'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newline_none'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_newlines_property'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_relative_seek'))
        suite.addTest(test.test_memoryio.PyStringIOPickleTest('test_textio_properties'))
        suite.addTest(test.test_memoryio.PyStringIOTest('testInit'))
        suite.addTest(test.test_memoryio.PyStringIOTest('testRead'))
        suite.addTest(test.test_memoryio.PyStringIOTest('testReadNoArgs'))
        suite.addTest(test.test_memoryio.PyStringIOTest('testSeek'))
        suite.addTest(test.test_memoryio.PyStringIOTest('testTell'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_detach'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_flags'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_flush'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_getvalue'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_init'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_instance_dict_leak'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_issue5265'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_iterator'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_lone_surrogates'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_argument'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_cr'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_crlf'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_default'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_empty'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_lf'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newline_none'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_newlines_property'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_overseek'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_pickling'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_read'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_readline'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_readlines'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_relative_seek'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_seek'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_subclassing'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_tell'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_textio_properties'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_truncate'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_write'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_writelines'))
        suite.addTest(test.test_memoryio.PyStringIOTest('test_writelines_error'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_memoryio, pattern)

run_test(__name__)
