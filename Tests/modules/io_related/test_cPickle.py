# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# test cPickle low-level bytecode output
#

import os
import unittest

from io import BytesIO

from iptest import IronPythonTestCase, is_cli, long, path_modifier, run_test

# We test IronPython's cPickle bytecode output against CPython's pickle.py
# output, since pickle.py's output (specifically its memoization behavior) is
# more predictable than cPickle's. (CPython's cPickle relies on reference counts
# to determine whether or not to memoize objects, which is hard to reproduce on
# IronPython.)
import pickle as cPickle

# Acquire refs to interned strings in an attempt to force deterministic CPython
# memo behavior if testing against CPython's cPickle module.
# (see http://mail.python.org/pipermail/python-dev/2006-August/067882.html)
held_refs = [u'hey', u'Bob', 'name', 'age', u'state1', u'state2', u'arg1']

def sorted_dict_repr(obj, memo=None):
    if memo is None:
        memo = set()
    else:
        memo = memo.copy()
    memo.add(id(obj))

    kv_reprs = []
    for k in sorted(obj.keys()):
        v = obj[k]
        if isinstance(v, dict) and id(v) in memo:
            val_repr = '{...}'
        else:
            val_repr = normalized_repr(v, memo)

        kv_reprs.append('%s: %s' % (normalized_repr(k, memo), val_repr))

    result = '{' + (', '.join(kv_reprs)) + '}'
    return result

def normalized_repr(obj, memo=None):
    """Return repr of obj. If obj is a dict, return it in key-sorted order, with
    keys normalized to str if they're unicode. Otherwise return the standard
    repr of an object."""
    if isinstance(obj, dict):
        return sorted_dict_repr(obj, memo)
    else:
        return repr(obj)

class NewClass(object):
    def __repr__(self):
        if hasattr(self, '__getstate__'):
            state = repr(self.__getstate__())
        else:
            state = sorted_dict_repr(self.__dict__)
        return "<%s instance with state %s>" % (
            type(self).__name__, normalized_repr(state))
class NewClass_GetState(NewClass):
    def __getstate__(self):
        return (u'state1', u'state2')
    def __setstate__(self, state): pass
class NewClass_GetNewArgs(NewClass):
    newargs = (u'arg1', 2, 3)
    def __getnewargs__(self):
        return self.newargs
    def __new__(cls, *args):
        if args != cls.newargs:
            raise AssertionError('args != cls.newargs')
        return NewClass.__new__(cls)
class NewClass_GetState_GetNewArgs(NewClass_GetState, NewClass_GetNewArgs): pass

def global_function(): pass

class NamedObject:
    def __init__(self, name, obj):
        self.name = name
        self.obj = obj

class CLIOnly:
    def __init__(self, obj):
        self.obj = obj

