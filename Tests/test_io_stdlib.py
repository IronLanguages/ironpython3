# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_io from StdLib
##

from iptest import is_ironpython, is_mono, generate_suite, run_test

import test.test_io

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_io)

    if is_ironpython:
        failing_tests = [
            test.test_io.CIOTest('test_BufferedIOBase_destructor'), # AssertionError: Lists differ: [2, 3, 1, 2] != [1, 2, 3]
            test.test_io.CIOTest('test_IOBase_destructor'), # AssertionError: Lists differ: [2, 3, 1, 2] != [1, 2, 3]
            test.test_io.CIOTest('test_RawIOBase_destructor'), # AssertionError: Lists differ: [2, 3, 1, 2] != [1, 2, 3]
            test.test_io.CIOTest('test_RawIOBase_read'), # TypeError: expected int, got NoneType
            test.test_io.CIOTest('test_TextIOBase_destructor'), # AssertionError: Lists differ: [1, 2, 3, 2] != [1, 2, 3]
            test.test_io.CIOTest('test_destructor'), # AssertionError: Lists differ: [2, 3, 1, 2] != [1, 2, 3]
            test.test_io.CIOTest('test_flush_error_on_close'), # AssertionError: OSError not raised by close
            test.test_io.CIOTest('test_invalid_operations'), # OSError: can't do nonzero cur-relative seeks
            test.test_io.CBufferedReaderTest('test_args_error'), # AssertionError: "BufferedReader" does not match "__init__() takes at most 2 arguments (4 given)"
            test.test_io.CBufferedReaderTest('test_buffering'), # TypeError: BufferedReader() takes at least 0 arguments (2 given)
            test.test_io.CBufferedReaderTest('test_close_error_on_close'), # AssertionError: None is not an instance of <class 'OSError'>
            test.test_io.CBufferedReaderTest('test_initialization'), # AssertionError: ValueError not raised by read
            test.test_io.CBufferedReaderTest('test_misbehaved_io_read'), # AssertionError: OSError not raised by read
            test.test_io.CBufferedReaderTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedReaderTest('test_read_non_blocking'), # AssertionError: b'' is not None
            test.test_io.CBufferedReaderTest('test_read_on_closed'), # AssertionError: ValueError not raised by read1
            test.test_io.CBufferedReaderTest('test_readonly_attributes'), # AssertionError: AttributeError not raised
            test.test_io.CBufferedReaderTest('test_uninitialized'), # AssertionError: (<class 'ValueError'>, <class 'AttributeError'>) not raised by read
            test.test_io.PyBufferedReaderTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.PyBufferedReaderTest('test_read_on_closed'), # AssertionError: ValueError not raised by read1
            test.test_io.CBufferedWriterTest('test_close_error_on_close'), # AssertionError: None is not an instance of <class 'OSError'>
            test.test_io.CBufferedWriterTest('test_initialization'), # AssertionError: ValueError not raised by write
            test.test_io.CBufferedWriterTest('test_max_buffer_size_removal'), # AssertionError: TypeError not raised
            test.test_io.CBufferedWriterTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedWriterTest('test_readonly_attributes'), # AssertionError: AttributeError not raised
            test.test_io.CBufferedWriterTest('test_uninitialized'), # TypeError: BufferedWriter() takes at least 1 argument (0 given)
            test.test_io.CBufferedWriterTest('test_write_error_on_close'), # AssertionError: OSError not raised by close
            test.test_io.CBufferedWriterTest('test_write_non_blocking'), # TypeError: expected int, got NoneType
            test.test_io.PyBufferedWriterTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedRWPairTest('test_constructor_max_buffer_size_removal'), # AssertionError: TypeError not raised
            test.test_io.CBufferedRWPairTest('test_reader_writer_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedRWPairTest('test_uninitialized'), # TypeError: BufferedRWPair() takes at least 2 arguments (0 given)
            test.test_io.PyBufferedRWPairTest('test_reader_writer_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedRandomTest('test_close_error_on_close'), # AssertionError: None is not an instance of <class 'OSError'>
            test.test_io.CBufferedRandomTest('test_max_buffer_size_removal'), # AssertionError: TypeError not raised
            test.test_io.CBufferedRandomTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.CBufferedRandomTest('test_read_non_blocking'), # AssertionError: b'' is not None
            test.test_io.CBufferedRandomTest('test_read_on_closed'), # AssertionError: ValueError not raised by read1
            test.test_io.CBufferedRandomTest('test_readonly_attributes'), # AssertionError: AttributeError not raised
            test.test_io.CBufferedRandomTest('test_repr'), # AssertionError: '<BufferedRandom object at 0x000000000000003A>' != '<_io.BufferedRandom>'
            test.test_io.CBufferedRandomTest('test_uninitialized'), # TypeError: BufferedRandom() takes at least 1 argument (0 given)
            test.test_io.CBufferedRandomTest('test_write_error_on_close'), # AssertionError: OSError not raised by close
            test.test_io.CBufferedRandomTest('test_write_non_blocking'), # TypeError: expected int, got NoneType
            test.test_io.PyBufferedRandomTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.PyBufferedRandomTest('test_read_on_closed'), # AssertionError: ValueError not raised by read1
            test.test_io.CTextIOWrapperTest('test_append_bom'), # AssertionError: b'\xef\xbb\xbfaaa\xef\xbb\xbfxxx' != b'\xef\xbb\xbfaaaxxx'
            test.test_io.CTextIOWrapperTest('test_close_error_on_close'), # AssertionError: OSError not raised
            test.test_io.CTextIOWrapperTest('test_encoded_writes'), # UnicodeEncodeError
            test.test_io.CTextIOWrapperTest('test_flush_error_on_close'), # AssertionError: OSError not raised by close
            test.test_io.CTextIOWrapperTest('test_initialization'), # AssertionError: ValueError not raised by read
            test.test_io.CTextIOWrapperTest('test_non_text_encoding_codecs_are_rejected'), # AssertionError: LookupError not raised
            test.test_io.CTextIOWrapperTest('test_nonnormalized_close_error_on_close'), # AssertionError: NameError not raised
            test.test_io.CTextIOWrapperTest('test_rawio'), # AttributeError: 'CMockRawIO' object has no attribute 'read1'
            test.test_io.CTextIOWrapperTest('test_read_nonbytes'), # AttributeError: 'StringIO' object has no attribute 'read1'
            test.test_io.CTextIOWrapperTest('test_seek_append_bom'), # OSError: [Errno -2146232800] Unable seek backward to overwrite data that previously existed in a file opened in Append mode.
            test.test_io.CTextIOWrapperTest('test_seek_bom'), # AssertionError: b'\xef\xbb\xbfbbb\xef\xbb\xbfzzz' != b'\xef\xbb\xbfbbbzzz'
            test.test_io.CTextIOWrapperTest('test_uninitialized'), # AssertionError: Exception not raised by repr
            test.test_io.CTextIOWrapperTest('test_unseekable'), # OSError: underlying stream is not seekable
            test.test_io.PyTextIOWrapperTest('test_nonnormalized_close_error_on_close'), # AssertionError: None is not an instance of <class 'NameError'>
            test.test_io.PyTextIOWrapperTest('test_seek_append_bom'), # OSError: [Errno -2146232800] Unable seek backward to overwrite data that previously existed in a file opened in Append mode.
            test.test_io.CMiscIOTest('test_io_after_close'), # AttributeError: 'TextIOWrapper' object has no attribute 'read1'
            test.test_io.CMiscIOTest('test_nonblock_pipe_write_bigbuf'), # AttributeError: 'module' object has no attribute 'fcntl'
            test.test_io.CMiscIOTest('test_nonblock_pipe_write_smallbuf'), # AttributeError: 'module' object has no attribute 'fcntl'
            test.test_io.CMiscIOTest('test_pickling'), # AssertionError: TypeError not raised by _dumps
            test.test_io.CMiscIOTest('test_readinto_buffer_overflow'), # IndexError: Index was outside the bounds of the array.
            test.test_io.CMiscIOTest('test_warn_on_dealloc'), # AssertionError: ResourceWarning not triggered
            test.test_io.CMiscIOTest('test_warn_on_dealloc_fd'), # AssertionError: ResourceWarning not triggered
            test.test_io.PyMiscIOTest('test_nonblock_pipe_write_bigbuf'), # AttributeError: 'module' object has no attribute 'fcntl'
            test.test_io.PyMiscIOTest('test_nonblock_pipe_write_smallbuf'), # AttributeError: 'module' object has no attribute 'fcntl'
            test.test_io.PyMiscIOTest('test_warn_on_dealloc'), # AssertionError: ResourceWarning not triggered
            test.test_io.PyMiscIOTest('test_warn_on_dealloc_fd'), # AssertionError: ResourceWarning not triggered

            # BufferError: memoryview: invalid buffer exported from object of type EmptyStruct
            test.test_io.CIOTest('test_buffered_file_io'),
            test.test_io.CIOTest('test_raw_bytes_io'),
            test.test_io.CIOTest('test_raw_file_io'),
            test.test_io.PyIOTest('test_buffered_file_io'),
            test.test_io.PyIOTest('test_raw_bytes_io'),
            test.test_io.PyIOTest('test_raw_file_io'),
            test.test_io.CBufferedRWPairTest('test_readinto'),
            test.test_io.PyBufferedRWPairTest('test_readinto'),

            # TODO: these are new in 3.6
            test.test_io.CIOTest('test_BufferedIOBase_readinto'),
            test.test_io.CIOTest('test_buffered_readinto_mixin'),
            test.test_io.CIOTest('test_next_nonsizeable'),
            test.test_io.CIOTest('test_optional_abilities'),
            test.test_io.PyIOTest('test_buffered_readinto_mixin'),
            test.test_io.PyIOTest('test_optional_abilities'),
            test.test_io.APIMismatchTest('test_RawIOBase_io_in_pyio_match'),
            test.test_io.APIMismatchTest('test_RawIOBase_pyio_in_io_match'),
            test.test_io.CBufferedReaderTest('test_readinto1'),
            test.test_io.CBufferedReaderTest('test_readinto1_array'),
            test.test_io.CBufferedReaderTest('test_readinto_array'),
            test.test_io.CBufferedRandomTest('test_readinto1'),
            test.test_io.CBufferedRandomTest('test_readinto1_array'),
            test.test_io.CBufferedRandomTest('test_readinto_array'),
            test.test_io.CTextIOWrapperTest('test_illegal_encoder'),
            test.test_io.CTextIOWrapperTest('test_issue25862'),
            test.test_io.CTextIOWrapperTest('test_read_byteslike'),
            test.test_io.PyTextIOWrapperTest('test_illegal_encoder'),
            test.test_io.PyTextIOWrapperTest('test_read_byteslike'),
        ]

        if is_mono:
            failing_tests += [
                test.test_io.PyMiscIOTest('test_create_fail'),
            ]

        skip_tests = [
            test.test_io.CBufferedReaderTest('test_override_destructor'), # StackOverflowException
            test.test_io.CBufferedWriterTest('test_override_destructor'), # StackOverflowException
            test.test_io.CBufferedRandomTest('test_override_destructor'), # StackOverflowException
            test.test_io.CTextIOWrapperTest('test_bufio_write_through'), # StackOverflowException
            test.test_io.CTextIOWrapperTest('test_override_destructor'), # StackOverflowException

            # failure prevents files from closing
            test.test_io.CIOTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.PyIOTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.CBufferedReaderTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.CBufferedWriterTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.CBufferedRandomTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.CTextIOWrapperTest('test_garbage_collection'), # AssertionError: filter ('', ResourceWarning) did not catch any warning
            test.test_io.PyIOTest('test_destructor'), # AssertionError: Lists differ: [2, 3, 1, 2] != [1, 2, 3]

            # StackOverflowException
            test.test_io.CBufferedReaderTest('test_recursive_repr'),
            test.test_io.PyBufferedReaderTest('test_recursive_repr'),
            test.test_io.CBufferedWriterTest('test_recursive_repr'),
            test.test_io.PyBufferedWriterTest('test_recursive_repr'),
            test.test_io.CBufferedRandomTest('test_recursive_repr'),
            test.test_io.PyBufferedRandomTest('test_recursive_repr'),
            test.test_io.CTextIOWrapperTest('test_recursive_repr'),
            test.test_io.PyTextIOWrapperTest('test_recursive_repr'),

            # __del__ not getting called on shutdown?
            test.test_io.CTextIOWrapperTest('test_create_at_shutdown_with_encoding'),
            test.test_io.CTextIOWrapperTest('test_create_at_shutdown_without_encoding'),
            test.test_io.PyTextIOWrapperTest('test_create_at_shutdown_with_encoding'),
            test.test_io.PyTextIOWrapperTest('test_create_at_shutdown_without_encoding'),
            test.test_io.CMiscIOTest('test_daemon_threads_shutdown_stderr_deadlock'),
            test.test_io.CMiscIOTest('test_daemon_threads_shutdown_stdout_deadlock'),

            # AttributeError: 'module' object has no attribute 'SIGALRM'
            test.test_io.CSignalsTest('test_interrupted_read_retry_buffered'),
            test.test_io.CSignalsTest('test_interrupted_read_retry_text'),
            test.test_io.CSignalsTest('test_interrupted_write_buffered'),
            test.test_io.CSignalsTest('test_interrupted_write_retry_buffered'),
            test.test_io.CSignalsTest('test_interrupted_write_retry_text'),
            test.test_io.CSignalsTest('test_interrupted_write_text'),
            test.test_io.CSignalsTest('test_interrupted_write_unbuffered'),
            test.test_io.CSignalsTest('test_reentrant_write_buffered'),
            test.test_io.CSignalsTest('test_reentrant_write_text'),
            test.test_io.PySignalsTest('test_interrupted_read_retry_buffered'),
            test.test_io.PySignalsTest('test_interrupted_read_retry_text'),
            test.test_io.PySignalsTest('test_interrupted_write_buffered'),
            test.test_io.PySignalsTest('test_interrupted_write_retry_buffered'),
            test.test_io.PySignalsTest('test_interrupted_write_retry_text'),
            test.test_io.PySignalsTest('test_interrupted_write_text'),
            test.test_io.PySignalsTest('test_interrupted_write_unbuffered'),

            # failure prevents files from closing
            test.test_io.CTextIOWrapperTest('test_seek_and_tell'), # TypeError: NoneType is not callable
            test.test_io.CMiscIOTest('test_attributes'), # AssertionError: 'r' != 'U'
            test.test_io.PyMiscIOTest('test_attributes'), # AssertionError: 'wb+' != 'rb+'
        ]

        if is_mono:
            skip_tests += [
                # On Mono, gc.collect() may return before collection is finished making some tests unreliable
                test.test_io.CBufferedRandomTest('test_destructor'),
                test.test_io.CBufferedWriterTest('test_destructor'),
                test.test_io.PyBufferedWriterTest('test_destructor'),
                test.test_io.PyBufferedRandomTest('test_destructor'),
                test.test_io.PyBufferedReaderTest('test_override_destructor'),
                test.test_io.PyBufferedWriterTest('test_override_destructor'),
                test.test_io.PyBufferedRandomTest('test_override_destructor'),

                test.test_io.CTextIOWrapperTest('test_destructor'),
                test.test_io.CIOTest('test_IOBase_finalize'),

                test.test_io.PyTextIOWrapperTest('test_destructor'),
                test.test_io.PyTextIOWrapperTest('test_override_destructor'),
                test.test_io.PyIOTest('test_RawIOBase_destructor'),
                test.test_io.PyIOTest('test_BufferedIOBase_destructor'),
                test.test_io.PyIOTest('test_IOBase_destructor'),
                test.test_io.PyIOTest('test_TextIOBase_destructor'),

                test.test_io.CMiscIOTest('test_blockingioerror'),
                test.test_io.PyMiscIOTest('test_blockingioerror'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
