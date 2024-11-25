# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

class Func:
    def __init__(self, name, args=1, cname=None):
        self.name = name
        self.args = args
        if cname is None:
            cname = name.capitalize()
        self.cname = cname

    def write(self, cw):
        params = ["double v%d" % i for i in range(self.args)]
        args = ["v%d" % i for i in range(self.args)]
        cw.enter_block("public static double %s(%s)" %
                       (self.name, ", ".join(params)))
        cw.write("return Check(%s, Math.%s(%s));" %
                 (", ".join(args), self.cname, ", ".join(args)))
        cw.exit_block()
        
#Func('fmod', 2), Func('modf'),
#Func('frexp'),Func('hypot', 2), Func('ldexp', 2),
#Func('atan2', 2), Func('pow', 2)

funcs = [
    Func('cos'), Func('sin'), Func('tan'),
    Func('cosh'), Func('sinh'), Func('tanh'),
    Func('acos'), Func('asin'), Func('atan'),
    Func('fabs', 1, 'Abs'),
    Func('sqrt'), Func('exp'),
]

def gen_funcs(cw):
    for func in funcs:
        func.write(cw)

def main():
    return generate(
        ("math functions", gen_funcs)
    )

if __name__ == "__main__":
    main()
