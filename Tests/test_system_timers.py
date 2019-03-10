# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import is_netcoreapp, run_test, skipUnlessIronPython
from time import sleep
from _thread import start_new_thread

if is_netcoreapp:
    import clr
    clr.AddReference("System.ComponentModel.TypeConverter")

COUNT = 0
TIMER_HELPER_FINISHED = False
DISPOSED_CALLED = False
EVENT_ERRORS = []

#------------------------------------------------------------------------------
#--Helper functions
def onTimedEvent(source, e):
    '''Helper function'''
    global COUNT
    COUNT = COUNT + 1

def disposed_helper(a, b):
    global DISPOSED_CALLED
    DISPOSED_CALLED = True

@skipUnlessIronPython()
class SystemTimersTest(unittest.TestCase):

    def timer_helper(self, num_handlers=1, sleep_time=5, event_handlers=[], aTimer=None, error_margin = 0.25):
        '''
        Helper function used to test a single Timer object under various conditions
        '''
        import System
        global COUNT
        global TIMER_HELPER_FINISHED

        COUNT = 0
        TIMER_HELPER_FINISHED = False

        try:
            #create and run the timer
            if aTimer==None:
                aTimer = System.Timers.Timer()

            for i in range(num_handlers): aTimer.Elapsed += System.Timers.ElapsedEventHandler(onTimedEvent)
            for handler in event_handlers: aTimer.Elapsed += System.Timers.ElapsedEventHandler(handler)

            aTimer.Interval = 100
            aTimer.Enabled = True
            sleep(sleep_time / 10.0)
            aTimer.Enabled = False
            sleep(sleep_time / 10.0)  #give the other thread calling 'onTimedEvent' a chance to catch up

            #ensure the timer invoked our event handler a reasonable number of times
            self.assertTrue(COUNT >= int(sleep_time - sleep_time*error_margin) * (num_handlers+len(event_handlers)), '%d is not >= %d' % (COUNT, int(sleep_time - sleep_time*error_margin) * (num_handlers+len(event_handlers))))
            self.assertTrue(COUNT <= int(sleep_time + sleep_time*error_margin) * (num_handlers+len(event_handlers)), '%d is not <= %d' % (COUNT, int(sleep_time + sleep_time*error_margin) * (num_handlers+len(event_handlers))))
        finally:
            TIMER_HELPER_FINISHED = True

    def test_sanity(self):
        '''
        http://msdn2.microsoft.com/en-us/library/system.timers.timer.aspx
        Minimal sanity check.
        '''
        self.timer_helper(num_handlers=1)


    def test_sanity_from_thread(self):
        '''
        Simply runs test_sanity from a thread.
        '''
        global TIMER_HELPER_FINISHED
        TIMER_HELPER_FINISHED = False

        start_new_thread(self.test_sanity, tuple())

        print("Waiting for test_sanity to finish", end="")
        while not TIMER_HELPER_FINISHED:
            print(".", end="")
            sleep(0.1)


    def test_multiple_events_one_timer(self):
        '''
        Multiple event handlers hooked up to a single Timer object
        '''
        global COUNT
        COUNT=0
        self.timer_helper(num_handlers=5)
        self.timer_helper(num_handlers=500)


    def test_elapsed_event_args(self):
        '''
        http://msdn2.microsoft.com/en-us/library/system.timers.elapsedeventargs.aspx
        '''
        import System
        global COUNT
        COUNT = 0

        sleep_time = 3

        def checkEventArgs(source, ea):
            '''
            Helper function just verifies that the second parameter is indeed the correct
            type and has a reasonable value.
            '''
            global COUNT
            COUNT = COUNT + 1

            #BUG? exceptions are not propagated out of events
            try:
                self.assertTrue(ea.SignalTime <= System.DateTime.Now)
            except Exception as e:
                print(e)
                EVENT_ERRORS.append(e)

        #create and run the timer
        aTimer = System.Timers.Timer()
        aTimer.Elapsed += System.Timers.ElapsedEventHandler(checkEventArgs)
        aTimer.Interval = 100
        aTimer.Enabled = True
        sleep(sleep_time / 10.0)
        aTimer.Enabled = False
        sleep(sleep_time / 10.0)  #give the other thread calling 'onTimedEvent' a chance to catch up

        #make sure it was called at least once
        self.assertTrue(COUNT > 0)

        #Let checkEventArgs take care of verification
        self.assertEqual(EVENT_ERRORS, [])


    def test_elapsed_event_handler(self):
        '''
        http://msdn2.microsoft.com/en-us/library/system.timers.elapsedeventhandler.aspx
        '''
        import System
        global COUNT
        COUNT = 0
        #################################
        def bad0(): pass
        def bad1(a): pass
        def bad3(a, b, c): pass
        bad_list = [bad0, bad1, bad3]

        for bad_func in bad_list:
            eeh = System.Timers.ElapsedEventHandler(bad_func)
            self.assertRaises(TypeError, eeh, "Sender", None)

        #################################
        def good2(a, b):
            global COUNT
            COUNT = COUNT + 1

        def goodDefault1(a, b=2):
            global COUNT
            COUNT = COUNT + 1

        def goodDefault2(a=1, b=2):
            global COUNT
            COUNT = COUNT + 1

        def goodAny(*a, **b):
            global COUNT
            COUNT = COUNT + 1

        good_list = [good2, goodDefault1, goodDefault2, goodAny]

        for good_func in good_list:
            self.timer_helper(num_handlers=0,
                        sleep_time=4,
                        event_handlers=[System.Timers.ElapsedEventHandler(good_func)])

        self.timer_helper(num_handlers=0, sleep_time=3, event_handlers=good_list)

    #TODO: @skip("multiple_execute")
    def test_timer(self):
        '''
        http://msdn2.microsoft.com/en-us/library/system.timers.timer_members.aspx
        '''
        import System
        global COUNT
        aTimer = System.Timers.Timer()

        #--Public Events...

        #Disposed
        self.assertFalse(DISPOSED_CALLED)
        aTimer.Disposed += disposed_helper

        #Elapsed
        #well covered by other tests


        #--Properties...

        #Container
        self.assertEqual(aTimer.Container, None)

        #Enabled
        #well tested by other tests
        self.assertFalse(aTimer.Enabled)
        aTimer.Enabled = True
        self.assertTrue(aTimer.Enabled)
        aTimer.Enabled = False

        #Interval
        #well tested by other tests
        self.assertEqual(aTimer.Interval, 100.0)
        aTimer.Interval = 20000.0
        self.assertEqual(aTimer.Interval, 20000.0)
        aTimer.Interval = 100.0

        #Site
        self.assertEqual(aTimer.Site, None)

        #SynchronizingObject
        self.assertEqual(aTimer.SynchronizingObject, None)

        #AutoReset
        self.assertTrue(aTimer.AutoReset)
        aTimer.AutoReset = False
        try:
            self.timer_helper(num_handlers=1, sleep_time=3, aTimer=aTimer)
            raise "Expected an AssertionError"
        except AssertionError:
            pass
        self.assertFalse(aTimer.AutoReset)
        aTimer.AutoReset = True

        #--Methods...

        #BeginInit
        aTimer.BeginInit()

        #EndInit
        aTimer.EndInit()

        #Start
        aTimer.Enabled = False
        self.assertFalse(aTimer.Enabled)
        aTimer.Start()
        self.assertTrue(aTimer.Enabled)
        #Stop
        aTimer.Stop()
        self.assertFalse(aTimer.Enabled)

        #Close
        aTimer.Close()

        #Dispose
        aTimer.Dispose()
        self.assertTrue(DISPOSED_CALLED)


    def test_timer_description_attribute(self):
        '''
        http://msdn2.microsoft.com/en-us/library/system.timers.timersdescriptionattribute.aspx
        '''
        print("Nothing to do from Python?")

run_test(__name__)
