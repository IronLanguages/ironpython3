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

from generate import generate

class TypeData(object):
    """
data structure used for tracking type data.  

name - the name of the     backing storage, a private static variable
type - the type name for the dynamic type that we're creating
typeType - the specific PythonType type that we're creating
entryName - the public entry name in the type cache.

Constructed as:

TypeData(type, name=None, typeType='PythonType', entryName=None)


by simply providing the type name you get a PythonType entry, name same as the the type, with
the private storage as a lower-case named version of the type.
    """
    __slots__ = ['name', 'type', 'typeType', 'entryName']
    def __init__(self, type, name=None, typeType='PythonType', entryName=None):
        self.type = type
        if name != None: self.name = name
        else: self.name = type.lower()

        if entryName != None: self.entryName = entryName
        else: self.entryName = self.type
        
        self.typeType = typeType
        
# list of all the types we auto-generate
data = [
    TypeData('Array'),
    TypeData('BuiltinFunction'),
    TypeData('PythonDictionary', entryName='Dict'),
    TypeData('FrozenSetCollection', entryName='FrozenSet'),
    TypeData('PythonFunction', entryName='Function'),
    TypeData('Builtin'),
    TypeData('Object', 'obj'),
    TypeData('SetCollection', entryName='Set'),
    TypeData('PythonType'),
    TypeData('String', 'str'),
    TypeData('PythonTuple'),
    TypeData('WeakReference'),
    TypeData('List'),
    TypeData('PythonFile'),
    TypeData('PythonModule', entryName='Module'),
    TypeData('Method'),
    TypeData('Enumerate'),
    TypeData('Int32', 'intType'),
    TypeData('Single', 'singleType'),
    TypeData('Double', 'doubleType'),
    TypeData('BigInteger'),
    TypeData('Complex'),
    TypeData('Super'),
    TypeData('OldClass'),
    TypeData('OldInstance'),
    TypeData('DynamicNull', 'nullType', entryName='Null'),
    TypeData('Boolean', 'boolType'),
	TypeData('PythonExceptions.BaseException', name='baseException', entryName = 'BaseException'),
]

# groups all of our type data by type, and outputs
# a variable for each one.
def gen_typecache_storage(cw):
    types = {}
    for x in data:
        if(types.has_key(x.typeType)):
            types[x.typeType].append(x)
        else:
            types[x.typeType] = [x]
    for type in types:        
        for a_type in types[type]:
            cw.write('private static %s %s;' % (type, a_type.name))
    
# outputs the public getters for each cached type    
def gen_typecache(cw):
    for x in data:
        cw.enter_block("public static %s %s" % (x.typeType, x.entryName))
        cw.enter_block("get")
        
        if x.typeType != 'PythonType': cast = '(%s)' % x.typeType
        else: cast = ""
        
        cw.write("if (%s == null) %s = %sDynamicHelpers.GetPythonTypeFromType(typeof(%s));" % (x.name, x.name, cast, x.type))
        cw.write("return %s;" % x.name)
        cw.exit_block()
        cw.exit_block()
        cw.write("")

def main():
    return generate(
        ("TypeCache Storage", gen_typecache_storage),
        ("TypeCache Entries", gen_typecache),
    )

if __name__ == "__main__":
    main()
