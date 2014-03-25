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

view_types = ['DictionaryKeyView', 'DictionaryItemView']
set_types = ['SetCollection', 'FrozenSetCollection']

ops = [
    ('|', 'Union'),
    ('&', 'Intersection'),
    ('^', 'SymmetricDifference'),
    ('-', 'Difference'),
]
comps = [
    ('==', 'xs.Count == ys.Count && xs.IsSubset(ys)'),
    ('!=', 'xs.Count != ys.Count || !xs.IsSubset(ys)'),
    ('>', 'ys.IsStrictSubset(xs)'),
    ('<', 'xs.IsStrictSubset(ys)'),
    ('>=', 'ys.IsSubset(xs)'),
    ('<=', 'xs.IsSubset(ys)'),
]

def equality(comp):
    return 'true' if comp != '!=' and '=' in comp else 'false'

def inequality(comp):
    return 'true' if '=' not in comp or comp == '!=' else 'false'

def gen_ops(ty):
    def _gen_ops(cw):
        for op, op_name in ops:
            for format_args in [
                (op, ty + ' x', 'IEnumerable y'),
                (op, 'IEnumerable y', ty + ' x'),
            ]:
                cw.enter_block('public static SetCollection operator %s(%s, %s)' % format_args)
                cw.writeline('return new SetCollection(SetStorage.%s(' % op_name)
                cw.indent()
                cw.writeline('SetStorage.GetItemsWorker(x.GetEnumerator()),')
                cw.writeline('SetStorage.GetItems(y)')
                cw.dedent()
                cw.writeline('));')
                cw.exit_block()
                cw.writeline()
    
    return _gen_ops

def gen_comps(ty):
    view_types_sorted = [ty] + [x for x in view_types if x != ty]
    def _gen_comps(cw):
        cw.enter_block('public override bool Equals(object obj)')
        cw.enter_block('if (obj == null)')
        cw.writeline('return false;')
        cw.exit_block()
        enter_block = cw.enter_block
        for check in view_types_sorted + set_types:
            enter_block('if (obj is %s)' % check)
            enter_block = cw.else_block
            cw.writeline('return this == (%s)obj;' % check)
        cw.exit_block()
        cw.writeline('return false;')
        cw.exit_block()
        cw.writeline()
        
        for right in view_types_sorted + set_types:
            for comp, expr in comps:
                cw.enter_block('public static bool operator %s(%s x, %s y)' % (comp, ty, right))
                if right == ty:
                    cw.enter_block('if (object.ReferenceEquals(x._dict, y._dict))')
                    cw.writeline('return %s;' % equality(comp))
                    cw.exit_block()
                elif right in view_types:
                    cw.enter_block('if (object.ReferenceEquals(x._dict, y._dict))')
                    cw.writeline('return %s;' % inequality(comp))
                    cw.exit_block()
                xs = 'SetStorage.GetItemsWorker(x.GetEnumerator())'
                if right in view_types:
                    ys = 'SetStorage.GetItemsWorker(y.GetEnumerator())'
                else:
                    ys = 'y._items'
                cw.writeline('SetStorage xs = %s;' % xs)
                cw.writeline('SetStorage ys = %s;' % ys)
                cw.writeline('return %s;' % expr)
                cw.exit_block()
                cw.writeline()
    
    return _gen_comps

def main():
    generators = [
        ('Set Operations (Keys)', gen_ops('DictionaryKeyView')),
        ('Set Comparison Operations (Keys)', gen_comps('DictionaryKeyView')),
        ('Set Operations (Items)', gen_ops('DictionaryItemView')),
        ('Set Comparison Operations (Items)', gen_comps('DictionaryItemView')),
    ]
    
    return generate(*generators)

if __name__ == '__main__':
    main()
