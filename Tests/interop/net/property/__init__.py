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
* Where the property/indexer is defined
  - interface
* Property/Indexer type
  - Same as described in the "field"
* How the property is defined
  - Static / instance
  - Read-write, read-only, write-only properties
    * Derivation scenario: base / derived type each has different accessor.
  - As interface implementation (interface-type . identifier)
* How the indexer is defined
  - Static / instance
  - this[parameter-list], or VB style, name[parameter-list]
    * signature again: ref, params
    * overloads
  - As interface implementation (interface-type.this[xxx], interface-type.Foo[xxx])
* set/get via Type|object (dot) (Static|Intance) Property|Indexer
* Set value with different type to the property/indexer, try None
* How Indexer choose the overload?
  - Incorrect argument number, type
  - (python) Overloaded index properties in general: foo['xyz'], foo['xyz', 'def'], (foo[] ?)
* repeating from the derived class or its instance
* (python) __set__/__get__...
* Negative scenario: property as By-ref argument
* Able to call the actual underlying methods
* Other operations against them
  - Call, dot, 
'''