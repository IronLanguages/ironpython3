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
* Where the field is defined
  - value type (enum), reference type, 
  - generic value/reference type (bound with value/reference type)
* Field type
  - value type: built-in number types, enum, user defined struct
  - reference type, interface?
  - Nullable<T>
  - array of something
  - generic type parameter and its' constructed type
* Field modifier
  - const (literal), 
  - readonly 
  - static / instance
* set/get via Type|object (dot) (Static|Intance) Field
* set value with something with different type, or, none
  - convert succeed, or fail
* repeating from the derived class or its instance
* (python) __set__/__get__/__delete__/__str__/__repr__
* (python) Type.__dict__['Field'], and possible __set__/__get__, GetValue/SetValue
* Use field as by-ref arguments
* Other operations against field
  - Augment: +=, <<=
  - Continuous dot operator
  - Call operator: ()
'''