# -*- coding: utf-8 -*-
#####################################################################################
#
#  Copyright (c) IronPython Contributors
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

import unittest
from datetime import datetime, date
import time
from test import test_support

class TestDatetime(unittest.TestCase):

    def test_strptime_1(self):
        # cp34706
        d = datetime.strptime("2013-11-29T16:38:12.507000", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 507000))

    @unittest.skip("%f parsing uses .net DateType with miliseconds accuracy")
    def test_strptime_2(self):
        d = datetime.strptime("2013-11-29T16:38:12.507042", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 507042))

    def test_strptime_3(self):
        d = datetime.strptime("2013-11-29T16:38:12.5070", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 507000))

    def test_strptime_4(self):
        d = datetime.strptime("2013-11-29T16:38:12.507", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 507000))

    def test_strptime_5(self):
        d = datetime.strptime("2013-11-29T16:38:12.50", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 500000))

    def test_strptime_6(self):
        d = datetime.strptime("2013-11-29T16:38:12.5", "%Y-%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(2013, 11, 29, 16, 38, 12, 500000))

    def test_strptime_7(self):
        d = datetime.strptime("11-29T16:38:12.123", "%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(1900, 11, 29, 16, 38, 12, 123000))

    def test_strptime_8(self):
        d = datetime.strptime("11-29T16:38:12.123", "%m-%dT%H:%M:%S.%f")
        self.assertEqual(d, datetime(1900, 11, 29, 16, 38, 12, 123000))

    def test_strptime_9(self):
        d = datetime.strptime('Monday 11. March 2002', "%A %d. %B %Y")
        self.assertEqual(d, datetime(2002, 3, 11, 0, 0))
    
    def test_strftime_1(self):
        d = datetime(2013, 11, 29, 16, 38, 12, 507000)
        self.assertEqual(d.strftime("%Y-%m-%dT%H:%M:%S.%f"), "2013-11-29T16:38:12.507000")

    def test_strftime_2(self):
        d = datetime(2013, 11, 29, 16, 38, 12, 507042)
        self.assertEqual(d.strftime("%Y-%m-%dT%H:%M:%S.%f"), "2013-11-29T16:38:12.507042")

    def test_strftime_3(self):
        # cp32215
        d = datetime(2012,2,8,4,5,6,12314)
        self.assertEqual(d.strftime('%f'), "012314")

    def test_cp23965(self):
        # this is locale dependant and assumes test will be run under en_US
        self.assertEqual(date(2013, 11, 30).strftime("%Y %A %B"), "2013 Saturday November")
        self.assertEqual(date(2013, 11, 30).strftime("%y %a %b"), "13 Sat Nov")

    def test_invalid_strptime(self):
        # cp30047
        self.assertRaises(ValueError, datetime.strptime, "9 August", "%B %d")
        d = datetime.strptime("9 August", "%d %B")
        self.assertEqual(d, datetime(1900, 8, 9, 0, 0))

    def test_strptime_day_of_week(self):
        t = time.strptime("2013", "%Y")
        self.assertEqual(t, (2013, 1, 1, 0, 0, 0, 1, 1, -1)) 
        t = time.strptime("2013-11", "%Y-%m")
        self.assertEqual(t, (2013, 11, 1, 0, 0, 0, 4, 305, -1))
        t = time.strptime("2013-11-29", "%Y-%m-%d")
        self.assertEqual(t, (2013, 11, 29, 0, 0, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16", "%Y-%m-%dT%H")
        self.assertEqual(t, (2013, 11, 29, 16, 0, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16:38", "%Y-%m-%dT%H:%M")
        self.assertEqual(t, (2013, 11, 29, 16, 38, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16:38:12", "%Y-%m-%dT%H:%M:%S")
        self.assertEqual(t, (2013, 11, 29, 16, 38, 12, 4, 333, -1))

        t = time.strptime("2013-11-29 Friday", "%Y-%m-%d %A")
        self.assertEqual(t, (2013, 11, 29, 0, 0, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16 Friday", "%Y-%m-%dT%H %A")
        self.assertEqual(t, (2013, 11, 29, 16, 0, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16:38 Friday", "%Y-%m-%dT%H:%M %A")
        self.assertEqual(t, (2013, 11, 29, 16, 38, 0, 4, 333, -1))
        t = time.strptime("2013-11-29T16:38:12 Friday", "%Y-%m-%dT%H:%M:%S %A")
        self.assertEqual(t, (2013, 11, 29, 16, 38, 12, 4, 333, -1))


def test_main():
    from unittest import main
    main(module='test_datetime')

if __name__ == "__main__":
    test_main()


