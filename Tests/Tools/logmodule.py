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

"""Used for getting a list of members defined in a module.

When run stand-alone outputs the differences for all built-in modules
and known types.

Also used by modulediff.py for testing to ensure we don't regress.
"""


import sys
import datetime
import _weakref

class C(object):
    def f(self): pass
class OC: pass
def f(): pass

knownTypes = [object, bytes, bool, int, str, list, tuple, dict, float, complex, int, type(f), type(C().f), type(OC), type(list.append), type([].append), type(None), type(object.__new__), 
              type(type.__dict__), type(Exception.args), type(complex.imag), type(type.__call__), type(type.__call__.__call__), type(datetime.datetime.max), type(datetime.date.max), type(datetime.time.max), 
              type(datetime.timedelta.max), type(staticmethod.__new__), str, type(_weakref.ProxyType.__add__), C.__weakref__.__class__, file, str]


BUILTIN_MODULES =  [
                    "__builtin__",
                    "_codecs",
                    "_collections", 
                    "copy_reg",
                    "_functools",
                    "_heapq", #CodePlex 21396
                    "_locale",
                    "_md5",
                    "_random",
                    "_sha",
                    "_sha256",
                    "_sha512",
                    "_sre",
                    "_struct",
                    "_subprocess",
                    "_warnings", 
                    "_weakref", 
                    "_winreg", 
                    "array", 
                    "binascii", 
                    "cPickle", 
                    "cStringIO", 
                    "cmath", 
                    "datetime", 
                    "errno",
                    "exceptions",
                    "future_builtins", #CodePlex 19580
                    "gc",
                    "imp",
                    "itertools", 
                    "marshal",
                    "math", 
                    "mmap",
                    "msvcrt",
                    "nt", 
                    "operator", 
                    "select",
                    "signal", #CodePlex 16414
                    "sys",
                    "thread",
                    "time", 
                    "xxsubtype",
                    ]
                    
MISSING_MODULES = [
                    "_csv", #CodePlex 21395
                    "_hotshot", #CodePlex 21397
                    "_json", #CodePlex 19581
                    "_lsprof", #CodePlex 21398
                    "_multibytecodec", #CodePlex 21399
                    "_ast", #CodePlex 21088
                    "_bisect", #CodePlex 21392
                    "_codecs_cn", #CodePlex 15507
                    "_codecs_hk", #CodePlex 15507
                    "_codecs_iso2022", #CodePlex 21394
                    "_codecs_jp", #CodePlex 15507
                    "_codecs_kr", #CodePlex 15507
                    "_codecs_tw", #CodePlex 15507
                    "_symtable", #IronPython incompat
                    "_types",  #Can't import this in CPython 2.6 either...
                    "audioop", #CodePlex 21400
                    "imageop", #Deprecated in CPy 2.6.  Removed in Cpy 3.0
                    "parser", #CodePlex 1347 - Won't fix
                    "strop", #CodePlex 21403
                    "zipimport", #CodePlex 391
                    "zlib", #CodePlex 2590
                  ]
                    
def dump_module(module):
    print('Module:', module)
    try:
        mod = __import__(module)
    except ImportError:
        print('  no module', module)
    else:
        dump_object(mod)
        
def dump_object(obj, depth = 1):
    for x in dir(obj):
        try:
            newobj = getattr(obj, x)
        except NotImplementedError:
            print('%s%-30s %r' % ('  ' * depth, x, '**NotImplemented**'))
        else:
            print('%s%-30s' % ('  ' * depth, x, ))
            #print '%s%-30s %r %r' % ('  ' * depth, x, type(newobj), type(obj))
    
            if (type(newobj) in knownTypes or 
               newobj in knownTypes or
               newobj is type):
                continue
            
            dump_object(newobj, depth + 1)

def dump_types():
    for knownType in knownTypes:
        print('Type:', knownType)
        dump_object(knownType)
    print('Type:', type)
    dump_object(type)

def dump_all():
    for module in BUILTIN_MODULES:
        dump_module(module)
        
    dump_types()

if __name__ == '__main__':
    if len(sys.argv) > 1:
        if sys.argv[1] == 'types_only':
            dump_types()
        else:
            dump_module(sys.argv[1])    
    else:
        dump_all()


