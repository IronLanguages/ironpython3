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
import System
import clr

import functools
import builtins

pythonExcs = ['ImportError', 'RuntimeError', 'UnicodeTranslateError', 'PendingDeprecationWarning',
              'LookupError', 'OSError', 'DeprecationWarning', 'UnicodeError', 'FloatingPointError', 'ReferenceError',
              'FutureWarning', 'AssertionError', 'RuntimeWarning', 'ImportWarning', 'UserWarning', 'SyntaxWarning', 
              'UnicodeWarning', 'StopIteration', 'BytesWarning', 'BufferError', 'ResourceWarning', 'FileExistsError',
              'BlockingIOError', 'NotADirectoryError']

class ExceptionInfo(object):
    def __init__(self, name, clrException, args, fields, subclasses, baseMapping = None):
        self.name = name
        self.clrException = clrException
        self.args = args
        self.fields = fields
        self.subclasses = subclasses
        self.parent = None
        self.baseMapping = baseMapping
        for child in subclasses:
            child.parent = self
    
    @property
    def ConcreteParent(self):
        while not self.parent.fields:
            self = self.parent
            if self.parent == None: return exceptionHierarchy
        
        return self.parent
        
    @property
    def PythonType(self):
        if not self.parent:
            return 'DynamicHelpers.GetPythonTypeFromType(typeof(%s))' % self.name
        else:
            return self.name

    @property 
    def ClrType(self):
        if not self.parent:
            return 'BaseException'
        elif self.fields:
            return '_' + self.name
        else:
            return self.name
    
    @property
    def ExceptionMappingName(self):
        if self.baseMapping:
            return self.baseMapping[self.baseMapping.rfind('.')+1:]
        return self.DotNetExceptionName

    @property
    def DotNetExceptionName(self):
        return self.clrException[self.clrException.rfind('.')+1:]
    
    @property
    def InternalPythonType(self):
        if not self.parent:
            return 'PythonExceptions._' + self.name
        else:
            return 'PythonExceptions.' + self.name

    def MakeNewException(self):
        if self.fields or self.name == 'BaseException':        
            return 'new PythonExceptions._%s()' % (self.name)
        else:
            return 'new PythonExceptions.%s(PythonExceptions.%s)' % (self.ConcreteParent.ClrType, self.name)

