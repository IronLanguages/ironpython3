# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

def add_not_null(arg_t):
    return arg_t if arg_t.endswith("?") else "[NotNull] " + arg_t

def get_type(mutable):
    if mutable:
        return 'SetCollection'
    else:
        return 'FrozenSetCollection'

def get_arg_ts(mutable):
    return [get_type(mutable), get_type(not mutable), 'object?']

def get_clrname(name):
    return ''.join(map(str.capitalize, name.split('_')))

def get_items(arg_t):
    if arg_t == 'object?':
        return 'SetStorage.GetItems(set)'
    else:
        return 'set._items'

def copy(cw, mutable):
    if mutable:
        cw.writeline('return copy();')
    else:
        cw.writeline('return Make(_items);')

def copy_op(cw, mutable, name):
    t = get_type(mutable)

    cw.enter_block('public %s %s()' % (t, name))
    copy(cw, mutable)
    cw.exit_block()
    cw.writeline()

def simple_op(cw, t, arg_t, name):
    clrname = get_clrname(name)

    cw.enter_block('public %s %s(%s set)' % (t, name, add_not_null(arg_t)))
    simple_op_worker(cw, t, arg_t, name)
    cw.exit_block()
    cw.writeline()

def simple_op_worker(cw, t, arg_t, name):
    clrname = get_clrname(name)

    if arg_t == 'object?':
        cw.writeline('SetStorage items;')
        cw.enter_block('if (SetStorage.GetItems(set, out items))')
        cw.writeline('items = SetStorage.%s(_items, items);' % clrname)
        cw.else_block()
        cw.writeline('items.%sUpdate(_items);' % clrname)
        cw.exit_block()
        cw.writeline('return Make(items);')
    else:
        cw.writeline(
            'return Make(SetStorage.%s(_items, set._items));' % clrname
        )

def enter_multiarg_op(cw, t, name):
    cw.enter_block('public %s %s([NotNull] params object[]/*!*/ sets)' % (t, name))
    cw.writeline()

def union_multiarg(cw, mutable):
    t = get_type(mutable)
    enter_multiarg_op(cw, t, 'union')

    cw.writeline('SetStorage res = _items.Clone();')
    cw.enter_block('foreach (object set in sets)')
    cw.writeline('res.UnionUpdate(SetStorage.GetItems(set));')
    cw.exit_block()
    cw.writeline()
    cw.writeline('return Make(res);')

    cw.exit_block()
    cw.writeline()

def intersection_multiarg(cw, mutable):
    t = get_type(mutable)
    enter_multiarg_op(cw, t, 'intersection')

    cw.enter_block('if (sets.Length == 0)')
    copy(cw, mutable)
    cw.exit_block()
    cw.writeline()

    cw.writeline('SetStorage res = _items;')
    cw.enter_block('foreach (object set in sets)')
    cw.writeline('SetStorage items, x = res, y;')
    cw.enter_block('if (SetStorage.GetItems(set, out items))')
    cw.writeline('y = items;')
    cw.writeline('SetStorage.SortBySize(ref x, ref y);')
    cw.writeline()
    cw.enter_block('if (%s(x, items) || %s(x, _items))' %
        (('object.ReferenceEquals',) * 2))
    cw.writeline('x = x.Clone();')
    cw.exit_block()
    cw.else_block()
    cw.writeline('y = items;')
    cw.writeline('SetStorage.SortBySize(ref x, ref y);')
    cw.writeline()
    cw.enter_block('if (object.ReferenceEquals(x, _items))')
    cw.writeline('x = x.Clone();')
    cw.exit_block()
    cw.exit_block()
    cw.writeline('x.IntersectionUpdate(y);')
    cw.writeline('res = x;')
    cw.exit_block()
    cw.writeline()

    cw.writeline('Debug.Assert(!object.ReferenceEquals(res, _items));')
    cw.writeline('return Make(res);')

    cw.exit_block()
    cw.writeline()

def difference(cw, t, arg_t):
    items = get_items(arg_t)

    cw.enter_block('public %s difference(%s set)' % (t, add_not_null(arg_t)))

    if (t == arg_t):
        cw.enter_block('if (object.ReferenceEquals(set, this))')
        cw.writeline('return Empty;')
        cw.exit_block()
        cw.writeline()

    cw.writeline('return Make(')
    cw.indent()
    cw.writeline('SetStorage.Difference(_items, %s)' % items)
    cw.dedent()
    cw.writeline(');');

    cw.exit_block()
    cw.writeline()

def difference_multiarg(cw, mutable):
    t = get_type(mutable)
    enter_multiarg_op(cw, t, 'difference')

    cw.enter_block('if (sets.Length == 0)')
    copy(cw, mutable)
    cw.exit_block()
    cw.writeline()

    cw.writeline('SetStorage res = _items;')
    cw.enter_block('foreach (object set in sets)')
    cw.enter_block('if (object.ReferenceEquals(set, this))')
    cw.writeline('return Empty;')
    cw.exit_block()
    cw.writeline()
    cw.writeline('SetStorage items = SetStorage.GetItems(set);')
    cw.enter_block('if (object.ReferenceEquals(res, _items))')
    cw.writeline('res = SetStorage.Difference(_items, items);')
    cw.else_block()
    cw.writeline('res.DifferenceUpdate(items);')
    cw.exit_block()
    cw.exit_block()
    cw.writeline()

    cw.writeline('Debug.Assert(!object.ReferenceEquals(res, _items));')
    cw.writeline('return Make(res);')

    cw.exit_block()
    cw.writeline()

