# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import threading
import typing

from iptest import IronPythonTestCase, run_test

class TraceListenerCountTest(IronPythonTestCase):
    def test_typing_lookup_on_thread_while_main_thread_traced(self):
        # https://github.com/IronLanguages/ironpython3/issues/2072
        results = []

        def tracer(frame, event, arg):
            return tracer

        def do_check():
            results.append(isinstance(None, typing.Iterable))

        thread = threading.Thread(target=do_check)
        sys.settrace(tracer)
        try:
            thread.start()
            thread.join(5)
            self.assertFalse(thread.is_alive())
        finally:
            sys.settrace(None)

        self.assertEqual(results, [False])

    def test_dummy_listener_replaced_by_thread_trace(self):
        # https://github.com/IronLanguages/ironpython3/pull/2075
        worker_trace_set = threading.Event()
        allow_probe = threading.Event()
        probe_events = []

        def main_trace(frame, event, arg):
            return main_trace

        def worker_trace(frame, event, arg):
            if frame.f_code.co_name == 'probe':
                probe_events.append(event)
            return worker_trace

        def worker():
            try:
                sys.settrace(worker_trace)
                worker_trace_set.set()
                allow_probe.wait()

                namespace = {}
                exec('def probe():\n    pass', namespace)
                namespace['probe']()
            finally:
                sys.settrace(None)

        thread = threading.Thread(target=worker)
        sys.settrace(main_trace)
        try:
            thread.start()
            self.assertTrue(worker_trace_set.wait(5))
        finally:
            sys.settrace(None)
            allow_probe.set()
            thread.join(5)
            self.assertFalse(thread.is_alive())

        self.assertIn('call', probe_events)

run_test(__name__)
