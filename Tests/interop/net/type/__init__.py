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
-------------------------------------------------------------------------------
ACCESSING TYPES

"Simple" type
* How can you get different types: value type / reference type, interface, 
  delegate,  enum, nullable

Type location
* How to get types living in mscorlib, system.dll (pre-loaded), and other 
  user assemblies? From GAC?

Nested type
* How can you get the nested type?

Namespace
* How can you get types decorated with namespace, or without namespace?
* Where namespace has the same name as type, or member?

Accessibility 
* The impact of accessibility (public / assembly / family / private / ...)
* The visibility of namespace which does not have public types

Array type
* How can you get array? 
  - One dimensional, Multi-dimensional, jagged, Non-zero lower bound

Generic type 
* How can you get the open generic type, closed generic type?
  - When one assembly contains non-generic type G, and generic type G`1
  - When one assembly contains generic type G`2, and G`3

Generic type with constraint 
* How can you get the closed generic type? 
  - What if the generic arguments are not valid?

Generic parameter
* Not applicable

Type which causes TypeLoadException
* Can you hold other types even if one type in the assembly caused 
  TypeLoadException?

Type from Reflection.Emit
* How can you hold types created by reflection.emit (not saved to disk yet)?

Type forwarder
* How can you hold the correct forwarded type? Both positive and negative 
  scenarios.

Type/Namespace having interesting names
* language keywords as type name/namespace
* weird chars, space, inside
* number/digits

Type Group
Able to get the generic type, and non-generic type

COM Type
* To be covered elsewhere

------------------------------------------------------------------------------
UTILIZATION OF TYPES

Object creation 
* Against special types:
  - static class, abstract class, interface, enum
  - Generic type 
    * with constraints
  - delegate to be covered in events
* Constructor overloads: choose which one
  - Ref/out modifiers, params, params dictionary (see more detail in the 
    "On method" section)
  - Static constructor
  - (python) __new__/__init__ overload resolution on Python built-ins.
  - Bugs:
    * Passing object[] to ctor which asks for object[], 
* (python) call different argument list style
  - And __call__/__new__?
* (python) keyword argument to set field/property along the creation 
  - Against value type/reference type
  - Constructors with 0, 1, 2 parameters, params, params dictionary([ParamDict])
  - Set bad field/property
    * literal, read-only field
    * read-only property
  - Field/property with such keyword name does not exist.
    * static field/property name as keyword
  - Name collision: argument, field/property
  - Keyword argument for property/field comes before the normal keyword 
    argument for ctor argument
    * how it is related to *, ** args
  - ctor overloads:
    * same argument but declared with different types
    * argument name/position: (C arg1, D arg2), (C arg2, D arg1)

Default member (Default Indexer)
* DefaultMemberAttribute can be defined at struct, class, interface
  - Generic type (where member could be of type T)	
* Member could be field, property, indexer, method, or event
  - Single/multiple default members (same/different MemberType)
* Static default member
  - Vb does not allow
* Accessing from the derived type, or current type
* How to different it from the ctor call?
* The reality:
  - Only default indexer member will likely work. 
  - Make sure no bad thing could happen with other default members
References:
* http://msdn2.microsoft.com/en-us/library/system.type.getdefaultmembers.aspx
* http://msdn2.microsoft.com/en-us/library/system.reflection.defaultmemberattribute.aspx 

CLR bjects from the .NET world
* Return values: null, clr number, enum, self-defined value type, reference 
  type, nullable type, delegate, array 
  - Conversions
* Interesting one - whose type is private type: Define a public/private 
  interface or base class which exercises every operation allowed 
  in C# (i.e., private/public/static members, properties, events, etc).  
  Provide a private implementation of this in C#.  Instantiate the private 
  class from the dynamic language and exercise every method/member the 
  interface or base class defines
  - What about a combination of base classes and interfaces which might clash?
  - Calling members on it

-------------------------------------------------------------------------------
SPECIAL CASES
* Visibility: ShowCLS flag 
* -X:PrivateBinding: Type, Nested Type, Method, Field, Property, Event, Indexer
  - Impact on the derivation scenario
'''