# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

import sys
import clr
import System

def SortTypesByNamespace(types):
    typesByNamespace = {}
    for t in types:
        # We ignore nested types. They will be loaded when the containing type is loaded
        # t.DeclaringType is failing on Silverlight (DDB 76340)
        t = clr.GetClrType(t)
        if t.DeclaringType:
            continue

        namespace = t.Namespace
        if not typesByNamespace.has_key(namespace):
            # This is the first type we are seeing from this namespace
            typesByNamespace[namespace] = []

        # t.Name is failing on Silverlight (DDB 76340)
        typeName = t.Name
        typesByNamespace[namespace].append(typeName)
        
    for namespace in typesByNamespace.keys():
        typesByNamespace[namespace].sort()

    return typesByNamespace

def CountTypes(typesByNamespace):
    count = 0

    for namespace in typesByNamespace.keys():
        count += len(typesByNamespace[namespace])

    return count

# Try loading an assembly in different ways

def LoadAssembly(assemName):
    try:
        return clr.LoadAssemblyByName(assemName)
    except IOError:
        return clr.LoadAssemblyByPartialName(assemName)
        
    return System.Reflection.Assembly.ReflectionOnlyLoadFrom(assemName)

# Get all public non-nested types in the assembly and list their names

def PrintTypeNames(cw, assemName):
    assem = LoadAssembly(assemName)
    types = assem.GetExportedTypes()
    typesByNamespace = SortTypesByNamespace(types)

    namespacesCount = len(typesByNamespace)
    sortedNamespaces = typesByNamespace.keys()
    sortedNamespaces.sort()

    cw.write('static IEnumerable<TypeName> Get_%s_TypeNames() {' % assemName.replace(".", ""))
    cw.write('    // %s' % assem.FullName)
    cw.write('    // Total number of types = %s' % CountTypes(typesByNamespace))
    cw.write('')
    cw.write('    string [] namespaces = new string[%i] {' % namespacesCount)
    for namespace in sortedNamespaces:
        cw.write('        "%s",' % namespace)
    cw.write('    };')
    cw.write('')
    cw.write('    string [][] types = new string[%i][] {' % namespacesCount)
    cw.write('')
    for namespace in sortedNamespaces:
        cw.write('        new string[%i] { // %s' % (len(typesByNamespace[namespace]), namespace))
        for t in typesByNamespace[namespace]:
            cw.write('            "%s",' % t)
        cw.write('        },')
        cw.write('')
    cw.write('    };')
    
    cw.write('    return GetTypeNames(namespaces, types);')
    cw.write('}')
    cw.write('')

# This is the set of assemblies used by a WinForms app. We can add more assemblies depending on the 
# priority of scenarios

default_assemblies = ["mscorlib", "System", "System.Drawing", "System.Windows.Forms", "System.Xml"]

def do_generate(cw):
    for assemName in default_assemblies:
        PrintTypeNames(cw, assemName)

def GetGeneratorList():
    if 'checkonly' in sys.argv:
        # skip checking - even if we're out of date (new types added) the code still does the right thing.
        # If we are out of date then we shouldn't fail tests.
        print "Skipping generate_AssemblyTypeNames in check only mode"
        return []
    
    if System.Environment.Version.Major == 4:
        return [("Well-known assembly type names (CLR 4)", do_generate)]
    else:
        return [("Well-known assembly type names", do_generate)]

def main():
    return generate(*GetGeneratorList())

if __name__ == "__main__":
    main()

