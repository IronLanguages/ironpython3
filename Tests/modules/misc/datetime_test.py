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

##
## Test the datetime module
##

from iptest.assert_util import *
import datetime

def test_date():
    #--------------------------------------------------------------------------
    #basic sanity checks
    x = datetime.date(2005,3,22)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 22)

    datetime.date(1,1,1)
    datetime.date(9999, 12, 31)
    datetime.date(2004, 2, 29)
    
    AssertError(ValueError, datetime.date, 2005, 4,31)
    AssertError(ValueError, datetime.date, 2005, 3,32)
    AssertError(ValueError, datetime.datetime, 2006, 2, 29)
    AssertError(ValueError, datetime.datetime, 2006, 9, 31)
    
    AssertError(ValueError, datetime.date, 0, 1, 1)
    AssertError(ValueError, datetime.date, 1, 0, 1)
    AssertError(ValueError, datetime.date, 1, 1, 0)
    AssertError(ValueError, datetime.date, 0, 0, 0)
    AssertError(ValueError, datetime.date, -1, 1, 1)
    AssertError(ValueError, datetime.date, 1, -1, 1)
    AssertError(ValueError, datetime.date, 1, 1, -1)
    AssertError(ValueError, datetime.date, -1, -1, -1)
    AssertError(ValueError, datetime.date, -10, -10, -10)
    AssertError(ValueError, datetime.date, 10000, 12, 31)
    AssertError(ValueError, datetime.date, 9999, 13, 31)
    AssertError(ValueError, datetime.date, 9999, 12, 32)
    AssertError(ValueError, datetime.date, 10000, 13, 32)
    AssertError(ValueError, datetime.date, 100000, 130, 320)
    
    #add
    x = datetime.date(2005, 3, 22) + datetime.timedelta(1)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 23)
    x = datetime.date(2005, 3, 22) + datetime.timedelta(0)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 22)

    #right add
    x = datetime.timedelta(1) + datetime.date(2005, 3, 22)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 23)
    x = datetime.timedelta(0) + datetime.date(2005, 3, 22)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 22)
    
    #subtract
    x = datetime.date(2005, 3, 22) - datetime.timedelta(1)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 21)
    x = datetime.date(2005, 3, 22) - datetime.timedelta(0)
    AreEqual(x.year, 2005)
    AreEqual(x.month, 3)
    AreEqual(x.day, 22)
    
    #equality
    x = datetime.date(2005, 3, 22)
    y = x
    Assert(x==y)
    y = datetime.date(2005, 3, 23)
    Assert(not(x==y))
    Assert(x==datetime.date(2005, 3, 22))
    
    #inequality
    x = datetime.date(2005, 3, 22)
    y = None
    Assert(x!=y)
    y = datetime.date(2005, 3, 23)
    Assert(x!=y)
    
    #ge
    Assert(datetime.date(2005, 3, 22) >= datetime.date(2005, 3, 22))
    Assert(datetime.date(2005, 3, 23) >= datetime.date(2005, 3, 22))
    Assert(datetime.date(2005, 3, 24) >= datetime.date(2005, 3, 22))
    Assert(not (datetime.date(2005, 3, 21) >= datetime.date(2005, 3, 22)))
    Assert(not (datetime.date(2005, 3, 20) >= datetime.date(2005, 3, 22)))
    
    #le
    Assert(datetime.date(2005, 3, 22) <= datetime.date(2005, 3, 22))
    Assert(datetime.date(2005, 3, 22) <= datetime.date(2005, 3, 23))
    Assert(datetime.date(2005, 3, 22) <= datetime.date(2005, 3, 24))
    Assert(not (datetime.date(2005, 3, 22) <= datetime.date(2005, 3, 21)))
    Assert(not (datetime.date(2005, 3, 22) <= datetime.date(2005, 3, 20)))
    
    #gt
    Assert(not (datetime.date(2005, 3, 22) > datetime.date(2005, 3, 22)))
    Assert(datetime.date(2005, 3, 23) > datetime.date(2005, 3, 22))
    Assert(datetime.date(2005, 3, 24) > datetime.date(2005, 3, 22))
    Assert(not (datetime.date(2005, 3, 21) > datetime.date(2005, 3, 22)))
    Assert(not (datetime.date(2005, 3, 20) > datetime.date(2005, 3, 22)))
    
    #lt
    Assert(not(datetime.date(2005, 3, 22) < datetime.date(2005, 3, 22)))
    Assert(datetime.date(2005, 3, 22) < datetime.date(2005, 3, 23))
    Assert(datetime.date(2005, 3, 22) < datetime.date(2005, 3, 24))
    Assert(not (datetime.date(2005, 3, 22) < datetime.date(2005, 3, 21)))
    Assert(not (datetime.date(2005, 3, 22) < datetime.date(2005, 3, 20)))
    
    #hash
    x = datetime.date(2005, 3, 22)
    Assert(x.__hash__()!=None)
    
    #reduce
    x = datetime.date(2005, 3, 22)
    Assert(x.__reduce__()[0] == datetime.date)
    AreEqual(type(x.__reduce__()[1]), tuple)
    
    #repr
    AreEqual(repr(datetime.date(2005, 3, 22)), 'datetime.date(2005, 3, 22)')
    
    #str
    AreEqual(str(datetime.date(2005, 3, 22)), '2005-03-22')
    
    #ctime
    AreEqual(datetime.date(2005, 3, 22).ctime(), 'Tue Mar 22 00:00:00 2005')
    
    #isocalendar
    x = datetime.date(2005, 3, 22).isocalendar()
    AreEqual(x[0], 2005)
    AreEqual(x[1], 12)
    AreEqual(x[2], 2)
    
    #isoformat
    x = datetime.date(2005, 3, 22).isoformat()
    AreEqual(x, "2005-03-22")
    
    #isoweekday
    AreEqual(datetime.date(2005, 3, 22).isoweekday(), 2)
    
    #replace
    x = datetime.date(2005, 3, 22)
    y = datetime.date(2005, 3, 22)

    z = x.replace(year=1000)
    AreEqual(y, x)
    AreEqual(z.year, 1000)

    z = x.replace(month=5)
    AreEqual(y, x)
    AreEqual(z.month, 5)

    z = x.replace(day=25)
    AreEqual(y, x)
    AreEqual(z.day, 25)
    
    z = x.replace(year=1000, month=5)
    AreEqual(y, x)
    AreEqual(z.year, 1000)
    AreEqual(z.month, 5)
    
    z = x.replace(year=1000, day=25)
    AreEqual(y, x)
    AreEqual(z.year, 1000)
    AreEqual(z.day, 25)
    
    z = x.replace(day=25, month=5)
    AreEqual(y, x)
    AreEqual(z.day, 25)
    AreEqual(z.month, 5)
    
    z = x.replace(day=25, month=5, year=1000)
    AreEqual(y, x)
    AreEqual(z.day, 25)
    AreEqual(z.month, 5)
    AreEqual(z.year, 1000)
    
    #strftime
    AreEqual(x.strftime("%y-%a-%b"), "05-Tue-Mar")
    AreEqual(x.strftime("%Y-%A-%B"), "2005-Tuesday-March")
    AreEqual(x.strftime("%Y%m%d"), '20050322')
    
    #timetuple
    AreEqual(datetime.date(2005, 3, 22).timetuple(), (2005, 3, 22, 0, 0, 0, 1, 81, -1))
    
    #toordinal
    AreEqual(datetime.date(2005, 3, 22).toordinal(), 732027)

    #weekday
    AreEqual(datetime.date(2005, 3, 22).weekday(), 1)
    
    #fromordinal
    x = datetime.date.fromordinal(1234567)
    AreEqual(x.year, 3381)
    AreEqual(x.month, 2)
    AreEqual(x.day, 16)
    
    #fromtimestamp
    x = datetime.date.fromtimestamp(1000000000.0)
    AreEqual(x.year, 2001)
    AreEqual(x.month, 9)
    AreEqual(x.day, 8)
    
    #max
    x = datetime.date.max
    AreEqual(x.year, 9999)
    AreEqual(x.month, 12)
    AreEqual(x.day, 31)
    
    #min
    x = datetime.date.min
    AreEqual(x.year, 1)
    AreEqual(x.month, 1)
    AreEqual(x.day, 1)
    
    #resolution
    AreEqual(repr(datetime.date.resolution), 'datetime.timedelta(1)')
    
    #today
    datetime.date.today()
    
    #boolean op
    Assert(datetime.date(2005, 3, 22))
    
    
    
