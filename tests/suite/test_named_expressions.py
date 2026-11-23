# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import ast
import unittest

from iptest import run_test

class NamedExpressionTest(unittest.TestCase):
    def test_scope(self):
        def direct_assignment():
            return (walrus_direct_local := 1), walrus_direct_local

        self.assertEqual(direct_assignment(), (1, 1))
        self.assertNotIn("walrus_direct_local", globals())

        def comprehension_assignment():
            values = [item for item in range(3) if (walrus_comprehension_local := item) >= 0]
            return values, walrus_comprehension_local

        self.assertEqual(comprehension_assignment(), ([0, 1, 2], 2))
        self.assertNotIn("walrus_comprehension_local", globals())

    def test_class_body(self):
        ns = {}
        exec("class A:\n    (x := 1)\n    y = x + 1", ns)
        self.assertEqual(ns["A"].x, 1)
        self.assertEqual(ns["A"].y, 2)

    def test_genexp_inner_loop_rebind(self):
        cases = [
            ("Inner reuse", "list(i for i in range(5) if (j := 0) for j in range(5))"),
            ("Inner unpacking reuse", "list(i for i in range(5) if (j := 0) for j, k in [(0, 1)])"),
        ]
        msg = "comprehension inner loop cannot rebind assignment expression target 'j'"
        for case, code in cases:
            with self.subTest(case=case):
                with self.assertRaisesRegex(SyntaxError, msg):
                    exec(code, {}) # Module scope
                with self.assertRaisesRegex(SyntaxError, msg):
                    exec(code, {}, {}) # Class scope
                with self.assertRaisesRegex(SyntaxError, msg):
                    exec(f"lambda: {code}", {}) # Function scope

    def test_unbound(self):
        def f(c):
            c and (x := 1)
            return x
        def g(c):
            c or (x := 1)
            return x
        def h(c):
            (x := 1) if c else 2
            return x
        def i(c):
            0 if c else (x := 1)
            return x
        def j(c):
            c < 0 < (x := 1)
            return x
        def k(c):
            [(x := 1) for _ in c]
            return x
        def l(c):
            if c:
                return x
            elif (x := 1):
                pass
            return x
        def m(c):
            if c:
                x = 1
            elif (y := 0):
                x = 2
            return x
        def n(c):
            try:
                assert c, (x := 1)
            except AssertionError:
                pass
            return x
        for func, ok, unbound in [(f, True, False), (g, False, True), (h, True, False), (i, False, True), (j, -1, 1), (k, [0], []), (l, False, True), (m, True, False), (n, False, True)]:
            with self.subTest(func=func.__name__):
                self.assertEqual(func(ok), 1)
                self.assertRaises(UnboundLocalError, func, unbound)

        def loop():
            n = 0
            while (x := n) < 3:
                n += 1
            del x
            return x
        self.assertRaises(UnboundLocalError, loop)

        def else_branch(c):
            if (x := c):
                pass
            elif (y := x + 1) > 1:
                pass
            else:
                return x, y
            return x
        self.assertEqual(else_branch(1), 1)
        self.assertEqual(else_branch(0), (0, 1))

    def test_invalid_target(self):
        for code in ["(x := 1) = 2", "del (x := 1)", "for (x := 1) in y: pass", "[0 for (x := 1) in y]", "with a as (x := 1): pass"]:
            with self.subTest(code=code):
                self.assertRaises(SyntaxError, compile, code, "", "exec")

    def test_fstring(self):
        for code in ["f'{[i for i in (w := [1])]}'", "f'{f\"{[i for i in (w := [1])]}\"}'", "f'{*a}'"]:
            with self.subTest(code=code):
                self.assertRaises(SyntaxError, compile, code, "", "exec")
        self.assertEqual(f'{(y := 1)}{y}', '11')

    def test_dict_key(self):
        self.assertRaises(SyntaxError, compile, "{a := 1: 2}", "", "exec")
        ns = {}
        exec("d = {(a := 1): 2}", ns)
        self.assertEqual(ns["d"], {1: 2})
        self.assertEqual(ns["a"], 1)

        def f():
            return {(n := 'x'): n}
        self.assertEqual(f(), {'x': 'x'})

    def test_ast(self):
        tree = ast.parse("(x := 1)")
        node = tree.body[0].value
        self.assertIsInstance(node, ast.NamedExpr)
        self.assertIsInstance(node.target, ast.Name)
        self.assertEqual(node.target.id, "x")
        self.assertIsInstance(node.target.ctx, ast.Store)

        ns = {}
        exec(compile(tree, "<ast>", "exec"), ns)
        self.assertEqual(ns["x"], 1)

        tree = ast.parse("r = any((w := x) > 1 for x in [1, 2])")
        ns = {}
        exec(compile(tree, "<ast>", "exec"), ns)
        self.assertEqual(ns["w"], 2)

        tree = ast.parse("list(i for i in range(5) if (j := 0) for j in range(5))")
        self.assertRaises(SyntaxError, compile, tree, "<ast>", "exec")

run_test(__name__)
