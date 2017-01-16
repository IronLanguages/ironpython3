# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
from generate import generate

MAX_ARGS = 16

def make_params(nargs, *prefix):
    params = ["object arg%d" % i for i in range(nargs)]
    return ", ".join(list(prefix) + params)

def make_params1(nargs, prefix=("CodeContext context",)):
    params = ["object arg%d" % i for i in range(nargs)]
    return ", ".join(list(prefix) + params)

def make_args(nargs, *prefix):
    params = ["arg%d" % i for i in range(nargs)]
    return ", ".join(list(prefix) + params)

def make_args1(nargs, prefix, start=0):
    args = ["arg%d" % i for i in range(start, nargs)]
    return ", ".join(list(prefix) + args)

def make_calltarget_type_args(nargs):
    return ', '.join(['PythonFunction'] + ['object'] * (nargs + 1))

def gen_args_comma(nparams, comma):
    args = ""
    for i in range(nparams):
        args = args + comma + ("object arg%d" % i)
        comma = ", "
    return args

def gen_args(nparams):
    return gen_args_comma(nparams, "")

def gen_args_call(nparams, *prefix):
    args = ""
    comma = ""
    for i in range(nparams):
        args = args + comma +("arg%d" % i)
        comma = ", "
    if prefix:
        if args:
            args = prefix[0] + ', ' + args
        else:
            args = prefix[0]
    return args

def gen_args_array(nparams):
    args = gen_args_call(nparams)
    if args: return "{ " + args + " }"
    else: return "{ }"

def gen_callargs(nparams):
    args = ""
    comma = ""
    for i in range(nparams):
        args = args + comma + ("callArgs[%d]" % i)
        comma = ","
    return args

def gen_args_paramscall(nparams):
    args = ""
    comma = ""
    for i in range(nparams):
        args = args + comma + ("args[%d]" % i)
        comma = ","
    return args


method_caller_template = """
class MethodBinding<%(typeParams)s> : BaseMethodBinding {
    private CallSite<Func<CallSite, CodeContext, object, object, %(typeParams)s, object>> _site;

    public MethodBinding(PythonInvokeBinder binder) {
        _site = CallSite<Func<CallSite, CodeContext, object, object, %(typeParams)s, object>>.Create(binder);
    }

    public object SelfTarget(CallSite site, CodeContext context, object target, %(callParams)s) {
        Method self = target as Method;
        if (self != null && self._inst != null) {
            return _site.Target(_site, context, self._func, self._inst, %(callArgs)s);
        }

        return ((CallSite<Func<CallSite, CodeContext, object, %(typeParams)s, object>>)site).Update(site, context, target, %(callArgs)s);
    }

    public object SelflessTarget(CallSite site, CodeContext context, object target, object arg0, %(callParamsSelfless)s) {
        Method self = target as Method;
        if (self != null && self._inst == null) {
            return _site.Target(_site, context, self._func, PythonOps.MethodCheckSelf(context, self, arg0), %(callArgsSelfless)s);
        }

        return ((CallSite<Func<CallSite, CodeContext, object, object, %(typeParams)s, object>>)site).Update(site, context, target, arg0, %(callArgsSelfless)s);
    }
    
    public override Delegate GetSelfTarget() {
        return new Func<CallSite, CodeContext, object, %(typeParams)s, object>(SelfTarget);
    }

    public override Delegate GetSelflessTarget() {
        return new Func<CallSite, CodeContext, object, object, %(typeParams)s, object>(SelflessTarget);
    }
}"""

def method_callers(cw):
    for nparams in range(1, MAX_ARGS-3):        
        cw.write(method_caller_template % {
                  'typeParams' : ', '.join(('T%d' % d for d in range(nparams))),
                  'callParams': ', '.join(('T%d arg%d' % (d,d) for d in range(nparams))),
                  'callParamsSelfless': ', '.join(('T%d arg%d' % (d,d+1) for d in range(nparams))),
                  'callArgsSelfless' : ', '.join(('arg%d' % (d+1) for d in range(nparams))),
                  'argCount' : nparams,
                  'callArgs': ', '.join(('arg%d' % d for d in range(nparams))),
                  'genFuncArgs' : make_calltarget_type_args(nparams),
                 })
                 
                 
def selfless_method_caller_switch(cw):
    cw.enter_block('switch (typeArgs.Length)')
    for i in range(1, MAX_ARGS-3):
        cw.write('case %d: binding = (BaseMethodBinding)Activator.CreateInstance(typeof(MethodBinding<%s>).MakeGenericType(typeArgs), binder); break;' % (i, ',' * (i-1)))
    cw.exit_block()
        