def test_datetime():
    x = datetime.datetime(2006,4,11,2,28,3,99,datetime.tzinfo())
    AreEqual(x.year, 2006)
    AreEqual(x.month, 4)
    AreEqual(x.day, 11)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 3)
    AreEqual(x.microsecond, 99)
    
    datetime.datetime(2006,4,11)
    datetime.datetime(2006,4,11,2)
    datetime.datetime(2006,4,11,2,28)
    datetime.datetime(2006,4,11,2,28,3)
    datetime.datetime(2006,4,11,2,28,3,99)
    datetime.datetime(2006,4,11,2,28,3,99, None)
    
    datetime.datetime(2006,4,11,hour=2)
    datetime.datetime(2006,4,11,hour=2,minute=28)
    datetime.datetime(2006,4,11,hour=2,minute=28,second=3)
    datetime.datetime(2006,4,11,hour=2,minute=28,second=3, microsecond=99)
    datetime.datetime(2006,4,11,hour=2,minute=28,second=3, microsecond=99, tzinfo=None)
    
    datetime.datetime(1, 1, 1, 0, 0, 0, 0) #min
    datetime.datetime(9999, 12, 31, 23, 59, 59, 999999) #max
    datetime.datetime(2004, 2, 29, 16, 20, 22, 262000) #leapyear
    
    AssertError(ValueError, datetime.datetime, 2006, 2, 29, 16, 20, 22, 262000) #bad leapyear
    AssertError(ValueError, datetime.datetime, 2006, 9, 31, 16, 20, 22, 262000) #bad number of days
    
    AssertError(ValueError, datetime.datetime, 0, 1, 1, 0, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 0, 1, 0, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, 0, 0, 0, 0, 0)
    
    AssertError(ValueError, datetime.datetime, -1, 1, 1, 0, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, -1, 1, 0, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, -1, 0, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, 1, -1, 0, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, 1, 0, -1, 0, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, 1, 0, 0, -1, 0)
    AssertError(ValueError, datetime.datetime, 1, 1, 1, 0, 0, 0, -1)
    AssertError(ValueError, datetime.datetime, -10, -10, -10, -10, -10, -10, -10)

    AssertError(ValueError, datetime.datetime, 10000, 12, 31, 23, 59, 59, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 13, 31, 23, 59, 59, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 12, 32, 23, 59, 59, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 12, 31, 24, 59, 59, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 12, 31, 23, 60, 59, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 12, 31, 23, 59, 60, 999999)
    AssertError(ValueError, datetime.datetime, 9999, 12, 31, 23, 59, 59, 1000000)
    AssertError(ValueError, datetime.datetime, 10000, 13, 32, 24, 60, 60, 1000000)
    AssertError(ValueError, datetime.datetime, 100000, 130, 320, 240, 600, 600, 10000000)
    AssertError(TypeError,  datetime.datetime, 2006, 4, 11, 2, 28, 3, 99, 1)
    
    #--------------------------------------------------------------------------
    #--Test subtraction datetime
    test_data = { ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 29, 15, 37, 28, 686000)) : ((0, 0, 0),(0, 0, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2007, 9, 29, 15, 37, 28, 686000)) : ((365, 0, 0),(-365, 0, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006,10, 29, 15, 37, 28, 686000)) : ((30, 0, 0),(-30, 0, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 30, 15, 37, 28, 686000)) : ((1, 0, 0),(-1, 0, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 29, 16, 37, 28, 686000)) : ((0, 3600, 0),(-1, 82800, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 29, 15, 38, 28, 686000)) : ((0, 60, 0),(-1, 86340, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 29, 15, 37, 29, 686000)) : ((0, 1, 0),(-1, 86399, 0)),
                  ((2006, 9, 29, 15, 37, 28, 686000), (2006, 9, 29, 15, 37, 28, 686001)) : ((0, 0, 1),(-1, 86399, 999999)),
                  ((1, 1, 1, 0, 0, 0, 0), (1, 1, 1, 0, 0, 0, 0)) : ((0, 0, 0),(0, 0, 0)),
                  ((9999, 12, 31, 23, 59, 59, 999999), (9999, 12, 31, 23, 59, 59, 999999)) : ((0, 0, 0),(0, 0, 0))
                  }
    for key, (value0, value1) in test_data.iteritems():
        dt1 = datetime.datetime(*key[1])
        dt0 = datetime.datetime(*key[0])
        
        x = dt1 - dt0
        AreEqual(x.days, value0[0])
        AreEqual(x.seconds, value0[1])
        AreEqual(x.microseconds, value0[2])
        
        y = dt0 - dt1
        AreEqual(y.days, value1[0])
        AreEqual(y.seconds, value1[1])
        AreEqual(y.microseconds, value1[2])
        
        
    #--------------------------------------------------------------------------
    #--Test subtraction - timedelta
    test_data = { ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, 0)) : (2006, 9, 29, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (1, 0, 0)) : (2006, 9, 28, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 1, 0)) : (2006, 9, 29, 15, 37, 27, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, 1)) : (2006, 9, 29, 15, 37, 28, 685999),
                  ((2006, 9, 29, 15, 37, 28, 686000), (1, 1, 1)) : (2006, 9, 28, 15, 37, 27, 685999),
                  
                  ((2006, 9, 29, 15, 37, 28, 686000), (-1, 0, 0)) : (2006, 9, 30, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, -1, 0)) : (2006, 9, 29, 15, 37, 29, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, -1)) : (2006, 9, 29, 15, 37, 28, 686001),
                  ((2006, 9, 29, 15, 37, 28, 686000), (-1, -1, -1)) : (2006, 9, 30, 15, 37, 29, 686001),
                  
                  ((9999, 12, 31, 23, 59, 59, 999999), (1, 1, 1)) : (9999, 12, 30, 23, 59, 58, 999998),
                  ((9999, 12, 31, 23, 59, 59, 999999), (9999*365, 0, 0)) : (7, 8, 21, 23, 59, 59, 999999),
                  ((9999, 12, 31, 23, 59, 59, 999999), (0, 0, 0)) : (9999, 12, 31, 23, 59, 59, 999999),
                  }
    for (dt1, td0), value in test_data.iteritems():
        
        x = datetime.datetime(*dt1) - datetime.timedelta(*td0)
        AreEqual(x.year,value[0])
        AreEqual(x.month, value[1])
        AreEqual(x.day, value[2])
        AreEqual(x.hour, value[3])
        AreEqual(x.minute, value[4])
        AreEqual(x.second, value[5])
        AreEqual(x.microsecond, value[6])
    
    
    #--------------------------------------------------------------------------
    #--Test addition
    test_data = { ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, 0)) : (2006, 9, 29, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (1, 0, 0)) : (2006, 9, 30, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 1, 0)) : (2006, 9, 29, 15, 37, 29, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, 1)) : (2006, 9, 29, 15, 37, 28, 686001),
                  ((2006, 9, 29, 15, 37, 28, 686000), (1, 1, 1)) : (2006, 9, 30, 15, 37, 29, 686001),
                  
                  ((2006, 9, 29, 15, 37, 28, 686000), (-1, 0, 0)) : (2006, 9, 28, 15, 37, 28, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, -1, 0)) : (2006, 9, 29, 15, 37, 27, 686000),
                  ((2006, 9, 29, 15, 37, 28, 686000), (0, 0, -1)) : (2006, 9, 29, 15, 37, 28, 685999),
                  ((2006, 9, 29, 15, 37, 28, 686000), (-1, -1, -1)) : (2006, 9, 28, 15, 37, 27, 685999),
    
                  ((9999, 12, 31, 23, 59, 59, 999999), (-1, -1, -1)) : (9999, 12, 30, 23, 59, 58, 999998),
                  ((9999, 12, 31, 23, 59, 59, 999999), (-9999*365, 0, 0)) : (7, 8, 21, 23, 59, 59, 999999),
                  ((9999, 12, 31, 23, 59, 59, 999999), (0, 0, 0)) : (9999, 12, 31, 23, 59, 59, 999999),
                  }
    for (dt1, td0), value in test_data.iteritems():
        
        x = datetime.datetime(*dt1) + datetime.timedelta(*td0)
        AreEqual(x.year,value[0])
        AreEqual(x.month, value[1])
        AreEqual(x.day, value[2])
        AreEqual(x.hour, value[3])
        AreEqual(x.minute, value[4])
        AreEqual(x.second, value[5])
        AreEqual(x.microsecond, value[6])
        
        #CodePlex Work Item 4861
        x = datetime.timedelta(*td0) + datetime.datetime(*dt1)
        AreEqual(x.year,value[0])
        AreEqual(x.month, value[1])
        AreEqual(x.day, value[2])
        AreEqual(x.hour, value[3])
        AreEqual(x.minute, value[4])
        AreEqual(x.second, value[5])
        AreEqual(x.microsecond, value[6])
    
    #--------------------------------------------------------------------------
    
    #today
    t0 = datetime.datetime.today()
    t1 = datetime.datetime.today()
    AreEqual(type(t0), datetime.datetime)
    Assert(t0<=t1)
    
    #now
    from time import sleep
    t0 = datetime.datetime.now()
    sleep(1)
    t1 = datetime.datetime.now()
    AreEqual(type(t0), datetime.datetime)
    Assert(t1>t0)
    datetime.datetime.now(None)
    
    #now
    t0 = datetime.datetime.utcnow()
    sleep(1)
    t1 = datetime.datetime.utcnow()
    AreEqual(type(t0), datetime.datetime)
    Assert(t1>t0)
    
    #fromtimestamp
    #Merlin Work Item 148717
    x = datetime.datetime.fromtimestamp(1000000000.0)
    AreEqual(x.year, 2001)
    AreEqual(x.month, 9)
    AreEqual(x.day, 8)
    #CodePlex 4862
    #AreEqual(x.hour, 18)
    AreEqual(x.minute, 46)
    AreEqual(x.second, 40)
    
    x = datetime.datetime.fromtimestamp(1000000000)
    AreEqual(x.year, 2001)
    AreEqual(x.month, 9)
    AreEqual(x.day, 8)
    #CodePlex Work Item 4862
    #AreEqual(x.hour, 18)
    AreEqual(x.minute, 46)
    AreEqual(x.second, 40)
    
    #fromordinal
    x = datetime.datetime.fromordinal(1234567)
    AreEqual(x.year, 3381)
    AreEqual(x.month, 2)
    AreEqual(x.day, 16)
    
    #fromtimestamp
    x = datetime.datetime.utcfromtimestamp(1000000000.0)
    AreEqual(x.year, 2001)
    AreEqual(x.month, 9)
    AreEqual(x.day, 9)
    #CodePlex 4862
    #AreEqual(x.hour, 1)
    AreEqual(x.minute, 46)
    AreEqual(x.second, 40)
    
    #combine
    x = datetime.datetime.combine(datetime.date(2005, 3, 22), datetime.time(2,28,3,99))
    y = datetime.datetime(2005, 3, 22, 2, 28, 3, 99)
    AreEqual(x, y)
    
    #strptime - new for Python 2.5. No need to test
    
    #min
    x = datetime.datetime.min
    AreEqual(x.year, datetime.MINYEAR)
    AreEqual(x.month, 1)
    AreEqual(x.day, 1)
    AreEqual(x.hour, 0)
    AreEqual(x.minute, 0)
    AreEqual(x.second, 0)
    AreEqual(x.microsecond, 0)
    #CodePlex Work Item 4863
    y = datetime.datetime(datetime.MINYEAR, 1, 1)
    AreEqual(x, y)
    
    #max
    #CodePlex Work Item 4864
    x = datetime.datetime.max
    AreEqual(x.year, datetime.MAXYEAR)
    AreEqual(x.month, 12)
    AreEqual(x.day, 31)
    AreEqual(x.hour, 23)
    AreEqual(x.minute, 59)
    AreEqual(x.second, 59)
    AreEqual(x.microsecond, 999999)
    #CodePlex Work Item 4865
    y = datetime.datetime(datetime.MAXYEAR, 12, 31, 23, 59, 59, 999999, None)
    AreEqual(x, y)
    
    #resolution
    #CodePlex Work Item 4866
    x = datetime.datetime.resolution
    AreEqual(x.days, 0)
    AreEqual(x.seconds, 0)
    AreEqual(x.microseconds, 1)
    
    #equality
    x = datetime.datetime(2005, 3, 22)
    y = x
    Assert(x==y)
    y = datetime.datetime(2005, 3, 23)
    Assert(not(x==y))
    Assert(x==datetime.datetime(2005, 3, 22))
    
    #inequality
    x = datetime.datetime(2005, 3, 22)
    y = None
    Assert(x!=y)
    y = datetime.datetime(2005, 3, 23)
    Assert(x!=y)
    
    #ge/le/gt/lt/eq/ne
    #CodePlex Work Item 4860
    #x_args = [2006, 9, 29, 15, 37, 28, 686000]
    x_args = [2006, 9, 29, 15, 37, 28]
    x = datetime.datetime(*x_args)
    for i in range(len(x_args)):
        
        y_args = x_args[0:]
        y_args [i] = y_args[i]+1
        y = datetime.datetime(*y_args)
        x_copy = datetime.datetime(*x_args)
        
        Assert(y>=x)
        Assert(x<=y)
        Assert(x<y)
        Assert(y>x)
        Assert(not x>y)
        Assert(not x>y)
        Assert(not x==y)
        Assert(x!=y)
        
        Assert(x==x_copy)
        Assert(x_copy >= x)
        Assert(x_copy <= x)
        
    #date and time
    date = datetime.date(2005, 3, 23)
    time = datetime.time(2,28,3,99)
    x = datetime.datetime(2005, 3, 23, 2, 28, 3, 99)
    AreEqual(x.date(), date)
    #CodePlex Work Item 4860
    AreEqual(x.time(), time)
    
    #timetz
    #CodePlex Work Item 4860
    x = datetime.datetime(2005, 3, 23, 2, 28, 3, 99)
    time = datetime.time(2,28,3,99)
    AreEqual(x.timetz(), time)
    
    #replace
    x = datetime.datetime(2005, 3, 23, 2, 28, 3, 99)
    y = datetime.datetime(2005, 3, 23, 2, 28, 3, 99)

    z = x.replace(year=1000)
    AreEqual(y, x)
    AreEqual(z.year, 1000)

    z = x.replace(month=7)
    AreEqual(y, x)
    AreEqual(z.month, 7)
    
    z = x.replace(day=12)
    AreEqual(y, x)
    AreEqual(z.day, 12)

    z = x.replace(hour=3)
    AreEqual(y, x)
    AreEqual(z.hour, 3)

    z = x.replace(minute=5)
    AreEqual(y, x)
    AreEqual(z.minute, 5)

    z = x.replace(second=25)
    AreEqual(y, x)
    AreEqual(z.second, 25)
    
    #CodePlex Work Item 4860
    z = x.replace(microsecond=250)
    AreEqual(y, x)
    AreEqual(z.microsecond, 250)
    
    z = x.replace(year=1000, month=7, day=12, hour=3, minute=5, second=25, microsecond=250)
    AreEqual(y, x)
    AreEqual(z.year, 1000)
    AreEqual(z.month, 7)
    AreEqual(z.day, 12)
    AreEqual(z.hour, 3)
    AreEqual(z.minute, 5)
    AreEqual(z.second, 25)
    #CodePlex Work Item 4860
    AreEqual(z.microsecond, 250)
    
    #astimezone
    #TODO
    #x = datetime.datetime(2005, 3, 23, 2, 28, 3, 99)
    #x.astimezone(...)
    
    #utcoffset
    x = datetime.datetime(2005, 3, 23, 2,28,3,99,None)
    AreEqual(x.utcoffset(), None)
    
    #dst
    x = datetime.datetime(2005, 3, 23,2,28,3,99,None)
    AreEqual(x.dst(), None)
    
    #tzname
    x = datetime.datetime(2005, 3, 23, 2,28,3,99,None)
    AreEqual(x.tzname(), None)
    
    #timetuple
    AreEqual(datetime.datetime(2005, 3, 23,2,28,3,99,None).timetuple(), (2005, 3, 23, 2, 28, 3, 2, 82, -1))
    
    #utctimetuple
    AreEqual(datetime.datetime(2005, 3, 23,2,28,3,99,None).utctimetuple(), (2005, 3, 23, 2, 28, 3, 2, 82, 0))
    
    #toordinal
    AreEqual(datetime.datetime(2005, 3, 23,2,28,3,99,None).toordinal(), 732028)

    #weekday
    AreEqual(datetime.datetime(2005, 3, 23,2,28,3,99,None).weekday(), 2)
    
    #isocalendar
    x = datetime.datetime(2005, 3, 22,2,28,3,99,None).isocalendar()
    AreEqual(x[0], 2005)
    AreEqual(x[1], 12)
    AreEqual(x[2], 2)
    
    #isoformat
    x = datetime.datetime(2005, 3, 22, 2,28,3,99,None).isoformat()
    AreEqual(x, '2005-03-22T02:28:03.000099')
    
    #isoweekday
    AreEqual(datetime.datetime(2005, 3, 22, 2,28,3,99,None).isoweekday(), 2)
    
    #ctime
    AreEqual(datetime.datetime(2005, 3, 22, 2,28,3,99,None).ctime(), 'Tue Mar 22 02:28:03 2005')
    
    
    #strftime
    x = datetime.datetime(2005, 3, 22, 2,28,3,99,None)
    AreEqual(x.strftime("%y-%a-%b"), "05-Tue-Mar")
    AreEqual(x.strftime("%Y-%A-%B"), "2005-Tuesday-March")
    AreEqual(x.strftime("%Y%m%d"), '20050322')
    AreEqual(x.strftime("%I:%M:%S"), "02:28:03")
    #Similar to Merlin Work Item 148470
    AreEqual(x.strftime("%H:%M:%S"), "02:28:03")
    #Similar to Merlin Work Item 148470
    if not is_silverlight:
        AreEqual(x.strftime("%a %A %b %B %c %d %H %I %j %m %M %p %S %U %w %W %x %X %y %Y %Z %%"), "Tue Tuesday Mar March 03/22/05 02:28:03 22 02 02 081 03 28 AM 03 12 2 12 03/22/05 02:28:03 05 2005  %")
    
