"""Tests for selector_events.py"""

import errno
import socket
import unittest
from unittest import mock
try:
    import ssl
except ImportError:
    ssl = None

import asyncio
from asyncio import selectors
from asyncio import test_utils
from asyncio.selector_events import BaseSelectorEventLoop
from asyncio.selector_events import _SelectorTransport
from asyncio.selector_events import _SelectorSslTransport
from asyncio.selector_events import _SelectorSocketTransport
from asyncio.selector_events import _SelectorDatagramTransport


MOCK_ANY = mock.ANY


class TestBaseSelectorEventLoop(BaseSelectorEventLoop):

    def close(self):
        # Don't call the close() method of the parent class, because the
        # selector is mocked
        self._closed = True

    def _make_self_pipe(self):
        self._ssock = mock.Mock()
        self._csock = mock.Mock()
        self._internal_fds += 1


def list_to_buffer(l=()):
    return bytearray().join(l)


def close_transport(transport):
    # Don't call transport.close() because the event loop and the selector
    # are mocked
    if transport._sock is None:
        return
    transport._sock.close()
    transport._sock = None


class BaseSelectorEventLoopTests(test_utils.TestCase):

    def setUp(self):
        self.selector = mock.Mock()
        self.selector.select.return_value = []
        self.loop = TestBaseSelectorEventLoop(self.selector)
        self.set_event_loop(self.loop)

    def test_make_socket_transport(self):
        m = mock.Mock()
        self.loop.add_reader = mock.Mock()
        self.loop.add_reader._is_coroutine = False
        transport = self.loop._make_socket_transport(m, asyncio.Protocol())
        self.assertIsInstance(transport, _SelectorSocketTransport)

        # Calling repr() must not fail when the event loop is closed
        self.loop.close()
        repr(transport)

        close_transport(transport)

    @unittest.skipIf(ssl is None, 'No ssl module')
    def test_make_ssl_transport(self):
        m = mock.Mock()
        self.loop.add_reader = mock.Mock()
        self.loop.add_reader._is_coroutine = False
        self.loop.add_writer = mock.Mock()
        self.loop.remove_reader = mock.Mock()
        self.loop.remove_writer = mock.Mock()
        waiter = asyncio.Future(loop=self.loop)
        with test_utils.disable_logger():
            transport = self.loop._make_ssl_transport(
                m, asyncio.Protocol(), m, waiter)
            # execute the handshake while the logger is disabled
            # to ignore SSL handshake failure
            test_utils.run_briefly(self.loop)

        # Sanity check
        class_name = transport.__class__.__name__
        self.assertIn("ssl", class_name.lower())
        self.assertIn("transport", class_name.lower())

        transport.close()
        # execute pending callbacks to close the socket transport
        test_utils.run_briefly(self.loop)

    @mock.patch('asyncio.selector_events.ssl', None)
    @mock.patch('asyncio.sslproto.ssl', None)
    def test_make_ssl_transport_without_ssl_error(self):
        m = mock.Mock()
        self.loop.add_reader = mock.Mock()
        self.loop.add_writer = mock.Mock()
        self.loop.remove_reader = mock.Mock()
        self.loop.remove_writer = mock.Mock()
        with self.assertRaises(RuntimeError):
            self.loop._make_ssl_transport(m, m, m, m)

    def test_close(self):
        class EventLoop(BaseSelectorEventLoop):
            def _make_self_pipe(self):
                self._ssock = mock.Mock()
                self._csock = mock.Mock()
                self._internal_fds += 1

        self.loop = EventLoop(self.selector)
        self.set_event_loop(self.loop)

        ssock = self.loop._ssock
        ssock.fileno.return_value = 7
        csock = self.loop._csock
        csock.fileno.return_value = 1
        remove_reader = self.loop.remove_reader = mock.Mock()

        self.loop._selector.close()
        self.loop._selector = selector = mock.Mock()
        self.assertFalse(self.loop.is_closed())

        self.loop.close()
        self.assertTrue(self.loop.is_closed())
        self.assertIsNone(self.loop._selector)
        self.assertIsNone(self.loop._csock)
        self.assertIsNone(self.loop._ssock)
        selector.close.assert_called_with()
        ssock.close.assert_called_with()
        csock.close.assert_called_with()
        remove_reader.assert_called_with(7)

        # it should be possible to call close() more than once
        self.loop.close()
        self.loop.close()

        # operation blocked when the loop is closed
        f = asyncio.Future(loop=self.loop)
        self.assertRaises(RuntimeError, self.loop.run_forever)
        self.assertRaises(RuntimeError, self.loop.run_until_complete, f)
        fd = 0
        def callback():
            pass
        self.assertRaises(RuntimeError, self.loop.add_reader, fd, callback)
        self.assertRaises(RuntimeError, self.loop.add_writer, fd, callback)

    def test_close_no_selector(self):
        self.loop.remove_reader = mock.Mock()
        self.loop._selector.close()
        self.loop._selector = None
        self.loop.close()
        self.assertIsNone(self.loop._selector)

    def test_socketpair(self):
        self.assertRaises(NotImplementedError, self.loop._socketpair)

    def test_read_from_self_tryagain(self):
        self.loop._ssock.recv.side_effect = BlockingIOError
        self.assertIsNone(self.loop._read_from_self())

    def test_read_from_self_exception(self):
        self.loop._ssock.recv.side_effect = OSError
        self.assertRaises(OSError, self.loop._read_from_self)

    def test_write_to_self_tryagain(self):
        self.loop._csock.send.side_effect = BlockingIOError
        with test_utils.disable_logger():
            self.assertIsNone(self.loop._write_to_self())

    def test_write_to_self_exception(self):
        # _write_to_self() swallows OSError
        self.loop._csock.send.side_effect = RuntimeError()
        self.assertRaises(RuntimeError, self.loop._write_to_self)

    def test_sock_recv(self):
        sock = test_utils.mock_nonblocking_socket()
        self.loop._sock_recv = mock.Mock()

        f = self.loop.sock_recv(sock, 1024)
        self.assertIsInstance(f, asyncio.Future)
        self.loop._sock_recv.assert_called_with(f, False, sock, 1024)

    def test__sock_recv_canceled_fut(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop._sock_recv(f, False, sock, 1024)
        self.assertFalse(sock.recv.called)

    def test__sock_recv_unregister(self):
        sock = mock.Mock()
        sock.fileno.return_value = 10

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop.remove_reader = mock.Mock()
        self.loop._sock_recv(f, True, sock, 1024)
        self.assertEqual((10,), self.loop.remove_reader.call_args[0])

    def test__sock_recv_tryagain(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.recv.side_effect = BlockingIOError

        self.loop.add_reader = mock.Mock()
        self.loop._sock_recv(f, False, sock, 1024)
        self.assertEqual((10, self.loop._sock_recv, f, True, sock, 1024),
                         self.loop.add_reader.call_args[0])

    def test__sock_recv_exception(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        err = sock.recv.side_effect = OSError()

        self.loop._sock_recv(f, False, sock, 1024)
        self.assertIs(err, f.exception())

    def test_sock_sendall(self):
        sock = test_utils.mock_nonblocking_socket()
        self.loop._sock_sendall = mock.Mock()

        f = self.loop.sock_sendall(sock, b'data')
        self.assertIsInstance(f, asyncio.Future)
        self.assertEqual(
            (f, False, sock, b'data'),
            self.loop._sock_sendall.call_args[0])

    def test_sock_sendall_nodata(self):
        sock = test_utils.mock_nonblocking_socket()
        self.loop._sock_sendall = mock.Mock()

        f = self.loop.sock_sendall(sock, b'')
        self.assertIsInstance(f, asyncio.Future)
        self.assertTrue(f.done())
        self.assertIsNone(f.result())
        self.assertFalse(self.loop._sock_sendall.called)

    def test__sock_sendall_canceled_fut(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertFalse(sock.send.called)

    def test__sock_sendall_unregister(self):
        sock = mock.Mock()
        sock.fileno.return_value = 10

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop.remove_writer = mock.Mock()
        self.loop._sock_sendall(f, True, sock, b'data')
        self.assertEqual((10,), self.loop.remove_writer.call_args[0])

    def test__sock_sendall_tryagain(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.send.side_effect = BlockingIOError

        self.loop.add_writer = mock.Mock()
        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertEqual(
            (10, self.loop._sock_sendall, f, True, sock, b'data'),
            self.loop.add_writer.call_args[0])

    def test__sock_sendall_interrupted(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.send.side_effect = InterruptedError

        self.loop.add_writer = mock.Mock()
        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertEqual(
            (10, self.loop._sock_sendall, f, True, sock, b'data'),
            self.loop.add_writer.call_args[0])

    def test__sock_sendall_exception(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        err = sock.send.side_effect = OSError()

        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertIs(f.exception(), err)

    def test__sock_sendall(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        sock.fileno.return_value = 10
        sock.send.return_value = 4

        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertTrue(f.done())
        self.assertIsNone(f.result())

    def test__sock_sendall_partial(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        sock.fileno.return_value = 10
        sock.send.return_value = 2

        self.loop.add_writer = mock.Mock()
        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertFalse(f.done())
        self.assertEqual(
            (10, self.loop._sock_sendall, f, True, sock, b'ta'),
            self.loop.add_writer.call_args[0])

    def test__sock_sendall_none(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        sock.fileno.return_value = 10
        sock.send.return_value = 0

        self.loop.add_writer = mock.Mock()
        self.loop._sock_sendall(f, False, sock, b'data')
        self.assertFalse(f.done())
        self.assertEqual(
            (10, self.loop._sock_sendall, f, True, sock, b'data'),
            self.loop.add_writer.call_args[0])

    def test_sock_connect(self):
        sock = test_utils.mock_nonblocking_socket()
        self.loop._sock_connect = mock.Mock()

        f = self.loop.sock_connect(sock, ('127.0.0.1', 8080))
        self.assertIsInstance(f, asyncio.Future)
        self.assertEqual(
            (f, sock, ('127.0.0.1', 8080)),
            self.loop._sock_connect.call_args[0])

    def test_sock_connect_timeout(self):
        # asyncio issue #205: sock_connect() must unregister the socket on
        # timeout error

        # prepare mocks
        self.loop.add_writer = mock.Mock()
        self.loop.remove_writer = mock.Mock()
        sock = test_utils.mock_nonblocking_socket()
        sock.connect.side_effect = BlockingIOError

        # first call to sock_connect() registers the socket
        fut = self.loop.sock_connect(sock, ('127.0.0.1', 80))
        self.assertTrue(sock.connect.called)
        self.assertTrue(self.loop.add_writer.called)
        self.assertEqual(len(fut._callbacks), 1)

        # on timeout, the socket must be unregistered
        sock.connect.reset_mock()
        fut.set_exception(asyncio.TimeoutError)
        with self.assertRaises(asyncio.TimeoutError):
            self.loop.run_until_complete(fut)
        self.assertTrue(self.loop.remove_writer.called)

    def test__sock_connect(self):
        f = asyncio.Future(loop=self.loop)

        sock = mock.Mock()
        sock.fileno.return_value = 10

        self.loop._sock_connect(f, sock, ('127.0.0.1', 8080))
        self.assertTrue(f.done())
        self.assertIsNone(f.result())
        self.assertTrue(sock.connect.called)

    def test__sock_connect_cb_cancelled_fut(self):
        sock = mock.Mock()
        self.loop.remove_writer = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop._sock_connect_cb(f, sock, ('127.0.0.1', 8080))
        self.assertFalse(sock.getsockopt.called)

    def test__sock_connect_writer(self):
        # check that the fd is registered and then unregistered
        self.loop._process_events = mock.Mock()
        self.loop.add_writer = mock.Mock()
        self.loop.remove_writer = mock.Mock()

        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.connect.side_effect = BlockingIOError
        sock.getsockopt.return_value = 0
        address = ('127.0.0.1', 8080)

        f = asyncio.Future(loop=self.loop)
        self.loop._sock_connect(f, sock, address)
        self.assertTrue(self.loop.add_writer.called)
        self.assertEqual(10, self.loop.add_writer.call_args[0][0])

        self.loop._sock_connect_cb(f, sock, address)
        # need to run the event loop to execute _sock_connect_done() callback
        self.loop.run_until_complete(f)
        self.assertEqual((10,), self.loop.remove_writer.call_args[0])

    def test__sock_connect_cb_tryagain(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.getsockopt.return_value = errno.EAGAIN

        # check that the exception is handled
        self.loop._sock_connect_cb(f, sock, ('127.0.0.1', 8080))

    def test__sock_connect_cb_exception(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.getsockopt.return_value = errno.ENOTCONN

        self.loop.remove_writer = mock.Mock()
        self.loop._sock_connect_cb(f, sock, ('127.0.0.1', 8080))
        self.assertIsInstance(f.exception(), OSError)

    def test_sock_accept(self):
        sock = test_utils.mock_nonblocking_socket()
        self.loop._sock_accept = mock.Mock()

        f = self.loop.sock_accept(sock)
        self.assertIsInstance(f, asyncio.Future)
        self.assertEqual(
            (f, False, sock), self.loop._sock_accept.call_args[0])

    def test__sock_accept(self):
        f = asyncio.Future(loop=self.loop)

        conn = mock.Mock()

        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.accept.return_value = conn, ('127.0.0.1', 1000)

        self.loop._sock_accept(f, False, sock)
        self.assertTrue(f.done())
        self.assertEqual((conn, ('127.0.0.1', 1000)), f.result())
        self.assertEqual((False,), conn.setblocking.call_args[0])

    def test__sock_accept_canceled_fut(self):
        sock = mock.Mock()

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop._sock_accept(f, False, sock)
        self.assertFalse(sock.accept.called)

    def test__sock_accept_unregister(self):
        sock = mock.Mock()
        sock.fileno.return_value = 10

        f = asyncio.Future(loop=self.loop)
        f.cancel()

        self.loop.remove_reader = mock.Mock()
        self.loop._sock_accept(f, True, sock)
        self.assertEqual((10,), self.loop.remove_reader.call_args[0])

    def test__sock_accept_tryagain(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        sock.accept.side_effect = BlockingIOError

        self.loop.add_reader = mock.Mock()
        self.loop._sock_accept(f, False, sock)
        self.assertEqual(
            (10, self.loop._sock_accept, f, True, sock),
            self.loop.add_reader.call_args[0])

    def test__sock_accept_exception(self):
        f = asyncio.Future(loop=self.loop)
        sock = mock.Mock()
        sock.fileno.return_value = 10
        err = sock.accept.side_effect = OSError()

        self.loop._sock_accept(f, False, sock)
        self.assertIs(err, f.exception())

    def test_add_reader(self):
        self.loop._selector.get_key.side_effect = KeyError
        cb = lambda: True
        self.loop.add_reader(1, cb)

        self.assertTrue(self.loop._selector.register.called)
        fd, mask, (r, w) = self.loop._selector.register.call_args[0]
        self.assertEqual(1, fd)
        self.assertEqual(selectors.EVENT_READ, mask)
        self.assertEqual(cb, r._callback)
        self.assertIsNone(w)

    def test_add_reader_existing(self):
        reader = mock.Mock()
        writer = mock.Mock()
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_WRITE, (reader, writer))
        cb = lambda: True
        self.loop.add_reader(1, cb)

        self.assertTrue(reader.cancel.called)
        self.assertFalse(self.loop._selector.register.called)
        self.assertTrue(self.loop._selector.modify.called)
        fd, mask, (r, w) = self.loop._selector.modify.call_args[0]
        self.assertEqual(1, fd)
        self.assertEqual(selectors.EVENT_WRITE | selectors.EVENT_READ, mask)
        self.assertEqual(cb, r._callback)
        self.assertEqual(writer, w)

    def test_add_reader_existing_writer(self):
        writer = mock.Mock()
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_WRITE, (None, writer))
        cb = lambda: True
        self.loop.add_reader(1, cb)

        self.assertFalse(self.loop._selector.register.called)
        self.assertTrue(self.loop._selector.modify.called)
        fd, mask, (r, w) = self.loop._selector.modify.call_args[0]
        self.assertEqual(1, fd)
        self.assertEqual(selectors.EVENT_WRITE | selectors.EVENT_READ, mask)
        self.assertEqual(cb, r._callback)
        self.assertEqual(writer, w)

    def test_remove_reader(self):
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_READ, (None, None))
        self.assertFalse(self.loop.remove_reader(1))

        self.assertTrue(self.loop._selector.unregister.called)

    def test_remove_reader_read_write(self):
        reader = mock.Mock()
        writer = mock.Mock()
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_READ | selectors.EVENT_WRITE,
            (reader, writer))
        self.assertTrue(
            self.loop.remove_reader(1))

        self.assertFalse(self.loop._selector.unregister.called)
        self.assertEqual(
            (1, selectors.EVENT_WRITE, (None, writer)),
            self.loop._selector.modify.call_args[0])

    def test_remove_reader_unknown(self):
        self.loop._selector.get_key.side_effect = KeyError
        self.assertFalse(
            self.loop.remove_reader(1))

    def test_add_writer(self):
        self.loop._selector.get_key.side_effect = KeyError
        cb = lambda: True
        self.loop.add_writer(1, cb)

        self.assertTrue(self.loop._selector.register.called)
        fd, mask, (r, w) = self.loop._selector.register.call_args[0]
        self.assertEqual(1, fd)
        self.assertEqual(selectors.EVENT_WRITE, mask)
        self.assertIsNone(r)
        self.assertEqual(cb, w._callback)

    def test_add_writer_existing(self):
        reader = mock.Mock()
        writer = mock.Mock()
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_READ, (reader, writer))
        cb = lambda: True
        self.loop.add_writer(1, cb)

        self.assertTrue(writer.cancel.called)
        self.assertFalse(self.loop._selector.register.called)
        self.assertTrue(self.loop._selector.modify.called)
        fd, mask, (r, w) = self.loop._selector.modify.call_args[0]
        self.assertEqual(1, fd)
        self.assertEqual(selectors.EVENT_WRITE | selectors.EVENT_READ, mask)
        self.assertEqual(reader, r)
        self.assertEqual(cb, w._callback)

    def test_remove_writer(self):
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_WRITE, (None, None))
        self.assertFalse(self.loop.remove_writer(1))

        self.assertTrue(self.loop._selector.unregister.called)

    def test_remove_writer_read_write(self):
        reader = mock.Mock()
        writer = mock.Mock()
        self.loop._selector.get_key.return_value = selectors.SelectorKey(
            1, 1, selectors.EVENT_READ | selectors.EVENT_WRITE,
            (reader, writer))
        self.assertTrue(
            self.loop.remove_writer(1))

        self.assertFalse(self.loop._selector.unregister.called)
        self.assertEqual(
            (1, selectors.EVENT_READ, (reader, None)),
            self.loop._selector.modify.call_args[0])

    def test_remove_writer_unknown(self):
        self.loop._selector.get_key.side_effect = KeyError
        self.assertFalse(
            self.loop.remove_writer(1))

    def test_process_events_read(self):
        reader = mock.Mock()
        reader._cancelled = False

        self.loop._add_callback = mock.Mock()
        self.loop._process_events(
            [(selectors.SelectorKey(
                1, 1, selectors.EVENT_READ, (reader, None)),
              selectors.EVENT_READ)])
        self.assertTrue(self.loop._add_callback.called)
        self.loop._add_callback.assert_called_with(reader)

    def test_process_events_read_cancelled(self):
        reader = mock.Mock()
        reader.cancelled = True

        self.loop.remove_reader = mock.Mock()
        self.loop._process_events(
            [(selectors.SelectorKey(
                1, 1, selectors.EVENT_READ, (reader, None)),
             selectors.EVENT_READ)])
        self.loop.remove_reader.assert_called_with(1)

    def test_process_events_write(self):
        writer = mock.Mock()
        writer._cancelled = False

        self.loop._add_callback = mock.Mock()
        self.loop._process_events(
            [(selectors.SelectorKey(1, 1, selectors.EVENT_WRITE,
                                    (None, writer)),
              selectors.EVENT_WRITE)])
        self.loop._add_callback.assert_called_with(writer)

    def test_process_events_write_cancelled(self):
        writer = mock.Mock()
        writer.cancelled = True
        self.loop.remove_writer = mock.Mock()

        self.loop._process_events(
            [(selectors.SelectorKey(1, 1, selectors.EVENT_WRITE,
                                    (None, writer)),
              selectors.EVENT_WRITE)])
        self.loop.remove_writer.assert_called_with(1)


class SelectorTransportTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()
        self.protocol = test_utils.make_test_protocol(asyncio.Protocol)
        self.sock = mock.Mock(socket.socket)
        self.sock.fileno.return_value = 7

    def create_transport(self):
        transport = _SelectorTransport(self.loop, self.sock, self.protocol,
                                       None)
        self.addCleanup(close_transport, transport)
        return transport

    def test_ctor(self):
        tr = self.create_transport()
        self.assertIs(tr._loop, self.loop)
        self.assertIs(tr._sock, self.sock)
        self.assertIs(tr._sock_fd, 7)

    def test_abort(self):
        tr = self.create_transport()
        tr._force_close = mock.Mock()

        tr.abort()
        tr._force_close.assert_called_with(None)

    def test_close(self):
        tr = self.create_transport()
        tr.close()

        self.assertTrue(tr.is_closing())
        self.assertEqual(1, self.loop.remove_reader_count[7])
        self.protocol.connection_lost(None)
        self.assertEqual(tr._conn_lost, 1)

        tr.close()
        self.assertEqual(tr._conn_lost, 1)
        self.assertEqual(1, self.loop.remove_reader_count[7])

    def test_close_write_buffer(self):
        tr = self.create_transport()
        tr._buffer.extend(b'data')
        tr.close()

        self.assertFalse(self.loop.readers)
        test_utils.run_briefly(self.loop)
        self.assertFalse(self.protocol.connection_lost.called)

    def test_force_close(self):
        tr = self.create_transport()
        tr._buffer.extend(b'1')
        self.loop.add_reader(7, mock.sentinel)
        self.loop.add_writer(7, mock.sentinel)
        tr._force_close(None)

        self.assertTrue(tr.is_closing())
        self.assertEqual(tr._buffer, list_to_buffer())
        self.assertFalse(self.loop.readers)
        self.assertFalse(self.loop.writers)

        # second close should not remove reader
        tr._force_close(None)
        self.assertFalse(self.loop.readers)
        self.assertEqual(1, self.loop.remove_reader_count[7])

    @mock.patch('asyncio.log.logger.error')
    def test_fatal_error(self, m_exc):
        exc = OSError()
        tr = self.create_transport()
        tr._force_close = mock.Mock()
        tr._fatal_error(exc)

        m_exc.assert_called_with(
            test_utils.MockPattern(
                'Fatal error on transport\nprotocol:.*\ntransport:.*'),
            exc_info=(OSError, MOCK_ANY, MOCK_ANY))

        tr._force_close.assert_called_with(exc)

    def test_connection_lost(self):
        exc = OSError()
        tr = self.create_transport()
        self.assertIsNotNone(tr._protocol)
        self.assertIsNotNone(tr._loop)
        tr._call_connection_lost(exc)

        self.protocol.connection_lost.assert_called_with(exc)
        self.sock.close.assert_called_with()
        self.assertIsNone(tr._sock)

        self.assertIsNone(tr._protocol)
        self.assertIsNone(tr._loop)


class SelectorSocketTransportTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()
        self.protocol = test_utils.make_test_protocol(asyncio.Protocol)
        self.sock = mock.Mock(socket.socket)
        self.sock_fd = self.sock.fileno.return_value = 7

    def socket_transport(self, waiter=None):
        transport = _SelectorSocketTransport(self.loop, self.sock,
                                             self.protocol, waiter=waiter)
        self.addCleanup(close_transport, transport)
        return transport

    def test_ctor(self):
        waiter = asyncio.Future(loop=self.loop)
        tr = self.socket_transport(waiter=waiter)
        self.loop.run_until_complete(waiter)

        self.loop.assert_reader(7, tr._read_ready)
        test_utils.run_briefly(self.loop)
        self.protocol.connection_made.assert_called_with(tr)

    def test_ctor_with_waiter(self):
        waiter = asyncio.Future(loop=self.loop)
        self.socket_transport(waiter=waiter)
        self.loop.run_until_complete(waiter)

        self.assertIsNone(waiter.result())

    def test_pause_resume_reading(self):
        tr = self.socket_transport()
        test_utils.run_briefly(self.loop)
        self.assertFalse(tr._paused)
        self.loop.assert_reader(7, tr._read_ready)
        tr.pause_reading()
        self.assertTrue(tr._paused)
        self.assertFalse(7 in self.loop.readers)
        tr.resume_reading()
        self.assertFalse(tr._paused)
        self.loop.assert_reader(7, tr._read_ready)
        with self.assertRaises(RuntimeError):
            tr.resume_reading()

    def test_read_ready(self):
        transport = self.socket_transport()

        self.sock.recv.return_value = b'data'
        transport._read_ready()

        self.protocol.data_received.assert_called_with(b'data')

    def test_read_ready_eof(self):
        transport = self.socket_transport()
        transport.close = mock.Mock()

        self.sock.recv.return_value = b''
        transport._read_ready()

        self.protocol.eof_received.assert_called_with()
        transport.close.assert_called_with()

    def test_read_ready_eof_keep_open(self):
        transport = self.socket_transport()
        transport.close = mock.Mock()

        self.sock.recv.return_value = b''
        self.protocol.eof_received.return_value = True
        transport._read_ready()

        self.protocol.eof_received.assert_called_with()
        self.assertFalse(transport.close.called)

    @mock.patch('logging.exception')
    def test_read_ready_tryagain(self, m_exc):
        self.sock.recv.side_effect = BlockingIOError

        transport = self.socket_transport()
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        self.assertFalse(transport._fatal_error.called)

    @mock.patch('logging.exception')
    def test_read_ready_tryagain_interrupted(self, m_exc):
        self.sock.recv.side_effect = InterruptedError

        transport = self.socket_transport()
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        self.assertFalse(transport._fatal_error.called)

    @mock.patch('logging.exception')
    def test_read_ready_conn_reset(self, m_exc):
        err = self.sock.recv.side_effect = ConnectionResetError()

        transport = self.socket_transport()
        transport._force_close = mock.Mock()
        with test_utils.disable_logger():
            transport._read_ready()
        transport._force_close.assert_called_with(err)

    @mock.patch('logging.exception')
    def test_read_ready_err(self, m_exc):
        err = self.sock.recv.side_effect = OSError()

        transport = self.socket_transport()
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal read error on socket transport')

    def test_write(self):
        data = b'data'
        self.sock.send.return_value = len(data)

        transport = self.socket_transport()
        transport.write(data)
        self.sock.send.assert_called_with(data)

    def test_write_bytearray(self):
        data = bytearray(b'data')
        self.sock.send.return_value = len(data)

        transport = self.socket_transport()
        transport.write(data)
        self.sock.send.assert_called_with(data)
        self.assertEqual(data, bytearray(b'data'))  # Hasn't been mutated.

    def test_write_memoryview(self):
        data = memoryview(b'data')
        self.sock.send.return_value = len(data)

        transport = self.socket_transport()
        transport.write(data)
        self.sock.send.assert_called_with(data)

    def test_write_no_data(self):
        transport = self.socket_transport()
        transport._buffer.extend(b'data')
        transport.write(b'')
        self.assertFalse(self.sock.send.called)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_buffer(self):
        transport = self.socket_transport()
        transport._buffer.extend(b'data1')
        transport.write(b'data2')
        self.assertFalse(self.sock.send.called)
        self.assertEqual(list_to_buffer([b'data1', b'data2']),
                         transport._buffer)

    def test_write_partial(self):
        data = b'data'
        self.sock.send.return_value = 2

        transport = self.socket_transport()
        transport.write(data)

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'ta']), transport._buffer)

    def test_write_partial_bytearray(self):
        data = bytearray(b'data')
        self.sock.send.return_value = 2

        transport = self.socket_transport()
        transport.write(data)

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'ta']), transport._buffer)
        self.assertEqual(data, bytearray(b'data'))  # Hasn't been mutated.

    def test_write_partial_memoryview(self):
        data = memoryview(b'data')
        self.sock.send.return_value = 2

        transport = self.socket_transport()
        transport.write(data)

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'ta']), transport._buffer)

    def test_write_partial_none(self):
        data = b'data'
        self.sock.send.return_value = 0
        self.sock.fileno.return_value = 7

        transport = self.socket_transport()
        transport.write(data)

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_tryagain(self):
        self.sock.send.side_effect = BlockingIOError

        data = b'data'
        transport = self.socket_transport()
        transport.write(data)

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    @mock.patch('asyncio.selector_events.logger')
    def test_write_exception(self, m_log):
        err = self.sock.send.side_effect = OSError()

        data = b'data'
        transport = self.socket_transport()
        transport._fatal_error = mock.Mock()
        transport.write(data)
        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal write error on socket transport')
        transport._conn_lost = 1

        self.sock.reset_mock()
        transport.write(data)
        self.assertFalse(self.sock.send.called)
        self.assertEqual(transport._conn_lost, 2)
        transport.write(data)
        transport.write(data)
        transport.write(data)
        transport.write(data)
        m_log.warning.assert_called_with('socket.send() raised exception.')

    def test_write_str(self):
        transport = self.socket_transport()
        self.assertRaises(TypeError, transport.write, 'str')

    def test_write_closing(self):
        transport = self.socket_transport()
        transport.close()
        self.assertEqual(transport._conn_lost, 1)
        transport.write(b'data')
        self.assertEqual(transport._conn_lost, 2)

    def test_write_ready(self):
        data = b'data'
        self.sock.send.return_value = len(data)

        transport = self.socket_transport()
        transport._buffer.extend(data)
        self.loop.add_writer(7, transport._write_ready)
        transport._write_ready()
        self.assertTrue(self.sock.send.called)
        self.assertFalse(self.loop.writers)

    def test_write_ready_closing(self):
        data = b'data'
        self.sock.send.return_value = len(data)

        transport = self.socket_transport()
        transport._closing = True
        transport._buffer.extend(data)
        self.loop.add_writer(7, transport._write_ready)
        transport._write_ready()
        self.assertTrue(self.sock.send.called)
        self.assertFalse(self.loop.writers)
        self.sock.close.assert_called_with()
        self.protocol.connection_lost.assert_called_with(None)

    def test_write_ready_no_data(self):
        transport = self.socket_transport()
        # This is an internal error.
        self.assertRaises(AssertionError, transport._write_ready)

    def test_write_ready_partial(self):
        data = b'data'
        self.sock.send.return_value = 2

        transport = self.socket_transport()
        transport._buffer.extend(data)
        self.loop.add_writer(7, transport._write_ready)
        transport._write_ready()
        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'ta']), transport._buffer)

    def test_write_ready_partial_none(self):
        data = b'data'
        self.sock.send.return_value = 0

        transport = self.socket_transport()
        transport._buffer.extend(data)
        self.loop.add_writer(7, transport._write_ready)
        transport._write_ready()
        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_ready_tryagain(self):
        self.sock.send.side_effect = BlockingIOError

        transport = self.socket_transport()
        transport._buffer = list_to_buffer([b'data1', b'data2'])
        self.loop.add_writer(7, transport._write_ready)
        transport._write_ready()

        self.loop.assert_writer(7, transport._write_ready)
        self.assertEqual(list_to_buffer([b'data1data2']), transport._buffer)

    def test_write_ready_exception(self):
        err = self.sock.send.side_effect = OSError()

        transport = self.socket_transport()
        transport._fatal_error = mock.Mock()
        transport._buffer.extend(b'data')
        transport._write_ready()
        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal write error on socket transport')

    @mock.patch('asyncio.base_events.logger')
    def test_write_ready_exception_and_close(self, m_log):
        self.sock.send.side_effect = OSError()
        remove_writer = self.loop.remove_writer = mock.Mock()

        transport = self.socket_transport()
        transport.close()
        transport._buffer.extend(b'data')
        transport._write_ready()
        remove_writer.assert_called_with(self.sock_fd)

    def test_write_eof(self):
        tr = self.socket_transport()
        self.assertTrue(tr.can_write_eof())
        tr.write_eof()
        self.sock.shutdown.assert_called_with(socket.SHUT_WR)
        tr.write_eof()
        self.assertEqual(self.sock.shutdown.call_count, 1)
        tr.close()

    def test_write_eof_buffer(self):
        tr = self.socket_transport()
        self.sock.send.side_effect = BlockingIOError
        tr.write(b'data')
        tr.write_eof()
        self.assertEqual(tr._buffer, list_to_buffer([b'data']))
        self.assertTrue(tr._eof)
        self.assertFalse(self.sock.shutdown.called)
        self.sock.send.side_effect = lambda _: 4
        tr._write_ready()
        self.assertTrue(self.sock.send.called)
        self.sock.shutdown.assert_called_with(socket.SHUT_WR)
        tr.close()


