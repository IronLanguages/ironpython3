# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

def make_arg_list(size, name ='T%(id)d'):
    return ', '.join([name % {'id': x} for x in range(size)])

def get_base(size):
    if size: return 'MutableTuple<' + make_arg_list(size) + '>'
    return 'MutableTuple'

def gen_generic_args(i, type='object'):
    return ', '.join([type]*i)

def gen_tuple(cw, size, prevSize):
    cw.write('[GeneratedCode("DLR", "2.0")]')
    cw.enter_block('public class MutableTuple<%s> : %s' % (make_arg_list(size), get_base(prevSize)))

    cw.write('public MutableTuple() { }')
    cw.write('')
    cw.write('public MutableTuple(' + make_arg_list(size, 'T%(id)d item%(id)d') + ')')
    cw.enter_block('  : base(' + make_arg_list(prevSize, 'item%(id)d') + ')')

    for i in range(prevSize, size):
        cw.write('Item%03d = item%d;' % (i, i))

    cw.exit_block()
    cw.write('')
    for i in range(prevSize, size):
        cw.write('public T%d Item%03d { get; set; }' % (i, i))
        cw.write('')

    cw.enter_block('public override object GetValue(int index)')
    cw.enter_block('switch(index)')
    for i in range(0, size):
        cw.write('case %d: return Item%03d;' % (i, i))
    cw.write('default: throw new ArgumentOutOfRangeException(nameof(index));')
    cw.exit_block()
    cw.exit_block()

    cw.write('')
    cw.enter_block('public override void SetValue(int index, object value)')
    cw.enter_block('switch(index)')
    for i in range(0, size):
        cw.write('case %d: Item%03d = (T%d)value; break;' % (i, i, i))
    cw.write('default: throw new ArgumentOutOfRangeException(nameof(index));')
    cw.exit_block()
    cw.exit_block()

    cw.write('public override int Capacity => %d;' % size)

    cw.exit_block()

tuples = [1, 2, 4, 8, 16, 32, 64, 128]


def gen_tuples(cw):
    prevSize = 0
    for size in tuples:
        gen_tuple(cw, size, prevSize)
        prevSize = size

def gen_one_pgf(cw, i, first, last=False):
    if first: cw.enter_block("if (size <= %i)" % i)
    elif not last: cw.else_block("if (size <= %i)" % i)
    cw.writeline("return typeof(MutableTuple<%s>);" % gen_generic_args(i, ''))

def gen_get_size(cw):
    ssizes = sorted(tuples)

    first = True
    cw.enter_block("if (size <= MutableTuple.MaxSize)")
    for i in ssizes[:-1]:
        gen_one_pgf(cw, i, first)
        first = False
    cw.else_block()
    gen_one_pgf(cw, ssizes[-1], False, True)
    cw.exit_block()
    cw.exit_block()

def main():
    return generate(
        ("Tuples", gen_tuples),
        ("Tuple Get From Size", gen_get_size),
    )

if __name__ == "__main__":
    main()