def test_timedelta():
    #CodePlex Work Item 4871
    x = datetime.timedelta(1, 2, 3, 4, 5, 6, 7)
    AreEqual(x.days, 50)
    AreEqual(x.seconds, 21902)
    AreEqual(x.microseconds, 4003)
    
    x = datetime.timedelta(days=1, seconds=2, microseconds=3)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 3)
    
    x = datetime.timedelta(1, 2, 3)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 3)
    
    x = datetime.timedelta(1, 2)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 0)
    
    x = datetime.timedelta(1, 2, 3.14)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 3)
    
    #CodePlex WorkItem 5132
    x = datetime.timedelta(1, 2, 3.74)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 4)
    
    #CodePlex Work Item 5149
    x = datetime.timedelta(1, 2.0000003, 3.33)
    AreEqual(x.days, 1)
    AreEqual(x.seconds, 2)
    AreEqual(x.microseconds, 4)
    
    #CodePlex WorkItem 5133
    x = datetime.timedelta(microseconds=-1)
    AreEqual(x.days, -1)
    AreEqual(x.seconds, 86399)
    AreEqual(x.microseconds, 999999)
    
    #CodePlex Work Item 5136
    datetime.timedelta(-99999999, 0, 0) #min
    datetime.timedelta(0, 0, 0)
    #CodePlex Work Item 5136
    datetime.timedelta(999999999, 3600*24-1, 1000000-1) #max

    AssertError(OverflowError, datetime.timedelta, -1000000000, 0, 0)
    
    #CodePlex WorkItem 5133
    x = datetime.timedelta(0, -1, 0)
    AreEqual(x.days, -1)
    AreEqual(x.seconds, 3600*24-1)
    AreEqual(x.microseconds, 0)
    x = datetime.timedelta(0, 0, -1)
    AreEqual(x.days, -1)
    AreEqual(x.seconds, 3600*24-1)
    AreEqual(x.microseconds, 999999)
    
    AssertError(OverflowError, datetime.timedelta, 1000000000, 0, 0)
    AssertError(OverflowError, datetime.timedelta, 999999999, 3600*24, 0)
    AssertError(OverflowError, datetime.timedelta, 999999999, 3600*24-1, 1000000)
    
    #min
    x = datetime.timedelta.min
    AreEqual(x.days, -999999999)
    AreEqual(x.seconds, 0)
    AreEqual(x.microseconds, 0)
    
    #max
    x = datetime.timedelta.max
    AreEqual(x.days, 999999999)
    AreEqual(x.seconds, 3600*24-1)
    AreEqual(x.microseconds, 999999)
    
    #CodePlex Work Item 5136
    x = datetime.timedelta.resolution
    AreEqual(x.days, 0)
    AreEqual(x.seconds, 0)
    AreEqual(x.microseconds, 1)
    
    #--------------------------------------------------------------------------
    #--Test addition
    test_data = { ((37, 28, 686000), (37, 28, 686000)) : (74, 57, 372000),
                  ((37, 28, 686000), (38, 28, 686000)) : (75, 57, 372000),
                  ((37, 28, 686000), (37, 29, 686000)) : (74, 58, 372000),
                  ((37, 28, 686000), (37, 28, 686001)) : (74, 57, 372001),
                  ((0, 0, 0), (0, 0, 0)) : (0, 0, 0),
                  #Related to CodePlex Work Item 5135
                  ((999999999, 0, 0), (0, 0, 0)) : (999999999, 0, 0),
                  ((37, 28, 686000), (-1, -1, -1)) : (36, 27, 685999),
                  ((-1, -1, -1), (37, 28, 686000)) : (36, 27, 685999),
                  }
                  
    for key, value0 in test_data.iteritems():
        dt1 = datetime.timedelta(*key[1])
        dt0 = datetime.timedelta(*key[0])
        
        x = dt1 + dt0
        AreEqual(x.days, value0[0])
        AreEqual(x.seconds, value0[1])
        AreEqual(x.microseconds, value0[2])
    
    
    #--------------------------------------------------------------------------
    #--Test subtraction
    test_data = { ((37, 28, 686000), (37, 28, 686000)) : ((0, 0, 0),(0, 0, 0)),
                  ((37, 28, 686000), (38, 28, 686000)) : ((1, 0, 0),(-1, 0, 0)),
                  ((37, 28, 686000), (-1, -1, -1)) : ((-39, 86370, 313999),(38, 29, 686001)),
                  ((37, 28, 686000), (37, 29, 686000)) : ((0, 1, 0),(-1, 86399, 0)),
                  ((37, 28, 686000), (37, 28, 686001)) : ((0, 0, 1),(-1, 86399, 999999)),
                  ((0, 0, 0), (0, 0, 0)) : ((0, 0, 0),(0, 0, 0)),
                  #CodePlex Work Item 5135
                  ((999999999, 0, 0), (999999999, 0, 0)) : ((0, 0, 0),(0, 0, 0))
                  }
    for key, (value0, value1) in test_data.iteritems():
        dt1 = datetime.timedelta(*key[1])
        dt0 = datetime.timedelta(*key[0])
        
        x = dt1 - dt0
        AreEqual(x.days, value0[0])
        AreEqual(x.seconds, value0[1])
        AreEqual(x.microseconds, value0[2])
        
        y = dt0 - dt1
        AreEqual(y.days, value1[0])
        AreEqual(y.seconds, value1[1])
        AreEqual(y.microseconds, value1[2])
    
    #multiply
    x = datetime.timedelta(37, 28, 686000) * 12
    AreEqual(x.days, 444)
    AreEqual(x.seconds, 344)
    AreEqual(x.microseconds, 232000)
    
    #division
    x = datetime.timedelta(37, 28, 686000) // 3
    AreEqual(x.days, 12)
    AreEqual(x.seconds, 3600*8+9)
    AreEqual(x.microseconds, 562000)
    
    #+
    x = datetime.timedelta(37, 28, 686000)
    y = +x
    Assert(y==x)
    
    #-
    x = datetime.timedelta(37, 28, 686000)
    y = -x
    AreEqual(y.days, -38)
    AreEqual(y.seconds, 86371)
    AreEqual(y.microseconds, 314000)
    
    #absolute
    x = datetime.timedelta(37, 28, 686000)
    AreEqual(abs(x), x)
    y = datetime.timedelta(-1, 0, 0)
    AreEqual(abs(y), datetime.timedelta(1, 0, 0))
    
    #equality
    x = datetime.timedelta(1, 2, 33)
    y = x
    Assert(x==y)
    y = datetime.timedelta(1, 2, 34)
    Assert(not(x==y))
    Assert(x==datetime.timedelta(1, 2, 33))
    
    #inequality
    x = datetime.timedelta(1, 2, 33)
    y = None
    Assert(x!=y)
    y = datetime.timedelta(1, 2, 34)
    Assert(x!=y)
    
    #ge
    Assert(datetime.timedelta(1, 2, 33) >= datetime.timedelta(1, 2, 33))
    Assert(datetime.timedelta(1, 2, 34) >= datetime.timedelta(1, 2, 33))
    Assert(datetime.timedelta(1, 2, 35) >= datetime.timedelta(1, 2, 33))
    Assert(not (datetime.timedelta(1, 2, 32) >= datetime.timedelta(1, 2, 33)))
    Assert(not (datetime.timedelta(1, 2, 31) >= datetime.timedelta(1, 2, 33)))
    
    #le
    Assert(datetime.timedelta(1, 2, 33) <= datetime.timedelta(1, 2, 33))
    Assert(datetime.timedelta(1, 2, 33) <= datetime.timedelta(1, 2, 34))
    Assert(datetime.timedelta(1, 2, 33) <= datetime.timedelta(1, 2, 35))
    Assert(not (datetime.timedelta(1, 2, 33) <= datetime.timedelta(1, 2, 32)))
    Assert(not (datetime.timedelta(1, 2, 33) <= datetime.timedelta(1, 2, 31)))
    
    #gt
    Assert(not (datetime.timedelta(1, 2, 33) > datetime.timedelta(1, 2, 33)))
    Assert(datetime.timedelta(1, 2, 34) > datetime.timedelta(1, 2, 33))
    Assert(datetime.timedelta(1, 2, 35) > datetime.timedelta(1, 2, 33))
    Assert(not (datetime.timedelta(1, 2, 32) > datetime.timedelta(1, 2, 33)))
    Assert(not (datetime.timedelta(1, 2, 31) > datetime.timedelta(1, 2, 33)))
    
    #lt
    Assert(not(datetime.timedelta(1, 2, 33) < datetime.timedelta(1, 2, 33)))
    Assert(datetime.timedelta(1, 2, 33) < datetime.timedelta(1, 2, 34))
    Assert(datetime.timedelta(1, 2, 33) < datetime.timedelta(1, 2, 35))
    Assert(not (datetime.timedelta(1, 2, 33) < datetime.timedelta(1, 2, 32)))
    Assert(not (datetime.timedelta(1, 2, 33) < datetime.timedelta(1, 2, 31)))
    
    #hash
    x = datetime.timedelta(1, 2, 33)
    Assert(x.__hash__()!=None)
    
    #bool
    Assert(datetime.timedelta(0, 0, 1))
    Assert(not(datetime.timedelta(0, 0, 0)))

