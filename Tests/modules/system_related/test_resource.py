# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# tests fro module 'resource' (Posix only)

import os
import sys
import unittest
import resource

from iptest import IronPythonTestCase, is_cli, is_linux, is_osx, run_test, skipUnlessIronPython

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

run_test(__name__)
