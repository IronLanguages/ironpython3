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
OVERVIEW

The goal of this test package is to provide a framework to exhaustively 
verify IronPython's .NET interop capabilities.  From a high-level, this means
we will be checking that:
- IronPython can utilize objects and types that have been implemented in some
  other CLR-supported language.  The goal here is to test all language features
  of IL
- other CLR-supported languages can utilize IronPython objects and types via
  the DLR hosting APIs and the new 'dynamic' keyword in C-sharp.  Please note 
  that the goal of this testing is to find bugs in IronPython's IDynamicObject
  implementation(s); not other CLR-supported languages' utilization of IDynamic
  objects.  A test plan specifically for this scenario has been created by the
  DLR - see the "Testing Cross Language Operations on the DLR" document.

While there are other key testing aspects, at the highest level utilization of 
.NET types from IronPython can be broken up into three key areas: 
- getting .NET types
- creating instances of .NET types and performing operations on them
- subclassing/implementing .NET classes/interfaces from Python


EDITORS NOTE
Throughout this document you will find references to documentation in
other packages similar to "See documentation for interop.net.field".  This simply
means that relative to this package (i.e., interop.net), you should follow
the 'field (package)' link at the bottom of this page.

------------------------------------------------------------------------------
GETTING THE .NET TYPE

KEY POINTS
* Can you get a type and how?
* What should happen when a naming conflict occurs? 
  - Merge or simply replace
  - Possible conflicts: 
    * .NET namespace
    * Type name
    * Generics
    * IronPython module
* When is the type is "visible"?  When should it be "invisible"?

INTERESTING TYPES
See documentation for interop.net.type

-------------------------------------------------------------------------------
UTILIZATION OF .NET OBJECTS

TYPES
See documentation for interop.net.type

METHODS
See documentation for interop.net.method

FIELDS
See documentation for interop.net.field

PROPERTIES/INDEXERS
See documentation for interop.net.property

EVENTS/DELEGATES
See documentation for interop.net.event

-------------------------------------------------------------------------------
DERIVING FROM .NET TYPES
See documentation for interop.net.derivation.

-------------------------------------------------------------------------------
PYTHON CHARACTERISTICS OF .NET OBJECTS
* standard attributes (i.e., __init__, __doc__, etc)
* help and documentation strings
* dir(xyz) vs. getattr(xyz, 'abc') vs vs xyz.abc - all three should have 
  equivalent results
* setattr(xyz, 'abc', foo) vs xyz.abc = foo - should have equivalent results
* look at interop.com.dlrcomlib.pytraits for more ideas

-------------------------------------------------------------------------------
PERFORMANCE
To be revisited.
* simple method invocations
* importing from .NET namespaces
* loading assemblies

-------------------------------------------------------------------------------
STRESS
* run .NET interop tests with gcstress environment variables set
* run .NET interop tests with Managed Debugging Assistants turned on
* run .NET interop tests with IronPython/DLR binaries installed into the
  global assembly cache
* check for memory leaks
* huge number of method parameters on a .NET method

-------------------------------------------------------------------------------
LOCALIZATION/GLOBALIZATION
To be revisited.
* tests should be run on a non-ENU operating system

-------------------------------------------------------------------------------
DEBUGGING EXPERIENCE
To be revisited.  No special requirements?

-------------------------------------------------------------------------------
COMPATIBILITY
* does the latest version of IronPython pass the previous version's .NET 
  interop tests?
* is IronPython compatible with Python for .NET (http://pythonnet.sourceforge.net/)?
* is .NET interop the same under x86 and x64 CLR?
* is .NET interop the same under different operating systems?
* is .NET interop the same under interactive sessions versus Python modules?

-------------------------------------------------------------------------------
SECURITY
To be revisited.  As IronPython is quite simply just another .NET program
running under the Common Language Runtime, CLR threat models should apply.

-------------------------------------------------------------------------------
CODE COVERAGE
Block coverage of the .NET binder should be maintained at 80% or higher.  As of
May 2009, the .NET binder resides in the IronPython.Runtime.Binding namespace
and we're sitting at 88.8% block coverage.

-------------------------------------------------------------------------------
ACCESSIBILITY
To be revisited.

-------------------------------------------------------------------------------
MISC. INTERESTING CASES
* Are certain CLR Exceptions interchangable with Python builtin exceptions?
* How does IronPython treats the following types:
  - IList, List, ArrayList
  - Hashtable, Dictionary`2, IDictionary
  - IEnumerable, IEnumerator
  - IComparable
* Operations on .NET namespaces
  - the top level
  - the nested level
  - the bottom level
* Operations on nested classes
* Operations on the Assembly/AssemblyBuilder type and instances

Special DLR Types
* Extensible<T> - Python currently provides implementations for a bunch of 
  members on the Extensible type (for some types) but they shouldn't be 
  necessary.  A raw Extensible<Xyz> should be have the same as an Xyz 

-------------------------------------------------------------------------------
EXISTING TESTS WHICH NEED TO BE FOLDED INTO interop.net:
* test_cliclass.py
* test_delegate.py
* test_inheritance.py
* test_methodbinder1.py
* test_methodbinder2.py
* test_methoddispatch.py
* test_static.py

-------------------------------------------------------------------------------
AREAS TARGETED FOR MORE TEST COVERAGE (February 2009)
* .NET classes created using Visual Basic which use IL features that cannot 
  presently be hit from C#-based assemblies
  - optional parameters
* running __builtin__ methods (isinstance, issubclass, len, help, dir, etc)
  against .NET types/objects
* Python methods attached to .NET types:
  - Dictionary().next()
  - Indexing on System.Xml.XmlDocument().SelectSingleNode(...).Attributes["something"] (DefaultMemberAttribute)
  - __cmp__, __lt__, __gt__, etc when one operand is a native Python type
* Event handlers implemented in Python:
  - removing
  - re-attaching
  - memory leaks
  - anything callable in Python should be capable of being used as a delegate
* Passing various Python objects to C# functions:
  - new-style class dictionaries used as IDictionary
  - setting integer properties with Python longs
  - objects implementing special methods such __int__
  - list objects should not be usable for IEnumerators
* Public vs protected methods with ref/out parameters
* .NET 4.0 features

-------------------------------------------------------------------------------
EXECUTION PLAN
To be revisited when more test resources become available.
'''