function_caller_template = """
public sealed class FunctionCaller<%(typeParams)s> : FunctionCaller {
    public FunctionCaller(int compat) : base(compat) { }
    
    public object Call%(argCount)d(CallSite site, CodeContext context, object func, %(callParams)s) {
        if (func is PythonFunction pyfunc && pyfunc.FunctionCompatibility == _compat) {
            return ((Func<%(genFuncArgs)s>)pyfunc.__code__.Target)(pyfunc, %(callArgs)s);
        }

        return ((CallSite<Func<CallSite, CodeContext, object, %(typeParams)s, object>>)site).Update(site, context, func, %(callArgs)s);
    }"""
    
defaults_template = """
    public object Default%(defaultCount)dCall%(argCount)d(CallSite site, CodeContext context, object func, %(callParams)s) {
        if (func is PythonFunction pyfunc && pyfunc.FunctionCompatibility == _compat) {
            int defaultIndex = pyfunc.Defaults.Length - pyfunc.NormalArgumentCount + %(argCount)d;
            return ((Func<%(genFuncArgs)s>)pyfunc.__code__.Target)(pyfunc, %(callArgs)s, %(defaultArgs)s);
        }

        return ((CallSite<Func<CallSite, CodeContext, object, %(typeParams)s, object>>)site).Update(site, context, func, %(callArgs)s);
    }"""

defaults_template_0 = """
public object Default%(argCount)dCall0(CallSite site, CodeContext context, object func) {
    if (func is PythonFunction pyfunc && pyfunc.FunctionCompatibility == _compat) {
        int defaultIndex = pyfunc.Defaults.Length - pyfunc.NormalArgumentCount;
        return ((Func<%(genFuncArgs)s>)pyfunc.__code__.Target)(pyfunc, %(defaultArgs)s);
    }

    return ((CallSite<Func<CallSite, CodeContext, object, object>>)site).Update(site, context, func);
}"""

def function_callers(cw):
    cw.write('''class FunctionCallerProperties {
    internal const int MaxGeneratedFunctionArgs = %d;
}''' % (MAX_ARGS-2))
    cw.write('')
    for nparams in range(1, MAX_ARGS-2):        
        cw.write(function_caller_template % {
                  'typeParams' : ', '.join(('T%d' % d for d in range(nparams))),
                  'callParams': ', '.join(('T%d arg%d' % (d,d) for d in range(nparams))),
                  'argCount' : nparams,
                  'callArgs': ', '.join(('arg%d' % d for d in range(nparams))),
                  'genFuncArgs' : make_calltarget_type_args(nparams),
                 })                    
                 
        for i in range(nparams + 1, MAX_ARGS - 2):
            cw.write(defaults_template % {
                      'typeParams' : ', '.join(('T%d' % d for d in range(nparams))),
                      'callParams': ', '.join(('T%d arg%d' % (d,d) for d in range(nparams))),
                      'argCount' : nparams,
                      'totalParamCount' : i,
                      'callArgs': ', '.join(('arg%d' % d for d in range(nparams))),
                      'defaultCount' : i - nparams,
                      'defaultArgs' : ', '.join(('pyfunc.Defaults[defaultIndex + %d]' % curDefault for curDefault in range(i - nparams))),
                      'genFuncArgs' : make_calltarget_type_args(i),
                     })                 
        cw.write('}')

def function_callers_0(cw):
    for i in range(1, MAX_ARGS - 2):
        cw.write(defaults_template_0 % {
                  'argCount' : i,
                  'defaultArgs' : ', '.join(('pyfunc.Defaults[defaultIndex + %d]' % curDefault for curDefault in range(i))),
                  'genFuncArgs' : make_calltarget_type_args(i),
                 })                 

function_caller_switch_template = """case %(argCount)d:                        
    callerType = typeof(FunctionCaller<%(arity)s>).MakeGenericType(typeParams);
    mi = callerType.GetMethod(baseName + "Call%(argCount)d");
    Debug.Assert(mi != null);
    fc = GetFunctionCaller(callerType, funcCompat);
    funcType = typeof(Func<,,,,%(arity)s>).MakeGenericType(allParams);

    return new Binding.FastBindResult<T>((T)(object)mi.CreateDelegate(funcType, fc), true);"""
    

def function_caller_switch(cw):
    for nparams in range(1, MAX_ARGS-2):
        cw.write(function_caller_switch_template % {
                  'arity' : ',' * (nparams - 1),
                  'argCount' : nparams,
                 })   