class TestBank:
    def normalize(test):
        obj, expectations = test
        if isinstance(obj, CLIOnly):
            obj = obj.obj
        if isinstance(obj, NamedObject):
            display_name = '<<%s>>' % obj.name
            obj = obj.obj
        else:
            display_name = repr(obj)
        return obj, expectations, display_name
    normalize = staticmethod(normalize)
    selph = None
    hey = u'hey'
    long_smallest_neg = -2**(8*255-1)
    long_largest_pos = 2**(8*255-1)-1
    list_recursive = [1]
    list_recursive.append(list_recursive)
    dict_recursive = {0:u'hey'}
    dict_recursive[1] = dict_recursive
    newinst0 = NewClass()
    newinst1 = NewClass()
    newinst1.name = u'Bob'
    newinst1.age = 3
    newinst2 = NewClass_GetState()
    newinst3 = NewClass_GetState()
    newinst3.name = u'Bob'
    newinst4 = NewClass_GetNewArgs(*NewClass_GetNewArgs.newargs)
    newinst5 = NewClass_GetNewArgs(*NewClass_GetNewArgs.newargs)
    newinst5.name = u'Bob'
    newinst6 = NewClass_GetState_GetNewArgs(*NewClass_GetState_GetNewArgs.newargs)
    newinst7 = NewClass_GetState_GetNewArgs(*NewClass_GetState_GetNewArgs.newargs)
    newinst7.name = u'Bob'

    # Test description format:
    # (thing_to_pickle, {
    #     protocol_m: expected_output,
    #     protocol_n: (expected_alternative1, expected_alternative2),
    #     })
    #
    # If thing_to_pickle is a NamedObject instance, then the NamedObject's
    # 'name' attr will be printed and its 'obj' attr will be pickled.
    #
    # If thing_to_pickle is wrapped in a CLIOnly() object, then the test will be
    # run only when testing against IronPython.
    #
    # If there's only one expected alternative, you can just use that instead
    # of a tuple of possible alternatives.
    #
    # The highest protocol in the test description that is equal to or less than
    # the current protocol is used for matching. For example, the actual output
    # from protocol 2 would be matched with the expected output from protocol 1
    # if only protocols 1 and 0 were specified in the test description.
    #
    # In the expected output strings, any angle bracket-delimited sequence
    # (e.g. "<2>" or "<\x05>") is a wildcard that matches exactly one
    # character when testing the pickler. When testing the unpickler, the angle
    # brackets are stripped and the string is used otherwise unmodified. This
    # is to allow slightly fuzzy matching against memo reference numbers and
    # dictionary entries, which are nondeterministic. As a corrolary, this
    # means that you can't use strings that contain literal angle brackets.

    tests = [
        (None, {
            0:b'N.',
            }),
        (True, {
            0:b'I01\n.',
            2:b'\x88.',
            }),
        (False, {
            0:b'I00\n.',
            2:b'\x89.',
            }),
        (1, {
            0:b'L1L\n.',
            1:b'K\x01.',
            }),
        (-1, {
            0:b'L-1L\n.',
            1:b'J\xff\xff\xff\xff.',
            }),
        (256, {
            0:b'L256L\n.',
            1:b'M\x00\x01.',
            }),
        (2**30, {
            0:b'L1073741824L\n.',
            1:b'J\x00\x00\x00\x40.',
            }),
        (long(0), {
            0:b'L0L\n.',
            1:b'K\x00.',
            2:b'K\x00.',
            }),
        (long(1), {
            0:b'L1L\n.',
            1:b'K\x01.',
            2:b'K\x01.',
            }),
        (long(-1), {
            0:b'L-1L\n.',
            1:b'J\xff\xff\xff\xff.',
            2:b'J\xff\xff\xff\xff.',
            }),
        (NamedObject('smallest negative long', long_smallest_neg), {
            0:b'L-63119152483029311134208743532558499922742388026788054750254580913134092068101349400775784006880690358767027267425582069324452263965802580263844047629781802969982182358009757991699604981229789271086050074968881969290609802036366711253590028004836270450354777054758408286889796663166144157436625779538926534222488932401695981290400341380008924794640968818996722769683214178380910532633711551074723814187845931105358601012620815151559279594339152157038471900846264123490479852950820722119447464310412741151715903477845113154386713414751950465264697590604369795983597920768026571572887653525297164440538776584100773888L\n.',
            2:b'\x8a\xff\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x80.',
            }),
        (NamedObject('smallest negative long - 1', long_smallest_neg-1), {
            0:b'L-63119152483029311134208743532558499922742388026788054750254580913134092068101349400775784006880690358767027267425582069324452263965802580263844047629781802969982182358009757991699604981229789271086050074968881969290609802036366711253590028004836270450354777054758408286889796663166144157436625779538926534222488932401695981290400341380008924794640968818996722769683214178380910532633711551074723814187845931105358601012620815151559279594339152157038471900846264123490479852950820722119447464310412741151715903477845113154386713414751950465264697590604369795983597920768026571572887653525297164440538776584100773889L\n.',
            2:b'\x8b\x00\x01\x00\x00\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\x7f\xff.',
            }),
        (NamedObject('largest positive long', long_largest_pos), {
            0:b'L63119152483029311134208743532558499922742388026788054750254580913134092068101349400775784006880690358767027267425582069324452263965802580263844047629781802969982182358009757991699604981229789271086050074968881969290609802036366711253590028004836270450354777054758408286889796663166144157436625779538926534222488932401695981290400341380008924794640968818996722769683214178380910532633711551074723814187845931105358601012620815151559279594339152157038471900846264123490479852950820722119447464310412741151715903477845113154386713414751950465264697590604369795983597920768026571572887653525297164440538776584100773887L\n.',
            2:b'\x8a\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\xff\x7f.',
            }),
        (NamedObject('largest positive long + 1', long_largest_pos+1), {
            0:b'L63119152483029311134208743532558499922742388026788054750254580913134092068101349400775784006880690358767027267425582069324452263965802580263844047629781802969982182358009757991699604981229789271086050074968881969290609802036366711253590028004836270450354777054758408286889796663166144157436625779538926534222488932401695981290400341380008924794640968818996722769683214178380910532633711551074723814187845931105358601012620815151559279594339152157038471900846264123490479852950820722119447464310412741151715903477845113154386713414751950465264697590604369795983597920768026571572887653525297164440538776584100773888L\n.',
            2:b'\x8b\x00\x01\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x80\x00.',
            }),
        (0.0, {
            0:( b'F0.0\n.',
                b'F0\n.',
                ),
            1:b'G\x00\x00\x00\x00\x00\x00\x00\x00.',
            }),
        (1.123, {
            0:b'F1.123\n.',
            1:b'G?\xf1\xf7\xce\xd9\x16\x87+.',
            }),
        (-1.123, {
            0:b'F-1.123\n.',
            1:b'G\xbf\xf1\xf7\xce\xd9\x16\x87+.',
            }),
        (500.5, {
            0:b'F500.5\n.',
            1:b'G@\x7fH\x00\x00\x00\x00\x00.',
            }),
        (1e+017, {
            0:b'F1e+17\n.',
            1:b'GCv4W\x85\xd8\xa0\x00.',
            }),
        (1e-014, {
            0:b'F1e-14\n.',
            1:b'G=\x06\x84\x9b\x86\xa1+\x9b.',
            }),
        (u'hey\x00hey', {
            0:b'Vhey\x00hey\np<0>\n.',
            1:b'X\x07\x00\x00\x00hey\x00heyq<\x00>.',
            }),
        (u'hey\xffhey', {
            0:b'Vhey\xffhey\np<0>\n.',
            1:b'X\x08\x00\x00\x00hey\xc3\xbfheyq<\x00>.',
            }),
        (u'hey\u0100hey', {
            0:b'Vhey\\u0100hey\np<0>\n.',
            1:b'X\x08\x00\x00\x00hey\xc4\x80heyq<\x00>.',
            }),
        (u'hey\u07ffhey', {
            0:( b'Vhey\\u07ffhey\np<0>\n.',
                b'Vhey\\u07FFhey\np<0>\n.',
                ),
            1:b'X\x08\x00\x00\x00hey\xdf\xbfheyq<\x00>.',
            }),
        (u'hey\u0800hey', {
            0:b'Vhey\\u0800hey\np<0>\n.',
            1:b'X\t\x00\x00\x00hey\xe0\xa0\x80heyq<\x00>.',
            }),
        (u'hey\uffffhey', {
            0:( b'Vhey\\uffffhey\np<0>\n.',
                b'Vhey\\uFFFFhey\np<0>\n.',
                ),
            1:b'X\t\x00\x00\x00hey\xef\xbf\xbfheyq<\x00>.',
            }),
        ((), {
            0:b'(t.',
            1:b').',
            }),
        ((7,), {
            0:b'(L7L\ntp<0>\n.',
            1:b'(K\x07tq<\x00>.',
            2:b'K\x07\x85q<\x00>.',
            }),
        ((7,8), {
            0:b'(L7L\nL8L\ntp<0>\n.',
            1:b'(K\x07K\x08tq<\x00>.',
            2:b'K\x07K\x08\x86q<\x00>.',
            }),
        ((7,8,9), {
            0:b'(L7L\nL8L\nL9L\ntp<0>\n.',
            1:b'(K\x07K\x08K\ttq<\x00>.',
            2:b'K\x07K\x08K\t\x87q<\x00>.',
            }),
        ((7,8,9,10), {
            0:b'(L7L\nL8L\nL9L\nL10L\ntp<0>\n.',
            1:b'(K\x07K\x08K\tK\ntq<\x00>.',
            2:b'(K\x07K\x08K\tK\ntq<\x00>.',
            }),
        ((hey, hey), {
            0:b'(Vhey\np<0>\nVhey\np<1>\ntp<2>\n.' if is_cli else b'(Vhey\np<0>\ng<0>\ntp<1>\n.',
            1:b'(X\x03\x00\x00\x00heyq<\x00>h<\x00>tq<\x01>.',
            2:b'X\x03\x00\x00\x00heyq<\x00>h<\x00>\x86q<\x01>.',
            }),
        ([], {
            0:b'(lp<0>\n.',
            1:b']q<\x00>.',
            }),
        ([5], {
            0:b'(lp<0>\nL5L\na.',
            1:b']q<\x00>K\x05a.',
            }),
        ([5,6], {
            0:b'(lp<0>\nL5L\naL6L\na.',
            1:b']q<\x00>(K\x05K\x06e.',
            }),
        (list(range(8)), {
            0:b'(lp<0>\nL0L\naL1L\naL2L\naL3L\naL4L\naL5L\naL6L\naL7L\na.',
            1:b']q<\x00>(K\x00K\x01K\x02K\x03K\x04K\x05K\x06K\x07e.',
            }),
        (list(range(9)), {
            0:b'(lp<0>\nL0L\naL1L\naL2L\naL3L\naL4L\naL5L\naL6L\naL7L\naL8L\na.',
            1:b']q\x00(K\x00K\x01K\x02K\x03K\x04K\x05K\x06K\x07K\x08e.',
            }),
        (list(range(10)), {
            0:b'(lp<0>\nL0L\naL1L\naL2L\naL3L\naL4L\naL5L\naL6L\naL7L\naL8L\naL9L\na.',
            1:b']q\x00(K\x00K\x01K\x02K\x03K\x04K\x05K\x06K\x07K\x08K\te.',
            }),
        ([hey, hey], {
            0:b'(lp<0>\nVhey\np<1>\naVhey\np<2>\na.' if is_cli else b'(lp<0>\nVhey\np<1>\nag<1>\na.',
            1:b']q<\x00>(X\x03\x00\x00\x00heyq<\x01>h<\x01>e.',
            }),
        ({}, {
            0:b'(dp<0>\n.',
            1:b'}q<\x00>.',
            }),
        ({1:2, 3:4}, {
            0:( b'(dp<0>\nL1L\nL2L\nsL3L\nL4L\ns.',
                b'(dp<0>\nL3L\nL4L\nsL1L\nL2L\ns.',
                ),
            1:( b'}q<\x00>(K\x01K\x02K\x03K\x04u.',
                b'}q<\x00>(K\x03K\x04K\x01K\x02u.',
                ),
            }),
        # Dict keys and values are kept to single digits and bracketed below to
        # avoid problems with nondeterministic dictionary entry ordering.
        # Everywhere else in the tests that there are dicts we keep them to two
        # elements or fewer and test for both possible orderings.
        (dict([(x,x) for x in range(8)]), {
            0:b'(dp<0>\nL0L\nL0L\nsL1L\nL1L\nsL2L\nL2L\nsL3L\nL3L\nsL4L\nL4L\nsL5L\nL5L\nsL6L\nL6L\nsL7L\nL7L\ns.',
            1:b'}q<\x00>(K<\x00>K<\x00>K<\x01>K<\x01>K<\x02>K<\x02>K<\x03>K<\x03>K<\x04>K<\x04>K<\x05>K<\x05>K<\x06>K<\x06>K<\x07>K<\x07>u.',
            }),
        (dict([(x,x) for x in range(9)]), {
            0:b'(dp<0>\nL0L\nL0L\nsL1L\nL1L\nsL2L\nL2L\nsL3L\nL3L\nsL4L\nL4L\nsL5L\nL5L\nsL6L\nL6L\nsL7L\nL7L\nsL8L\nL8L\ns.',
            1:b'}q\x00(K\x00K\x00K\x01K\x01K\x02K\x02K\x03K\x03K\x04K\x04K\x05K\x05K\x06K\x06K\x07K\x07K\x08K\x08u.',
            }),
        (dict([(x,x) for x in range(10)]), {
            0:b'(dp<0>\nL0L\nL0L\nsL1L\nL1L\nsL2L\nL2L\nsL3L\nL3L\nsL4L\nL4L\nsL5L\nL5L\nsL6L\nL6L\nsL7L\nL7L\nsL8L\nL8L\nsL9L\nL9L\ns.',
            1:b'}q\x00(K\x00K\x00K\x01K\x01K\x02K\x02K\x03K\x03K\x04K\x04K\x05K\x05K\x06K\x06K\x07K\x07K\x08K\x08K\tK\tu.',
            }),
        ({hey: hey}, {
            0:b'(dp<0>\nVhey\np<1>\nVhey\np<2>\ns.' if is_cli else b'(dp<0>\nVhey\np<1>\ng<1>\ns.',
            1:b'}q<\x00>X\x03\x00\x00\x00heyq<\x01>h<\x01>s.',
            }),
        (list_recursive, {
            0:b'(lp<0>\nL1L\nag0\na.',
            1:b']q<\x00>(K\x01h<\x00>e.',
            }),
        (dict_recursive, {
            0:( b'(dp<0>\nL0L\nVhey\np<1>\nsL1L\ng<0>\ns.',
                b'(dp<0>\nL1L\ng<0>\nsL0L\nVhey\np<1>\ns.',
                ),
            1:( b'}q<\x00>(K\x00X\x03\x00\x00\x00heyq<\x01>K\x01h<\x00>u.',
                b'}q<\x00>(K\x01h<\x00>K\x00X\x03\x00\x00\x00heyq<\x01>u.',
                ),
            }),

        (NewClass, {
            0:b'c%s\nNewClass\np<0>\n.'.replace(b"%s", __name__.encode("ascii")),
            1:b'c%s\nNewClass\nq<\x00>.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (global_function, {
            0:b'c%s\nglobal_function\np<0>\n.'.replace(b"%s", __name__.encode("ascii")),
            1:b'c%s\nglobal_function\nq<\x00>.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (len, {
            0:b'c__builtin__\nlen\np<0>\n.',
            1:b'c__builtin__\nlen\nq<\x00>.',
            3:b'cbuiltins\nlen\nq<\x00>.',
            }),

        (newinst0, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>.'.replace(b"%s", __name__.encode("ascii")),
            2:(b'c%s\nNewClass\nq\x00)\x81q\x01.').replace(b"%s", __name__.encode("ascii")),
            }),
        (newinst1, {
            0:( b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nVname\np<6>\nVBob\np<7>\nsVage\np<8>\nL3L\nsb.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nVage\np<6>\nL3L\nsVname\np<7>\nVBob\np<8>\nsb.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nS\'name\'\np<6>\nVBob\np<7>\nsS\'age\'\np<8>\nL3L\nsb.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nS\'age\'\np<6>\nL3L\nsS\'name\'\np<7>\nVBob\np<8>\nsb.'.replace(b"%s", __name__.encode("ascii")),
                ),
            1:( b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>(X\x04\x00\x00\x00nameq<\x06>X\x03\x00\x00\x00Bobq<\x07>X\x03\x00\x00\x00ageq<\x08>K\x03ub.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>(X\x03\x00\x00\x00ageq<\x06>K\x03X\x04\x00\x00\x00nameq<\x07>X\x03\x00\x00\x00Bobq<\x08>ub.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>(U\x04nameq<\x06>X\x03\x00\x00\x00Bobq<\x07>U\x03ageq<\x08>K\x03ub.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>(U\x03ageq<\x06>K\x03U\x04nameq<\x07>X\x03\x00\x00\x00Bobq<\x08>ub.'.replace(b"%s", __name__.encode("ascii")),
                ),
            2:( b'c%s\nNewClass\nq<\x00>)\x81q<\x01>}q<\x02>(X\x04\x00\x00\x00nameq<\x03>X\x03\x00\x00\x00Bobq<\x04>X\x03\x00\x00\x00ageq<\x05>K\x03ub.'.replace(b"%s", __name__.encode("ascii")),
                b'c%s\nNewClass\nq<\x06>)\x81q<\x07>}q<\x08>(X\x03\x00\x00\x00ageq<\x00>K\x03X\x04\x00\x00\x00nameq<\x01>X\x03\x00\x00\x00Bobq<\x02>ub.'.replace(b"%s", __name__.encode("ascii")),
                b'c%s\nNewClass\nq<\x03>)\x81q<\x04>}q<\x05>(U\x04nameq<\x06>X\x03\x00\x00\x00Bobq<\x07>U\x03ageq<\x08>K\x03ub.'.replace(b"%s", __name__.encode("ascii")),
                b'c%s\nNewClass\nq<\x00>)\x81q<\x01>}q<\x02>(U\x03ageq<\x03>K\x03U\x04nameq<\x04>X\x03\x00\x00\x00Bobq<\x05>ub.'.replace(b"%s", __name__.encode("ascii")),
                ),
            }),
        (newinst2, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetState\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(Vstate1\np<5>\nVstate2\np<6>\ntp<7>\nb.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetState\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>(X\x06\x00\x00\x00state1q<\x05>X\x06\x00\x00\x00state2q<\x06>tq<\x07>b.'.replace(b"%s", __name__.encode("ascii")),
            2:b'c%s\nNewClass_GetState\nq<\x00>)\x81q<\x01>X\x06\x00\x00\x00state1q<\x02>X\x06\x00\x00\x00state2q<\x03>\x86q<\x04>b.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (newinst3, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetState\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(Vstate1\np<5>\nVstate2\np<6>\ntp<7>\nb.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetState\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>(X\x06\x00\x00\x00state1q<\x05>X\x06\x00\x00\x00state2q<\x06>tq<\x07>b.'.replace(b"%s", __name__.encode("ascii")),
            2:b'c%s\nNewClass_GetState\nq<\x00>)\x81q<\x01>X\x06\x00\x00\x00state1q<\x02>X\x06\x00\x00\x00state2q<\x03>\x86q<\x04>b.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (newinst4, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetNewArgs\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetNewArgs\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>.'.replace(b"%s", __name__.encode("ascii")),
            2:b'c%s\nNewClass_GetNewArgs\nq\x00X\x04\x00\x00\x00arg1q\x01K\x02K\x03\x87q\x02\x81q\x03.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (newinst5, {
            0:( b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetNewArgs\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nS\'name\'\np<6>\nVBob\np<7>\nsb.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetNewArgs\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(dp<5>\nVname\np<6>\nVBob\np<7>\nsb.'.replace(b"%s", __name__.encode("ascii")),
                ),
            1:( b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetNewArgs\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>U\x04nameq<\x06>X\x03\x00\x00\x00Bobq<\x07>sb.'.replace(b"%s", __name__.encode("ascii")),
                b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetNewArgs\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>}q<\x05>X\x04\x00\x00\x00nameq<\x06>X\x03\x00\x00\x00Bobq<\x07>sb.'.replace(b"%s", __name__.encode("ascii")),
                ),
            2:( b'c%s\nNewClass_GetNewArgs\nq<\x00>X\x04\x00\x00\x00arg1q<\x01>K\x02K\x03\x87q<\x02>\x81q<\x03>}q<\x04>U\x04nameq<\x05>X\x03\x00\x00\x00Bobq<\x06>sb.'.replace(b"%s", __name__.encode("ascii")),
                b'c%s\nNewClass_GetNewArgs\nq<\x00>X\x04\x00\x00\x00arg1q<\x01>K\x02K\x03\x87q<\x02>\x81q<\x03>}q<\x04>X\x04\x00\x00\x00nameq<\x05>X\x03\x00\x00\x00Bobq<\x06>sb.'.replace(b"%s", __name__.encode("ascii")),
                ),
            }),
        (newinst6, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetState_GetNewArgs\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(Vstate1\np<5>\nVstate2\np<6>\ntp<7>\nb.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetState_GetNewArgs\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>(X\x06\x00\x00\x00state1q<\x05>X\x06\x00\x00\x00state2q<\x06>tq<\x07>b.'.replace(b"%s", __name__.encode("ascii")),
            2:b'c%s\nNewClass_GetState_GetNewArgs\nq<\x00>X\x04\x00\x00\x00arg1q<\x01>K\x02K\x03\x87q<\x02>\x81q<\x03>X\x06\x00\x00\x00state1q<\x04>X\x06\x00\x00\x00state2q<\x05>\x86q<\x06>b.'.replace(b"%s", __name__.encode("ascii")),
            }),
        (newinst7, {
            0:b'ccopy_reg\n_reconstructor\np<0>\n(c%s\nNewClass_GetState_GetNewArgs\np<1>\nc__builtin__\nobject\np<2>\nNtp<3>\nRp<4>\n(Vstate1\np<5>\nVstate2\np<6>\ntp<7>\nb.'.replace(b"%s", __name__.encode("ascii")),
            1:b'ccopy_reg\n_reconstructor\nq<\x00>(c%s\nNewClass_GetState_GetNewArgs\nq<\x01>c__builtin__\nobject\nq<\x02>Ntq<\x03>Rq<\x04>(X\x06\x00\x00\x00state1q<\x05>X\x06\x00\x00\x00state2q<\x06>tq<\x07>b.'.replace(b"%s", __name__.encode("ascii")),
            2:b'c%s\nNewClass_GetState_GetNewArgs\nq<\x00>X\x04\x00\x00\x00arg1q<\x01>K\x02K\x03\x87q<\x02>\x81q<\x03>X\x06\x00\x00\x00state1q<\x04>X\x06\x00\x00\x00state2q<\x05>\x86q<\x06>b.'.replace(b"%s", __name__.encode("ascii")),
            }),
        ]
    if is_cli: #https://github.com/IronLanguages/main/issues/853
        tests.append((0.33333333333333331,
                      {
                       0:b'F0.33333333333333331\n.',
                       1:b'G?\xd5UUUUUU.',
                      }
                     ))
    else:
        tests.append((0.33333333333333331,
                      {
                       0:b'F0.33333333333333331\n.',
                       1:b'G?\xd5UUUUUU.',
                      }
                     ))

class MyData(object):
    def __init__(self, value):
        self.value = value

def persistent_id(obj):
    if hasattr(obj, 'value'):
        return 'MyData: %s' % obj.value
    return None

def persistent_load(id):
    return MyData(id[8:])

class CPickleTest(IronPythonTestCase):
    def test_pickler(self, module=cPickle, verbose=True):
        def get_expected(expectations, proto):
            while proto >= 0:
                try:
                    return expectations[proto]
                except KeyError:
                    proto -= 1

            raise RuntimeError(
                "no expectation found in %r for protocol %d" % (expectations, proto)
                )

        def match(pattern, text):
            import re

            if isinstance(pattern, bytes):
                patterns = re.split(br'<.*?>', pattern)
            else:
                patterns = re.split(r'<.*?>', pattern)
            curpos = 0

            if len(text) != sum([len(p) for p in patterns]) + len(patterns) - 1: return False

            for pattern in patterns:
                if text[curpos:curpos+len(pattern)] != pattern:
                    return False
                curpos += len(pattern) + 1

            return True

        self.assertTrue(match('a', 'a'))
        self.assertTrue(not match('a<1>', 'a'))
        self.assertTrue(match('a<1>', 'ab'))
        self.assertTrue(not match('a', 'b'))
        self.assertTrue(match('a<1>c', 'abc'))
        self.assertTrue(match('a<1>c', 'abc'))
        self.assertTrue(match('<1>', 'a'))
        self.assertTrue(match('<1><1>', 'xy'))
        self.assertTrue(not match('<1><1>', 'xyz'))

        s = BytesIO()

        picklers = [
            module.Pickler(s, protocol=0),
            module.Pickler(s, protocol=1),
            module.Pickler(s, protocol=2),
            module.Pickler(s, protocol=3),
            ]

        TestBank.selph = self
        for test in TestBank.tests:
            obj, expectations = test
            if isinstance(obj, CLIOnly):
                if not is_cli:
                    continue
                else:
                    obj = obj.obj

            obj, _unused, display_name = TestBank.normalize(test)

            if verbose: print("Testing %s..." % display_name.encode("utf-8"), end=" ")
            for proto, pickler in enumerate(picklers):
                s.seek(0)
                s.truncate()
                pickler.clear_memo()
                pickler.dump(obj)
                if verbose: print(proto, end=" ")
                expected = get_expected(expectations, proto)
                if not isinstance(expected, tuple):
                    expected = (expected,)
                actual = s.getvalue()
                # ignore protocol opcodes so that we can use the same literal
                # representation in the test bank
                if proto == 2:
                    self.assertEqual(actual[:2], b"\x80\x02")
                    actual = actual[2:]
                elif proto == 3:
                    self.assertEqual(actual[:2], b"\x80\x03")
                    actual = actual[2:]
                for pattern in expected:
                    if match(pattern, actual):
                        break
                else:
                    if len(expected) == 1:
                        self.fail('expected\n%r, got\n%r' % (expected[0], actual))
                    else:
                        self.fail('expected one of\n%r, got\n%r' % (expected, actual))

            if verbose: print('ok')


        if verbose: print("Tests completed")

    def test_unpickler(self, module=cPickle, verbose=True):
        s = BytesIO()
        TestBank.selph = self
        for test in TestBank.tests:
            obj, pickle_lists, display_name = TestBank.normalize(test)
            expected = normalized_repr(obj)

            if verbose: print("Testing %s..." % display_name.encode("utf-8"), end=" ")

            for proto in range(3):
                if proto not in pickle_lists:
                    continue

                if verbose: print(proto, end=" ")

                pickles = pickle_lists[proto]
                if not isinstance(pickles, tuple):
                    pickles = (pickles,)

                pickle_num = 0

                for pickle in pickles:
                    unpickler = module.Unpickler(s)
                    s.seek(0)
                    s.truncate()
                    s.write(pickle.replace(b'<', b'').replace(b'>', b''))
                    s.seek(0)
                    unpickled_obj = unpickler.load()
                    actual = normalized_repr(unpickled_obj)

                    if expected != actual:
                        print()
                        self.fail('Wrong unpickled value:\n'
                            'with pickle %d %r\n'
                            'expected\n'
                            '%r, got\n'
                            '%r' % (pickle_num, pickle, expected, actual))

                    pickle_num += 1

            if verbose: print('ok')

    def test_persistent_load(self):
        class MyData(object):
            def __init__(self, value):
                self.value = value

        def persistent_id(obj):
            if hasattr(obj, 'value'):
                return 'MyData: %s' % obj.value
            return None

        def persistent_load(id):
            return MyData(id[8:])


        src = BytesIO()
        p = cPickle.Pickler(src)
        p.persistent_id = persistent_id

        value = MyData('abc')
        p.dump(value)

        up = cPickle.Unpickler(BytesIO(src.getvalue()))
        up.persistent_load = persistent_load
        res = up.load()

        self.assertEqual(res.value, value.value)

        # errors
        src = BytesIO()
        p = cPickle.Pickler(src)
        p.persistent_id = persistent_id

        value = MyData('abc')
        p.dump(value)

        up = cPickle.Unpickler(BytesIO(src.getvalue()))

        # exceptions vary between cPickle & Pickle
        try:
            up.load()
            self.assertUnreachable()
        except Exception as e:
            pass

    def test_pers_load(self):
        src = BytesIO()
        p = cPickle.Pickler(src)
        p.persistent_id = persistent_id

        value = MyData('abc')
        p.dump(value)

        up = cPickle.Unpickler(BytesIO(src.getvalue()))
        up.persistent_load = persistent_load
        res = up.load()

        self.assertEqual(res.value, value.value)

        # errors
        src = BytesIO()
        p = cPickle.Pickler(src)
        p.persistent_id = persistent_id

        value = MyData('abc')
        p.dump(value)

        up = cPickle.Unpickler(BytesIO(src.getvalue()))

        # exceptions vary betwee cPickle & Pickle
        try:
            up.load()
            self.assertUnreachable()
        except Exception as e:
            pass

    def test_loads_negative(self):
        self.assertRaises(EOFError, cPickle.loads, b"")

    def test_load_negative(self):
        if cPickle.__name__ == "cPickle":   # pickle vs. cPickle report different exceptions, even on Cpy
            filename = os.tempnam()
            for temp in ['\x02', "No"]:
                self.write_to_file(filename, content=temp)
                f = open(filename)
                self.assertRaises(cPickle.UnpicklingError, cPickle.load, f)
                f.close()

    def test_cp15803(self):
        import os
        _testdir = 'cp15803'
        pickle_file = os.path.join(self.test_dir, "cp15803.pickle")

        module_txt = """
class K%s(object):
    static_member = 1
    def __init__(self):
        self.member = 2

%s = K%s()
"""

        _testdir_init       = os.path.join(self.test_dir, _testdir, '__init__.py')
        self.write_to_file(_testdir_init, module_txt % ("", "FROM_INIT", ""))

        _testdir_mod        = os.path.join(self.test_dir, _testdir, 'mod.py')
        self.write_to_file(_testdir_mod, module_txt % ("Mod", "FROM_MOD", "Mod") + """
from cp15803 import K
FROM_INIT_IN_MOD = K()
"""
        )

        _testdir_sub        = os.path.join(self.test_dir, _testdir, 'sub')
        _testdir_sub_init   = os.path.join(_testdir_sub, '__init__.py')
        self.write_to_file(_testdir_sub_init)

        _testdir_sub_submod = os.path.join(_testdir_sub, 'submod.py')
        self.write_to_file(_testdir_sub_submod, module_txt % ("SubMod", "FROM_SUB_MOD", "SubMod") + """
from cp15803 import mod
FROM_MOD_IN_SUBMOD = mod.KMod()
"""
        )

        with path_modifier(self.test_dir) as p:
            import cp15803
            import cp15803.mod
            import cp15803.sub.submod
            import cp15803.sub.submod as newname

            try:
                for x in [
                            cp15803.K(), cp15803.FROM_INIT,
                            cp15803.mod.KMod(), cp15803.mod.FROM_MOD, cp15803.mod.K(), cp15803.mod.FROM_INIT_IN_MOD,
                            cp15803.sub.submod.KSubMod(), cp15803.sub.submod.FROM_SUB_MOD, cp15803.sub.submod.FROM_MOD_IN_SUBMOD,
                            cp15803.sub.submod.mod.KMod(), cp15803.sub.submod.mod.FROM_MOD, cp15803.sub.submod.mod.K(), cp15803.sub.submod.mod.FROM_INIT_IN_MOD,

                            newname.KSubMod(), newname.FROM_SUB_MOD, newname.FROM_MOD_IN_SUBMOD,
                            newname.mod.KMod(), newname.mod.FROM_MOD, newname.mod.K(), newname.mod.FROM_INIT_IN_MOD,
                            ]:
                    with open(pickle_file, "wb") as f:
                        cPickle.dump(x, f)

                    with open(pickle_file, "rb") as f:
                        x_unpickled = cPickle.load(f)

                        self.assertEqual(x.__class__.__name__, x_unpickled.__class__.__name__)
                        self.assertEqual(x.static_member, x_unpickled.static_member)
                        self.assertEqual(x.member, x_unpickled.member)

            finally:
                import os
                try:
                    os.unlink(pickle_file)
                    self.clean_directory(os.path.join(self.test_dir, _testdir))
                except:
                    pass

    def test_cp945(self):
        #--sanity
        try:
            x = 1/0
            Fail("should have been division by zero error")
        except Exception as e:
            ex = e

        x_pickled = cPickle.dumps(ex)
        x_unpickled = cPickle.loads(x_pickled)
        self.assertEqual(x_unpickled.args[0], ex.args[0])

        #--comprehensive
        import builtins

        special_types = [UnicodeTranslateError, UnicodeEncodeError, UnicodeDecodeError]
        exception_types = [x for x in builtins.__dict__.values() if isinstance(x, type) and issubclass(x, BaseException) and x not in special_types]

        for exception_type in exception_types:
            except_list = [exception_type(), exception_type("a single param")]

            for t_except in except_list:
                try:
                    raise t_except
                except exception_type as e:
                    ex = e

                x_pickled = cPickle.dumps(ex)
                x_unpickled = cPickle.loads(x_pickled)
                if x_unpickled.args:
                    self.assertEqual(x_unpickled.args[0], ex.args[0])
                else:
                    self.assertFalse(ex.args)

        #--special cases
        for e in [  UnicodeEncodeError("1", u"2", 3, 4, "5"),
                    UnicodeDecodeError("1", b"2", 3, 4, "5"),
                    UnicodeTranslateError(u"1", 2, 3, "4")
                    ]:
            x_pickled = cPickle.dumps(e)
            x_unpickled = cPickle.loads(x_pickled)
            self.assertEqual(x_unpickled.object, e.object)

    def test_carriage_return_round_trip(self):
        # pickler shouldn't use ASCII and strip off \r
        self.assertEqual(cPickle.loads(cPickle.dumps('\r\n')), '\r\n')

    def test_metaclass_mixed_new_old_style(self):
        class mc(type): pass

        class mo(object): __metaclass__ = mc

        class c:
            def f(self): pass

        c.of = c.f

        global d
        class d(c, mo): pass

        self.assertEqual(type(cPickle.loads(cPickle.dumps(d()))), d)

run_test(__name__)