@unittest.skipIf(ssl is None, 'No ssl module')
class SelectorSslTransportTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()
        self.protocol = test_utils.make_test_protocol(asyncio.Protocol)
        self.sock = mock.Mock(socket.socket)
        self.sock.fileno.return_value = 7
        self.sslsock = mock.Mock()
        self.sslsock.fileno.return_value = 1
        self.sslcontext = mock.Mock()
        self.sslcontext.wrap_socket.return_value = self.sslsock

    def ssl_transport(self, waiter=None, server_hostname=None):
        transport = _SelectorSslTransport(self.loop, self.sock, self.protocol,
                                          self.sslcontext, waiter=waiter,
                                          server_hostname=server_hostname)
        self.addCleanup(close_transport, transport)
        return transport

    def _make_one(self, create_waiter=None):
        transport = self.ssl_transport()
        self.sock.reset_mock()
        self.sslsock.reset_mock()
        self.sslcontext.reset_mock()
        self.loop.reset_counters()
        return transport

    def test_on_handshake(self):
        waiter = asyncio.Future(loop=self.loop)
        tr = self.ssl_transport(waiter=waiter)
        self.assertTrue(self.sslsock.do_handshake.called)
        self.loop.assert_reader(1, tr._read_ready)
        test_utils.run_briefly(self.loop)
        self.assertIsNone(waiter.result())

    def test_on_handshake_reader_retry(self):
        self.loop.set_debug(False)
        self.sslsock.do_handshake.side_effect = ssl.SSLWantReadError
        transport = self.ssl_transport()
        self.loop.assert_reader(1, transport._on_handshake, None)

    def test_on_handshake_writer_retry(self):
        self.loop.set_debug(False)
        self.sslsock.do_handshake.side_effect = ssl.SSLWantWriteError
        transport = self.ssl_transport()
        self.loop.assert_writer(1, transport._on_handshake, None)

    def test_on_handshake_exc(self):
        exc = ValueError()
        self.sslsock.do_handshake.side_effect = exc
        with test_utils.disable_logger():
            waiter = asyncio.Future(loop=self.loop)
            transport = self.ssl_transport(waiter=waiter)
        self.assertTrue(waiter.done())
        self.assertIs(exc, waiter.exception())
        self.assertTrue(self.sslsock.close.called)

    def test_on_handshake_base_exc(self):
        waiter = asyncio.Future(loop=self.loop)
        transport = self.ssl_transport(waiter=waiter)
        exc = BaseException()
        self.sslsock.do_handshake.side_effect = exc
        with test_utils.disable_logger():
            self.assertRaises(BaseException, transport._on_handshake, 0)
        self.assertTrue(self.sslsock.close.called)
        self.assertTrue(waiter.done())
        self.assertIs(exc, waiter.exception())

    def test_cancel_handshake(self):
        # Python issue #23197: cancelling an handshake must not raise an
        # exception or log an error, even if the handshake failed
        waiter = asyncio.Future(loop=self.loop)
        transport = self.ssl_transport(waiter=waiter)
        waiter.cancel()
        exc = ValueError()
        self.sslsock.do_handshake.side_effect = exc
        with test_utils.disable_logger():
            transport._on_handshake(0)
        transport.close()
        test_utils.run_briefly(self.loop)

    def test_pause_resume_reading(self):
        tr = self._make_one()
        self.assertFalse(tr._paused)
        self.loop.assert_reader(1, tr._read_ready)
        tr.pause_reading()
        self.assertTrue(tr._paused)
        self.assertFalse(1 in self.loop.readers)
        tr.resume_reading()
        self.assertFalse(tr._paused)
        self.loop.assert_reader(1, tr._read_ready)
        with self.assertRaises(RuntimeError):
            tr.resume_reading()

    def test_write(self):
        transport = self._make_one()
        transport.write(b'data')
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_bytearray(self):
        transport = self._make_one()
        data = bytearray(b'data')
        transport.write(data)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)
        self.assertEqual(data, bytearray(b'data'))  # Hasn't been mutated.
        self.assertIsNot(data, transport._buffer)  # Hasn't been incorporated.

    def test_write_memoryview(self):
        transport = self._make_one()
        data = memoryview(b'data')
        transport.write(data)
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_no_data(self):
        transport = self._make_one()
        transport._buffer.extend(b'data')
        transport.write(b'')
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_str(self):
        transport = self._make_one()
        self.assertRaises(TypeError, transport.write, 'str')

    def test_write_closing(self):
        transport = self._make_one()
        transport.close()
        self.assertEqual(transport._conn_lost, 1)
        transport.write(b'data')
        self.assertEqual(transport._conn_lost, 2)

    @mock.patch('asyncio.selector_events.logger')
    def test_write_exception(self, m_log):
        transport = self._make_one()
        transport._conn_lost = 1
        transport.write(b'data')
        self.assertEqual(transport._buffer, list_to_buffer())
        transport.write(b'data')
        transport.write(b'data')
        transport.write(b'data')
        transport.write(b'data')
        m_log.warning.assert_called_with('socket.send() raised exception.')

    def test_read_ready_recv(self):
        self.sslsock.recv.return_value = b'data'
        transport = self._make_one()
        transport._read_ready()
        self.assertTrue(self.sslsock.recv.called)
        self.assertEqual((b'data',), self.protocol.data_received.call_args[0])

    def test_read_ready_write_wants_read(self):
        self.loop.add_writer = mock.Mock()
        self.sslsock.recv.side_effect = BlockingIOError
        transport = self._make_one()
        transport._write_wants_read = True
        transport._write_ready = mock.Mock()
        transport._buffer.extend(b'data')
        transport._read_ready()

        self.assertFalse(transport._write_wants_read)
        transport._write_ready.assert_called_with()
        self.loop.add_writer.assert_called_with(
            transport._sock_fd, transport._write_ready)

    def test_read_ready_recv_eof(self):
        self.sslsock.recv.return_value = b''
        transport = self._make_one()
        transport.close = mock.Mock()
        transport._read_ready()
        transport.close.assert_called_with()
        self.protocol.eof_received.assert_called_with()

    def test_read_ready_recv_conn_reset(self):
        err = self.sslsock.recv.side_effect = ConnectionResetError()
        transport = self._make_one()
        transport._force_close = mock.Mock()
        with test_utils.disable_logger():
            transport._read_ready()
        transport._force_close.assert_called_with(err)

    def test_read_ready_recv_retry(self):
        self.sslsock.recv.side_effect = ssl.SSLWantReadError
        transport = self._make_one()
        transport._read_ready()
        self.assertTrue(self.sslsock.recv.called)
        self.assertFalse(self.protocol.data_received.called)

        self.sslsock.recv.side_effect = BlockingIOError
        transport._read_ready()
        self.assertFalse(self.protocol.data_received.called)

        self.sslsock.recv.side_effect = InterruptedError
        transport._read_ready()
        self.assertFalse(self.protocol.data_received.called)

    def test_read_ready_recv_write(self):
        self.loop.remove_reader = mock.Mock()
        self.loop.add_writer = mock.Mock()
        self.sslsock.recv.side_effect = ssl.SSLWantWriteError
        transport = self._make_one()
        transport._read_ready()
        self.assertFalse(self.protocol.data_received.called)
        self.assertTrue(transport._read_wants_write)

        self.loop.remove_reader.assert_called_with(transport._sock_fd)
        self.loop.add_writer.assert_called_with(
            transport._sock_fd, transport._write_ready)

    def test_read_ready_recv_exc(self):
        err = self.sslsock.recv.side_effect = OSError()
        transport = self._make_one()
        transport._fatal_error = mock.Mock()
        transport._read_ready()
        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal read error on SSL transport')

    def test_write_ready_send(self):
        self.sslsock.send.return_value = 4
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data'])
        transport._write_ready()
        self.assertEqual(list_to_buffer(), transport._buffer)
        self.assertTrue(self.sslsock.send.called)

    def test_write_ready_send_none(self):
        self.sslsock.send.return_value = 0
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data1', b'data2'])
        transport._write_ready()
        self.assertTrue(self.sslsock.send.called)
        self.assertEqual(list_to_buffer([b'data1data2']), transport._buffer)

    def test_write_ready_send_partial(self):
        self.sslsock.send.return_value = 2
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data1', b'data2'])
        transport._write_ready()
        self.assertTrue(self.sslsock.send.called)
        self.assertEqual(list_to_buffer([b'ta1data2']), transport._buffer)

    def test_write_ready_send_closing_partial(self):
        self.sslsock.send.return_value = 2
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data1', b'data2'])
        transport._write_ready()
        self.assertTrue(self.sslsock.send.called)
        self.assertFalse(self.sslsock.close.called)

    def test_write_ready_send_closing(self):
        self.sslsock.send.return_value = 4
        transport = self._make_one()
        transport.close()
        transport._buffer = list_to_buffer([b'data'])
        transport._write_ready()
        self.assertFalse(self.loop.writers)
        self.protocol.connection_lost.assert_called_with(None)

    def test_write_ready_send_closing_empty_buffer(self):
        self.sslsock.send.return_value = 4
        transport = self._make_one()
        transport.close()
        transport._buffer = list_to_buffer()
        transport._write_ready()
        self.assertFalse(self.loop.writers)
        self.protocol.connection_lost.assert_called_with(None)

    def test_write_ready_send_retry(self):
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data'])

        self.sslsock.send.side_effect = ssl.SSLWantWriteError
        transport._write_ready()
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

        self.sslsock.send.side_effect = BlockingIOError()
        transport._write_ready()
        self.assertEqual(list_to_buffer([b'data']), transport._buffer)

    def test_write_ready_send_read(self):
        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data'])

        self.loop.remove_writer = mock.Mock()
        self.sslsock.send.side_effect = ssl.SSLWantReadError
        transport._write_ready()
        self.assertFalse(self.protocol.data_received.called)
        self.assertTrue(transport._write_wants_read)
        self.loop.remove_writer.assert_called_with(transport._sock_fd)

    def test_write_ready_send_exc(self):
        err = self.sslsock.send.side_effect = OSError()

        transport = self._make_one()
        transport._buffer = list_to_buffer([b'data'])
        transport._fatal_error = mock.Mock()
        transport._write_ready()
        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal write error on SSL transport')
        self.assertEqual(list_to_buffer(), transport._buffer)

    def test_write_ready_read_wants_write(self):
        self.loop.add_reader = mock.Mock()
        self.sslsock.send.side_effect = BlockingIOError
        transport = self._make_one()
        transport._read_wants_write = True
        transport._read_ready = mock.Mock()
        transport._write_ready()

        self.assertFalse(transport._read_wants_write)
        transport._read_ready.assert_called_with()
        self.loop.add_reader.assert_called_with(
            transport._sock_fd, transport._read_ready)

    def test_write_eof(self):
        tr = self._make_one()
        self.assertFalse(tr.can_write_eof())
        self.assertRaises(NotImplementedError, tr.write_eof)

    def check_close(self):
        tr = self._make_one()
        tr.close()

        self.assertTrue(tr.is_closing())
        self.assertEqual(1, self.loop.remove_reader_count[1])
        self.assertEqual(tr._conn_lost, 1)

        tr.close()
        self.assertEqual(tr._conn_lost, 1)
        self.assertEqual(1, self.loop.remove_reader_count[1])

        test_utils.run_briefly(self.loop)

    def test_close(self):
        self.check_close()
        self.assertTrue(self.protocol.connection_made.called)
        self.assertTrue(self.protocol.connection_lost.called)

    def test_close_not_connected(self):
        self.sslsock.do_handshake.side_effect = ssl.SSLWantReadError
        self.check_close()
        self.assertFalse(self.protocol.connection_made.called)
        self.assertFalse(self.protocol.connection_lost.called)

    @unittest.skipIf(ssl is None, 'No SSL support')
    def test_server_hostname(self):
        self.ssl_transport(server_hostname='localhost')
        self.sslcontext.wrap_socket.assert_called_with(
            self.sock, do_handshake_on_connect=False, server_side=False,
            server_hostname='localhost')


