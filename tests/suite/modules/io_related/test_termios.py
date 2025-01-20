# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# tests for module 'termios' (Posix only)

import unittest

from iptest import is_posix, is_linux, is_osx

if is_posix:
    import termios
else:
    try:
        import termios
    except ImportError:
        pass
    else:
        raise AssertionError("There should be no module termios on Windows")


@unittest.skipUnless(is_posix, "Posix-specific test")
class TermiosTest(unittest.TestCase):

    def verify_flags(self, names, values, typ=int):
        for i, flag in enumerate(names):
            with self.subTest(flag=flag):
                self.assertTrue(hasattr(termios, flag))
                self.assertEqual(getattr(termios, flag), values[i])


    def test_iflags(self):
        iflags = ["IGNBRK", "BRKINT", "IGNPAR", "PARMRK", "INPCK", "ISTRIP", "INLCR", "IGNCR", "ICRNL", "IXON", "IXOFF", "IXANY"]
        if is_osx:
            iflag_values = [0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x200, 0x400, 0x800]
        elif is_linux:
            iflag_values = [0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x400, 0x1000, 0x800]
        self.verify_flags(iflags, iflag_values)


    def test_oflags(self):
        oflags = ["OPOST", "ONLCR"]
        if is_osx:
            oflag_values = [0x1, 0x2]
        elif is_linux:
            oflag_values = [0x1, 0x4]
        self.verify_flags(oflags, oflag_values)


    def test_cflags(self):
        cflags = ["CSIZE", "CS5", "CS6", "CS7", "CS8", "CREAD", "PARENB", "HUPCL"]
        if is_osx:
            cflag_values = [0x300, 0x0, 0x100, 0x200, 0x300, 0X800, 0x1000, 0x4000]
        elif is_linux:
            cflags += ["CBAUD", "CBAUDEX"]
            cflag_values = [0x30, 0x0, 0x10, 0x20, 0x30, 0x80, 0x0100, 0x400, 0x100f, 0x1000]
        self.verify_flags(cflags, cflag_values)


    def test_lflags(self):
        lflags = ["ECHOKE", "ECHOE", "ECHOK", "ECHO", "ECHONL", "ECHOPRT", "ECHOCTL", "ISIG", "ICANON", "IEXTEN", "TOSTOP", "FLUSHO", "PENDIN", "NOFLSH"]
        if is_osx:
            lflag_values = [0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x400, 0x400000, 0x800000, 0x20000000, 0x80000000]
        elif is_linux:
            lflag_values = [0x800, 0x10, 0x20, 0x8, 0x40, 0x400, 0x200, 0x1, 0x2, 0x8000, 0x100, 0x1000, 0x4000, 0x80]
        self.verify_flags(lflags, lflag_values)


    def test_when_enum(self):
        when = ["TCSANOW", "TCSADRAIN", "TCSAFLUSH"]
        when_values = [0, 1, 2]
        self.verify_flags(when, when_values)


    def test_tcflush_queue(self):
        queue = ["TCIFLUSH", "TCOFLUSH", "TCIOFLUSH"]
        if is_osx:
            queue_values = [1, 2, 3]
        elif is_linux:
            queue_values = [0, 1, 2]
        self.verify_flags(queue, queue_values)


    def test_tcflow_action(self):
        action = ["TCOOFF", "TCOON", "TCIOFF", "TCION"]
        if is_osx:
            action_values = [1, 2, 3, 4]
        elif is_linux:
            action_values = [0, 1, 2, 3]
        self.verify_flags(action, action_values)


    def test_cc(self):
        if is_osx:
            cc = ["VEOF", "VEOL", "VEOL2", "VERASE", "VWERASE", "VKILL", "VREPRINT", "VINTR", "VQUIT", "VSUSP", "VSTART", "VSTOP", "VLNEXT", "VDISCARD", "VMIN", "VTIME", "NCCS"]
            cc_values = [0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 12, 13, 14, 15, 16, 17, 20]
        elif is_linux:
            cc = ["VEOF", "VEOL", "VEOL2", "VERASE", "VWERASE", "VKILL", "VREPRINT", "VINTR", "VQUIT", "VSUSP", "VSTART", "VSTOP", "VLNEXT", "VDISCARD", "VMIN", "VTIME", "VSWTC", "NCCS"]
            cc_values = [4, 11, 16, 2, 14, 3, 12, 0, 1, 10, 8, 9, 15, 13, 6, 5, 7, 32]
        self.verify_flags(cc, cc_values)


if __name__ == "__main__":
    unittest.main(verbosity=2)
