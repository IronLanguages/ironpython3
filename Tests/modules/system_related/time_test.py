#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
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

from iptest.assert_util import *

import time

def test_strftime():
    t = time.localtime()
    x = time.strftime('%x %X', t)
    
    Assert(len(x) > 3)
    
    x1 = time.strftime('%x', t)
    x2 = time.strftime('%X', t)
    
    Assert(len(x1) > 1)
    Assert(len(x2) > 1)
    
    AreEqual(x, x1 + ' ' + x2)
    
    t = time.gmtime()
    AreEqual(time.strftime('%c', t), time.strftime('%x %X', t))

    AreEqual(time.strftime('%Y-%m-%d %H:%M:%S', time.localtime(time.mktime((1994,11,15,12,3,10,0,0,-1)))), "1994-11-15 12:03:10")

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
        AreEqual(time.strftime('%Y-%m-%d %H:%M:%S',time.struct_time(dateTime)), result)

def test_strptime():
    import time
    d = time.strptime("July 3, 2006 At 0724 GMT", "%B %d, %Y At %H%M GMT")
    AreEqual(d[0], 2006)
    AreEqual(d[1], 7)
    AreEqual(d[2], 3)
    AreEqual(d[3], 7)
    AreEqual(d[4], 24)
    AreEqual(d[5], 0)
    AreEqual(d[6], 0)
    AreEqual(d[7], 184)
    
    #CodePlex Work Item 2557
    AreEqual((2006, 7, 3, 7, 24, 0, 0, 184, -1), time.strptime("%07/03/06 07:24:00", "%%%c"))
    AreEqual((1900, 6, 1, 0, 0, 0, 4, 152, -1), time.strptime("%6", "%%%m"))
    AreEqual((1942, 1, 1, 0, 0, 0, 3, 1, -1), time.strptime("%1942", "%%%Y"))
    AreEqual((1900, 1, 6, 0, 0, 0, 5, 6, -1), time.strptime("%6", "%%%d"))
        
    AreEqual((1900, 7, 9, 19, 30, 0, 4, 190, -1), time.strptime('Fri, July 9 7:30 PM', '%a, %B %d %I:%M %p'))
    # CPY & IPY differ on daylight savings time for this parse
        
    AssertError(ValueError, time.strptime, "July 3, 2006 At 0724 GMT", "%B %x, %Y At %H%M GMT")
    
#Skip under silverlight because we cannot determine whether the AMD processor bug applies
#here or not.
@skip("silverlight win32")
def test_sleep():
    #The QueryPerformanceCounter() system  call is broken in XP and 2K3 (see
    #http://channel9.msdn.com/ShowPost.aspx?PostID=156175) for
    #certain AMD64 multi-proc machines.
    #This means that randomly y can end up being less than x.
    if get_environ_variable("PROCESSOR_REVISION")=="0508" and get_environ_variable("PROCESSOR_LEVEL")=="15":
        print "Bailing test_sleep for certain AMD64 machines!"
        return
        
    sleep_time = 5
    safe_deviation = 0.20

    x = time.clock()
    time.sleep(sleep_time)
    y = time.clock()
    
    print
    print "x is", x
    print "y is", y
    
    if y>x:
        Assert(y-x > sleep_time*(1-(safe_deviation/2)))
        Assert(y-x < sleep_time*(1+safe_deviation))  # make sure we're close...

def test_dst():
    if is_silverlight:
        print "Dev10 524020"
        return
    AreEqual(time.altzone, time.timezone+[3600,-3600][time.daylight])
    t = time.time()
    AreEqual(time.mktime(time.gmtime(t))-time.mktime(time.localtime(t)), time.timezone)

def test_tzname():
    AreEqual(type(time.tzname), tuple)
    AreEqual(len(time.tzname), 2)

def test_struct_time():
    AreEqual(time.struct_time("123456789"), ('1', '2', '3', '4', '5', '6', '7', '8', '9'))
    
    class Exc(Exception): pass

    class C:
        def __getitem__(self, i): raise Exc
        def __len__(self): return 9
    
    AssertError(Exc, time.struct_time, C())

def test_gmtime():
    AreEqual(time.gmtime(1015758000.0), (2002, 3, 10, 11, 0, 0, 6, 69, 0))
    AreEqual(time.gmtime(0), (1970, 1, 1, 0, 0, 0, 3, 1, 0))

def test_localtime():
    AreEqual(time.mktime(time.localtime(0)), 0)

def test_asctime():
    AreEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, 0)), 'Fri Sep 04 14:57:11 2009')
    AreEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, 1)), 'Fri Sep 04 14:57:11 2009')
    AreEqual(time.asctime((2009, 9, 4, 14, 57, 11, 4, 247, -1)), 'Fri Sep 04 14:57:11 2009')


run_test(__name__)