def gen_lazy_call_targets(cw):
    for nparams in range(MAX_ARGS):
        cw.enter_block("public static object OriginalCallTarget%d(%s)" % (nparams, make_params(nparams, "PythonFunction function")))
        cw.write("function.__code__.LazyCompileFirstTarget(function);")
        cw.write("return ((Func<%s>)function.__code__.Target)(%s);" % (make_calltarget_type_args(nparams), gen_args_call(nparams, 'function')))
        cw.exit_block()
        cw.write('')

def gen_recursion_checks(cw):
    for nparams in range(MAX_ARGS):
        cw.enter_block("internal class PythonFunctionRecursionCheck%d" % (nparams, ))
        cw.write("private readonly Func<%s> _target;" % (make_calltarget_type_args(nparams), ))
        cw.write('')
        cw.enter_block('public PythonFunctionRecursionCheck%d(Func<%s> target)' % (nparams, make_calltarget_type_args(nparams)))
        cw.write('_target = target;')
        cw.exit_block()
        cw.write('')
        
        cw.enter_block('public object CallTarget(%s)' % (make_params(nparams, "PythonFunction/*!*/ function"), ))
        cw.enter_block('try')
        cw.write('PythonOps.FunctionPushFrame(function.Context.LanguageContext);')
        cw.write('return _target(%s);' % (gen_args_call(nparams, 'function'), ))
        cw.finally_block()
        cw.write('PythonOps.FunctionPopFrame();')
        cw.exit_block()        
        cw.exit_block()
        cw.exit_block()
        cw.write('')

def gen_recursion_delegate_switch(cw):
    for nparams in range(MAX_ARGS):
        cw.case_label('case %d:' % nparams)
        cw.write('finalTarget = new Func<%s>(new PythonFunctionRecursionCheck%d((Func<%s>)finalTarget).CallTarget);' % (make_calltarget_type_args(nparams), nparams, make_calltarget_type_args(nparams)))
        cw.write('break;')
        cw.dedent()
    
def get_call_type(postfix):
    if postfix == "": return "CallType.None"
    else: return "CallType.ImplicitInstance"


def make_call_to_target(cw, index, postfix, extraArg):
        cw.enter_block("public override object Call%(postfix)s(%(params)s)", postfix=postfix,
                       params=make_params1(index))
        cw.write("if (target%(index)d != null) return target%(index)d(%(args)s);", index=index,
                 args = make_args1(index, extraArg))
        cw.write("throw BadArgumentError(%(callType)s, %(nargs)d);", callType=get_call_type(postfix), nargs=index)
        cw.exit_block()

def make_call_to_targetX(cw, index, postfix, extraArg):
        cw.enter_block("public override object Call%(postfix)s(%(params)s)", postfix=postfix,
                       params=make_params1(index))
        cw.write("return target%(index)d(%(args)s);", index=index, args = make_args1(index, extraArg))
        cw.exit_block()

def make_error_calls(cw, index):
        cw.enter_block("public override object Call(%(params)s)", params=make_params1(index))
        cw.write("throw BadArgumentError(CallType.None, %(nargs)d);", nargs=index)
        cw.exit_block()

        if index > 0:
            cw.enter_block("public override object CallInstance(%(params)s)", params=make_params1(index))
            cw.write("throw BadArgumentError(CallType.ImplicitInstance, %(nargs)d);", nargs=index)
            cw.exit_block()

def gen_call(nargs, nparams, cw, extra=[]):
    args = extra + ["arg%d" % i for i in range(nargs)]
    cw.enter_block("public override object Call(%s)" % make_params1(nargs))
    
    # first emit error checking...
    ndefaults = nparams-nargs
    if nargs != nparams:    
        cw.write("if (Defaults.Length < %d) throw BadArgumentError(%d);" % (ndefaults,nargs))
    
    # emit the common case of no recursion check
    if (nargs == nparams):
        cw.write("if (!EnforceRecursion) return target(%s);" % ", ".join(args))
    else:        
        dargs = args + ["Defaults[Defaults.Length - %d]" % i for i in range(ndefaults, 0, -1)]
        cw.write("if (!EnforceRecursion) return target(%s);" % ", ".join(dargs))
    
    # emit non-common case of recursion check
    cw.write("PushFrame();")
    cw.enter_block("try")

    # make function body
    if (nargs == nparams):
        cw.write("return target(%s);" % ", ".join(args))
    else:        
        dargs = args + ["Defaults[Defaults.Length - %d]" % i for i in range(ndefaults, 0, -1)]
        cw.write("return target(%s);" % ", ".join(dargs))
    
    cw.finally_block()
    cw.write("PopFrame();")
    cw.exit_block()
        
    cw.exit_block()