def test_time():
    #basic sanity checks
    x = datetime.time(2,28,3,99,datetime.tzinfo())
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 3)
    AreEqual(x.microsecond, 99)
    
    x = datetime.time(2,28,3,99,None)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 3)
    AreEqual(x.microsecond, 99)
    
    x = datetime.time(2,28,3,99)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 3)
    AreEqual(x.microsecond, 99)
    
    x = datetime.time(2,28,3)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 3)
    AreEqual(x.microsecond, 0)
    
    x = datetime.time(2,28)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 28)
    AreEqual(x.second, 0)
    AreEqual(x.microsecond, 0)
    
    x = datetime.time(2)
    AreEqual(x.hour, 2)
    AreEqual(x.minute, 0)
    AreEqual(x.second, 0)
    AreEqual(x.microsecond, 0)
    
    x = datetime.time()
    AreEqual(x.hour, 0)
    AreEqual(x.minute, 0)
    AreEqual(x.second, 0)
    AreEqual(x.microsecond, 0)
    
    datetime.time(0, 0, 0, 0) #min
    datetime.time(23, 59, 59, 999999) #max
    
    #negative cases
    #CodePlex Work Item 5143
    AssertError(ValueError, datetime.time, -1, 0, 0, 0)
    AssertError(ValueError, datetime.time, 0, -1, 0, 0)
    AssertError(ValueError, datetime.time, 0, 0, -1, 0)
    AssertError(ValueError, datetime.time, 0, 0, 0, -1)
    AssertError(ValueError, datetime.time, -10, -10, -10, -10)
    
    #CodePlex Work Item 5143
    AssertError(ValueError, datetime.time, 24, 59, 59, 999999)
    AssertError(ValueError, datetime.time, 23, 60, 59, 999999)
    AssertError(ValueError, datetime.time, 23, 59, 60, 999999)
    AssertError(ValueError, datetime.time, 23, 59, 59, 1000000)
    AssertError(ValueError, datetime.time, 24, 60, 60, 1000000)
    AssertError(ValueError, datetime.time, 240, 600, 600, 10000000)
    
    #min
    x = datetime.time.min
    AreEqual(x.hour, 0)
    AreEqual(x.minute, 0)
    AreEqual(x.second, 0)
    AreEqual(x.microsecond, 0)
    #max
    x = datetime.time.max
    AreEqual(x.hour, 23)
    AreEqual(x.minute, 59)
    AreEqual(x.second, 59)
    AreEqual(x.microsecond, 999999)
    
    #resolution
    #CodePlex Work Item 5145
    x = datetime.time.resolution
    AreEqual(repr(x), 'datetime.timedelta(0, 0, 1)')
    
    #equality
    x = datetime.time(1, 2, 33, 444)
    y = x
    Assert(x==y)
    y = datetime.time(1, 2, 33, 445)
    Assert(not(x==y))
    Assert(x==datetime.time(1, 2, 33, 444))
    
    #inequality
    x = datetime.time(1, 2, 33, 444)
    y = None
    Assert(x!=y)
    y = datetime.time(1, 2, 33, 445)
    Assert(x!=y)
    
    #ge
    Assert(datetime.time(1, 2, 33, 444) >= datetime.time(1, 2, 33, 444))
    Assert(datetime.time(1, 2, 33, 445) >= datetime.time(1, 2, 33, 444))
    Assert(datetime.time(1, 2, 33, 446) >= datetime.time(1, 2, 33, 444))
    Assert(not (datetime.time(1, 2, 33, 443) >= datetime.time(1, 2, 33, 444)))
    Assert(not (datetime.time(1, 2, 33, 442) >= datetime.time(1, 2, 33, 444)))
    
    #le
    Assert(datetime.time(1, 2, 33, 444) <= datetime.time(1, 2, 33, 444))
    Assert(datetime.time(1, 2, 33, 444) <= datetime.time(1, 2, 33, 445))
    Assert(datetime.time(1, 2, 33, 444) <= datetime.time(1, 2, 33, 446))
    Assert(not (datetime.time(1, 2, 33, 444) <= datetime.time(1, 2, 33, 443)))
    Assert(not (datetime.time(1, 2, 33, 444) <= datetime.time(1, 2, 33, 442)))
    
    #gt
    Assert(not (datetime.time(1, 2, 33, 444) > datetime.time(1, 2, 33, 444)))
    Assert(datetime.time(1, 2, 33, 445) > datetime.time(1, 2, 33, 444))
    Assert(datetime.time(1, 2, 33, 446) > datetime.time(1, 2, 33, 444))
    Assert(not (datetime.time(1, 2, 33, 443) > datetime.time(1, 2, 33, 444)))
    Assert(not (datetime.time(1, 2, 33, 442) > datetime.time(1, 2, 33, 444)))
    
    #lt
    Assert(not(datetime.time(1, 2, 33, 444) < datetime.time(1, 2, 33, 444)))
    Assert(datetime.time(1, 2, 33, 444) < datetime.time(1, 2, 33, 445))
    Assert(datetime.time(1, 2, 33, 444) < datetime.time(1, 2, 33, 446))
    Assert(not (datetime.time(1, 2, 33, 444) < datetime.time(1, 2, 33, 443)))
    Assert(not (datetime.time(1, 2, 33, 444) < datetime.time(1, 2, 33, 442)))
    
    #hash
    x = datetime.time(1, 2, 33, 444)
    Assert(x.__hash__()!=None)
    
    #bool
    Assert(datetime.time(0, 0, 0, 1))
    #CodePlex Work Item 5139
    Assert(not (datetime.time(0, 0, 0, 0)))
    
    #replace
    x = datetime.time(1, 2, 33, 444)
    y = datetime.time(1, 2, 33, 444)

    z = x.replace(hour=3)
    AreEqual(y, x)
    AreEqual(z.hour, 3)

    z = x.replace(minute=5)
    AreEqual(y, x)
    AreEqual(z.minute, 5)

    z = x.replace(second=25)
    AreEqual(y, x)
    AreEqual(z.second, 25)
    
    z = x.replace(microsecond=250)
    AreEqual(y, x)
    AreEqual(z.microsecond, 250)
    
    z = x.replace(hour=3, minute=5, second=25, microsecond=250)
    AreEqual(y, x)
    AreEqual(z.hour, 3)
    AreEqual(z.minute, 5)
    AreEqual(z.second, 25)
    AreEqual(z.microsecond, 250)
    
    #isoformat
    #CodePlex Work Item 5146
    x = datetime.time(2,28,3,99).isoformat()
    AreEqual(x, '02:28:03.000099')
    
    #strftime
    x = datetime.time(13,28,3,99)
    AreEqual(x.strftime("%I:%M:%S"), "01:28:03")
    AreEqual(x.strftime("%H:%M:%S"), "13:28:03")
    
    #utcoffset
    #CodePlex Work Item 5147
    x = datetime.time(2,28,3,99,None)
    AreEqual(x.utcoffset(), None)
    
    #dst
    x = datetime.time(2,28,3,99,None)
    AreEqual(x.dst(), None)
    
    #tzname
    #CodePlex Work Item 5148
    x = datetime.time(2,28,3,99,None)
    AreEqual(x.tzname(), None)
    
