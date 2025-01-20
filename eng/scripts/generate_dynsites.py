# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.


from generate import generate

MaxTypes = 16

def gen_delegate_func(cw):
    for i in range(MaxTypes + 1):
        cw.write("case %(length)d: return typeof(Func<%(targs)s>).MakeGenericType(types);", length = i + 1, targs = "," * i)

def gen_delegate_action(cw):
    for i in range(MaxTypes):
        cw.write("case %(length)d: return typeof(Action<%(targs)s>).MakeGenericType(types);", length = i + 1, targs = "," * i)

def gen_max_delegate_arity(cw):
    cw.write('private const int MaximumArity = %d;' % (MaxTypes + 1))

def main():
    return generate(
        ("Delegate Action Types", gen_delegate_action),
        ("Delegate Func Types", gen_delegate_func),
        ("Maximum Delegate Arity", gen_max_delegate_arity),
# outer ring generators
        ("Delegate Microsoft Scripting Action Types", gen_delegate_action),
        ("Delegate Microsoft Scripting Scripting Func Types", gen_delegate_func),
    )

if __name__ == "__main__":
    main()