def gen_params_callN(cw, any):
    cw.enter_block("public override object Call(CodeContext context, params object[] args)")
    cw.write("if (!IsContextAware) return Call(args);")
    cw.write("")
    cw.enter_block("if (Instance == null)")
    cw.write("object[] newArgs = new object[args.Length + 1];")
    cw.write("newArgs[0] = context;")
    cw.write("Array.Copy(args, 0, newArgs, 1, args.Length);")
    cw.write("return Call(newArgs);")
    cw.else_block()
    
    # need to call w/ Context, Instance, *args
    
    if any:
        cw.enter_block("switch (args.Length)")
        for i in range(MAX_ARGS-1):
                if i == 0:
                    cw.write(("case %d: if(target2 != null) return target2(context, Instance); break;") % (i))
                else:
                    cw.write(("case %d: if(target%d != null) return target%d(context, Instance, " + gen_args_paramscall(i) + "); break;") % (i, i+2, i+2))
        cw.exit_block()
        cw.enter_block("if (targetN != null)")
        cw.write("object [] newArgs = new object[args.Length+2];")
        cw.write("newArgs[0] = context;")
        cw.write("newArgs[1] = Instance;")
        cw.write("Array.Copy(args, 0, newArgs, 2, args.Length);")
        cw.write("return targetN(newArgs);")
        cw.exit_block()
        cw.write("throw BadArgumentError(args.Length);")
        cw.exit_block()
    else:
        cw.write("object [] newArgs = new object[args.Length+2];")
        cw.write("newArgs[0] = context;")
        cw.write("newArgs[1] = Instance;")
        cw.write("Array.Copy(args, 0, newArgs, 2, args.Length);")
        cw.write("return target(newArgs);")
        cw.exit_block()
        
    cw.exit_block()
    cw.write("")

CODE = """
public static object Call(%(params)s) {
    FastCallable fc = func as FastCallable;
    if (fc != null) return fc.Call(%(args)s);

    return PythonCalls.Call(func, %(argsArray)s);
}"""

def gen_python_switch(cw):
    for nparams in range(MAX_ARGS):
        genArgs = make_calltarget_type_args(nparams)
        cw.write("""case %d: 
    originalTarget = (Func<%s>)OriginalCallTarget%d;
    return typeof(Func<%s>);""" % (nparams, genArgs, nparams, genArgs))

fast_type_call_template = """
class FastBindingBuilder<%(typeParams)s> : FastBindingBuilderBase {
    public FastBindingBuilder(CodeContext context, PythonType type, PythonInvokeBinder binder, Type siteType, Type[] genTypeArgs) :
        base(context, type, binder, siteType, genTypeArgs) {
    }

    protected override Delegate GetNewSiteDelegate(PythonInvokeBinder binder, object func) {
        return new Func<%(newInitDlgParams)s>(new NewSite<%(typeParams)s>(binder, func).Call);
    }

    protected override Delegate MakeDelegate(int version, Delegate newDlg, LateBoundInitBinder initBinder) {
        return new Func<%(funcParams)s>(
            new FastTypeSite<%(typeParams)s>(
                version, 
                (Func<%(newInitDlgParams)s>)newDlg,
                initBinder
            ).CallTarget
        );
    }
}

class FastTypeSite<%(typeParams)s> {
    private readonly int _version;
    private readonly Func<%(newInitDlgParams)s> _new;
    private readonly CallSite<Func<%(nestedSlowSiteParams)s>> _initSite;

    public FastTypeSite(int version, Func<%(newInitDlgParams)s> @new, LateBoundInitBinder initBinder) {
        _version = version;
        _new = @new;
        _initSite = CallSite<Func<%(nestedSlowSiteParams)s>>.Create(initBinder);
    }

    public object CallTarget(CallSite site, CodeContext context, object type, %(callTargetArgs)s) {
        PythonType pt = type as PythonType;
        if (pt != null && pt.Version == _version) {
            object res = _new(context, type, %(callTargetPassedArgs)s);
            _initSite.Target(_initSite, context, res, %(callTargetPassedArgs)s);

            return res;
        }

        return ((CallSite<Func<%(funcParams)s>>)site).Update(site, context, type, %(callTargetPassedArgs)s);
    }
}

class NewSite<%(typeParams)s> {
    private readonly CallSite<Func<%(nestedSiteParams)s>> _site;
    private readonly object _target;

    public NewSite(PythonInvokeBinder binder, object target) {
        _site = CallSite<Func<%(nestedSiteParams)s>>.Create(binder);
        _target = target;
    }

    public object Call(CodeContext context, object typeOrInstance, %(callTargetArgs)s) {
        return _site.Target(_site, context, _target, typeOrInstance, %(callTargetPassedArgs)s);
    }
}
"""
def gen_fast_type_callers(cw):
    for nparams in range(1, 6):       
        funcParams = 'CallSite, CodeContext, object, ' + ', '.join(('T%d' % d for d in range(nparams))) + ', object'
        newInitDlgParams = 'CodeContext, object, ' + ', '.join(('T%d' % d for d in range(nparams))) + ', object'
        callTargetArgs = ', '.join(('T%d arg%d' % (d, d) for d in range(nparams)))
        callTargetPassedArgs = ', '.join(('arg%d' % (d, ) for d in range(nparams)))
        nestedSiteParams = 'CallSite, CodeContext, object, object, ' + ', '.join(('T%d' % d for d in range(nparams))) + ', object'
        nestedSlowSiteParams = 'CallSite, CodeContext, object, ' + ', '.join(('T%d' % d for d in range(nparams))) + ', object'
        cw.write(fast_type_call_template % {
                  'typeParams' : ', '.join(('T%d' % d for d in range(nparams))),
                  'funcParams' : funcParams,
                  'newInitDlgParams' : newInitDlgParams,
                  'callTargetArgs' : callTargetArgs,
                  'callTargetPassedArgs': callTargetPassedArgs,
                  'nestedSiteParams' : nestedSiteParams,
                  'nestedSlowSiteParams' : nestedSlowSiteParams,
                 })
                 