def symmetric_difference(cw, t, arg_t):
    cw.enter_block('public %s symmetric_difference(%s set)' % (t, add_not_null(arg_t)))

    if (t == arg_t):
        cw.enter_block('if (object.ReferenceEquals(set, this))')
        cw.writeline('return Empty;')
        cw.exit_block()
        cw.writeline()

    simple_op_worker(cw, t, arg_t, 'symmetric_difference')

    cw.exit_block()
    cw.writeline()

def gen_setops(mutable):
    def _gen_setops(cw):
        t = get_type(mutable)
        arg_ts = get_arg_ts(mutable)

        for arg_t in arg_ts:
            items = get_items(arg_t)
            cw.enter_block('public bool isdisjoint(%s set)' % add_not_null(arg_t))
            cw.writeline('return _items.IsDisjoint(%s);' % items)
            cw.exit_block()
            cw.writeline()

        for arg_t in arg_ts:
            items = get_items(arg_t)
            cw.enter_block('public bool issubset(%s set)' % add_not_null(arg_t))
            cw.writeline('return _items.IsSubset(%s);' % items)
            cw.exit_block()
            cw.writeline()

        for arg_t in arg_ts:
            items = get_items(arg_t)
            cw.enter_block('public bool issuperset(%s set)' % add_not_null(arg_t))
            cw.writeline('return %s.IsSubset(_items);' % items)
            cw.exit_block()
            cw.writeline()

        copy_op(cw, mutable, 'union')
        for arg_t in arg_ts:
            simple_op(cw, t, arg_t, 'union')
        union_multiarg(cw, mutable)

        copy_op(cw, mutable, 'intersection')
        for arg_t in arg_ts:
            simple_op(cw, t, arg_t, 'intersection')
        intersection_multiarg(cw, mutable)

        copy_op(cw, mutable, 'difference')
        for arg_t in arg_ts:
            difference(cw, t, arg_t)
        difference_multiarg(cw, mutable)

        for arg_t in arg_ts:
            symmetric_difference(cw, t, arg_t)

    return _gen_setops

op_symbols = [ '|', '&', '^', '-' ]
op_names = [ 'union', 'intersection', 'symmetric_difference', 'difference' ]
op_upnames = [ 'update' ] + [x + '_update' for x in op_names[1:]]
op_clrnames = [ 'BitwiseOr', 'BitwiseAnd', 'ExclusiveOr', 'Subtract' ]

def gen_op(cw, t_left, t_right, symbol, name):
    cw.enter_block(
        'public static %s operator %s(%s x, %s y)' %
        (t_left, symbol, add_not_null(t_left), add_not_null(t_right))
    )
    cw.writeline('return x.%s(y);' % name)
    cw.exit_block()
    cw.writeline()

def gen_ops(mutable):
    def _gen_ops(cw):
        t = get_type(mutable)
        u = get_type(not mutable)
        ops = list(zip(op_symbols, op_names))

        for symbol, name in ops:
            gen_op(cw, t, t, symbol, name)
        for symbol, name in ops:
            gen_op(cw, t, u, symbol, name)

    return _gen_ops

def gen_mutating_op(cw, t, arg_t, symbol, upname, clrname):
    cw.writeline('[SpecialName]')
    cw.enter_block('public %s InPlace%s(%s set)' % (t, clrname, add_not_null(arg_t)))

    if arg_t == 'object?':
        cw.enter_block(
            'if (set is %s || set is %s)' %
            tuple(map(get_type, [False, True]))
        )

    cw.writeline('%s(set);' % upname)
    cw.writeline('return this;')

    if arg_t == 'object?':
        cw.exit_block()
        cw.writeline()

        cw.writeline('throw PythonOps.TypeError(')
        cw.indent()
        cw.writeline(
            '''"unsupported operand type(s) for %s=: '{0}' and '{1}'",''' %
            symbol
        )
        cw.writeline('%s(this), %s(set)' % (('PythonTypeOps.GetName',) * 2))
        cw.dedent()
        cw.writeline(');')

    cw.exit_block()
    cw.writeline()

def gen_mutating_ops(cw):
    t = get_type(True)
    arg_ts = get_arg_ts(True)

    for op in zip(op_symbols, op_upnames, op_clrnames):
        for arg_t in arg_ts:
            gen_mutating_op(cw, t, arg_t, *op)

compares = [ '>', '<', '>=', '<=' ]

def is_subset(compare):
    return compare == '<' or compare == '<='

def is_strict(compare):
    return not compare.endswith('=')

