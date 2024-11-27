# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# tests for module 'resource' (Posix only)

import unittest

from iptest import IronPythonTestCase, is_posix, is_linux, is_osx, run_test

if is_posix:
    import resource
else:
    try:
        import resource
    except ImportError:
        pass
    else:
        raise AssertionError("There should be no module resource on Windows")

@unittest.skipUnless(is_posix, "Posix-specific test")
class ResourceTest(IronPythonTestCase):
    def setUp(self):
        self.RLIM_NLIMITS = 16 if is_linux else 9

    def test_infinity(self):
        if is_osx:
            self.assertEqual(resource.RLIM_INFINITY, (1<<63)-1)
        else:
            self.assertEqual(resource.RLIM_INFINITY, -1)

    def test_getrlimit(self):
        for r in range(self.RLIM_NLIMITS):
            lims = resource.getrlimit(r)
            self.assertIsInstance(lims, tuple)
            self.assertEqual(len(lims), 2)
            self.assertIsInstance(lims[0], int)
            self.assertIsInstance(lims[1], int)

        self.assertRaises(TypeError, resource.getrlimit, None)
        self.assertRaises(TypeError, resource.getrlimit, "abc")
        self.assertRaises(TypeError, resource.getrlimit, 4.0)
        self.assertRaises(ValueError, resource.getrlimit, -1)
        self.assertRaises(ValueError, resource.getrlimit, self.RLIM_NLIMITS)

    def test_setrlimit(self):
        r = resource.RLIMIT_CORE
        lims = resource.getrlimit(r)
        rlim_cur = lims[0]
        rlim_max = lims[1]
        # usually max core size is unlimited so a good resource limit to test setting
        if (rlim_max == resource.RLIM_INFINITY):
            try:
                resource.setrlimit(r, (0, rlim_max))
                self.assertEqual(resource.getrlimit(r), (0, rlim_max) )

                resource.setrlimit(r, (10, rlim_max))
                self.assertEqual(resource.getrlimit(r), (10, rlim_max) )

                resource.setrlimit(r, [0, rlim_max]) # using a list
                self.assertEqual(resource.getrlimit(r), (0, rlim_max) )

                resource.setrlimit(r, (resource.RLIM_INFINITY, rlim_max))
                self.assertEqual(resource.getrlimit(r), (resource.RLIM_INFINITY, rlim_max) )

                resource.setrlimit(r, (-1, rlim_max))
                self.assertEqual(resource.getrlimit(r), (resource.RLIM_INFINITY, rlim_max) )

                resource.setrlimit(r, (-2, rlim_max))
                self.assertEqual(resource.getrlimit(r), (resource.RLIM_INFINITY-1, rlim_max) )

                resource.setrlimit(r, ((1<<63)-1, rlim_max))
                self.assertEqual(resource.getrlimit(r), ((1<<63)-1, rlim_max) )

                resource.setrlimit(r, (-(1<<63), rlim_max))
                if is_osx:
                    self.assertEqual(resource.getrlimit(r), (0, rlim_max) )
                else:
                    self.assertEqual(resource.getrlimit(r), (-(1<<63), rlim_max) )

                self.assertRaises(ValueError, resource.setrlimit, r, (rlim_max, rlim_max-1))
            finally:
                resource.setrlimit(r, lims)

    def test_setrlimit_error(self):
        self.assertRaises(TypeError, resource.setrlimit, None, (0, 0))
        self.assertRaises(TypeError, resource.setrlimit, "abc", (0, 0))
        self.assertRaises(TypeError, resource.setrlimit, 4.0, (0, 0))
        self.assertRaises(ValueError, resource.setrlimit, -1, (0, 0))
        self.assertRaises(ValueError, resource.setrlimit, self.RLIM_NLIMITS, (0, 0))
        self.assertRaises(ValueError, resource.setrlimit, 0, (0,))
        self.assertRaises(ValueError, resource.setrlimit, 0, (0, 0, 0))
        self.assertRaises(ValueError, resource.setrlimit, 0, (2.3, 0, 0))
        self.assertRaises(TypeError, resource.setrlimit, 0, None)

    @unittest.skipUnless(not is_osx, "prlimit not available on macOS")
    def test_prlimit(self):
        r = resource.RLIMIT_CORE
        lims = resource.getrlimit(r)
        self.assertEqual(resource.prlimit(0, r), lims)
        rlim_cur = lims[0]
        rlim_max = lims[1]
        # usually max core size is unlimited so a good resource limit to test setting
        if (rlim_max == resource.RLIM_INFINITY):
            try:
                resource.prlimit(0, r, (1024, rlim_max))
                self.assertEqual(resource.getrlimit(r), (1024, rlim_max) )
            finally:
                resource.prlimit(0, r, lims)

    def test_pagesize(self):
        ps = resource.getpagesize()
        self.assertIsInstance(ps, int)
        self.assertTrue(ps > 0)
        self.assertTrue((ps & (ps-1) == 0)) # ps is power of 2

    def test_getrusage(self):
        self.assertEqual(resource.struct_rusage.n_fields, 16)
        self.assertEqual(resource.struct_rusage.n_sequence_fields, 16)
        self.assertEqual(resource.struct_rusage.n_unnamed_fields, 0)

        ru = resource.getrusage(resource.RUSAGE_SELF)
        self.assertIsInstance(ru, resource.struct_rusage)
        self.assertEqual(len(ru), resource.struct_rusage.n_fields)
        self.assertIsInstance(ru[0], float)
        self.assertIsInstance(ru[1], float)
        for i in range(2, resource.struct_rusage.n_fields):
            self.assertIsInstance(ru[i], int)

        ru2 = resource.struct_rusage(ru)
        self.assertEqual(ru, ru2)

        ru2 = resource.struct_rusage(ru, {})
        self.assertEqual(ru, ru2)

        ru2 = resource.struct_rusage(ru, {'ru_utime': 0.0, 'foo': 'bar'}) # dict is ignored
        self.assertEqual(ru, ru2)

        self.assertRaises(TypeError, resource.struct_rusage)
        self.assertRaises(TypeError, resource.struct_rusage, 0)
        self.assertRaises(TypeError, resource.struct_rusage, range(15))
        self.assertRaises(TypeError, resource.struct_rusage, range(17))
        self.assertRaises(TypeError, resource.struct_rusage, range(16), 0)

        ru2 = resource.struct_rusage(range(resource.struct_rusage.n_sequence_fields))
        self.assertEqual(ru2[15], 15)
        self.assertEqual(ru2.ru_nivcsw, 15)

run_test(__name__)