def gen_fast_type_caller_switch(cw):
    for nparams in range(1, 6):
        cw.write('case %d: baseType = typeof(FastBindingBuilder<%s>); break;' % (nparams, (',' * (nparams - 1))))

fast_init_template = """
class FastInitSite<%(typeParams)s> {
    private readonly int _version;
    private readonly PythonFunction _slot;
    private readonly CallSite<Func<CallSite, CodeContext, PythonFunction, object, %(typeParams)s, object>> _initSite;

    public FastInitSite(int version, PythonInvokeBinder binder, PythonFunction target) {
        _version = version;
        _slot = target;
        _initSite = CallSite<Func<CallSite, CodeContext, PythonFunction, object,  %(typeParams)s, object>>.Create(binder);
    }

    public object CallTarget(CallSite site, CodeContext context, object inst, %(callParams)s) {
        IPythonObject pyObj = inst as IPythonObject;
        if (pyObj != null && pyObj.PythonType.Version == _version) {
            _initSite.Target(_initSite, context, _slot, inst, %(callArgs)s);
            return inst;
        }

        return ((CallSite<Func<CallSite, CodeContext, object,  %(typeParams)s, object>>)site).Update(site, context, inst, %(callArgs)s);
    }

    public object EmptyCallTarget(CallSite site, CodeContext context, object inst, %(callParams)s) {
        IPythonObject pyObj = inst as IPythonObject;
        if ((pyObj != null && pyObj.PythonType.Version == _version) || DynamicHelpers.GetPythonType(inst).Version == _version) {
            return inst;
        }

        return ((CallSite<Func<CallSite, CodeContext, object,  %(typeParams)s, object>>)site).Update(site, context, inst, %(callArgs)s);
    }
}
"""

MAX_FAST_INIT_ARGS = 6
def gen_fast_init_callers(cw):
    for nparams in range(1, MAX_FAST_INIT_ARGS):       
        callParams = ', '.join(('T%d arg%d' % (d, d) for d in range(nparams)))
        callArgs = ', '.join(('arg%d' % (d, ) for d in range(nparams)))
        cw.write(fast_init_template % {
                  'typeParams' : ', '.join(('T%d' % d for d in range(nparams))),
                  'callParams' : callParams,
                  'callArgs': callArgs,
                 })

def gen_fast_init_switch(cw):
    for nparams in range(1, MAX_FAST_INIT_ARGS):
        cw.write("case %d: initSiteType = typeof(FastInitSite<%s>); break;" % (nparams, ',' * (nparams-1), ))

def gen_fast_init_max_args(cw):
    cw.write("public const int MaxFastLateBoundInitArgs = %d;" % MAX_FAST_INIT_ARGS)

