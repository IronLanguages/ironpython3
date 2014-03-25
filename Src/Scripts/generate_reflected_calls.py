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

import sys
from generate import generate

MAX_ARGS = 3
MAX_HELPERS = 10
TYPE_CODE_TYPES = ['Int16', 'Int32', 'Int64', 'Boolean', 'Char', 'Byte', 'Decimal', 'DateTime', 'Double', 'Single', 'UInt16', 'UInt32', 'UInt64', 'String', 'SByte']

def get_args(i):
    return ['arg' + str(x) for x in xrange(i)]

def get_arr_args(i):
    return ['args[' + str(x) + ']' for x in xrange(i)]

def get_object_args(i):
    return ['object arg' + str(x) for x in xrange(i)]

def get_type_names(i):
    if i == 1: return ['T0']
    return ['T' + str(x) for x in xrange(i)]    

def get_func_type_names(i):
    return get_type_names(i - 1) + ['TRet']

def get_cast_args(i):
    return ['%s != null ? (%s)%s : default(%s)' % (x[0], x[1], x[0], x[1]) for x in zip(get_args(i), get_type_names(i))]

def get_type_params(i):
    if i == 0: return ''
    return '<' + ', '.join(get_type_names(i)) + '>'

def get_func_type_params(i):
    if i == 0: return ''
    return '<' + ', '.join(get_func_type_names(i)) + '>'
    
def gen_invoke_instance(cw):
    cw.enter_block('public virtual object InvokeInstance(object instance, params object[] args)')
    cw.enter_block('switch(args.Length)')
    
    for i in xrange(MAX_HELPERS-1):
        cw.write('case %d: return Invoke(%s);' % (i, ', '.join(['instance'] + get_arr_args(i))))
    
    cw.write('default: throw new InvalidOperationException();')
    cw.exit_block() # switch
    cw.exit_block() # function
    cw.write('')

def gen_invoke(cw):
    cw.enter_block('public virtual object Invoke(params object[] args)')
    cw.enter_block('switch(args.Length)')
    
    for i in xrange(MAX_HELPERS):
        cw.write('case %d: return Invoke(%s);' % (i, ', '.join(get_arr_args(i))))
    
    cw.write('default: throw new InvalidOperationException();')
    cw.exit_block() # switch
    cw.exit_block() # function
    cw.write('')
    
def gen_invoke_base_methods(cw):
    for i in xrange(MAX_HELPERS):
        cw.write('public virtual object Invoke(%s) { throw new InvalidOperationException(); }' % (', '.join(get_object_args(i)), ))
    cw.write('')

