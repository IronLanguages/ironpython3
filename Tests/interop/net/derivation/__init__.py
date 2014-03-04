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

'''
* Derive from:
  - struct, 
  - delegate type, 
  - Interface (which can declare property, indexer, event, method), 
    * PythonTypeA < interfaceTwo < interfaceOne
  - Reference types 
    * sealed ref type
    * abstract
  - generic type: open/closed
  - System.Delegate, System.Enum, System.ValueType, System.Array
  - System.MarshalByRefObject
  - Literal (int, str) 
  - Extensbile<T>
  - non-sense object (expression/simply variable)
  - derive from multiple types
    * pure clr types: Interesting scenario example, such 
      as "class X (System.ValueType, IOneInterface): pass"
    * mixed clr type and language type
  - Derive again from the same .net type: any visible impact?
  - deep derivation chain: 
    * language type A derives from .NET type B, another language type C 
      derives from A
  - deriving from something throw, and derive again (bug)
* Body
  - (python) __new__, __init__
    * Deal with base type's ctor overload
  - Derive with complete or Incomplete implementation
    * Each language might need support defining event/property/indexer
    * Where there is no need to implement anything
    * Completely implement the direct base type/interface, but not the 
      grandparent.
    * (python) Incomplete implementations should only throw an error at runtime 
      when the functionality is used
  - Support explicit interface implementation? 
    * Possible duplicate method names...
  - Try to access all private/protected/public members (including operators, 
    op_implicit...) from the base (clr or lang) type
  - (python) Defining field 
    * "new" field 
    * existing name
  - (python) Defining method 
    * "new" method
    * overriding the existing virtual/override method
* able to call base types' method
* compatible and incompatible method signatures
  - Params <=> *arg
  - Ref arg
* Such methods to be called by 
  - the base type, 
  - other method in the current type
  - (python) __slots__, __metaclass__
  - __slots__ and overriding virtual members
  - Supported operators we treated like python... (big)
* Inspect the new type
* Consume it after creating such object
  - Try to access private/protected/public members from the base type
    * (python) protected members should be available on derived types, but not 
      on non-derived types
x = object()
x.MemberwiseClone() # error
class foo(object): pass
foo().MemberwiseClone() # works
  - Try to access the newly defined members
  - pass it back to .NET world, where someone is calling
    * the new interface method
    * the new virtual method
* Versioning
  - Where the type/interface comes with new member
  - Mostly related to derivation
'''