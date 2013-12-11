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

# types derived from built-in types

class myint(int): pass
class mylong(long): pass
class myfloat(float): pass
class mycomplex(complex): pass

class mystr(str): pass

class mytuple(tuple): pass
class mylist(list): pass
class mydict(dict): pass

class myset(set): pass
class myfrozenset(frozenset): pass

import sys

if not sys.platform == 'silverlight':
  class myfile(file): pass

# to define type constant

def _func(): pass
class _class:
    def method(self): pass
    
class types:
    functionType        = type(_func)
    instancemethodType  = type(_class().method)
    classType           = type(_class)
    lambdaType          = type(lambda : 1)

if sys.platform in ['cli', 'silverlight']:
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
    array_long      = System.Array[long]
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
        print "%-25s : %r" % (x, getattr(types, x))
