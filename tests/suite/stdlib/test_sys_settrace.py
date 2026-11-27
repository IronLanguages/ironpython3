# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sys_settrace from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_sys_settrace

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_sys_settrace, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_sys_settrace.TraceTestCase('test_10_ireturn'), # CPython 3.4 reports the return on the line of `else: pass`, which its bytecode ends the if statement with
            test.test_sys_settrace.TraceTestCase('test_13_genexp'), # the generator abandoned by any() is closed by the garbage collector, not at once by reference counting
        ]
        if sys.version_info >= (3, 6):
            failing_tests += [
                # f_lineno doesn't reject jumps into or between with and try blocks: ValueError not raised
                test.test_sys_settrace.JumpTestCase('test_no_jump_backwards_into_try_except_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_backwards_into_try_finally_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_backwards_into_with_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_between_except_blocks_2'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_between_with_blocks'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_forwards_into_try_except_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_forwards_into_try_finally_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_forwards_into_with_block'),
                # f_lineno doesn't check the event type: jumps from 'call' and 'exception' events are allowed, 'return' and yield give a different message
                test.test_sys_settrace.JumpTestCase('test_no_jump_from_call'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_from_exception_event'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_from_return_event'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_from_yield'),
                # jumping past a line in a finally block loses the pending exception: ZeroDivisionError not raised
                test.test_sys_settrace.JumpTestCase('test_jump_in_nested_finally_2'),
                test.test_sys_settrace.JumpTestCase('test_jump_in_nested_finally_3'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_with_assignment'), # no line event for the `as x:` continuation line, so the jump never happens
                # asyncio.new_event_loop() fails: socket.fileno() returns Int64, which selectors rejects
                test.test_sys_settrace.JumpTestCase('test_jump_across_async_with'),
                test.test_sys_settrace.JumpTestCase('test_jump_backwards_out_of_async_with_block'),
                test.test_sys_settrace.JumpTestCase('test_jump_forwards_out_of_async_with_block'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_for_block_backwards'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_for_block_forwards'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_with_assignment'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_with_block_within_finally_block'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_with_block_within_for_block'),
                test.test_sys_settrace.JumpTestCase('test_jump_out_of_async_with_block_within_with_block'),
                test.test_sys_settrace.JumpTestCase('test_jump_over_async_for_block_before_else'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_backwards_into_async_for_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_backwards_into_async_with_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_between_async_with_blocks'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_forwards_into_async_for_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_forwards_into_async_with_block'),
                test.test_sys_settrace.JumpTestCase('test_no_jump_into_async_for_block_before_else'),
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
