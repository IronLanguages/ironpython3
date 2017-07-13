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

import gc
import unittest

from time import clock

from iptest import is_cli, is_cli64, is_mono, run_test, skipUnlessIronPython

def evalLoop(code, N):
    for i in range(N):
        func = compile(code, '<>', 'exec')
        eval(func)
    
def evalTest(code, N):
    from System import GC
    startMem = GC.GetTotalMemory(True)
    startTime = clock()
    evalLoop(code, N)
    endTime = clock()
    if is_mono:
        gc.collect()
    else:
        gc.collect(2)
    endMem = GC.GetTotalMemory(True)
    return max(endMem-startMem, 0)

t_list = [
        "if not 1 + 2 == 3: raise AssertionError('Assertion Failed')",
        "(a,b) = (0, 1)",
        "2+"*10 + "2",
    
        "import sys",
        
        "from time import clock",
    
        "eval('2+2')",
        "globals(), locals()",
    
        "try:\n    x = 10\nexcept:\n    pass",
    
        "def f(): pass",
        "def f(a): pass",
        "def f(a, b, c, d, e, f, g, h, i, j): pass",
    
        "def f(*args): pass",
        "def f(a, *args): pass",
        "def f(func, *args): func(args)",
        "def f(**args): pass",
    
        "def f(a, b=2, c=[2,3]): pass",
    
        "def f(x):\n    for i in range(x):\n        yield i",
        "def f(x):\n    print locals()",
        "def f(x):\n    print globals()",
    
        "lambda x: x + 2",
    
        "(lambda x: x + 2)(0)",
        "(lambda x, y, z, u, v, w: x + 2)(0, 0, 0, 0, 0, 0)",
    
        "class C:\n    pass",
    
        "class C:\n    class D:pass\n    pass",
        "def f(x):\n    def g(y):pass\n    pass",
        "def f(x):\n    def g(*y):pass\n    pass",
    
        "class C:\n    def f(self):\n        pass",
        "def f():\n    class C: pass\n    pass",
        "def f():pass\nclass C:pass\nf()",
    ]

@skipUnlessIronPython()
class MemoryTest(unittest.TestCase):
    def setUp(self):
        super(MemoryTest, self).setUp()

        import clr
        clr.AddReference("Microsoft.Dynamic")
        from Microsoft.Scripting.Generation import Snippets

        self.skipMemoryCheck = Snippets.Shared.SaveSnippets or clr.GetCurrentRuntime().Configuration.DebugMode
        self.expectedMem = 24000

        # account for adaptive compilation
        if is_cli64:
            self.expectedMem = int(self.expectedMem*1.25)

    def test_t_list(self):
        for code in t_list:
            baseMem = evalTest(code, 10)
            
            usedMax = max(self.expectedMem, 4*baseMem)
            if not self.skipMemoryCheck:
                for repetitions in [100, 500]:
                    usedMem = evalTest(code, repetitions)
                    self.assertTrue(usedMem < usedMax, "Allocated %i (max %i, base %i) running %s %d times" % (usedMem, usedMax, baseMem, code, repetitions))
            else:
                # not to measure the memory usage, but still try to peverify the code at the end
                evalTest(code, 2)
    
        e = compile("def f(): return 42\n", "", "single")
        names = {}
        eval(e, names)
        self.assertEqual(names['f'](), 42)
            
        code = """
x=2
def f(y):
    return x+y
z = f(3)
        """
        e = compile(code, "", "exec")
        names = {}
        eval(e, names)
        self.assertEqual(names['z'], 5)

    def test_cp26005(self):
        def coroutine():
            try: pass
            except: pass
            just_numbers = list(range(1,1000))
            def inner_method():
                        return just_numbers
            yield None
            yield None
        
        from System import GC
        def get_memory():
            for _ in range(4):
                GC.Collect()
                GC.WaitForPendingFinalizers()
            return GC.GetTotalMemory(True)/1e6
        before = get_memory()
        for j in range(10000):
            crt = coroutine()
            next(crt)
        after = get_memory()
        self.assertTrue(after-before < 10)


run_test(__name__)