# format is name, args, (fields, ...), (subclasses, ...)
exceptionHierarchy = ExceptionInfo('BaseException', 'IronPython.Runtime.Exceptions.PythonException', None, None, (
            ExceptionInfo('SystemExit', 'IronPython.Runtime.Exceptions.SystemExitException', None, ('code',), ()),
            ExceptionInfo('KeyboardInterrupt', 'Microsoft.Scripting.KeyboardInterruptException', None, (), ()),
            ExceptionInfo('GeneratorExit', 'IronPython.Runtime.Exceptions.GeneratorExitException', None, (), ()),
            ExceptionInfo('Exception', 'IronPython.Runtime.Exceptions.PythonException', None, (), (
                ExceptionInfo('StopIteration', 'IronPython.Runtime.Exceptions.StopIterationException', None, ('value',), ()),
                ExceptionInfo('ArithmeticError', 'System.ArithmeticException', None, (), (
                    ExceptionInfo('FloatingPointError', 'IronPython.Runtime.Exceptions.FloatingPointException', None, (), ()),
                    ExceptionInfo('OverflowError', 'System.OverflowException', None, (), ()),
                    ExceptionInfo('ZeroDivisionError', 'System.DivideByZeroException', None, (), ()),
                    ),
                ),
                ExceptionInfo('AssertionError', 'IronPython.Runtime.Exceptions.AssertionException', None, (), ()),
                ExceptionInfo('AttributeError', 'IronPython.Runtime.Exceptions.AttributeErrorException', None, (), (), baseMapping = 'System.MissingMemberException'),
                ExceptionInfo('BufferError', 'IronPython.Runtime.Exceptions.BufferException', None, (), ()),
                ExceptionInfo('OSError', 'IronPython.Runtime.Exceptions.OSException', None, ('errno', 'strerror', 'filename', 'winerror', 'filename2'), (
                    ExceptionInfo('BlockingIOError', 'IronPython.Runtime.Exceptions.BlockingIOException', None, ('characters_written', ), ()),
                    ExceptionInfo('FileExistsError', 'IronPython.Runtime.Exceptions.FileExistsException', None, (), ()),
                    ExceptionInfo('FileNotFoundError', 'System.IO.FileNotFoundException', None, (), ()),
                    ExceptionInfo('PermissionError', 'System.UnauthorizedAccessException', None, (), ()),
                    ExceptionInfo('NotADirectoryError', 'IronPython.Runtime.Exceptions.NotADirectoryException', None, (), ()),
                    ),
                ),
                ExceptionInfo('EOFError', 'System.IO.EndOfStreamException', None, (), ()),
                ExceptionInfo('ImportError', 'IronPython.Runtime.Exceptions.ImportException', None, (), ()),
                ExceptionInfo('LookupError', 'IronPython.Runtime.Exceptions.LookupException', None, (), (
                    ExceptionInfo('IndexError', 'System.IndexOutOfRangeException', None, (), ()),
                    ExceptionInfo('KeyError', 'System.Collections.Generic.KeyNotFoundException', None, (), ()),
                    ),
                ),
                ExceptionInfo('MemoryError', 'System.OutOfMemoryException', None, (), ()),
                ExceptionInfo('NameError', 'IronPython.Runtime.UnboundNameException', None, (), (
                    ExceptionInfo('UnboundLocalError', 'IronPython.Runtime.UnboundLocalException', None, (), ()),
                    ),
                ),
                ExceptionInfo('ReferenceError', 'IronPython.Runtime.Exceptions.ReferenceException', None, (), ()),
                ExceptionInfo('RuntimeError', 'IronPython.Runtime.Exceptions.RuntimeException', None, (), (
                    ExceptionInfo('NotImplementedError', 'System.NotImplementedException', None, (), ()),
                    ),
                ),
                ExceptionInfo('SyntaxError', 'Microsoft.Scripting.SyntaxErrorException', None, ('text', 'print_file_and_line', 'filename', 'lineno', 'offset', 'msg'), (
                    ExceptionInfo('IndentationError', 'IronPython.Runtime.Exceptions.IndentationException', None, (), (
                         ExceptionInfo('TabError', 'IronPython.Runtime.Exceptions.TabException', None, (), ()),
                             ),
                         ),                                    
                    ),                                
                ),
                ExceptionInfo('SystemError', 'System.SystemException', None, (), ()),
                ExceptionInfo('TypeError', 'IronPython.Runtime.Exceptions.TypeErrorException', None, (), (), baseMapping = 'Microsoft.Scripting.ArgumentTypeException'),
                ExceptionInfo('ValueError', 'IronPython.Runtime.Exceptions.ValueErrorException', None, (), (
                    ExceptionInfo('UnicodeError', 'IronPython.Runtime.Exceptions.UnicodeException', None, (), (
                        ExceptionInfo('UnicodeDecodeError', 'System.Text.DecoderFallbackException', ('encoding', 'object', 'start', 'end', 'reason'), ('start', 'reason', 'object', 'end', 'encoding'), ()),
                        ExceptionInfo('UnicodeEncodeError', 'System.Text.EncoderFallbackException', ('encoding', 'object', 'start', 'end', 'reason'), ('start', 'reason', 'object', 'end', 'encoding'), ()),
                        ExceptionInfo('UnicodeTranslateError', 'IronPython.Runtime.Exceptions.UnicodeTranslateException', None, ('start', 'reason', 'object', 'end', 'encoding'), ()),
                            ),
                        ),
                    ),
                    baseMapping = 'System.ArgumentException'
                ),
                ExceptionInfo('Warning', 'System.ComponentModel.WarningException', None, (), (
                        ExceptionInfo('DeprecationWarning', 'IronPython.Runtime.Exceptions.DeprecationWarningException', None, (), ()),
                        ExceptionInfo('PendingDeprecationWarning', 'IronPython.Runtime.Exceptions.PendingDeprecationWarningException', None, (), ()),
                        ExceptionInfo('RuntimeWarning', 'IronPython.Runtime.Exceptions.RuntimeWarningException', None, (), ()),
                        ExceptionInfo('SyntaxWarning', 'IronPython.Runtime.Exceptions.SyntaxWarningException', None, (), ()),
                        ExceptionInfo('UserWarning', 'IronPython.Runtime.Exceptions.UserWarningException', None, (), ()),
                        ExceptionInfo('FutureWarning', 'IronPython.Runtime.Exceptions.FutureWarningException', None, (), ()),
                        ExceptionInfo('ImportWarning', 'IronPython.Runtime.Exceptions.ImportWarningException', None, (), ()),
                        ExceptionInfo('UnicodeWarning', 'IronPython.Runtime.Exceptions.UnicodeWarningException', None, (), ()),
                        ExceptionInfo('BytesWarning', 'IronPython.Runtime.Exceptions.BytesWarningException', None, (), ()),
                        ExceptionInfo('ResourceWarning', 'IronPython.Runtime.Exceptions.ResourceWarningException', None, (), ()),
                    ),
                ),                
            ),      
        ),
    )
)