MAX_INSTRUCTION_PROVIDED_CALLS = 7
def gen_call_expression_instruction_switch(cw):
    for i in range(MAX_INSTRUCTION_PROVIDED_CALLS):
        cw.case_label('case %d:' % i)
        cw.write('compiler.Compile(Parent.LocalContext);')
        cw.write('compiler.Compile(_target);')
        for j in range(i):
            cw.write('compiler.Compile(args[%d].Expression);' % j)
        cw.write('compiler.Instructions.Emit(new Invoke%dInstruction(Parent.PyContext));' % i)        
        cw.write('return;')
        cw.dedent()

def gen_call_expression_instructions(cw):
    for i in range(MAX_INSTRUCTION_PROVIDED_CALLS):
        siteargs = 'object, ' * (i + 1)
        argfetch = '\n'.join(['        var arg%d = frame.Pop();' % (j-1) for j in range(i, 0, -1)])
        callargs = ', '.join(['target'] + ['arg%d' % j for j in range(i)])
        cw.write("""
class Invoke%(argcount)dInstruction : InvokeInstruction {
    private readonly CallSite<Func<CallSite, CodeContext, %(siteargs)sobject>> _site;

    public Invoke%(argcount)dInstruction(PythonContext context) {
        _site = context.CallSite%(argcount)d;
    }

    public override int ConsumedStack {
        get {
            return %(consumedCount)d;
        }
    }

    public override int Run(InterpretedFrame frame) {
%(argfetch)s
        var target = frame.Pop();
        frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), %(callargs)s));
        return +1;
    }
}""" % {'siteargs': siteargs, 'argfetch' : argfetch, 'callargs' : callargs, 'argcount' : i, 'consumedCount' : i + 2 })


def gen_shared_call_sites_storage(cw):
    for i in range(MAX_INSTRUCTION_PROVIDED_CALLS):
        siteargs = 'object, ' * (i + 1)
        cw.writeline('private CallSite<Func<CallSite, CodeContext, %sobject>> _callSite%d;' % (siteargs, i))

def gen_shared_call_sites_properties(cw):
    for i in range(MAX_INSTRUCTION_PROVIDED_CALLS):
        siteargs = 'object, ' * (i + 1)
        cw.enter_block('internal CallSite<Func<CallSite, CodeContext, %sobject>> CallSite%d' % (siteargs, i))
        cw.enter_block('get')
        cw.writeline('EnsureCall%dSite();' % i)

        cw.writeline('return _callSite%d;' % i)
        cw.exit_block()
        cw.exit_block()
        
        cw.writeline('')
        
        cw.enter_block('private void EnsureCall%dSite()' % i)
        cw.enter_block('if (_callSite%d == null)' % i)
        cw.writeline('Interlocked.CompareExchange(')
        cw.indent()
        cw.writeline('ref _callSite%d,' % i)
        cw.writeline('CallSite<Func<CallSite, CodeContext, %sobject>>.Create(Invoke(new CallSignature(%d))),' % (siteargs, i))
        cw.writeline('null')
        cw.dedent()
        cw.writeline(');')
        cw.exit_block()
        cw.exit_block()
        
        cw.writeline('')

def main(): 
    return generate(
        ("Python Selfless Method Caller Switch", selfless_method_caller_switch),
        ("Python Method Callers", method_callers),
        ("Python Shared Call Sites Properties", gen_shared_call_sites_properties),
        ("Python Shared Call Sites Storage", gen_shared_call_sites_storage),
        ("Python Call Expression Instructions", gen_call_expression_instructions),
        ("Python Call Expression Instruction Switch", gen_call_expression_instruction_switch),
        ("Python Fast Init Max Args", gen_fast_init_max_args),
        ("Python Fast Init Switch", gen_fast_init_switch),
        ("Python Fast Init Callers", gen_fast_init_callers),
        ("Python Fast Type Caller Switch", gen_fast_type_caller_switch),
        ("Python Fast Type Callers", gen_fast_type_callers),
        ("Python Recursion Enforcement", gen_recursion_checks),
        ("Python Recursion Delegate Switch", gen_recursion_delegate_switch),
        ("Python Lazy Call Targets", gen_lazy_call_targets),
        ("Python Zero Arg Function Callers", function_callers_0),
        ("Python Function Callers", function_callers),
        ("Python Function Caller Switch", function_caller_switch),
        ("Python Call Target Switch", gen_python_switch),
    )

if __name__ == "__main__":
    main()
