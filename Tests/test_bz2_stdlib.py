# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_bz2 from StdLib
##

import unittest
import codecs
import sys

from iptest import run_test

import test.test_bz2

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_bz2.BZ2CompressorTest('testCompress'))
        suite.addTest(test.test_bz2.BZ2CompressorTest('testCompress4G'))
        suite.addTest(test.test_bz2.BZ2CompressorTest('testCompressChunks10'))
        suite.addTest(test.test_bz2.BZ2CompressorTest('testCompressEmptyString'))
        #suite.addTest(test.test_bz2.BZ2CompressorTest('testPickle')) # AssertionError: TypeError not raised
        suite.addTest(test.test_bz2.BZ2DecompressorTest('testDecompress'))
        suite.addTest(test.test_bz2.BZ2DecompressorTest('testDecompress4G'))
        suite.addTest(test.test_bz2.BZ2DecompressorTest('testDecompressChunks10'))
        suite.addTest(test.test_bz2.BZ2DecompressorTest('testDecompressUnusedData'))
        suite.addTest(test.test_bz2.BZ2DecompressorTest('testEOFError'))
        #suite.addTest(test.test_bz2.BZ2DecompressorTest('testPickle')) # AssertionError: TypeError not raised
        suite.addTest(test.test_bz2.BZ2DecompressorTest('test_Constructor'))
        suite.addTest(test.test_bz2.BZ2FileTest('testAppend'))
        suite.addTest(test.test_bz2.BZ2FileTest('testBadArgs'))
        suite.addTest(test.test_bz2.BZ2FileTest('testClosedIteratorDeadlock'))
        suite.addTest(test.test_bz2.BZ2FileTest('testContextProtocol'))
        suite.addTest(test.test_bz2.BZ2FileTest('testFileno'))
        suite.addTest(test.test_bz2.BZ2FileTest('testIterator'))
        suite.addTest(test.test_bz2.BZ2FileTest('testIteratorMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testMixedIterationAndReads'))
        suite.addTest(test.test_bz2.BZ2FileTest('testMultiStreamOrdering'))
        suite.addTest(test.test_bz2.BZ2FileTest('testOpenBytesFilename'))
        #suite.addTest(test.test_bz2.BZ2FileTest('testOpenDel')) # PermissionError: [WinError 32] The process cannot access the file because it is being used by another process
        suite.addTest(test.test_bz2.BZ2FileTest('testOpenNonexistent'))
        suite.addTest(test.test_bz2.BZ2FileTest('testPeek'))
        suite.addTest(test.test_bz2.BZ2FileTest('testPeekBytesIO'))
        suite.addTest(test.test_bz2.BZ2FileTest('testRead'))
        suite.addTest(test.test_bz2.BZ2FileTest('testRead0'))
        suite.addTest(test.test_bz2.BZ2FileTest('testRead100'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadBadFile'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadBytesIO'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadChunk10'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadChunk10MultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadInto'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadLine'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadLineMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadLines'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadLinesMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadMonkeyMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadMultiStreamTrailingJunk'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadTrailingJunk'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadable'))
        suite.addTest(test.test_bz2.BZ2FileTest('testReadlinesNoNewline'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekBackwards'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekBackwardsAcrossStreams'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekBackwardsBytesIO'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekBackwardsFromEnd'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekBackwardsFromEndAcrossStreams'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekForward'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekForwardAcrossStreams'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekForwardBytesIO'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPostEnd'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPostEndMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPostEndTwice'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPostEndTwiceMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPreStart'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekPreStartMultiStream'))
        suite.addTest(test.test_bz2.BZ2FileTest('testSeekable'))
        #suite.addTest(test.test_bz2.BZ2FileTest('testThreading')) # unstable
        suite.addTest(test.test_bz2.BZ2FileTest('testWithoutThreading'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWritable'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWrite'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWriteBytesIO'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWriteChunks10'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWriteLines'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWriteMethodsOnReadOnlyFile'))
        suite.addTest(test.test_bz2.BZ2FileTest('testWriteNonDefaultCompressLevel'))
        #suite.addTest(test.test_bz2.BZ2FileTest('test_read_truncated')) # EOFError: Compressed file ended before the end-of-stream marker was reached
        suite.addTest(test.test_bz2.CompressDecompressTest('testCompress'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testCompressEmptyString'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompress'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressBadData'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressEmpty'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressIncomplete'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressMultiStream'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressMultiStreamTrailingJunk'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressToEmptyString'))
        suite.addTest(test.test_bz2.CompressDecompressTest('testDecompressTrailingJunk'))
        suite.addTest(test.test_bz2.OpenTest('test_bad_params'))
        suite.addTest(test.test_bz2.OpenTest('test_binary_modes'))
        suite.addTest(test.test_bz2.OpenTest('test_encoding'))
        suite.addTest(test.test_bz2.OpenTest('test_encoding_error_handler'))
        suite.addTest(test.test_bz2.OpenTest('test_fileobj'))
        suite.addTest(test.test_bz2.OpenTest('test_implicit_binary_modes'))
        suite.addTest(test.test_bz2.OpenTest('test_newline'))
        suite.addTest(test.test_bz2.OpenTest('test_text_modes'))
        suite.addTest(test.test_bz2.OpenTest('test_x_mode'))
        return suite
        
    else:
        return loader.loadTestsFromModule(test.test_bz2, pattern)

run_test(__name__)
