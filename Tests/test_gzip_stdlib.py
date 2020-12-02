# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_gzip from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_gzip

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_gzip.TestGzip('test_1647484'))
        suite.addTest(test.test_gzip.TestGzip('test_append'))
        suite.addTest(test.test_gzip.TestGzip('test_buffered_reader'))
        suite.addTest(test.test_gzip.TestGzip('test_bytes_filename'))
        suite.addTest(test.test_gzip.TestGzip('test_compress'))
        suite.addTest(test.test_gzip.TestGzip('test_decompress'))
        suite.addTest(test.test_gzip.TestGzip('test_exclusive_write'))
        suite.addTest(test.test_gzip.TestGzip('test_fileobj_from_fdopen'))
        suite.addTest(test.test_gzip.TestGzip('test_io_on_closed_object'))
        suite.addTest(test.test_gzip.TestGzip('test_many_append'))
        suite.addTest(test.test_gzip.TestGzip('test_metadata'))
        suite.addTest(test.test_gzip.TestGzip('test_mode'))
        suite.addTest(test.test_gzip.TestGzip('test_mtime'))
        #suite.addTest(test.test_gzip.TestGzip('test_non_seekable_file')) # https://github.com/IronLanguages/ironpython3/issues/1070
        suite.addTest(test.test_gzip.TestGzip('test_paddedfile_getattr'))
        suite.addTest(test.test_gzip.TestGzip('test_peek'))
        suite.addTest(test.test_gzip.TestGzip('test_prepend_error'))
        suite.addTest(test.test_gzip.TestGzip('test_read'))
        suite.addTest(test.test_gzip.TestGzip('test_read1'))
        suite.addTest(test.test_gzip.TestGzip('test_read_truncated'))
        suite.addTest(test.test_gzip.TestGzip('test_read_with_extra'))
        suite.addTest(test.test_gzip.TestGzip('test_readline'))
        suite.addTest(test.test_gzip.TestGzip('test_readlines'))
        suite.addTest(test.test_gzip.TestGzip('test_seek_read'))
        suite.addTest(test.test_gzip.TestGzip('test_seek_whence'))
        suite.addTest(test.test_gzip.TestGzip('test_seek_write'))
        suite.addTest(test.test_gzip.TestGzip('test_textio_readlines'))
        suite.addTest(test.test_gzip.TestGzip('test_with_open'))
        suite.addTest(test.test_gzip.TestGzip('test_write'))
        suite.addTest(test.test_gzip.TestGzip('test_write_bytearray'))
        suite.addTest(test.test_gzip.TestGzip('test_write_incompatible_type'))
        suite.addTest(test.test_gzip.TestGzip('test_write_memoryview'))
        suite.addTest(test.test_gzip.TestGzip('test_zero_padded_file'))
        suite.addTest(test.test_gzip.TestOpen('test_bad_params'))
        suite.addTest(test.test_gzip.TestOpen('test_binary_modes'))
        suite.addTest(test.test_gzip.TestOpen('test_encoding'))
        suite.addTest(test.test_gzip.TestOpen('test_encoding_error_handler'))
        suite.addTest(test.test_gzip.TestOpen('test_fileobj'))
        suite.addTest(test.test_gzip.TestOpen('test_implicit_binary_modes'))
        suite.addTest(test.test_gzip.TestOpen('test_newline'))
        suite.addTest(test.test_gzip.TestOpen('test_text_modes'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_gzip, pattern)

run_test(__name__)