def gen_fast_creation(cw):
    cw.write('/// <summary>')
    cw.write('/// Fast creation works if we have a known primitive types for the entire')
    cw.write('/// method siganture.  If we have any non-primitive types then FastCreate')
    cw.write('/// falls back to SlowCreate which works for all types.')
    cw.write('/// ')
    cw.write('/// Fast creation is fast because it avoids using reflection (MakeGenericType')
    cw.write('/// and Activator.CreateInstance) to create the types.  It does this through')
    cw.write('/// calling a series of generic methods picking up each strong type of the')
    cw.write('/// signature along the way.  When it runs out of types it news up the ')
    cw.write('/// appropriate CallInstruction with the strong-types that have been built up.')
    cw.write('/// ')
    cw.write('/// One relaxation is that for return types which are non-primitive types')
    cw.write('/// we can fallback to object due to relaxed delegates.')
    cw.write('/// </summary>')
    for i in xrange(MAX_ARGS):        
        cw.enter_block('private static CallInstruction FastCreate%s(MethodInfo target, ParameterInfo[] pi)' % get_type_params(i))
        
        cw.write('Type t = TryGetParameterOrReturnType(target, pi, %d);' % (i, ))
        cw.enter_block('if (t == null)')
        
        typeArgs = ', '.join(get_type_names(i))
        if i == 0:           
            cw.write('return new ActionCallInstruction(target);')           
        else:
            cw.enter_block('if (target.ReturnType == typeof(void))')
            cw.write('return new ActionCallInstruction<%s>(target);' % (typeArgs, ))
            cw.exit_block()
            cw.write('return new FuncCallInstruction<%s>(target);' % (typeArgs, ))
        cw.exit_block()
        
        cw.write('')
        cw.write('if (t.IsEnum()) return SlowCreate(target, pi);')
        
        cw.enter_block('switch (t.GetTypeCode())')
        cw.enter_block('case TypeCode.Object:')
        if i == MAX_ARGS-1:
            cw.write('Debug.Assert(pi.Length == %d);' % (MAX_ARGS-1))
            cw.write('if (t.IsValueType()) goto default;')
            cw.write('')
            cw.write('return new FuncCallInstruction<%s>(target);' % (', '.join(get_type_names(i) + ['Object']), ) )
        else:
            cw.enter_block('if (t != typeof(object) && (IndexIsNotReturnType(%d, target, pi) || t.IsValueType()))' % (i, ))
            cw.write("// if we're on the return type relaxed delegates makes it ok to use object")
            cw.write("goto default;")
            cw.exit_block() # if 
            cw.write('return FastCreate<%s>(target, pi);' % (', '.join(get_type_names(i) + ['Object']), ) )
        cw.exit_block() # case
        
        for typeName in TYPE_CODE_TYPES:
            if i == MAX_ARGS-1:
                cw.write('case TypeCode.%s: return new FuncCallInstruction<%s>(target);' % (typeName, ', '.join(get_type_names(i) + [typeName])))
            else:
                cw.write('case TypeCode.%s: return FastCreate<%s>(target, pi);' % (typeName, ', '.join(get_type_names(i) + [typeName])))

        cw.write('default: return SlowCreate(target, pi);')
        cw.exit_block() # switch
        cw.exit_block() # method
        cw.write('')

def get_get_helper_type(cw):
    cw.enter_block('private static Type GetHelperType(MethodInfo info, Type[] arrTypes)')
    cw.write('Type t;')
    cw.enter_block('if (info.ReturnType == typeof(void))')
    cw.enter_block('switch (arrTypes.Length)')
    
    for i in xrange(MAX_HELPERS):
        if i == 0:
            cw.write('case %d: t = typeof(ActionCallInstruction); break;' % (i, ))
        else:
            cw.write('case %d: t = typeof(ActionCallInstruction<%s>).MakeGenericType(arrTypes); break;' % (i, ','*(i-1)))
    cw.write('default: throw new InvalidOperationException();')
    
    cw.exit_block() # switch
    
    cw.else_block()
    cw.enter_block('switch (arrTypes.Length)')
    
    for i in xrange(1, MAX_HELPERS+1):
        cw.write('case %d: t = typeof(FuncCallInstruction<%s>).MakeGenericType(arrTypes); break;' % (i, ','*(i-1)) )
    cw.write('default: throw new InvalidOperationException();')
        
    cw.exit_block() # switch
    cw.exit_block() # else/if
    
    cw.write('return t;')
    cw.exit_block() # method

def get_explicit_caching(cw): 
    for delegate, type_params_maker, s in [('Func', get_func_type_params, 1), ('Action', get_type_params, 0)]:
        for i in xrange(MAX_HELPERS):
            type_params = type_params_maker(s + i)
            cw.enter_block('public static MethodInfo Cache%s%s(%s%s method)' % (delegate, type_params, delegate, type_params))
            cw.write('var info = method.GetMethodInfo();')
            cw.enter_block('lock (_cache)')
            cw.write('_cache[info] = new %sCallInstruction%s(method);' % (delegate, type_params))            
            cw.exit_block()
            cw.write('return info;')            
            cw.exit_block()
            cw.write('')

def gen_call_instruction(cw):
    cw.enter_block('public partial class CallInstruction')

    cw.write('private const int MaxHelpers = ' + str(MAX_HELPERS) + ';')
    cw.write('private const int MaxArgs = ' + str(MAX_ARGS) + ';')
    cw.write('')
     
    gen_invoke_instance(cw)
    gen_invoke(cw)
    gen_invoke_base_methods(cw)
        
    gen_fast_creation(cw)
    get_get_helper_type(cw)
    get_explicit_caching(cw)
    
    cw.exit_block()
    cw.write('')