def get_exception_info(pythonName, curHierarchy):
    for exception in curHierarchy.subclasses:
        if exception.name == pythonName:
            return exception
    for exception in curHierarchy.subclasses:
        res = get_exception_info(pythonName, exception)
        if res is not None:
            return res

def get_all_exceps(l, curHierarchy):
    # if we have duplicate CLR exceptions (e.g. VMSError and Exception)
    # only generate the one highest in the Python hierarchy
    for exception in curHierarchy.subclasses:
        found = False
        for e in l:
            if e.clrException == exception.clrException:
                found = True
                break
        
        if not found:
            l.append(exception)
    for exception in curHierarchy.subclasses:
        get_all_exceps(l, exception)
    return l

ip = clr.LoadAssemblyByName('IronPython')
ms = clr.LoadAssemblyByName('Microsoft.Scripting')
md = clr.LoadAssemblyByName('Microsoft.Dynamic')
sysdll = clr.LoadAssemblyByPartialName('System')

def get_type(name):
    if name.startswith('IronPython'):            return ip.GetType(name)
    if name.startswith('Microsoft.Scripting'):   
        res = ms.GetType(name)
        return res if res is not None else md.GetType(name)

    if name.startswith('System.ComponentModel'): return sysdll.GetType(name)
    
    return System.Type.GetType(name)


def exception_distance(a):
    distance = 0
    while a.FullName != "System.Exception":
        a = a.BaseType
        distance += 1
    return distance
     
def get_compare_name(ex_info):
    return ex_info.baseMapping or ex_info.clrException

def compare_exceptions(a, b):
    a, b = get_compare_name(a), get_compare_name(b)
    
    ta = get_type(a)
    tb = get_type(b)
    
    if ta == None:
        raise Exception("Exception class not found %s " % a)

    if tb == None:
        raise Exception("Exception class not found %s " % b)
    
    if ta.IsSubclassOf(tb): return -1
    if tb.IsSubclassOf(ta): return 1
    
    da = exception_distance(ta)
    db = exception_distance(tb)
    
    # put exceptions further from System.Exception 1st, those further later...
    if da != db: return db - da
    
    def cmp(a, b):
        return (a > b) - (a < b)

    return cmp(ta.Name, tb.Name)
    
def gen_topython_helper(cw):
    cw.enter_block("private static BaseException/*!*/ ToPythonHelper(System.Exception clrException)")
    
    allExceps = get_all_exceps([], exceptionHierarchy)
    allExceps.sort(key=functools.cmp_to_key(compare_exceptions))
    
    for x in allExceps:
        cw.writeline('if (clrException is %s) return %s;' % (x.ExceptionMappingName, x.MakeNewException()))

    cw.writeline('return new BaseException(Exception);')    
    cw.exit_block()

        