class SelectorSslWithoutSslTransportTests(unittest.TestCase):

    @mock.patch('asyncio.selector_events.ssl', None)
    def test_ssl_transport_requires_ssl_module(self):
        Mock = mock.Mock
        with self.assertRaises(RuntimeError):
            _SelectorSslTransport(Mock(), Mock(), Mock(), Mock())


class SelectorDatagramTransportTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()
        self.protocol = test_utils.make_test_protocol(asyncio.DatagramProtocol)
        self.sock = mock.Mock(spec_set=socket.socket)
        self.sock.fileno.return_value = 7

    def datagram_transport(self, address=None):
        transport = _SelectorDatagramTransport(self.loop, self.sock,
                                               self.protocol,
                                               address=address)
        self.addCleanup(close_transport, transport)
        return transport

    def test_read_ready(self):
        transport = self.datagram_transport()

        self.sock.recvfrom.return_value = (b'data', ('0.0.0.0', 1234))
        transport._read_ready()

        self.protocol.datagram_received.assert_called_with(
            b'data', ('0.0.0.0', 1234))

    def test_read_ready_tryagain(self):
        transport = self.datagram_transport()

        self.sock.recvfrom.side_effect = BlockingIOError
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        self.assertFalse(transport._fatal_error.called)

    def test_read_ready_err(self):
        transport = self.datagram_transport()

        err = self.sock.recvfrom.side_effect = RuntimeError()
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal read error on datagram transport')

    def test_read_ready_oserr(self):
        transport = self.datagram_transport()

        err = self.sock.recvfrom.side_effect = OSError()
        transport._fatal_error = mock.Mock()
        transport._read_ready()

        self.assertFalse(transport._fatal_error.called)
        self.protocol.error_received.assert_called_with(err)

    def test_sendto(self):
        data = b'data'
        transport = self.datagram_transport()
        transport.sendto(data, ('0.0.0.0', 1234))
        self.assertTrue(self.sock.sendto.called)
        self.assertEqual(
            self.sock.sendto.call_args[0], (data, ('0.0.0.0', 1234)))

    def test_sendto_bytearray(self):
        data = bytearray(b'data')
        transport = self.datagram_transport()
        transport.sendto(data, ('0.0.0.0', 1234))
        self.assertTrue(self.sock.sendto.called)
        self.assertEqual(
            self.sock.sendto.call_args[0], (data, ('0.0.0.0', 1234)))

    def test_sendto_memoryview(self):
        data = memoryview(b'data')
        transport = self.datagram_transport()
        transport.sendto(data, ('0.0.0.0', 1234))
        self.assertTrue(self.sock.sendto.called)
        self.assertEqual(
            self.sock.sendto.call_args[0], (data, ('0.0.0.0', 1234)))

    def test_sendto_no_data(self):
        transport = self.datagram_transport()
        transport._buffer.append((b'data', ('0.0.0.0', 12345)))
        transport.sendto(b'', ())
        self.assertFalse(self.sock.sendto.called)
        self.assertEqual(
            [(b'data', ('0.0.0.0', 12345))], list(transport._buffer))

    def test_sendto_buffer(self):
        transport = self.datagram_transport()
        transport._buffer.append((b'data1', ('0.0.0.0', 12345)))
        transport.sendto(b'data2', ('0.0.0.0', 12345))
        self.assertFalse(self.sock.sendto.called)
        self.assertEqual(
            [(b'data1', ('0.0.0.0', 12345)),
             (b'data2', ('0.0.0.0', 12345))],
            list(transport._buffer))

    def test_sendto_buffer_bytearray(self):
        data2 = bytearray(b'data2')
        transport = self.datagram_transport()
        transport._buffer.append((b'data1', ('0.0.0.0', 12345)))
        transport.sendto(data2, ('0.0.0.0', 12345))
        self.assertFalse(self.sock.sendto.called)
        self.assertEqual(
            [(b'data1', ('0.0.0.0', 12345)),
             (b'data2', ('0.0.0.0', 12345))],
            list(transport._buffer))
        self.assertIsInstance(transport._buffer[1][0], bytes)

    def test_sendto_buffer_memoryview(self):
        data2 = memoryview(b'data2')
        transport = self.datagram_transport()
        transport._buffer.append((b'data1', ('0.0.0.0', 12345)))
        transport.sendto(data2, ('0.0.0.0', 12345))
        self.assertFalse(self.sock.sendto.called)
        self.assertEqual(
            [(b'data1', ('0.0.0.0', 12345)),
             (b'data2', ('0.0.0.0', 12345))],
            list(transport._buffer))
        self.assertIsInstance(transport._buffer[1][0], bytes)

    def test_sendto_tryagain(self):
        data = b'data'

        self.sock.sendto.side_effect = BlockingIOError

        transport = self.datagram_transport()
        transport.sendto(data, ('0.0.0.0', 12345))

        self.loop.assert_writer(7, transport._sendto_ready)
        self.assertEqual(
            [(b'data', ('0.0.0.0', 12345))], list(transport._buffer))

    @mock.patch('asyncio.selector_events.logger')
    def test_sendto_exception(self, m_log):
        data = b'data'
        err = self.sock.sendto.side_effect = RuntimeError()

        transport = self.datagram_transport()
        transport._fatal_error = mock.Mock()
        transport.sendto(data, ())

        self.assertTrue(transport._fatal_error.called)
        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal write error on datagram transport')
        transport._conn_lost = 1

        transport._address = ('123',)
        transport.sendto(data)
        transport.sendto(data)
        transport.sendto(data)
        transport.sendto(data)
        transport.sendto(data)
        m_log.warning.assert_called_with('socket.send() raised exception.')

    def test_sendto_error_received(self):
        data = b'data'

        self.sock.sendto.side_effect = ConnectionRefusedError

        transport = self.datagram_transport()
        transport._fatal_error = mock.Mock()
        transport.sendto(data, ())

        self.assertEqual(transport._conn_lost, 0)
        self.assertFalse(transport._fatal_error.called)

    def test_sendto_error_received_connected(self):
        data = b'data'

        self.sock.send.side_effect = ConnectionRefusedError

        transport = self.datagram_transport(address=('0.0.0.0', 1))
        transport._fatal_error = mock.Mock()
        transport.sendto(data)

        self.assertFalse(transport._fatal_error.called)
        self.assertTrue(self.protocol.error_received.called)

    def test_sendto_str(self):
        transport = self.datagram_transport()
        self.assertRaises(TypeError, transport.sendto, 'str', ())

    def test_sendto_connected_addr(self):
        transport = self.datagram_transport(address=('0.0.0.0', 1))
        self.assertRaises(
            ValueError, transport.sendto, b'str', ('0.0.0.0', 2))

    def test_sendto_closing(self):
        transport = self.datagram_transport(address=(1,))
        transport.close()
        self.assertEqual(transport._conn_lost, 1)
        transport.sendto(b'data', (1,))
        self.assertEqual(transport._conn_lost, 2)

    def test_sendto_ready(self):
        data = b'data'
        self.sock.sendto.return_value = len(data)

        transport = self.datagram_transport()
        transport._buffer.append((data, ('0.0.0.0', 12345)))
        self.loop.add_writer(7, transport._sendto_ready)
        transport._sendto_ready()
        self.assertTrue(self.sock.sendto.called)
        self.assertEqual(
            self.sock.sendto.call_args[0], (data, ('0.0.0.0', 12345)))
        self.assertFalse(self.loop.writers)

    def test_sendto_ready_closing(self):
        data = b'data'
        self.sock.send.return_value = len(data)

        transport = self.datagram_transport()
        transport._closing = True
        transport._buffer.append((data, ()))
        self.loop.add_writer(7, transport._sendto_ready)
        transport._sendto_ready()
        self.sock.sendto.assert_called_with(data, ())
        self.assertFalse(self.loop.writers)
        self.sock.close.assert_called_with()
        self.protocol.connection_lost.assert_called_with(None)

    def test_sendto_ready_no_data(self):
        transport = self.datagram_transport()
        self.loop.add_writer(7, transport._sendto_ready)
        transport._sendto_ready()
        self.assertFalse(self.sock.sendto.called)
        self.assertFalse(self.loop.writers)

    def test_sendto_ready_tryagain(self):
        self.sock.sendto.side_effect = BlockingIOError

        transport = self.datagram_transport()
        transport._buffer.extend([(b'data1', ()), (b'data2', ())])
        self.loop.add_writer(7, transport._sendto_ready)
        transport._sendto_ready()

        self.loop.assert_writer(7, transport._sendto_ready)
        self.assertEqual(
            [(b'data1', ()), (b'data2', ())],
            list(transport._buffer))

    def test_sendto_ready_exception(self):
        err = self.sock.sendto.side_effect = RuntimeError()

        transport = self.datagram_transport()
        transport._fatal_error = mock.Mock()
        transport._buffer.append((b'data', ()))
        transport._sendto_ready()

        transport._fatal_error.assert_called_with(
                                   err,
                                   'Fatal write error on datagram transport')

    def test_sendto_ready_error_received(self):
        self.sock.sendto.side_effect = ConnectionRefusedError

        transport = self.datagram_transport()
        transport._fatal_error = mock.Mock()
        transport._buffer.append((b'data', ()))
        transport._sendto_ready()

        self.assertFalse(transport._fatal_error.called)

    def test_sendto_ready_error_received_connection(self):
        self.sock.send.side_effect = ConnectionRefusedError

        transport = self.datagram_transport(address=('0.0.0.0', 1))
        transport._fatal_error = mock.Mock()
        transport._buffer.append((b'data', ()))
        transport._sendto_ready()

        self.assertFalse(transport._fatal_error.called)
        self.assertTrue(self.protocol.error_received.called)

    @mock.patch('asyncio.base_events.logger.error')
    def test_fatal_error_connected(self, m_exc):
        transport = self.datagram_transport(address=('0.0.0.0', 1))
        err = ConnectionRefusedError()
        transport._fatal_error(err)
        self.assertFalse(self.protocol.error_received.called)
        m_exc.assert_called_with(
            test_utils.MockPattern(
                'Fatal error on transport\nprotocol:.*\ntransport:.*'),
            exc_info=(ConnectionRefusedError, MOCK_ANY, MOCK_ANY))


if __name__ == '__main__':
    unittest.main()
