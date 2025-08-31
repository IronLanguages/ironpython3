# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import *

class o1: pass
class o2: pass
class o3(o2): pass

class n1(object): 
    pass
class n2(object): 
    __slots__ = ['a', 'b']
class n3(object): 
    __slots__ = ['a', 'b', 'c']

class n4(n1): pass
class n5(n1):
    __slots__ = ['e', 'f']
    
class n6(n2): pass
class n7(n2):
    __slots__ = ['g', 'h', 'i']
    
class n8(object):
    __slots__ = ['__dict__']

os = [eval("o%s" % i) for i in range(1, 4)]
ns = [eval("n%s" % i) for i in range(1, 9)]
alls = os + ns + [object, float]

def combinators(handle, items, n):
    if n == 0:
        yield []
        return
    for i, item in enumerate(items):
        this = [item]
        for others in combinators(handle, handle(items, i), n-1):
            yield this + others

def combinations(items, n):
    def skipIthItem(items, i):
        return items[:i] + items[i+1:]
    return combinators(skipIthItem, items, n)

win_exception_map = {
    'Cannot create a consistent method resolution' : 'mro order', 
    'multiple bases have instance lay-out conflict' : 'lay-out conflicit',
}

cli_exception_map = {
    'invalid order for base classes' : 'mro order',
    'can only extend one CLI or builtin type' : 'lay-out conflicit',
}

def get_exception_summary():
    exception_map = is_cli and cli_exception_map or win_exception_map

    for (x, y) in list(exception_map.items()):
        if x in sys.exc_value.message: 
            return y
    
    return sys.exc_value.message 
        
count = 0

class test(object):
    def test__pass(self):
        global count
        for i in range(1, 4):
            for ts in combinations(alls, i):
                new_class = "g%s" % count
                count += 1
                base_types = ', '.join([t.__name__ for t in ts])
                
                code = "class %s(%s): pass" % (new_class, base_types)
                try: 
                    printwith("case", code)
                    exec(code, globals())
                except:
                    printwith("same", get_exception_summary())
                    
                    
    def test__with_slots(self):
        global count
        
        for i in range(1, 4):
            for ts in combinations(alls, i):
                new_class = "g%s" % count
                count += 1
                base_types = ', '.join([t.__name__ for t in ts])
                        
                code = "class %s(%s): __slots__ = 'abc'" % (new_class, base_types)
                try: 
                    printwith("case", code)
                    exec(code, globals())
                except:
                    printwith("same", get_exception_summary())
    
    # this depends on the first two tests. 
    # no good to look into the diff if the below tests still fail
    def test_derive_from_g(self):
        all_g = [x for x in dir(sys.modules[__name__]) if x[0] == 'g' and x[1:].isdigit()]
        for y in all_g:
            code = "class dg(%s): pass" % y  # __slots__ = 'a'
            try:
                printwith("case", code)
                exec(code)
            except:
                printwith("same", get_exception_summary())

# TODO: reduce the test case number by merging the first two like this:
#                if count % 2 == 0:
#                    code = "class %s(%s): pass" % (new_class, base_types)
#                else:
#                    code = "class %s(%s): __slots__ = 'abc'" % (new_class, base_types)
        
runtests(test)