def get_clr_name(e):
    return e.replace('Error', '') + 'Exception'

FACTORY = """
public static Exception %(name)s(string format, params object[] args) {
    return new %(clrname)s(string.Format(format, args));
}"""

def factory_gen(cw):
    for e in pythonExcs:
        cw.write(FACTORY, name=e, clrname=get_clr_name(e))

CLASS1 = """
[Serializable]
public class %(name)s : %(supername)s, IPythonAwareException {
    private object _pyExceptionObject;
    private List<DynamicStackFrame> _frames;
    private TraceBack _traceback;
    
    public %(name)s() : base() { }
    public %(name)s(string msg) : base(msg) { }
    public %(name)s(string message, Exception innerException)
        : base(message, innerException) {
    }
#if FEATURE_SERIALIZATION
    protected %(name)s(SerializationInfo info, StreamingContext context) : base(info, context) { }
    
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        info.AddValue("frames", _frames);
        info.AddValue("traceback", _traceback);
        base.GetObjectData(info, context);
    }
#endif

    object IPythonAwareException.PythonException {
        get { 
            if (_pyExceptionObject == null) {
                var newEx = %(make_new_exception)s;
                newEx.InitializeFromClr(this);
                _pyExceptionObject = newEx;
            }
            return _pyExceptionObject; 
        }
        set { _pyExceptionObject = value; }
    }
    
    List<DynamicStackFrame> IPythonAwareException.Frames {
        get { return _frames; }
        set { _frames = value; }
    }
    
    TraceBack IPythonAwareException.TraceBack {
        get { return _traceback; }
        set { _traceback = value; }
    }
}
"""

def gen_one_exception(cw, e):
    supername = getattr(builtins, e).__bases__[0].__name__
    if not supername in pythonExcs and supername != 'Warning':
        supername = ''
    cw.write(CLASS1, name=get_clr_name(e), supername=get_clr_name(supername), make_new_exception = get_exception_info(e, exceptionHierarchy).MakeNewException())

def gen_one_exception_maker(e):
    def gen_one_exception_specialized(x):
        return gen_one_exception(x, e)

    return gen_one_exception_specialized

def fix_object(name):
    if name == "object": return "@object"
    return name

