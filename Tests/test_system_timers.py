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
skiptest("win32")
skiptest("silverlight")

from time   import sleep
from _thread import start_new_thread

import System

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

def timer_helper(num_handlers=1, sleep_time=5, event_handlers=[], aTimer=None, error_margin = 0.25):
    '''
    Helper function used to test a single Timer object under various conditions
    '''
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
    
        aTimer.Interval = 1000
        aTimer.Enabled = True
        sleep(sleep_time)
        aTimer.Enabled = False
        sleep(sleep_time)  #give the other thread calling 'onTimedEvent' a chance to catch up
        
        #ensure the timer invoked our event handler a reasonable number of times
        Assert(COUNT >= int(sleep_time - sleep_time*error_margin) * (num_handlers+len(event_handlers)), str(COUNT))
        Assert(COUNT <= int(sleep_time + sleep_time*error_margin) * (num_handlers+len(event_handlers)), str(COUNT))
    finally:
        TIMER_HELPER_FINISHED = True

#------------------------------------------------------------------------------
#--Test functions
def test_sanity():
    '''
    http://msdn2.microsoft.com/en-us/library/system.timers.timer.aspx
    Minimal sanity check.
    '''
    timer_helper(num_handlers=1)

    
def test_sanity_from_thread():
    '''
    Simply runs test_sanity from a thread.
    '''
    global TIMER_HELPER_FINISHED
    TIMER_HELPER_FINISHED = False
    
    start_new_thread(test_sanity, tuple())

    print("Waiting for test_sanity to finish", end=' ')
    while not TIMER_HELPER_FINISHED:
        print(".", end=' ')
        sleep(1)


def test_multiple_events_one_timer():
    '''
    Multiple event handlers hooked up to a single Timer object
    '''
    timer_helper(num_handlers=5)
    timer_helper(num_handlers=500)
    
    
def test_elapsed_event_args():
    '''
    http://msdn2.microsoft.com/en-us/library/system.timers.elapsedeventargs.aspx
    '''
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
            Assert(ea.SignalTime <= System.DateTime.Now)
        except Exception as e:
            print(e)
            EVENT_ERRORS.append(e)
        
    #create and run the timer
    aTimer = System.Timers.Timer()
    aTimer.Elapsed += System.Timers.ElapsedEventHandler(checkEventArgs)
    aTimer.Interval = 1000
    aTimer.Enabled = True
    sleep(sleep_time)
    aTimer.Enabled = False
    sleep(sleep_time)  #give the other thread calling 'onTimedEvent' a chance to catch up
    
    #make sure it was called at least once
    Assert(COUNT > 0)
   
    #Let checkEventArgs take care of verification
    AreEqual(EVENT_ERRORS, [])
    

def test_elapsed_event_handler():
    '''
    http://msdn2.microsoft.com/en-us/library/system.timers.elapsedeventhandler.aspx
    '''
    #################################
    def bad0(): pass
    def bad1(a): pass
    def bad3(a, b, c): pass
    bad_list = [bad0, bad1, bad3]
    
    for bad_func in bad_list:
        eeh = System.Timers.ElapsedEventHandler(bad_func)
        AssertError(TypeError, eeh, "Sender", None)

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
        timer_helper(num_handlers=0,
                     sleep_time=4,
                     event_handlers=[System.Timers.ElapsedEventHandler(good_func)])
        
    timer_helper(num_handlers=0, sleep_time=3, event_handlers=good_list)

@skip("multiple_execute")
def test_timer():
    '''
    http://msdn2.microsoft.com/en-us/library/system.timers.timer_members.aspx
    '''
    global COUNT
    aTimer = System.Timers.Timer()
    
    
    #--Public Events...
    
    #Disposed
    AreEqual(DISPOSED_CALLED, False)
    aTimer.Disposed += disposed_helper
    
    #Elapsed
    #well covered by other tests
    
    
    #--Properties...
    
    #Container
    AreEqual(aTimer.Container, None)
    
    #Enabled
    #well tested by other tests
    AreEqual(aTimer.Enabled, False)
    aTimer.Enabled = True
    AreEqual(aTimer.Enabled, True)
    aTimer.Enabled = False
    
    #Interval
    #well tested by other tests
    AreEqual(aTimer.Interval, 100.0)
    aTimer.Interval = 20000.0
    AreEqual(aTimer.Interval, 20000.0)
    aTimer.Interval = 100.0
    
    #Site
    AreEqual(aTimer.Site, None)
    
    #SynchronizingObject
    AreEqual(aTimer.SynchronizingObject, None)
    
    #AutoReset
    AreEqual(aTimer.AutoReset, True)
    aTimer.AutoReset = False
    try:
        timer_helper(num_handlers=1, sleep_time=3, aTimer=aTimer)
        raise "Expected an AssertionError"
    except AssertionError:
        pass
    AreEqual(aTimer.AutoReset, False)
    aTimer.AutoReset = True
    
    #--Methods...
    
    #BeginInit
    aTimer.BeginInit()
    
    #EndInit
    aTimer.EndInit()
    
    #Start
    aTimer.Enabled = False
    AreEqual(aTimer.Enabled, False)
    aTimer.Start()
    AreEqual(aTimer.Enabled, True)
    #Stop
    aTimer.Stop()
    AreEqual(aTimer.Enabled, False)
    
    #Close
    aTimer.Close()
    
    #Dispose
    aTimer.Dispose()
    AreEqual(DISPOSED_CALLED, True)
    
    
def test_timer_description_attribute():
    '''
    http://msdn2.microsoft.com/en-us/library/system.timers.timersdescriptionattribute.aspx
    '''
    print("Nothing to do from Python?")
    
    
run_test(__name__)