@skip("win32")
def test_tzinfo():
    x = datetime.tzinfo()
    AssertError(NotImplementedError, x.utcoffset, None)
    AssertError(NotImplementedError, x.dst, None)
    AssertError(NotImplementedError, x.tzname, None)
    AssertError(NotImplementedError, x.fromutc, None)
    
def test_invariant():
    for x in range(0, 10000, 99):
        now = datetime.datetime.now()
        delta = datetime.timedelta(microseconds=x)
        AreEqual(now, now + delta - delta)
        AreEqual(now, -delta + now + delta)

    pow10 = 1
    for x in range(10):
        pow10 = 10 * pow10
        for y in [-9, -1, 0, 1, 49, 99]:
            delta = datetime.timedelta(microseconds= pow10 + y)
            AreEqual(delta * 2, delta + delta)
            
            delta = datetime.timedelta(microseconds= -pow10 - y)
            AreEqual(delta * 3, delta + delta + delta)

@skip("win32")
def test_cli_interop():
    # can construct Python datetime from .NET datetime
    import System
    now = System.DateTime.Now
    pyNow = datetime.datetime(System.DateTime.Now)
    AreEqual(pyNow.month, now.Month)
    AreEqual(pyNow.day, now.Day)
    AreEqual(pyNow.year, now.Year)