def gen_one_new_exception(cw, exception, parent):
    if exception.fields:
        cw.writeline('[MultiRuntimeAware]')
        cw.writeline('private static PythonType %sStorage;' % (exception.name, ))
        cw.enter_block('public static PythonType %s' % (exception.name, ))
        cw.enter_block('get')
        cw.enter_block('if (%sStorage == null)' % (exception.name, ))
        cw.writeline('Interlocked.CompareExchange(ref %sStorage, CreateSubType(%s, typeof(_%s), (msg, innerException) => new %s(msg, innerException)), null);' % (exception.name, exception.parent.PythonType, exception.name, exception.DotNetExceptionName))
        cw.exit_block() # if
        cw.writeline('return %sStorage;' % (exception.name, ))
        cw.exit_block()
        cw.exit_block()
        cw.writeline()

        cw.writeline('[PythonType("%s"), PythonHidden, DynamicBaseType, Serializable]' % exception.name)
        if exception.ConcreteParent.fields:
            cw.enter_block('public partial class _%s : _%s' % (exception.name, exception.ConcreteParent.name))
        else:
            cw.enter_block('public partial class _%s : %s' % (exception.name, exception.ConcreteParent.name))
                
        for field in exception.fields:
            cw.writeline('private object _%s;' % field)
        
        if exception.fields:
            cw.writeline('')

        cw.writeline('public _%s() : base(%s) { }' % (exception.name, exception.name))
        cw.writeline('public _%s(PythonType type) : base(type) { }' % (exception.name, ))
        cw.writeline('')
        
        cw.enter_block('public new static object __new__(PythonType cls, [ParamDictionary]IDictionary<object, object> kwArgs, params object[] args)')
        cw.writeline('return Activator.CreateInstance(cls.UnderlyingSystemType, cls);')
        cw.exit_block()
        cw.writeline('')

        if exception.args:        
            argstr = ', '.join(['object ' + fix_object(x) for x in exception.args])             
            cw.enter_block('public void __init__(%s)' % (argstr))
            for arg in exception.args:
                cw.writeline('_%s = %s;' % (arg, fix_object(arg)))
            cw.writeline('args = PythonTuple.MakeTuple(' + ', '.join([fix_object(x) for x in exception.args]) + ');')
            cw.exit_block()
            cw.writeline('')
            cw.enter_block('public override void __init__(params object[] args)')
            cw.enter_block('if (args == null || args.Length != %d)' % (len(exception.args), ))
            cw.writeline('throw PythonOps.TypeError("__init__ takes exactly %d arguments ({0} given)", args.Length);' % len(exception.args))
            cw.exit_block()
            cw.writeline('__init__(' + ', '.join([fix_object(x) for x in exception.args]) + ');')
            cw.exit_block()
            cw.writeline('')
        
        for field in exception.fields:
            cw.enter_block('public object %s' % fix_object(field))
            cw.writeline('get { return _%s; }' % field)
            cw.writeline('set { _%s = value; }' % field)
            cw.exit_block()
            cw.writeline('')
        
        cw.exit_block()
        cw.writeline('')

    else:
        cw.writeline('[MultiRuntimeAware]')
        cw.writeline('private static PythonType %sStorage;' % (exception.name, ))
        cw.enter_block('public static PythonType %s' % (exception.name, ))
        cw.enter_block('get')
        cw.enter_block('if (%sStorage == null)' % (exception.name, ))
        cw.writeline('Interlocked.CompareExchange(ref %sStorage, CreateSubType(%s, "%s", (msg, innerException) => new %s(msg, innerException)), null);' % (exception.name, exception.parent.PythonType, exception.name, exception.DotNetExceptionName))
        cw.exit_block() # if
        cw.writeline('return %sStorage;' % (exception.name, ))
        cw.exit_block()
        cw.exit_block()
        cw.writeline()
        
    for child in exception.subclasses:
        gen_one_new_exception(cw, child, exception)
            
def newstyle_gen(cw):
    for child in exceptionHierarchy.subclasses:
        gen_one_new_exception(cw, child, exceptionHierarchy)
        
def gen_one_exception_module_entry(cw, exception, parent):
    cw.write("public static PythonType %s = %s;" % (exception.name, exception.InternalPythonType))
    for child in exception.subclasses:
        gen_one_exception_module_entry(cw, child, exception)
        
def module_gen(cw):
    cw.write("public static object BaseException = DynamicHelpers.GetPythonTypeFromType(typeof(PythonExceptions.BaseException));")
    
    for child in exceptionHierarchy.subclasses:
        gen_one_exception_module_entry(cw, child, exceptionHierarchy)

def gen_one_exception_builtin_entry(cw, exception, parent):
    cw.enter_block("public static PythonType %s" % (exception.name, ))
    if exception.fields:
        cw.write('get { return %s; }' % (exception.InternalPythonType, ))
    else:
        cw.write('get { return %s; }' % (exception.InternalPythonType, ))
    cw.exit_block()

    for child in exception.subclasses:
        gen_one_exception_builtin_entry(cw, child, exception)

def builtin_gen(cw):
    for child in exceptionHierarchy.subclasses:
        gen_one_exception_builtin_entry(cw, child, exceptionHierarchy)

def main():
    gens = [
        ("ToPython Exception Helper", gen_topython_helper),
        ("Exception Factories", factory_gen),
        ("Python New-Style Exceptions", newstyle_gen),
        ("builtin exceptions", builtin_gen),
    ]

    for e in pythonExcs:
        gens.append((get_clr_name(e), gen_one_exception_maker(e)))

    return generate(*gens)

if __name__ == "__main__":
    main()
