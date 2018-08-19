# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate
import operator

nullValue = 0

fieldList = [
    ('__neg__', 'OperatorNegate'),
    ('__invert__', 'OperatorOnesComplement'),

    ('__dict__', 'Dict'),
    ('__module__', 'Module'),
    ('__getattribute__', 'GetAttribute'),
    ('__bases__', 'Bases'),
    ('__subclasses__', 'Subclasses'),
    ('__name__', 'Name'),
    ('__class__', 'Class'),

    ('__builtins__', 'Builtins'),
    
    ('__getattr__', 'GetBoundAttr'),
    ('__setattr__', 'SetAttr'),
    ('__delattr__', 'DelAttr'),
    
    ('__getitem__', 'GetItem'),
    ('__setitem__', 'SetItem'),
    ('__delitem__', 'DelItem'),
    
    ('__init__', 'Init'),
    ('__new__', 'NewInst'),    
    ('__del__', 'Unassign'),
    
    ('__str__', 'String'),
    ('__repr__', 'Repr'),
    
    ('__contains__', 'Contains'),
    ('__len__', 'Length'),
    ('__reversed__', 'Reversed'),
    ('__iter__', 'Iterator'),
    ('__next__', 'Next'),    

    ('__weakref__', 'WeakRef'),
    ('__file__', 'File'),
    ('__import__', 'Import'),
    ('__doc__', 'Doc'),
    ('__call__', 'Call'),
    
    ('__abs__', 'AbsoluteValue'),
    ('__coerce__', 'Coerce'),
    ('__int__', 'ConvertToInt'),
    ('__float__', 'ConvertToFloat'),
    ('__long__', 'ConvertToLong'),
    ('__complex__', 'ConvertToComplex'),
    ('__hex__', 'ConvertToHex'),
    ('__oct__', 'ConvertToOctal'),
    ('__reduce__', 'Reduce'),
    ('__reduce_ex__', 'ReduceExtended'),

    ('__nonzero__', 'NonZero'),
    ('__pos__', 'Positive'),
    
    ('__hash__', 'Hash'),
    ('__cmp__', 'Cmp'),
    ('__divmod__', 'DivMod'),
    ('__rdivmod__', 'ReverseDivMod'),
    
    ('__path__', 'Path'),
    
    ('__get__', 'GetDescriptor'),
    ('__set__', 'SetDescriptor'),
    ('__delete__', 'DeleteDescriptor'),
    ('__all__', 'All'),
    

    ('clsException', 'ClrExceptionKey'),
    ('keys', 'Keys'),
    ('args', 'Arguments'),
    ('write', 'ConsoleWrite'),
    ('readline', 'ConsoleReadLine'),
    ('msg', 'ExceptionMessage'),
    ('filename', 'ExceptionFilename'),
    ('lineno', 'ExceptionLineNumber'),
    ('offset', 'ExceptionOffset'),
    ('text', 'Text'),
    ('softspace', 'Softspace'),
    ('next', 'GeneratorNext'),
    ('setdefaultencoding', 'SetDefaultEncoding'),
    ('exitfunc', 'SysExitFunc'),
    ('None', 'None'),
    
    ('__metaclass__', 'MetaClass'),
    ('__mro__', 'MethodResolutionOrder'),
    ('__getslice__', 'GetSlice'),
    ('__setslice__', 'SetSlice'),
    ('__delslice__', 'DeleteSlice'),
    ('__future__', 'Future'),
    ('division', 'Division'),
    ('nested_scopes', 'NestedScopes'),
    ('generators', 'Generators'),
    ('*', 'Star'),
    ('**', 'StarStar'),
    ('locals', 'Locals'),
    ('vars', 'Vars'),
    ('dir', 'Dir'),
    ('eval', 'Eval'),
    ('execfile', 'ExecFile'),
    ('_', 'Underscore'),
    ('__gen_$_parm__', 'GeneratorParmName'),
    ('$env', 'EnvironmentParmName'),
    ('iter', 'Iter'),
    ('__slots__', 'Slots'),

    ('__getinitargs__', 'GetInitArgs'),
    ('__getnewargs__', 'GetNewArgs'),
    ('__getstate__', 'GetState'),
    ('__setstate__', 'SetState'),
    ('__newobj__', 'BuildNewObject'),
    ('_reconstructor', 'Reconstructor'),
    ('iteritems', 'IterItems'),
    ('real', 'RealPart'),
    ('imag', 'ImaginaryPart'),
    ('__missing__', 'Missing'),
    ('with_statement','WithStmt'),
    ('append', 'Append'),
    ('extend', 'Extend'),
    ('update', 'Update'),
    ('this', 'ThisArgument'),
    ('__index__', 'Index'),
    ('__trunc__', 'Truncate'),
    ('absolute_import', 'AbsoluteImport'),
    ('print_function', 'PrintFunction'),
    ('unicode_literals', 'UnicodeLiterals'),
    ('__package__', 'Package'),
    ]

def generate_symbols(cw):
    for x in fieldList:
        cw.writeline("private static SymbolId _%s;" % (x[1],))
    
    for x in fieldList:
        cw.writeline("///<summary>Symbol for '%s'</summary> " % x[0])
        cw.enter_block("public static SymbolId %s" % x[1])
        cw.enter_block('get')
        cw.writeline("if (_%s == SymbolId.Empty) _%s = MakeSymbolId(\"%s\");" % (x[1], x[1], x[0]))
        cw.writeline("return _%s;" % (x[1],))
        cw.exit_block()
        cw.exit_block()

def main():
    return generate(
        ("Symbols - Other Symbols", generate_symbols),
    )

if __name__ == "__main__":
    main()
