# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import datetime
import os
import time

from iptest import IronPythonTestCase, is_cli, is_windows, run_test

class TimeTest(IronPythonTestCase):
    def test_strftime(self):
        t = time.localtime()
        x = time.strftime('%x %X', t)

        self.assertTrue(len(x) > 3)

        x1 = time.strftime('%x', t)
        x2 = time.strftime('%X', t)

        self.assertTrue(len(x1) > 1)
        self.assertTrue(len(x2) > 1)

        self.assertEqual(x, x1 + ' ' + x2)

        t = time.gmtime()
        self.assertEqual(time.strftime('%c', t), time.strftime('%x %X', t))

        self.assertEqual(time.strftime('%Y-%m-%d %H:%M:%S', time.localtime(time.mktime((1994,11,15,12,3,10,0,0,-1)))), "1994-11-15 12:03:10")

        tests = [  # user reported scenario modified to use all DST values
                ((2003, 5, 2, 0, 0, 0, 4, 122, 0), '2003-05-02 00:00:00'),
                ((2003, 5, 2, 0, 0, 0, 4, 122, 1), '2003-05-02 00:00:00'),
                ((2003, 5, 2, 0, 0, 0, 4, 122, -1), '2003-05-02 00:00:00'),
                # DST in 2003 starts 3/9/2003 and ends 11/2/2003, test interesting dates
                ((2003, 3, 8, 0, 0, 0, 4, 122, 0), '2003-03-08 00:00:00'),
                ((2003, 3, 8, 0, 0, 0, 4, 122, 1), '2003-03-08 00:00:00'),
                ((2003, 3, 8, 0, 0, 0, 4, 122, -1), '2003-03-08 00:00:00'),

                ((2003, 3, 9, 0, 0, 0, 4, 122, 0),  '2003-03-09 00:00:00'),
                ((2003, 3, 9, 0, 0, 0, 4, 122, 1),  '2003-03-09 00:00:00'),
                ((2003, 3, 9, 0, 0, 0, 4, 122, -1),  '2003-03-09 00:00:00'),

                ((2003, 3, 10, 0, 0, 0, 4, 122, 0),   '2003-03-10 00:00:00'),
                ((2003, 3, 10, 0, 0, 0, 4, 122, 1),   '2003-03-10 00:00:00'),
                ((2003, 3, 10, 0, 0, 0, 4, 122, -1),   '2003-03-10 00:00:00'),

                ((2003, 11, 1, 0, 0, 0, 4, 122, 0), '2003-11-01 00:00:00'),
                ((2003, 11, 1, 0, 0, 0, 4, 122, 1), '2003-11-01 00:00:00'),
                ((2003, 11, 1, 0, 0, 0, 4, 122, -1), '2003-11-01 00:00:00'),

                ((2003, 11, 2, 0, 0, 0, 4, 122, 0),  '2003-11-02 00:00:00'),
                ((2003, 11, 2, 0, 0, 0, 4, 122, 1),  '2003-11-02 00:00:00'),
                ((2003, 11, 2, 0, 0, 0, 4, 122, -1),  '2003-11-02 00:00:00'),

                ((2003, 11, 3, 0, 0, 0, 4, 122, 0),   '2003-11-03 00:00:00'),
                ((2003, 11, 3, 0, 0, 0, 4, 122, 1),   '2003-11-03 00:00:00'),
                ((2003, 11, 3, 0, 0, 0, 4, 122, -1),   '2003-11-03 00:00:00'),
                ]

        for dateTime, result in tests:
            self.assertEqual(time.strftime('%Y-%m-%d %H:%M:%S', time.struct_time(dateTime)), result)

        # https://github.com/IronLanguages/ironpython3/issues/1121
        self.assertTrue(time.strftime("%Z%z")) # make sure it doesn't produce an empty string

    def test_strptime(self):
        import time
        d = time.strptime("July 3, 2006 At 0724 GMT", "%B %d, %Y At %H%M GMT")
        self.assertEqual(d[0], 2006)
        self.assertEqual(d[1], 7)
        self.assertEqual(d[2], 3)
        self.assertEqual(d[3], 7)
        self.assertEqual(d[4], 24)
        self.assertEqual(d[5], 0)
        self.assertEqual(d[6], 0)
        self.assertEqual(d[7], 184)

        #CodePlex Work Item 2557
        self.assertEqual((2006, 7, 3, 7, 24, 0, 0, 184, -1), time.strptime("%07/03/06 07:24:00", "%%%c"))
        self.assertEqual((1900, 6, 1, 0, 0, 0, 4, 152, -1), time.strptime("%6", "%%%m"))
        self.assertEqual((1942, 1, 1, 0, 0, 0, 3, 1, -1), time.strptime("%1942", "%%%Y"))
        self.assertEqual((1900, 1, 6, 0, 0, 0, 5, 6, -1), time.strptime("%6", "%%%d"))

        # https://github.com/IronLanguages/main/issues/239
        self.assertEqual(time.strptime('Fri', '%a'), (1900, 1, 1, 0, 0, 0, 4, 1, -1))
        for i, d in enumerate(('Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun')):
            self.assertEqual((1900, 7, 9, 19, 30, 0, i, 190, -1), time.strptime(d + ', July 9 7:30 PM', '%a, %B %d %I:%M %p'))

        self.assertEqual((1900, 7, 9, 19, 30, 0, 0, 190, -1), time.strptime('July 9 7:30 PM', '%B %d %I:%M %p'))
        # CPY & IPY differ on daylight savings time for this parse

        self.assertRaises(ValueError, time.strptime, "July 3, 2006 At 0724 GMT", "%B %x, %Y At %H%M GMT")

    def test_sleep(self):
        sleep_time = 3
        safe_deviation = 0.20

        x = time.clock()
        time.sleep(sleep_time)
        y = time.clock()

        print()
        print("x is", x)
        print("y is", y)

        if y>x:
            self.assertTrue(y-x > sleep_time*(1-(safe_deviation/2)))
            self.assertTrue(y-x < sleep_time*(1+safe_deviation))  # make sure we're close...

    def test_dst(self):
        if time.daylight and time.altzone != time.timezone: self.assertEqual(time.altzone, time.timezone-3600)
        t = time.time()
        self.assertEqual(time.mktime(time.gmtime(t))-time.mktime(time.localtime(t)), time.timezone)

    def test_tzname(self):
        self.assertEqual(type(time.tzname), tuple)
        self.assertEqual(len(time.tzname), 2)

    def test_struct_time(self):
        self.assertEqual(time.struct_time("123456789"), ('1', '2', '3', '4', '5', '6', '7', '8', '9'))

        class Exc(Exception): pass

        class C:
            def __getitem__(self, i): raise Exc
            def __len__(self): return 9

        self.assertRaises(Exc, time.struct_time, C())

    def test_gmtime(self):
        self.assertEqual(time.gmtime(1015758000.0), (2002, 3, 10, 11, 0, 0, 6, 69, 0))
        self.assertEqual(time.gmtime(0), (1970, 1, 1, 0, 0, 0, 3, 1, 0))

    def test_localtime(self):
        self.assertEqual(time.mktime(time.localtime(0)), 0)

    def test_localtime_dst(self):
        # https://github.com/IronLanguages/ironpython3/issues/570
        # check every hour of a year so we go through the DST transitions (if the local time zone has any)
        #
        # Note: time.timezone/time.altzone are computed once at process start and only hold a single
        # standard/daylight offset pair, so they can't describe a zone whose standard offset itself
        # changes mid-year (e.g. a jurisdiction abolishing DST); don't use them as an oracle here.
        t0 = 1767225600 # 2026-01-01 00:00 UTC
        for h in range(365 * 24):
            t = t0 + h * 3600
            lt = time.localtime(t)
            self.assertEqual(time.mktime(lt), t, lt)
            self.assertEqual(datetime.datetime.fromtimestamp(t).timetuple()[:6], lt[:6])
            self.assertEqual(datetime.date.fromtimestamp(t).timetuple()[:3], lt[:3])

    def test_mktime_forced_dst_no_daylight(self):
        # https://github.com/IronLanguages/ironpython3/issues/570
        # Forcing isdst=1 on a date that has no actual daylight saving (e.g. after a
        # jurisdiction permanently abolishes DST, as America/Vancouver does on
        # 2026-11-01) must give the same result as isdst=0/-1
        #
        # Scope the local time zone to America/Vancouver for this test alone, so it
        # reproduces regardless of the machine's configured zone
        old_tz = os.environ.get('TZ')
        os.environ['TZ'] = 'America/Vancouver'
        try:
            if is_cli:
                import System
                System.TimeZoneInfo.ClearCachedData()
                active = System.TimeZoneInfo.Local.Id == 'America/Vancouver'
            else:
                active = hasattr(time, 'tzset')
                if active:
                    time.tzset()

            if not active:
                # TZ isn't honored at runtime on this platform (e.g. native Windows,
                # where both .NET's TimeZoneInfo.Local and the CRT read the OS/registry
                # setting instead): nothing to scope, so there's nothing to test here.
                self.skipTest('cannot override the local time zone on this platform')

            t = (2026, 12, 15, 12, 0, 0, 0, 0, 0)
            self.assertEqual(time.mktime(t), time.mktime(t[:8] + (1,)))
            self.assertEqual(time.mktime(t), time.mktime(t[:8] + (-1,)))
        finally:
            if old_tz is None:
                os.environ.pop('TZ', None)
            else:
                os.environ['TZ'] = old_tz
            if is_cli:
                System.TimeZoneInfo.ClearCachedData()
            elif hasattr(time, 'tzset'):
                time.tzset()

    def test_asctime(self):
        self.assertEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, 0)), 'Fri Sep  4 14:57:11 2009')
        self.assertEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, 1)), 'Fri Sep  4 14:57:11 2009')
        self.assertEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, -1)), 'Fri Sep  4 14:57:11 2009')

    def test_year_lt_100(self):
        t = (50, 7, 1, 12, 0, 0, 2, 182, -1)
        if is_cli or not is_windows:
            # years below 100 are taken literally and not mapped to 19xx/20xx
            self.assertEqual(time.localtime(time.mktime(t))[:6], t[:6])
        else:
            self.assertRaises(OverflowError, time.mktime, t)


run_test(__name__)
