# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# types derived from built-in types

class myint(int): pass
class mylong(int): pass
class myfloat(float): pass
class mycomplex(complex): pass

class mystr(str): pass

class mytuple(tuple): pass
class mylist(list): pass
class mydict(dict): pass

class myset(set): pass
class myfrozenset(frozenset): pass

from .test_env import is_cli

def _func(): pass
class _class:
    def method(self): pass
    
class types:
    functionType        = type(_func)
    instancemethodType  = type(_class().method)
    classType           = type(_class)
    lambdaType          = type(lambda : 1)

if is_cli:
    object_attrs_before_clr_import = dir(object())
    import System
    object_attrs_after_clr_import = dir(object())
    clr_specific_attrs = [attr for attr in object_attrs_after_clr_import
                          if attr not in object_attrs_before_clr_import]
    
    def remove_clr_specific_attrs(attr_list):
        return [attr for attr in attr_list if attr not in clr_specific_attrs]
    
    # CLR array shortcut
    array_cli       = System.Array
    array_int       = System.Array[int]
    array_object    = System.Array[object]
    array_byte      = System.Array[System.Byte]
    
    # sample numberes?
    clr_signed_types = (System.SByte, System.Int16, System.Int32, System.Int64, System.Decimal, System.Single, System.Double)
    clr_unsigned_types = (System.Byte, System.UInt16, System.UInt32, System.UInt64)
    clr_all_types = clr_signed_types + clr_unsigned_types
    
    clr_all_plus1  = [t.Parse("1") for t in clr_all_types]
    clr_all_minus1 = [t.Parse("-1") for t in clr_signed_types]
    clr_all_max    = [t.MaxValue for t in clr_all_types]
    clr_all_min    = [t.MinValue for t in clr_all_types]
    
    clr_numbers = {}
    for t in clr_all_types:
        clr_numbers[t.__name__ + "Max"] = t.MaxValue
        clr_numbers[t.__name__ + "Min"] = t.MinValue
        clr_numbers[t.__name__ + "PlusOne"] = t.Parse("1")
        if not t in clr_unsigned_types:
            clr_numbers[t.__name__ + "MinusOne"] = t.Parse("-1")
        
if __name__ == '__main__':
    # for eye check
    for x in dir(types):
        print("%-25s : %r" % (x, getattr(types, x)))
