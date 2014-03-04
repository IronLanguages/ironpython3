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
from iptest.assert_util import *
import _locale
import _random

#_getdefaultlocale
def test_getdefaultlocale():
    result1 = _locale._getdefaultlocale()
    result2 = _locale._getdefaultlocale()
    AreEqual(result1,result2)
    
#localeconv
def test_localeconv():
    result = _locale.localeconv()
    Assert(result != None, "The method does not return the correct value")
    Assert(len(result)>=14, "The elements in the sequence are not enough")
    
#strxfrm
def test_strxfrm():
    str1 = "this is a test !"
    str2 = _locale.strxfrm(str1)
    cmp(str1,str2)
    
    str1 = "this is a test&^%%^$%$ !"
    str2 = _locale.strxfrm(str1)
    cmp(str1,str2)
    
    str1 = "this \" \"is a test&^%%^$%$ !"
    str2 = _locale.strxfrm(str1)
    cmp(str1,str2)
    
    str1 = "123456789"
    str2 = _locale.strxfrm(str1)
    cmp(str1,str2)
    
    #argument is int type
    i = 12
    AssertError(TypeError,_locale.strxfrm,i)
    
    #argument is random type
    obj = _random.Random()
    AssertError(TypeError,_locale.strxfrm,obj)
    
def collate(str1,str2,result):
    if result ==0:
        Assert(_locale.strcoll(str1,str2)==0, "expected that collating \" %r \" and \" %r \" result should greater than zero" % (str1, str2))
    elif result>0:
        Assert(_locale.strcoll(str1,str2)>0, "expected that collating \" %r \" and \" %r \" result should greater than zero" % (str1, str2))
    elif result<0:
        Assert(_locale.strcoll(str1,str2)<0, "expected that collating \" %r \" and \" %r \" result should less than zero" % (str1, str2))

def validcollate():
    # str1 equal str2
    result = 0
    str1 ="This is a test"
    str2 ="This is a test"
    collate(str1,str2,result)
    
    str1 ="@$%$%+_)(*"
    str2 ="@$%$%+_)(*"
    collate(str1,str2,result)
    
    str1 ="123This is a test"
    str2 ="123This is a test"
    collate(str1,str2,result)
    
    # str1 is greater than str2
    result =1
    str1 = "this is a test"
    collate(str1,str2,result)
    
    str1 = "234 this is a test"
    str2 = "123 this is a test"
    collate(str1,str2,result)
      
    str2 = "@^@%^#& this is a test"
    str2 = "#^@%^#& this is a test"
    collate(str1,str2,result)
     
    # str1 is less than  str2
    result = -1
    str1 = "this is a test"
    str2 = "This Is a Test"
    collate(str1,str2,result)
        
    str1 = "This is a test123"
    str2 = "This is a test456"
    collate(str1,str2,result)
    
    str1 = "#$%This is a test"
    str2 = "@#$This is a test"
    collate(str1,str2,result)
    
    # an argument is int
    str2 = 3
    AssertError(TypeError,_locale.strcoll,str1,str2)
    str2 = -1
    AssertError(TypeError,_locale.strcoll,str1,str2)
    
    # an argument is Random type
    str2 = _random.Random()
    AssertError(TypeError,_locale.strcoll,str1,str2)

@skip("cli", "silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21913  
def test_strcoll():
    _locale.setlocale(_locale.LC_COLLATE,"English")
    validcollate()
    
    _locale.setlocale(_locale.LC_COLLATE,"French")
    validcollate()

#CodePlex Work Item #9218
@skip("cli silverlight")
def test_setlocale():
    c_list = [ _locale.LC_ALL,
               _locale.LC_COLLATE,
               _locale.LC_CTYPE,
               _locale.LC_MONETARY,
               _locale.LC_NUMERIC,
               _locale.LC_TIME,
               ]
    
    for c in c_list:
        resultLocale = None
        _locale.setlocale(c,"English")
        resultLocale = _locale.setlocale(c)
        AreEqual(resultLocale,"English_United States.1252")
            
                    
    for c in c_list:
        resultLocale = None
        _locale.setlocale(c,"French")
        resultLocale = _locale.setlocale(c)
        AreEqual(resultLocale,"French_France.1252")
    
def test_setlocale_negative():
    #the locale is empty string
    c= _locale.LC_ALL
    locale =''
    _locale.setlocale(c,locale)
    
    #the locale is None
    locale = _locale.setlocale(c)
    resultLocale =_locale.setlocale(c)
    AreEqual(locale,resultLocale)
    
    #set Numeric as a unknown locale,should thorw a _locale.Error
    locale ="11-22"
    AssertError(_locale.Error,_locale.setlocale,c,locale)
    locale ="@^#^&%"
    AssertError(_locale.Error,_locale.setlocale,c,locale)
    locale ="xxxxxx"
    AssertError(_locale.Error,_locale.setlocale,c,locale)

#LC_ALL,LC_COLLATE,LC_CTYPE,LC_MONETARY,LC_NUMERIC,LC_TIME,LC_CHAR_MAX
def test_Locale_category():
    AreEqual(0,_locale.LC_ALL)
    AreEqual(1,_locale.LC_COLLATE)
    AreEqual(2,_locale.LC_CTYPE)
    AreEqual(3,_locale.LC_MONETARY)
    AreEqual(4,_locale.LC_NUMERIC)
    AreEqual(5,_locale.LC_TIME)
    AreEqual(127,_locale.CHAR_MAX)

def test_bad_category():
    AssertError(TypeError, _locale.setlocale, 'LC_NUMERIC', 'en_US.UTF8')
    AssertError(TypeError, _locale.setlocale, 'LC_NUMERIC', None)

run_test(__name__)