def gen_comparison(cw, t, compare):
    cw.writeline('[return: MaybeNotImplemented]')
    cw.enter_block(
        'public static object operator %s([NotNull] %s self, object? other)' %
        (compare, t)
    )

    cw.enter_block('if (SetStorage.GetItemsIfSet(other, out SetStorage items))')
    if is_subset(compare):
        left = 'self._items'
        right = 'items'
    else:
        left = 'items'
        right = 'self._items'
    if is_strict(compare):
        func = 'IsStrictSubset'
    else:
        func = 'IsSubset'
    cw.writeline('return %s.%s(%s);' % (left, func, right))
    cw.exit_block()
    cw.writeline()

    cw.writeline('return NotImplementedType.Value;')

    cw.exit_block()
    cw.writeline()

def suppress(cw, *msgs):
    if len(msgs) == 0:
        return

    comma = ''
    res = '['
    for msg in msgs:
        res += comma + 'System.Diagnostics.CodeAnalysis.SuppressMessage('
        res += msg + ')'
        comma = ' ,'
    res += ']'

    cw.writeline(res)

def gen_comparisons(cw, t):
    cw.writeline('#region IRichComparable')
    cw.writeline()

    for compare in compares:
        gen_comparison(cw, t, compare)

    cw.writeline('#endregion')
    cw.writeline()

def gen_ienumerable(cw, mutable):
    cw.writeline('#region IEnumerable Members')
    cw.writeline()

    cw.enter_block('IEnumerator IEnumerable.GetEnumerator()')
    cw.writeline('return new SetIterator(_items, %s);' % str(mutable).lower())
    cw.exit_block()
    cw.writeline()

    cw.writeline('#endregion')
    cw.writeline()
    cw.writeline('#region IEnumerable<object?> Members')
    cw.writeline()

    cw.enter_block('IEnumerator<object?> IEnumerable<object?>.GetEnumerator()')
    cw.writeline('return new SetIterator(_items, %s);' % str(mutable).lower())
    cw.exit_block()
    cw.writeline()

    cw.writeline('#endregion')
    cw.writeline()

def gen_icodeformattable(cw):
    cw.writeline('#region ICodeFormattable Members')
    cw.writeline()

    cw.enter_block('public virtual string/*!*/ __repr__(CodeContext/*!*/ context)')
    cw.writeline('return SetStorage.SetToString(context, this, _items);')
    cw.exit_block()
    cw.writeline()

    cw.writeline('#endregion')
    cw.writeline()

def gen_icollection(cw):
    cw.writeline('#region ICollection Members')
    cw.writeline()

    cw.enter_block('void ICollection.CopyTo(Array array, int index)')
    cw.writeline('int i = 0;')
    cw.enter_block('foreach (var o in this)')
    cw.writeline('array.SetValue(o, index + i++);')
    cw.exit_block()
    cw.exit_block()
    cw.writeline()

    cw.enter_block('public int Count')
    cw.writeline('[PythonHidden]')
    cw.writeline('get { return _items.Count; }')
    cw.exit_block()
    cw.writeline()

    cw.enter_block('bool ICollection.IsSynchronized')
    cw.writeline('get { return false; }')
    cw.exit_block()
    cw.writeline()

    cw.enter_block('object ICollection.SyncRoot')
    cw.writeline('get { return this; }')
    cw.exit_block()
    cw.writeline()

    cw.writeline('#endregion')
    cw.writeline()

def gen_iweakreferenceable(cw):
    cw.writeline('#region IWeakReferenceable Members')
    cw.writeline()

    cw.writeline('private WeakRefTracker? _tracker;')
    cw.writeline()

    cw.enter_block('WeakRefTracker? IWeakReferenceable.GetWeakRef()')
    cw.writeline('return _tracker;')
    cw.exit_block()
    cw.writeline()

    cw.enter_block('bool IWeakReferenceable.SetWeakRef(WeakRefTracker value)')
    cw.writeline('return Interlocked.CompareExchange(ref _tracker, value, null) == null;')
    cw.exit_block()
    cw.writeline()

    cw.enter_block('void IWeakReferenceable.SetFinalizer(WeakRefTracker value)')
    cw.writeline('_tracker = value;')
    cw.exit_block()
    cw.writeline()

    cw.writeline('#endregion')
    cw.writeline()

def gen_interfaces(mutable):
    def _gen_interfaces(cw):
        t = get_type(mutable)

        gen_comparisons(cw, t)
        gen_ienumerable(cw, mutable)
        gen_icodeformattable(cw)
        gen_icollection(cw)
        gen_iweakreferenceable(cw)

    return _gen_interfaces

def main():
    generators = [
        ('NonOperator Operations', gen_setops),
        ('Operators', gen_ops),
        ('Interface Implementations', gen_interfaces),
    ]

    mutable_generators = [
        ('Mutating Operators', gen_mutating_ops),
    ]

    _generators = []
    for title, func in generators:
        for bit in [True, False]:
            _generators.append((
                title + ' (' + get_type(bit) + ')',
                func(bit)
            ))
    _generators.extend(mutable_generators)

    return generate(*_generators)

if __name__ == '__main__':
    main()
