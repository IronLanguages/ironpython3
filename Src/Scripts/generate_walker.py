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
import System
import clr

# This generates PythonWalker.Generated.cs
# usage is just:
#   ipyd generate_walker.py
# it wills can all types in IronPython and detect AST nodes based on type inheritence.

def inherits(t, p):
    if not t:
        return False
    elif t.FullName == p:
        return True
    else:
        return inherits(t.BaseType, p)

def get_ast(assembly, roots):
    import clr

    sets = {}
    for root in roots:
        sets[root] = set()

    for node in assembly.GetTypes():
        # skip abstract types
        if node.IsAbstract: continue

        for root in roots:
            if inherits(node, root) and node.IsPublic: 
                sets[root].add(node.Name)
                break

    result = []
    for root in roots:
        result.extend(sorted(list(sets[root])))

    return result
    
def gen_walker(cw, nodes, method, value):
    space = 0
    for node in nodes:
        if space: cw.write("")
        cw.write("// %s" % node)
        cw.write("%s bool Walk(%s node) { return %s; }" % (method, node, value))
        cw.write("%s void PostWalk(%s node) { }" % (method, node))
        space = 1

def get_python_nodes():
    nodes = get_ast(
        clr.LoadAssemblyByPartialName("IronPython"),
        [
            "IronPython.Compiler.Ast.Expression",
            "IronPython.Compiler.Ast.Statement",
            "IronPython.Compiler.Ast.Node"
        ]
    )
    return nodes

def gen_python_walker(cw):
    gen_walker(cw, get_python_nodes(), "public virtual", "true")

def gen_python_walker_nr(cw):
    gen_walker(cw, get_python_nodes(), "public override", "false")

def main():
    return generate(
        ("Python AST Walker", gen_python_walker),
        ("Python AST Walker Nonrecursive", gen_python_walker_nr),
    )

if __name__=="__main__":
    main()
