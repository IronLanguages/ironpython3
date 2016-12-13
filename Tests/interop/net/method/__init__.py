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
General
* Where it is defined
* How it is defined
  - Static and instance 
  - Explicit interface method implementation
  - Virtual, new, override

Passing arguments with different parameter lists
In C#, 
    public void M1(int x, int y) { }
    public void M2(int x, params int[] y)
    public void M3(int x, [DefaultParameterValue(5)] int y)
    public void M6([Optional] int x, [Optional] object y) 
    public void M7([Out] int x) 
    public void M7([ParamsDictionary] IattributesCollection dict) 
* Methods with  
  - one of such special parameter, 
  - two of such special parameters
  - one normal, the other special parameter. And different orders.
  - 3+ parameters with mixed kind
* Where DefaultParameterValue/Optional Attributes are not the last parameters.
* Different parameter types - string, int, Boolean, Object, self-defined 
  struct, class, variables.
* Check optional (missing) value
* Argument name are language special:
  - (python) True, def
* Params as an intermediate parameter (only via params setter?  Or also via 
  IL or direct attr?)
(Python)
* try with different parameter lists:
  - positional argument, 
  - keyword argument, 
  - *tuple style, 
  - *dictionary style, 
  - Mixed of them.
* Argument for the default value parameter (provide, not provide...)
* Negative scenarios
  - "params" could not be "keyword" argument?
  - Provide parameter more than once (such as positional, and keyword...)

Passing arguments for By-ref parameters
In C#,
    public void M1(int x, ref int y) { ... }
    public void M2(int x, out int y) { ... }
* Methods with
  - Similar aspect as the previous scenario
  - Consider mixing with default value/ params arguments
  - Ref, out parameters are not at last.
* Same for constructor.
(Python)
* call each of them with different parameter lists (similar to previous matrix)
* pay attention to: 
  - clr.Reference
  - Whether you need pass argument
  - What kind of argument
  - Mix&match is not supported
* Negative scenarios. 

Method Overloads
* Related to method signatures
* Related to argument values/types
* (python?) Related to return values (specifically conversions on returns, 
  nullable types, etc...)
* Methods with different parameters
* Non-generic / generic methods have the same name 
  - Activator.CreateInstance scenario
  - G<T>.M(int), G<T>.M(T)
  - G.M(int), G.M<T>(T)
* Static method and instance method have the same name
  - instance M(C), static M(thisType, C)
* Same name as explicit interface method

Operator Overloading
* all supported overloads (well-behavior)
  - http://msdn2.microsoft.com/en-us/library/2sk3x8a7(vs.71).aspx 
* unary/binary overloads (not defined as usual)
  - positive
    * instance method
    * static method where the first parameter is not the declaring type
    * has CodeContext as the first argument
  - negative
    * unary operator has  0, 2, 3 parameter
    * binary operator has 0, 1, 3 parameters
* Call the operator explicitly
* Provide one-side comparison operator overload, but access the reverse-side 
  operator
* Provide simple operator overload, but call in-place operator	
* Overloads 
* Value type which has operator overloads
* Something Python is unable to practice:
  - op_Assign, ...
* Something decorated with special attribute (GetItem, GetBoundMember...)
(Python)
* __xxx__ call style

User-defined conversions
* TODO

Implicit conversion operator
* Where it is applied: only passing as argument to method
* Where it is defined:
  - Value type, reference type, generics 
* How it is defined: 
  - Instance op_Implicit method
  - Overload scenario
* (python) Implicit vs. Explicit: when do we use what
* C#: http://msdn2.microsoft.com/en-us/library/aa691280(VS.71).aspx 
  - Implicit enumeration conversions 
  - Implicit reference conversions 
  - Boxing conversions 
    * Check the pass-in value/identity?
  - User-defined implicit conversions
    * Builtin number/char/string/bool/enum -> class/struct 
    * Class/struct -> class/struct 
    * Generic types (G<T>, G<int>, GOfInt, G<K, V>)
    * Conversion among base/derived types

Explicit conversion operator
* Not supported by the language yet, tracking as bug 314284
* (python) __int__, __long__, __float__, __complex__, __bool__
'''