def gen_action_call_instruction(cw, i):
    type_params = get_type_params(i)

    cw.enter_block('internal sealed class ActionCallInstruction%s : CallInstruction' % type_params) 
    cw.write('private readonly Action%s _target;' % type_params)
    
    # properties
    cw.write('public override MethodInfo Info { get { return _target.GetMethod(); } }')
    cw.write('public override int ArgumentCount { get { return %d; } }' % (i))
    cw.write('')
    
    # ctor(delegate)
    cw.enter_block('public ActionCallInstruction(Action%s target)' % type_params)
    cw.write('_target = target;')
    cw.exit_block()
    cw.write('')
    
    # ctor(info)
    cw.enter_block('public ActionCallInstruction(MethodInfo target)')
    cw.write('_target = (Action%s)target.CreateDelegate(typeof(Action%s));' % (type_params, type_params))
    cw.exit_block()
    cw.write('')
    
    # invoke
    cw.enter_block('public override object Invoke(%s)' % (', '.join(get_object_args(i)), ))       
    cw.write('_target(%s);' % (', '.join(get_cast_args(i)), ))
    cw.write('return null;')
    cw.exit_block()
    cw.write('')
    
    # run
    gen_interpreted_run(cw, i, False)
    
    cw.exit_block()
    cw.write('')

def gen_func_call_instruction(cw, i):
    type_params = get_func_type_params(i)

    cw.enter_block('internal sealed class FuncCallInstruction%s : CallInstruction' % type_params)
    cw.write('private readonly Func%s _target;' % type_params)
    
    # properties
    cw.write('public override MethodInfo Info { get { return _target.GetMethodInfo(); } }')
    cw.write('public override int ArgumentCount { get { return %d; } }' % (i - 1))
    cw.write('')
    
    # ctor(delegate)
    cw.enter_block('public FuncCallInstruction(Func%s target)' % type_params)
    cw.write('_target = target;')
    cw.exit_block()
    cw.write('')
    
    # ctor(info)
    cw.enter_block('public FuncCallInstruction(MethodInfo target)')
    cw.write('_target = (Func%s)target.CreateDelegate(typeof(Func%s));' % (type_params, type_params))
    cw.exit_block()
    cw.write('')
    
    # invoke    
    cw.enter_block('public override object Invoke(%s)' % (', '.join(get_object_args(i-1)), ))       
    cw.write('return _target(%s);' % (', '.join(get_cast_args(i-1)), ))
    cw.exit_block()
    cw.write('')
    
    # run
    gen_interpreted_run(cw, i - 1, True)
    
    cw.exit_block()
    cw.write('')

def gen_interpreted_run(cw, n, isFunc):
    cw.enter_block('public override int Run(InterpretedFrame frame)')
    
    args = ''
    for i in xrange(0, n):
        if i > 0: args += ', '
        args += '(T%d)frame.Data[frame.StackIndex - %d]' % (i, n - i)
    
    if isFunc:
        call = 'frame.Data[frame.StackIndex - %d] = _target(%s);' % (n, args)
        si = n - 1
    else:
        call = '_target(%s);' % args
        si = n
    
    cw.write(call)
    cw.write('frame.StackIndex -= %d;' % si)
    cw.write('return 1;')
    
    cw.exit_block()

def gen_action_call_instructions(cw):
    for i in xrange(MAX_HELPERS):
        gen_action_call_instruction(cw, i)

def gen_func_call_instructions(cw):
    for i in xrange(1, MAX_HELPERS+1):
        gen_func_call_instruction(cw, i)
        
def gen_slow_caller(cw):
    cw.enter_block('internal sealed partial class MethodInfoCallInstruction : CallInstruction')
     
    for i in xrange(MAX_ARGS):
        cw.enter_block('public override object Invoke(%s)' % (', '.join(get_object_args(i)), ))
        cw.write('return InvokeWorker(%s);' % (', '.join(get_args(i)), ))
        cw.exit_block()
    
    cw.exit_block()
    
def gen_all(cw):
    gen_call_instruction(cw)
    gen_action_call_instructions(cw)
    gen_func_call_instructions(cw)
    gen_slow_caller(cw)

def main():
    return generate(
        ("Reflected Caller", gen_all),
    )

if __name__ == "__main__":
    main()