def test_cp13704():
    '''
    TODO: 
    - extend this for datetime.date, datetime.time, etc
    - splatted args, keyword args, etc
    '''
    min_args = 3

    if not (is_cli or is_silverlight):
        AssertErrorWithMessage(TypeError, "Required argument 'year' (pos 1) not found", 
                               datetime.datetime)
        AssertErrorWithMessage(TypeError, "an integer is required",  
                               datetime.datetime, None, None)
        AssertErrorWithMessage(TypeError, "function takes at most 8 arguments (9 given)", 
                               datetime.datetime, None, None, None, None, None, None, None, None, None)
        AssertErrorWithMessage(TypeError, "function takes at most 8 arguments (10 given)", 
                               datetime.datetime, None, None, None, None, None, None, None, None, None, None)
        
    else: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=13704
        AssertErrorWithMessage(TypeError, "function takes at least %d arguments (0 given)" % min_args, 
                               datetime.datetime)
        AssertErrorWithMessage(TypeError, "function takes at least %d arguments (2 given)" % min_args, 
                               datetime.datetime, None, None)
        AssertErrorWithMessage(TypeError, "function takes at most 8 arguments (9 given)", 
                               datetime.datetime, None, None, None, None, None, None, None, None, None)
        AssertErrorWithMessage(TypeError, "function takes at most 8 arguments (10 given)", 
                               datetime.datetime, None, None, None, None, None, None, None, None, None, None)
                           

