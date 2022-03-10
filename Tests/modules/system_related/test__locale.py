# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import _locale
import _random
import unittest

from iptest import is_cli, is_netcoreapp, is_posix, run_test

class _LocaleTest(unittest.TestCase):

    def setUp(self):
        super().setUp()
        self.saved_lc = [(lc, _locale.setlocale(lc, None)) for lc in
                            (getattr(_locale, lc_name) for lc_name in
                                dir(_locale) if lc_name.startswith('LC_') and lc_name != 'LC_ALL')
                        ]
        _locale.setlocale(_locale.LC_ALL, 'C')

    def tearDown(self):
        for lc, setting in self.saved_lc:
            _locale.setlocale(lc, setting)
        super().tearDown()

    def test_getdefaultlocale(self):
        result1 = _locale._getdefaultlocale()
        result2 = _locale._getdefaultlocale()
        self.assertEqual(result1,result2)
    
    def test_localeconv(self):
        result = _locale.localeconv()
        self.assertTrue(result != None, "The method does not return the correct value")
        self.assertTrue(len(result)>=14, "The elements in the sequence are not enough")
    
    def test_strxfrm(self):
        str1 = "this is a test !"
        str2 = _locale.strxfrm(str1)
        str1.__eq__(str2)
        
        str1 = "this is a test&^%%^$%$ !"
        str2 = _locale.strxfrm(str1)
        str1.__eq__(str2)
        
        str1 = "this \" \"is a test&^%%^$%$ !"
        str2 = _locale.strxfrm(str1)
        str1.__eq__(str2)
        
        str1 = "123456789"
        str2 = _locale.strxfrm(str1)
        str1.__eq__(str2)
        
        #argument is int type
        i = 12
        self.assertRaises(TypeError,_locale.strxfrm,i)
        
        #argument is random type
        obj = _random.Random()
        self.assertRaises(TypeError,_locale.strxfrm,obj)
    
    def collate(self,str1,str2,result):
        if result ==0:
            self.assertTrue(_locale.strcoll(str1,str2)==0, "expected that collating \" %r \" and \" %r \" result should greater than zero" % (str1, str2))
        elif result>0:
            self.assertTrue(_locale.strcoll(str1,str2)>0, "expected that collating \" %r \" and \" %r \" result should greater than zero" % (str1, str2))
        elif result<0:
            self.assertTrue(_locale.strcoll(str1,str2)<0, "expected that collating \" %r \" and \" %r \" result should less than zero" % (str1, str2))

    def validcollate(self):
        # str1 equal str2
        result = 0
        str1 ="This is a test"
        str2 ="This is a test"
        self.collate(str1,str2,result)
        
        str1 ="@$%$%+_)(*"
        str2 ="@$%$%+_)(*"
        self.collate(str1,str2,result)
        
        str1 ="123This is a test"
        str2 ="123This is a test"
        self.collate(str1,str2,result)
        
        # str1 is greater than str2
        result =1
        str1 = "this is a test"
        self.collate(str1,str2,result)
        
        str1 = "234 this is a test"
        str2 = "123 this is a test"
        self.collate(str1,str2,result)
        
        str2 = "@^@%^#& this is a test"
        str2 = "#^@%^#& this is a test"
        self.collate(str1,str2,result)
        
        # str1 is less than  str2
        result = -1
        str1 = "this is a test"
        str2 = "This Is a Test"
        self.collate(str1,str2,result)
            
        str1 = "This is a test123"
        str2 = "This is a test456"
        self.collate(str1,str2,result)
        
        str1 = "#$%This is a test"
        str2 = "@#$This is a test"
        self.collate(str1,str2,result)
        
        # an argument is int
        str2 = 3
        self.assertRaises(TypeError,_locale.strcoll,str1,str2)
        str2 = -1
        self.assertRaises(TypeError,_locale.strcoll,str1,str2)
        
        # an argument is Random type
        str2 = _random.Random()
        self.assertRaises(TypeError,_locale.strcoll,str1,str2)

    @unittest.skipIf(is_cli, 'https://github.com/IronLanguages/main/issues/447')
    def test_strcoll(self):
        _locale.setlocale(_locale.LC_COLLATE,"English")
        self.validcollate()
        
        _locale.setlocale(_locale.LC_COLLATE,"French")
        self.validcollate()

    @unittest.skipIf(is_cli, 'https://github.com/IronLanguages/main/issues/290')
    def test_setlocale(self):
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
            self.assertEqual(resultLocale,"English_United States.1252")
                
                        
        for c in c_list:
            resultLocale = None
            _locale.setlocale(c,"French")
            resultLocale = _locale.setlocale(c)
            self.assertEqual(resultLocale,"French_France.1252")

    @unittest.skipIf(is_netcoreapp and is_posix, 'TODO: figure out')
    def test_setlocale_negative(self):
        #the locale is empty string
        c= _locale.LC_ALL
        locale =''
        _locale.setlocale(c,locale)
        
        #the locale is None
        locale = _locale.setlocale(c)
        resultLocale =_locale.setlocale(c)
        self.assertEqual(locale,resultLocale)
        
        #set Numeric as a unknown locale,should thorw a _locale.Error
        locale ="11-22"
        self.assertRaises(_locale.Error,_locale.setlocale,c,locale)
        locale ="@^#^&%"
        self.assertRaises(_locale.Error,_locale.setlocale,c,locale)
        locale ="xxxxxx"
        self.assertRaises(_locale.Error,_locale.setlocale,c,locale)

    def test_Locale_category(self):
        """LC_ALL,LC_COLLATE,LC_CTYPE,LC_MONETARY,LC_NUMERIC,LC_TIME,LC_CHAR_MAX"""
        self.assertEqual(0,_locale.LC_ALL)
        self.assertEqual(1,_locale.LC_COLLATE)
        self.assertEqual(2,_locale.LC_CTYPE)
        self.assertEqual(3,_locale.LC_MONETARY)
        self.assertEqual(4,_locale.LC_NUMERIC)
        self.assertEqual(5,_locale.LC_TIME)
        self.assertEqual(127,_locale.CHAR_MAX)

    def test_bad_category(self):
        self.assertRaises(TypeError, _locale.setlocale, 'LC_NUMERIC', 'en_US.UTF8')
        self.assertRaises(TypeError, _locale.setlocale, 'LC_NUMERIC', None)

run_test(__name__)
