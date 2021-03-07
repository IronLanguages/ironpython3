# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import types
import unittest

from iptest import run_test

x = 123456      # global to possibly mislead the closures

class ClosureTest(unittest.TestCase):

    def test_simple_cases(self):
        def f():
            x = 1
            def g():
                return x
            return g

        self.assertEqual(f()(), 1)

        def f():
            x = 2
            def g(y):
                return x + y
            return g

        self.assertEqual(f()(3), 5)

        def f(y):
            x = 3
            def g():
                return x + y
            return g

        self.assertEqual(f(5)(), 8)

        def f(x,y):
            def g():
                return x**2 + y**2
            return g

        self.assertEqual(f(2,3)(), 13)

        def f(p):
            x = 3
            def g(q):
                y = 5
                def h(r):
                    z = 7
                    def i(s):
                        return x,y,z,p,q,r,s
                    return i(13)
                return h(11)
            return g(9)
        self.assertEqual(f(17), (3, 5, 7, 17, 9, 11, 13))

    def test_binding(self):
        def f():
            def g():
                def h():
                    return x
                x = 13
                return h
            x = 17
            return g

        self.assertEqual(f()()(), 13)


        def f():
            def g():
                def h(x):
                    return x
                x = 13
                return h
            x = 17
            return g

        self.assertEqual(f()()(19), 19)

        def f():
            def g():
                x = p
                def h(y):
                    return x + y
                return h

            p = 3
            three = g()
            p = 5
            five = g()
            p = 7
            seven = g()

            return three(4), five(4), seven(4)

        self.assertEqual(f(), (7, 9, 11))

    def test_skip_levels(self):
        def f():
            x = 4
            def g():
                def h():
                    return x + 2
                return h
            return g

        self.assertEqual(f()()(),6)

        def f():
            x = 5
            def g():
                def h():
                    def i():
                        return x + 3
                    return i
                return h
            return g

        self.assertEqual(f()()()(), 8)

        def f():
            x = 6
            def g():
                def h():
                    def i():
                        return x
                    return i()
                return h()
            return g()

        self.assertEqual(f(), 6)

    def test_recursion(self):
        def f():
            y = []
            def g(x):
                y.append(x)
                if (x > 0): g(x - 1)
            g(10)
            return y

        self.assertEqual(f(), [10,9,8,7,6,5,4,3,2,1,0])

    def test_classes(self):
        def f():
            x = 23
            class c:
                y = x
            return c()

        self.assertEqual(f().y, 23)

        def f():
            x = 23
            class c:
                def m(self):
                    return x
            x = 29
            return c().m()

        self.assertEqual(f(), 29)

    def test_generators(self):
        def f():
            x = 10
            class c:
                def m(self):
                    def n():
                        return i
                    for i in range(x):
                        yield n()
            return c()

        self.assertEqual(list(f().m()), [0,1,2,3,4,5,6,7,8,9])

        def f(i):
            def g(j):
                for k in range(i+j):
                    def h():
                        return k
                    yield h()
            return g

        self.assertEqual(list(f(3)(5)), [0, 1, 2, 3, 4, 5, 6, 7])

    def test_lambda_and_self(self):
        class C:
            def __init__(self):
                self.setm (lambda: self.m ('lambda and self test'))
            def m(self, t):
                return t
            def setm(self, n):
                self.n = n

        self.assertEqual(C().n(), "lambda and self test")

    def test_global(self):
        global x
        global y
        global z
        class c:
            x = 5
            y = x
            x = 7
            z = x

        self.assertEqual(c.y, 5)
        self.assertEqual(x, 123456)
        self.assertEqual(c.z, c.x)
        self.assertEqual(c.x, 7)

        class c:
            global x
            self.assertEqual(x, 123456)

            def f(self):
                return x

        self.assertEqual(c().f(), 123456)

        def f():
            global x
            def g():
                def h():
                    return x
                return h
            x = 654321
            return g
        self.assertEqual(f()()(), 654321)


        def f():
            x = 10
            class c:
                x = 5

                def m(self):
                    return x
            return c()
        self.assertEqual(f().m(), 10)

        def f():
            def g():
                print(a)
            g()
            a = 10
        self.assertRaises(NameError, f)

        x = 123456
        def f():
            x = 123
            def g():
                global x
                self.assertEqual(x, 123456)
                def h():
                    return x
                return h()
            return g()

        self.assertEqual(f(), 123456)

        def f():
            x = 7
            def g():
                global x
                def h():
                    return x
                return h()
            return g()
        self.assertEqual(f(), 123456)


        y = 654321
        def f():
            [x, y] = 3, 7
            def g():
                self.assertEqual(x, 3)
                self.assertEqual(y, 7)
            g()
            self.assertEqual(x, 3)
            self.assertEqual(y, 7)
            return x, y
        self.assertEqual(f(), (3, 7))
        self.assertEqual(x, 123456)
        self.assertEqual(y, 654321)


        def f():
            def f1():
                [a, b] = [2,3]
                self.assertEqual(a, 2)
                self.assertEqual(b, 3)
            f1()
            a = 3
            self.assertEqual(a, 3)
            del a
        f()


        x = "global x"
        y = "global y"
        z = "global z"

        def test():
            self.assertEqual(y, "global y")
            exec("y = 10", globals())
            self.assertEqual(y, 10)
        test()

        def test2():
            self.assertEqual(x, "global x")
            exec("x = 5", globals())
            self.assertEqual(x, 5)
            yield x

        self.assertEqual(next(test2()), 5)

        class C:
            self.assertEqual(z, "global z")
            exec("z = 7", globals())
            self.assertEqual(z, 7)

    def test_gh817(self):
        # https://github.com/IronLanguages/ironpython3/issues/817

        for x in [lambda: i for i in [1,2]]:
            self.assertEqual(x(), 2)

        self.assertEqual([tuple(i for j in [1]) for i in [1]], [(1,)])
        self.assertEqual({tuple(i for j in [1]) for i in [1]}, {(1,)})

        self.assertEqual({i: tuple(j for j in t if i != j) for t in ((1,2),) for i in t}, {1: (2,), 2: (1,)})

        self.assertEqual([lambda: i for i in [1]][0](), 1)

        self.assertEqual([x * a for a in range(3) if a == 2 for x in range(5,7)], [10, 12])
        self.assertRaises(UnboundLocalError, lambda: [x * a for a in range(3) if x == 2 for x in range(5,7)])

        def foo1(z):
            return [(x for x in range(y)) for y in range(z)]
        self.assertEqual([list(x) for x in foo1(3)], [[], [0], [0, 1]])

        def foo2(z):
            return [(x + z for x in range(y)) for y in range(z)]
        self.assertEqual([list(x) for x in foo2(3)], [[], [3], [3, 4]])

        dl = [{ y: (z**2 for z in range(x)) for y in range(3)} for x in range(4)]
        self.assertIsInstance(dl, list)
        self.assertEqual(len(dl), 4)
        for p, d in enumerate(dl):
            self.assertIsInstance(d, dict)
            for k in range(3):
                g = d[k]
                self.assertIsInstance(g, types.GeneratorType)
                self.assertEqual(list(g), [0, 1, 4][:p])

    def test_gh1119(self):
        x = [[[1]]]
        res = [[tuple(str(d) for d in z) for z in y] for y in x]
        self.assertEqual(res, [[('1',)]])

run_test(__name__)