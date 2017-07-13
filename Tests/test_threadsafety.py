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

# test cases to verify the thread safety of the IronPython engine

import unittest

from iptest import is_mono, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ThreadSafetyTest(unittest.TestCase):

    #TODO: @skip("multiple_execute")
    @unittest.skipIf(is_mono, 'this causes an exception on mono, need to file a bug see https://github.com/IronLanguages/main/issues/1619')
    def test_all(self):
        from System.Threading import ManualResetEvent, Thread, ThreadStart
        class MyOC:
            one = 'one'
            two = 'two'
            three = 'three'
            four = 'four'
            five = 'five'
            six = 'six'
        
        class MyUserType(object):
            one = 'one'
            two = 'two'
            three = 'three'
            four = 'four'
            five = 'five'
            six = 'six'
        
        class BaseUserTypeOne(object):
            pass
        
        class BaseUserTypeTwo(object):
            pass
        
        class DerivedUserType(BaseUserTypeOne):
            pass
        
            
            
        go = ManualResetEvent(False)
        
        # setup a table used for transforming loop iterations into identifiers.
        # 1 becomes A, 11 becomes AA, 12 becomes AB, 123 becomes ABC, etc...
        
        identityTable = [chr(x) for x in range(256)]
        transTable = ''
        for x in identityTable:
            if ord(x) >= ord('0') and ord(x) <= ord('9'):
                transTable = transTable + chr(ord('A') + ord(x) - ord('0'))
            else:
                transTable = transTable + x
        
        # base test infrastructure, reader thread, writer thread, and test
        # runner
        def reader():
            try:
                try:
                    go.WaitOne()
                    for i in xrange(loopCnt): readerWorker(i)
                except Exception, e:
                    self.fail('Test failed, unexpected exception from reader: %r' % e)
            finally:
                global readerAlive
                readerAlive = False
        
        class writer:
            def __init__(self, writer, index):
                self.writer = writer
                self.index = index
            def __call__(self, *args):
                try:
                    try:
                        go.WaitOne()
                        for i in xrange(loopCnt): self.writer(i, self.index)
                    except Exception, e:
                        self.fail('Test failed (writer through exception): %r' % e)
                finally:
                    global writerAlive
                    writerAlive = False
        
        def runTest():
            global writerAlive
            global readerAlive
            writerAlive = True
            readerAlive = True
            
            a = Thread(ThreadStart(reader))
            a.IsBackground = True
            a.Start()
            
            if isinstance(writerWorker, tuple):
                b = []
                index = 0
                for wr in writerWorker:
                    th = Thread(ThreadStart(writer(wr, index)))
                    th.IsBackground = True
                    th.Start()
                    b.append(th)
                    index = index + 1
            else:
                b = [Thread(ThreadStart(writer(writerWorker, 0)))]
                b[0].IsBackground = True
                b[0].Start()
            
            go.Set()
            
            a.Join()
            for th in b: th.Join()
        
        # individual test cases, setup reader/writer workers
        # and then call run test
        
        
        # OldClass test cases
        
        def Reader_Dir(i):
            x = dir(oc)
        
        def Reader_CheckOneKey(i):
            self.assertEqual(getattr(oc, 'outsideFuncEnv'), outsideFuncEnvValue)
            
        def Reader_HasAttr(i):
            attrName = str(i).translate(transTable)
            hasattr(oc, attrName)
        
        def Reader_VerifyAllPresent(i):
            # verify that we can find all the previous values in the dictionary
            global writerAlive
            if not writerAlive: return  # writer takes signifcantly less time, stop checking once we're no longer experiencing contention
            
            global prevValues
            for x in prevValues.keys():
                #print 'checking ', x
                self.assertEqual(hasattr(oc, x), True)
                #print getattr(oc,x), prevValues[x]
                self.assertEqual(getattr(oc, x), prevValues[x])
            for x in dir(oc):
                if not prevValues.has_key(x):
                    prevValues[x] = getattr(oc, x)
                    
        # generic writer, just keeps adding more and more attributes
        def Writer_Generic(i, writerIndex):
            attrName = str(i).translate(transTable)
            x = object()
            setattr(oc, attrName, x)
            self.assertEqual(getattr(oc, attrName), x)
        
        # generic writer, adds  an attribute and then removes it.
        def Writer_Generic_Delete(i, writerIndex):
            attrName = str(i).translate(transTable)
            x = object()
            setattr(oc, attrName, x)
            self.assertEqual(getattr(oc, attrName), x)
            delattr(oc, attrName)
        
        # adds attributes to the class, deletes them in batches of 50
        def Writer_Generic_BatchDelete(i, writerIndex):
            attrName = str(i).translate(transTable)
            x = object()
            setattr(oc, attrName, x)
            self.assertEqual(getattr(oc, attrName), x)
            if i != 0 and i % 50 == 0:
                for x in xrange(i-50, i):
                    attrName = str(x).translate(transTable)
                    delattr(oc, attrName)
            
            
        def Reader_ExtraKeys(i):
            global oc
            if (i % 6) == 0:
                # create a new instance every 6 iterations (6 is the number
                # of custom keys per old-instance dict)
                oc = MyOC()
            if hasattr(oc, 'abc'): self.assertEqual(oc.abc, 'abc')
            if hasattr(oc, 'abcd'): self.assertEqual(oc.abcd, 'abcd')
            if hasattr(oc, 'abcde'): self.assertEqual(oc.abcde, 'abcde')
            if hasattr(oc, 'abcdef'): self.assertEqual(oc.abcdef, 'abcdef')
            if hasattr(oc, 'abcdefg'): self.assertEqual(oc.abcdefg, 'abcdefg')
            if hasattr(oc, 'abcdefgh'): self.assertEqual(oc.abcdefgh, 'abcdefgh')
            
        def Writer_ExtraKeys(i, writerIndex):
            if i % 6 == 0: oc.abc = 'abc'
            elif i % 6 == 1: oc.abcd = 'abcd'
            elif i % 6 == 2: oc.abcde = 'abcde'
            elif i % 6 == 3: oc.abcdef = 'abcdef'
            elif i % 6 == 4: oc.abcdefg = 'abcdefg'
            elif i % 6 == 5: oc.abcdefgh = 'abcdefgh'
            
            
        def Init_OldClass(loopCnt):
            global oc, outsideFuncEnvValue, prevValues
            prevValues = {}
        
            outsideFuncEnvValue = object()
            oc = MyOC()
            
            # fill in some attributes so we don't hit extra values...
            for id in range(loopCnt, loopCnt+20):
                setattr(oc, str(id).translate(transTable), object())
            
            # now set a value that's outside of the extra values
            setattr(oc, 'outsideFuncEnv', outsideFuncEnvValue)
            
        
        def Nop(i):
            pass
        
        # List Test Cases
            
        def List_Writer(i, writerIndex):
            myList.append(i)
            
        def List_Writer_Extend(i, writerIndex):
            myList.extend([0,1,2,3,4,5,6,7,8,9])
            
        def List_Reader(i):
            global lastRead
            if myList: lastRead = myList[-1]
                
        def List_Clear(i, writerIndex):
            if i % 50 == 0:
                if myList: del myList[:]
        
        def List_Slice_Set(i, writerIndex):
            if i % 50 == 0:
                myList[:] = []
        
        def List_Index_Writer(i, writerIndex):
            global lastRead
            if myList: myList[-1] = lastRead
        
        def Init_List_Sort(loopCnt):
            global listOfLists1,listOfLists2
            
            listOfLists1 = []
            listOfLists2 = []
            for x in range(200):
                listOfLists1.append([])
                listOfLists2.append([])
            for x in listOfLists1:
                for y in listOfLists2:
                    x.append(y)
                    y.append(x)
        
        def List_Sorter(i, writerIndex):
            global listOfLists1, listOfLists2
            if writerIndex == 1: listOfLists1.sort()
            else: listOfLists2.sort()
        
        def Init_List(loopCnt):
            global myList, lastRead
            myList = []
            lastRead = 0
        
        def PostCondition_List(expectedLength):
            # create a closer around the inner function w/ the expected length
            def PostCondition_List_Inner():
                self.assertEqual(len(myList), expectedLength)
            return PostCondition_List_Inner
        
        # user type test cases
        
        # verify mro / base classes update atomically
        def UserType_Write_Bases(i, writerIndex):
            if BaseUserTypeOne in myUserType.__bases__:
                myUserType.__bases__ = (BaseUserTypeTwo,)
            else:
                myUserType.__bases__ = (BaseUserTypeOne,)
        
        def UserType_Read_BasesAndMro(i):
            bases = myUserType.__bases__
            self.assertTrue(bases == (BaseUserTypeOne,) or bases == (BaseUserTypeTwo,))
            mro = myUserType.__mro__
            self.assertTrue(mro == (DerivedUserType, BaseUserTypeOne, object) or mro == (DerivedUserType, BaseUserTypeTwo, object))
        
        def Init_UserType_Bases(loopCnt):
            global myUserType
            myUserType = DerivedUserType
            
        def Init_UserType(loopCnt):
            global oc, outsideFuncEnvValue, prevValues
            prevValues = {}
            
            outsideFuncEnvValue = object()
            oc = MyUserType()
            
            # set a value that's outside of the extra values
            setattr(oc, 'outsideFuncEnv', outsideFuncEnvValue)
            
        def Init_NewTypeMaker(loopCnt):
            global baseTypes
            baseTypes = (object,)
        
        def CreateType(i, writerIndex):
            global baseTypes
                
            # we keep deriving a new type from a new combination of base types - always
            # object and an ever-growing list of old-style classes.  Thread 0 is responsible
            # for pushing a new base type in.  This causes all the creation threads to hit
            # contention and wait for a type to be published.
            type('foo', baseTypes, {})
            
            if writerIndex == 0:
                class foo: pass
                baseTypes = baseTypes + (foo,)
        
        
        
        testCases = [ # initialization, reader, writer, test loopCnt, post-condition.
                    #    Multiple writes can be provided w/ a tuple
                    
                    # new type maker tests
                    (Init_NewTypeMaker, Nop, (CreateType, CreateType, CreateType, CreateType, CreateType, CreateType), 100),
                    
                    # user type tests
                    (Init_UserType, Reader_ExtraKeys, (Writer_ExtraKeys,Writer_ExtraKeys), 100000),
                    (Init_UserType, Reader_CheckOneKey, Writer_Generic_BatchDelete, 10000),
                    (Init_UserType, Reader_CheckOneKey, Writer_Generic, 10000),
                    (Init_UserType, Reader_CheckOneKey, Writer_Generic_Delete, 10000),
                    (Init_UserType, Reader_Dir, Writer_Generic_BatchDelete, 10000),
                    (Init_UserType, Reader_VerifyAllPresent, Writer_Generic, 10000),
                    (Init_UserType, Reader_Dir, Writer_Generic_Delete, 10000),
                    (Init_UserType, Reader_HasAttr, Writer_Generic, 10000),
                    (Init_UserType_Bases, UserType_Read_BasesAndMro, UserType_Write_Bases, 10000),
                    
                    # list tests
                    
                    (Init_List_Sort, Nop, (List_Sorter, List_Sorter), 1000),
                    (Init_List, List_Reader, (List_Index_Writer, List_Slice_Set), 100000),
                    (Init_List, List_Reader, (List_Index_Writer, List_Clear), 100000),
                    (Init_List, List_Reader, (List_Writer), 10000, PostCondition_List(10000)),
                    (Init_List, List_Reader, (List_Index_Writer, List_Writer_Extend), 10000, PostCondition_List(10000*10)),
                    (Init_List, List_Reader, (List_Writer, List_Index_Writer), 10000, PostCondition_List(10000)),
                    (Init_List, Nop, (List_Writer_Extend, List_Writer_Extend), 10000, PostCondition_List(10000*2*10)),
                    (Init_List, Nop, (List_Writer, List_Writer, List_Writer), 10000, PostCondition_List(10000*3)),
                    
                    # old-style class tests
                    (Init_OldClass, Reader_ExtraKeys, (Writer_ExtraKeys,Writer_ExtraKeys), 100000),
                    (Init_OldClass, Reader_CheckOneKey, Writer_Generic_BatchDelete, 10000),
                    (Init_OldClass, Reader_CheckOneKey, Writer_Generic, 10000),
                    (Init_OldClass, Reader_CheckOneKey, Writer_Generic_Delete, 10000),
                    (Init_OldClass, Reader_Dir, Writer_Generic_BatchDelete, 10000),
                    (Init_OldClass, Reader_VerifyAllPresent, Writer_Generic, 10000),
                    (Init_OldClass, Reader_Dir, Writer_Generic_Delete, 10000),
                    (Init_OldClass, Reader_HasAttr, Writer_Generic, 10000),
                    ]
        
        def doOneTest(test):
            """runs a single test, argument is a testCase tuple"""
            print 'running test', test
            global loopCnt, writerWorker, readerWorker
            
            # call init function
            loopCnt = test[3]
            test[0](loopCnt)
            
            # set our workers
            readerWorker = test[1]
            writerWorker = test[2]
            
            # set our loopcnt
            
            # run the test
            runTest()
        
            if len(test) >= 5: test[4]()
    
        for test in testCases:
            doOneTest(test)
    
    def test_random(self):    
        import _random
        random = _random.Random()
        from System.Threading import Thread, ParameterizedThreadStart    
        global zeroCount
        zeroCount = 0
        
        def foo((ntimes,nbits)):
            for i in xrange(ntimes):
                x = random.getrandbits(nbits)
                if x == 0:
                    zeroCount += 1
        
        def run_many(nthreads,ntimes,nbits):
            lst_threads = []
            for i in xrange(nthreads):
                t = Thread(ParameterizedThreadStart(foo))
                t.Start((ntimes,nbits))
                lst_threads.append(t)
            for t in lst_threads:
                t.Join()
        
        run_many(10,10**6,63)
        self.assertTrue(zeroCount < 3)
    
run_test(__name__)
