# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
from generate import generate

MAX_TYPES = 16

MAX_ARGS = 3
MAX_HELPERS = 7
TYPE_CODE_TYPES = ['Int16', 'Int32', 'Int64', 'Boolean', 'Char', 'Byte', 'Decimal', 'DateTime', 'Double', 'Single', 'UInt16', 'UInt32', 'UInt64', 'String', 'SByte']

def get_args(i):
    return ['arg' + str(x) for x in range(i)]

def get_arr_args(i):
    return ['args[' + str(x) + ']' for x in range(i)]

def get_object_args(i):
    return ['object arg' + str(x) for x in range(i)]

def get_type_names(i):
    if i == 1: return ['T0']
    return ['T' + str(x) for x in range(i)]    

def get_invoke_type_names(i):
    return get_type_names(i - 1) + ['TRet']

def get_cast_args(i):
    return ['(%s)%s' % (x[0], x[1]) for x in zip(get_type_names(i), get_args(i))]

def get_type_params(i):
    if i == 0: return ''
    return '<' + ', '.join(get_type_names(i)) + '>'
    
    
def gen_instruction(cw, n):
    type_names = get_type_names(n)
    class_type_params = ','.join(type_names + ['TRet'])
    func_type_params = ','.join(['CallSite'] + type_names + ['TRet'])
    func_type = 'Func<%s>' % func_type_params
  
    cw.enter_block('internal class DynamicInstruction<%s> : Instruction' % class_type_params)
    cw.write('private CallSite<%s> _site;' % func_type)
    cw.write('')    
    cw.enter_block('public static Instruction Factory(CallSiteBinder binder)')
    cw.write('return new DynamicInstruction<%s>(CallSite<%s>.Create(binder));' % (class_type_params, func_type))
    cw.exit_block()
    cw.write('')
    
    cw.enter_block('private DynamicInstruction(CallSite<%s> site)' % func_type)
    cw.write('_site = site;')
    cw.exit_block()
    cw.write('')
    
    cw.write('public override int ProducedStack { get { return 1; } }')
    cw.write('public override int ConsumedStack { get { return %d; } }' % n)
    cw.write('')
    
    gen_interpreted_run(cw, n)
    cw.write('')
    cw.enter_block('public override string ToString()')
    cw.write('return "Dynamic(" + _site.Binder.ToString() + ")";')
    cw.exit_block()
    
    cw.exit_block()
    cw.write('')
    
def gen_interpreted_run(cw, n):
    cw.enter_block('public override int Run(InterpretedFrame frame)')
    
    args = '_site'
    for i in range(0, n):
        args += ', (T%d)frame.Data[frame.StackIndex - %d]' % (i, n - i)
    
    cw.write('frame.Data[frame.StackIndex - %d] = _site.Target(%s);' % (n, args))\
    
    if n != 1:
        cw.write('frame.StackIndex -= %d;' % (n - 1))
    
    cw.write('return 1;')
    
    cw.exit_block()
    
def gen_types(cw):
    for i in range(MAX_TYPES):
        cw.write('case %d: genericType = typeof(DynamicInstruction<%s>); break;' %
                  (i+1, ''.join([',']*i)))
                  
def gen_untyped(cw):
    for i in range(MAX_TYPES):
        cw.write('case %d: return DynamicInstruction<%s>.Factory(binder);' % 
                  (i, ', '.join(['object']*(i+1))))
    
def gen_instructions(cw):
    for i in range(MAX_TYPES):
        gen_instruction(cw, i)
    

def gen_run_method(cw, n, is_void):
    type_params = ['T%d' % i for i in range(n)]
    param_names = ['T%d arg%d' % (i,i) for i in range(n)] 
    if is_void:
        ret_type = 'void'
        name_extra = 'Void'
    else:
        ret_type = 'TRet'
        name_extra = ''
        type_params.append(ret_type)
        
    if type_params: types = '<' + ','.join(type_params) + '>'
    else: types = ''
    
    cw.enter_block('internal %s Run%s%d%s(%s)' % (ret_type, name_extra, n,
                                                types, 
                                                ','.join(param_names)))
        
    cw.enter_block('if (_compiled != null || TryGetCompiled())')
    args = ', '.join(['arg%d' % i for i in range(n)])
    if is_void:
        cw.write('((Action%s)_compiled)(%s);' % (types, args))
        cw.write('return;')
    else:
        cw.write('return ((Func%s)_compiled)(%s);' % (types, args))
    cw.exit_block()
    cw.write('')
    cw.write('var frame = MakeFrame();')
    for i in range(n):
        cw.write('frame.Data[%d] = arg%d;' % (i,i))
    
    cw.write('var current = frame.Enter();')
    cw.write('try { _interpreter.Run(frame); } finally { frame.Leave(current); }')
    
    if not is_void: 
        cw.write('return (TRet)frame.Pop();')
        
    cw.exit_block()
    cw.write('')
    
def gen_run_maker(cw, n, is_void):
    type_params = ['T%d' % i for i in range(n)]
    
    if is_void:
        name_extra = 'Void'
        delegate_name = 'Action'
    else:
        type_params.append('TRet')
        name_extra = ''
        delegate_name = 'Func'

    if type_params: types = '<' + ','.join(type_params) + '>'
    else: types = ''

    cw.enter_block('internal static Delegate MakeRun%s%d%s(LightLambda lambda)' % (name_extra, n, types))
    cw.write('return new %s%s(lambda.Run%s%d%s);' % (delegate_name, types, name_extra, n, types, ));
    cw.exit_block()

def gen_run_methods(cw):
    cw.write('internal const int MaxParameters = %d;' % MAX_TYPES)
    for i in range(MAX_TYPES):
        gen_run_method(cw, i, False)
        gen_run_method(cw, i, True)
        gen_run_maker(cw, i, False)
        gen_run_maker(cw, i, True)
        

def gen_instructionlist_factory(cw):
    for i in range(1, MAX_TYPES):
        gen_args = ', '.join(get_type_names(i))
        cw.enter_block('public void EmitDynamic<%s, TRet>(CallSiteBinder binder)' % gen_args)
        cw.write('Emit(DynamicInstruction<%s, TRet>.Factory(binder));' % gen_args)
        cw.exit_block()
        cw.write('')
    
def main():
    return generate(
        ("Dynamic InstructionList Factory", gen_instructionlist_factory),
        ("LightLambda Run Methods", gen_run_methods),
        ("Dynamic Instructions", gen_instructions),
        ("Dynamic Instruction Types", gen_types),
        ("Untyped Dynamic Instructions", gen_untyped),
    )

if __name__ == "__main__":
    main()
