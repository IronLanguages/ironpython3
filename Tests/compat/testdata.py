# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
is_cli = sys.implementation.name == "ironpython"

list_int        = [-2, 0, 2, 5]
list_float      = [-10.5, 0.0, 2.0, 2.1, 2.9, 50.001]
list_long       = [-2, 0, 5, 123456789]
list_bool       = [True, False]

list_complex    = [2+0j, -1-3.5j, 4+0j, 4.0+0j, 3-2j, 0j]
list_str        = ['hello', 'hello ', 'world', ' world', 'h', 'a', 'b', 'c', ' ', '', 'hello']

class myint(int) : pass
class myfloat(float): pass
class mylong(long): pass
class mystr(str): pass
class mycomplex(complex): pass

list_myint      = [myint(x)     for x in [-3, 2, 0, 400]]
list_myfloat    = [myfloat(x)   for x in [2.1, -10.5, -0.0, 50.001, 2]]
list_mylong     = [mylong(x)    for x in [2, 5, -22, 0]]
list_mystr      = [mystr(x)     for x in ["world", "b", "bc", '']]
list_mycomplex  = [mycomplex(x) for x in [2, 0, 2+0j, 3-2j]] 

list_set        = [ set([1, 2]), set(range(5)), set([3, 5]) ]
list_frozenset  = [ frozenset([1, 2]), frozenset(list(range(5))), frozenset([3, 5]) ]
list_dict       = [ {}, {5:2}, {1:2, 5:2}, {4:3, 3:4}, {1:2, 5:2, 7:9} ]

def merge_lists(*args) :
    collection = []
    for x in args:
        collection.extend(x)
    return collection 

def get_Int64_Byte():
    list_byte       = [ 2, 3 ]
    list_Int64      = [ 5 ]
    
    if is_cli: 
        import System
        list_byte       = [ System.Byte.Parse(str(x))       for x in list_byte ]
        list_Int64      = [ System.Int64.Parse(str(x))      for x in list_Int64 ]
        
    return merge_lists(
        list_byte, 
        list_Int64,
        )

def get_clrnumbers():
    list_ushort     = [ 2, 3 ]
    list_short      = [ 2, -3]
    list_byte       = [ 2, 3 ]
    list_sbyte      = [ -2, 3]
    list_uint       = [ 2, 3]
    list_ulong      = [ 2, 3]
    list_Int64      = [ 5 ]
    list_decimal    = list_float[:]

    if is_cli: 
        import System
        list_ushort     = [ System.UInt16.Parse(str(x))     for x in list_ushort ]
        list_short      = [ System.Int16.Parse(str(x))      for x in list_short ]
        list_byte       = [ System.Byte.Parse(str(x))       for x in list_byte ]
        list_sbyte      = [ System.SByte.Parse(str(x))      for x in list_sbyte ]
        list_uint       = [ System.UInt32.Parse(str(x))     for x in list_uint ]
        list_ulong      = [ System.UInt64.Parse(str(x))     for x in list_ulong ]
        list_Int64      = [ System.Int64.Parse(str(x))      for x in list_Int64 ]
        list_decimal    = [ System.Decimal.Parse(str(x))    for x in list_decimal ]
        
    return merge_lists(
        list_ushort, 
        list_short, 
        list_byte, 
        list_sbyte, 
        list_uint, 
        list_ulong,
        list_Int64,
        list_decimal,
        )
    
def get_enums(): 
    list_enum_byte      = [0]
    list_enum_sbyte     = [1]
    list_enum_ushort    = [0]
    list_enum_short     = [1]
    list_enum_uint      = [0]
    list_enum_int       = [1]
    list_enum_ulong     = [0]
    list_enum_long      = [1]

    if is_cli:     
        import clr
        import os
        import sys
        clr.AddReferenceToFileAndPath(os.path.join(sys.exec_prefix, "IronPythonTest.dll"))
        from IronPythonTest import DaysByte, DaysInt, DaysLong, DaysSByte, DaysShort, DaysUInt, DaysULong, DaysUShort
        list_enum_byte      = [DaysByte.None]
        list_enum_sbyte     = [DaysSByte.Mon]
        list_enum_ushort    = [DaysUShort.None]
        list_enum_short     = [DaysShort.Mon]
        list_enum_uint      = [DaysUInt.None]
        list_enum_int       = [DaysInt.Mon]
        list_enum_ulong     = [DaysULong.None]
        list_enum_long      = [DaysLong.Mon]
    
    return merge_lists(
        list_enum_byte,
        list_enum_sbyte,
        list_enum_ushort,
        list_enum_short,
        list_enum_uint,
        list_enum_int,
        list_enum_ulong,
        list_enum_long,
        )    

long_string  ="abcdefghijklmnopqrstuvwxyz"

def get_comparable_number_lists_as_list():
    return [
                list_int, 
                list_float,
                list_long,
                list_bool,
                
                list_myint,
                list_myfloat,
                list_mylong,
           ]

def get_all_number_lists_as_list():
    return [
                list_int, 
                list_float,
                list_long,
                list_bool,
                
                list_myint,
                list_myfloat,
                list_mylong,
                
                list_complex,
           ]
           
def get_comparable_numbers_as_list():
    collection = []
    for x in get_comparable_number_lists_as_list():
        collection.extend(x)
    return collection 

def get_all_numbers_as_list():
    collection = []
    for x in get_all_number_lists_as_list():
        collection.extend(x)
    return collection 


