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
Delegate
* How the delegate is defined?
  - Different signatures (return type, argument list, params, 
    params dictionary, ref/out args)
  - Generic delegate type 

* Instantiation
  - With static/Instance (variable or literal) CLR method on value type/reference 
    type, 
    * When Type.Method has overloads
    * No match or more than one matching method found.
    * Type|Instance (dot) StaticMethod|InstanceMethod
  - Another delegate
    * Type compatible, or not
* Pri 2: it might be interesting to support some "light-weight coercion" here 
  where compatible delegates cast w/o new code gen.  It's a feature, but just 
  adding some minimal tests of coercible delegates would be interesting.  For 
  example a delegate object foo(subclass a) could have a otherclass bar(baseclass a) 
  cast to it - such conversions should be implicit.
  - Others: 
    * a type, indexer, field, operator, property, NULL
    * interface.Method, ...?
    * Language function/methods
  - Possible need for type conversion
    * Static type should not be in the signature
  - C# reference: http://msdn2.microsoft.com/en-us/library/aa691347(VS.71).aspx 
* Operations on delegate 
  - +, +=, -, -=
    * Add the same method multiple times
* Removing once - the last occurrence is the one actually removed
    * Removing the same method multiple times 
* Impossible removal is benign
    * Becomes empty invocation list after removing.
  - invocation
    * call expression, __call__, "Invoke"
  - other wild operations

Event 
* Where it is defined
  - Interface
* How it is defined
  - Add only, remove only, different accessibility / modifiers
  - Static or instance
  - Explicit event from interface
* Operations on Type|object (dot) (Static|Intance) Event
  - +=, -=, =, Add/Remove (?)
    * The choices of the right side: 
* delegate, method, others
* compatible, not compatible  
    * (python) __add__, __iadd__, __sub__, __isub__ direct calls
  - call operator, explicit "invoke"?
  - Other operations:
    * Use it as the right-hand operand somewhere else?
'''