def test_pickle():
    import cPickle
    now = datetime.datetime.now()
    nowstr = cPickle.dumps(now)
    AreEqual(now, cPickle.loads(nowstr))

@skip("silverlight")
def test_datetime_datetime_pickled_by_cpy():
    import cPickle
    with open(r"pickles\cp18666.pickle", "rb") as f:
        expected_dt    = datetime.datetime(2009, 8, 6, 8, 42, 38, 196000)
        pickled_cpy_dt = cPickle.load(f)
        AreEqual(expected_dt, pickled_cpy_dt)

    expected_dt    = datetime.datetime(2009, 8, 6, 8, 42, 38, 196000, mytzinfo())
    pickled_cpy_dt = cPickle.loads("cdatetime\ndatetime\np1\n(S'\\x07\\xd9\\x01\\x02\\x03\\x04\\x05\\x00\\x00\\x06'\nc" + __name__ + "\nmytzinfo\np2\n(tRp3\ntRp4\n.")
    AreEqual(expected_dt.tzinfo, pickled_cpy_dt.tzinfo)

class mytzinfo(datetime.tzinfo):
    def __repr__(self): return 'hello'
    def __eq__(self, other): 
        return type(other) == type(self)

def test_datetime_repr():
    AreEqual(repr(datetime.datetime(2009, 1, 2, 3, 4, 5, 6, mytzinfo())), "datetime.datetime(2009, 1, 2, 3, 4, 5, 6, tzinfo=hello)")

#--MAIN------------------------------------------------------------------------
run_test(__name__)
