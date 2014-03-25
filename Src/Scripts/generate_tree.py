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

class Expression:
    def __init__(self, kind, type, interop = False, reducible = False, doc=""):
        self.kind = kind
        self.type = type
        self.interop = interop
        self.reducible = reducible
        self.doc = doc

expressions = [
    #
    #          enum kind                        tree node type
    #

    #
    #   DO NOT REORDER THESE, THEY COME FROM THE LINQ V1 ENUM
    #

    #          Enum Value               Expression Class                   Flags

    Expression("Add",                   "BinaryExpression",                interop = True,                      doc="arithmetic addition without overflow checking"),
    Expression("AddChecked",            "BinaryExpression",                                                     doc="arithmetic addition with overflow checking"),
    Expression("And",                   "BinaryExpression",                interop = True,                      doc="a bitwise AND operation"),
    Expression("AndAlso",               "BinaryExpression",                                                     doc="a short-circuiting conditional AND operation"),
    Expression("ArrayLength",           "UnaryExpression",                                                      doc="getting the length of a one-dimensional array"),
    Expression("ArrayIndex",            "BinaryExpression",                                                     doc="indexing into a one-dimensional array"),
    Expression("Call",                  "MethodCallExpression",                                                 doc="represents a method call"),
    Expression("Coalesce",              "BinaryExpression",                                                     doc="a null coalescing operation"),
    Expression("Conditional",           "ConditionalExpression",                                                doc="a conditional operation"),
    Expression("Constant",              "ConstantExpression",                                                   doc="an expression that has a constant value"),
    Expression("Convert",               "UnaryExpression",                                                      doc="a cast or conversion operation. If the operation is a numeric conversion, it overflows silently if the converted value does not fit the target type"),
    Expression("ConvertChecked",        "UnaryExpression",                                                      doc="a cast or conversion operation. If the operation is a numeric conversion, an exception is thrown if the converted value does not fit the target type"),
    Expression("Divide",                "BinaryExpression",                interop = True,                      doc="arithmetic division"),
    Expression("Equal",                 "BinaryExpression",                interop = True,                      doc="an equality comparison"),
    Expression("ExclusiveOr",           "BinaryExpression",                interop = True,                      doc="a bitwise XOR operation"),
    Expression("GreaterThan",           "BinaryExpression",                interop = True,                      doc='a "greater than" numeric comparison'),
    Expression("GreaterThanOrEqual",    "BinaryExpression",                interop = True,                      doc='a "greater than or equal" numeric comparison'),
    Expression("Invoke",                "InvocationExpression",                                                 doc="applying a delegate or lambda expression to a list of argument expressions"),
    Expression("Lambda",                "LambdaExpression",                                                     doc="a lambda expression"),
    Expression("LeftShift",             "BinaryExpression",                interop = True,                      doc="a bitwise left-shift operation"),
    Expression("LessThan",              "BinaryExpression",                interop = True,                      doc='a "less than" numeric comparison'),
    Expression("LessThanOrEqual",       "BinaryExpression",                interop = True,                      doc='a "less than or equal" numeric comparison'),
    Expression("ListInit",              "ListInitExpression",                                                   doc="creating a new IEnumerable object and initializing it from a list of elements"),
    Expression("MemberAccess",          "MemberExpression",                                                     doc="reading from a field or property"),
    Expression("MemberInit",            "MemberInitExpression",                                                 doc="creating a new object and initializing one or more of its members"),
    Expression("Modulo",                "BinaryExpression",                interop = True,                      doc="an arithmetic remainder operation"),
    Expression("Multiply",              "BinaryExpression",                interop = True,                      doc="arithmetic multiplication without overflow checking"),
    Expression("MultiplyChecked",       "BinaryExpression",                                                     doc="arithmetic multiplication with overflow checking"),
    Expression("Negate",                "UnaryExpression",                 interop = True,                      doc="an arithmetic negation operation"),
    Expression("UnaryPlus",             "UnaryExpression",                 interop = True,                      doc="a unary plus operation. The result of a predefined unary plus operation is simply the value of the operand, but user-defined implementations may have non-trivial results"),
    Expression("NegateChecked",         "UnaryExpression",                                                      doc="an arithmetic negation operation that has overflow checking"),
    Expression("New",                   "NewExpression",                                                        doc="calling a constructor to create a new object"),
    Expression("NewArrayInit",          "NewArrayExpression",                                                   doc="creating a new one-dimensional array and initializing it from a list of elements"),
    Expression("NewArrayBounds",        "NewArrayExpression",                                                   doc="creating a new array where the bounds for each dimension are specified"),
    Expression("Not",                   "UnaryExpression",                 interop = True,                      doc="a bitwise complement operation"),
    Expression("NotEqual",              "BinaryExpression",                interop = True,                      doc="an inequality comparison"),
    Expression("Or",                    "BinaryExpression",                interop = True,                      doc="a bitwise OR operation"),
    Expression("OrElse",                "BinaryExpression",                                                     doc="a short-circuiting conditional OR operation"),
    Expression("Parameter",             "ParameterExpression",                                                  doc="a reference to a parameter or variable defined in the context of the expression"),
    Expression("Power",                 "BinaryExpression",                interop = True,                      doc="raising a number to a power"),
    Expression("Quote",                 "UnaryExpression",                                                      doc="an expression that has a constant value of type Expression. A Quote node can contain references to parameters defined in the context of the expression it represents"),
    Expression("RightShift",            "BinaryExpression",                interop = True,                      doc="a bitwise right-shift operation"),
    Expression("Subtract",              "BinaryExpression",                interop = True,                      doc="arithmetic subtraction without overflow checking"),
    Expression("SubtractChecked",       "BinaryExpression",                                                     doc="arithmetic subtraction with overflow checking"),
    Expression("TypeAs",                "UnaryExpression",                                                      doc="an explicit reference or boxing conversion where null reference (Nothing in Visual Basic) is supplied if the conversion fails"),
    Expression("TypeIs",                "TypeBinaryExpression",                                                 doc="a type test"),

    # New types in LINQ V2                                                                      

    Expression("Assign",                "BinaryExpression",                                                     doc="an assignment"),
    Expression("Block",                 "BlockExpression",                                                      doc="a block of expressions"),
    Expression("DebugInfo",             "DebugInfoExpression",                                                  doc="a debugging information"),
    Expression("Decrement",             "UnaryExpression",                 interop = True,                      doc="a unary decrement"),
    Expression("Dynamic",               "DynamicExpression",                                                    doc="a dynamic operation"),
    Expression("Default",               "DefaultExpression",                                                    doc="a default value"),
    Expression("Extension",             "ExtensionExpression",                                                  doc="an extension expression"),
    Expression("Goto",                  "GotoExpression",                                                       doc="a goto"),
    Expression("Increment",             "UnaryExpression",                 interop = True,                      doc="a unary increment"),
    Expression("Index",                 "IndexExpression",                                                      doc="an index operation"),
    Expression("Label",                 "LabelExpression",                                                      doc="a label"),
    Expression("RuntimeVariables",      "RuntimeVariablesExpression",                                           doc="a list of runtime variables"),
    Expression("Loop",                  "LoopExpression",                                                       doc="a loop"),
    Expression("Switch",                "SwitchExpression",                                                     doc="a switch operation"),
    Expression("Throw",                 "UnaryExpression",                                                      doc="a throwing of an exception"),
    Expression("Try",                   "TryExpression",                                                        doc="a try-catch expression"),
    Expression("Unbox",                 "UnaryExpression",                                                      doc="an unbox value type operation"),
    Expression("AddAssign",             "BinaryExpression",                interop = True, reducible = True,    doc="an arithmetic addition compound assignment without overflow checking"),
    Expression("AndAssign",             "BinaryExpression",                interop = True, reducible = True,    doc="a bitwise AND compound assignment"),
    Expression("DivideAssign",          "BinaryExpression",                interop = True, reducible = True,    doc="an arithmetic division compound assignment "),
    Expression("ExclusiveOrAssign",     "BinaryExpression",                interop = True, reducible = True,    doc="a bitwise XOR compound assignment"),
    Expression("LeftShiftAssign",       "BinaryExpression",                interop = True, reducible = True,    doc="a bitwise left-shift compound assignment"),
    Expression("ModuloAssign",          "BinaryExpression",                interop = True, reducible = True,    doc="an arithmetic remainder compound assignment"),
    Expression("MultiplyAssign",        "BinaryExpression",                interop = True, reducible = True,    doc="arithmetic multiplication compound assignment without overflow checking"),
    Expression("OrAssign",              "BinaryExpression",                interop = True, reducible = True,    doc="a bitwise OR compound assignment"),
    Expression("PowerAssign",           "BinaryExpression",                interop = True, reducible = True,    doc="raising a number to a power compound assignment"),
    Expression("RightShiftAssign",      "BinaryExpression",                interop = True, reducible = True,    doc="a bitwise right-shift compound assignment"),
    Expression("SubtractAssign",        "BinaryExpression",                interop = True, reducible = True,    doc="arithmetic subtraction compound assignment without overflow checking"),
    Expression("AddAssignChecked",      "BinaryExpression",                reducible = True,                    doc="an arithmetic addition compound assignment with overflow checking"),
    Expression("MultiplyAssignChecked", "BinaryExpression",                reducible = True,                    doc="arithmetic multiplication compound assignment with overflow checking"),
    Expression("SubtractAssignChecked", "BinaryExpression",                reducible = True,                    doc="arithmetic subtraction compound assignment with overflow checking"),
    Expression("PreIncrementAssign",    "UnaryExpression",                 reducible = True,					doc="an unary prefix increment"),
    Expression("PreDecrementAssign",    "UnaryExpression",                 reducible = True,					doc="an unary prefix decrement"),
    Expression("PostIncrementAssign",   "UnaryExpression",                 reducible = True,					doc="an unary postfix increment"),
    Expression("PostDecrementAssign",   "UnaryExpression",                 reducible = True,					doc="an unary postfix decrement"),
    Expression("TypeEqual",             "TypeBinaryExpression",                                                 doc="a exact type test"),
    Expression("OnesComplement",        "UnaryExpression",                 interop = True,                      doc="a ones complement"),
    Expression("IsTrue",                "UnaryExpression",                 interop = True,                      doc="a true condition value"),
    Expression("IsFalse",               "UnaryExpression",                 interop = True,                      doc="a false condition value"),
]

def get_unique_types():
    return sorted(list(set(filter(None, map(lambda n: n.type, expressions)))))

def gen_tree_nodes(cw):
    for node in expressions:
        cw.write("/// <summary>")
        cw.write("/// A node that represents " + node.doc + ".")
        cw.write("/// </summary>")
        cw.write(node.kind + ",")

def gen_stackspiller_switch(cw):
    
    no_spill_node_kinds =  ["Quote", "Parameter", "Constant", "RuntimeVariables", "Default", "DebugInfo"]
    
    # nodes that need spilling
    for node in expressions:
        if node.kind in no_spill_node_kinds or node.reducible:
            continue
    
        method = "Rewrite"
        
        # special case certain expressions
        if node.kind in ["Quote", "Throw", "Assign"]:
            method += node.kind
        
        #special case AndAlso and OrElse
        if node.kind in ["AndAlso", "OrElse", "Coalesce"]:
            method += "Logical"

        cw.write("case ExpressionType." + node.kind + ":")
        cw.write("    result = " + method + node.type + "(node, stack);")           
        cw.write("    break;")

    # core reducible nodes
    for node in expressions:
        if node.reducible:
            cw.write("case ExpressionType." + node.kind + ":")

    cw.write("    result = RewriteReducibleExpression(node, stack);")
    cw.write("    break;")
    
    # no spill nodes
    for kind in no_spill_node_kinds:
        cw.write("case ExpressionType." + kind + ":")

    cw.write("    return new Result(RewriteAction.None, node);")


def gen_compiler(cw):
    for node in expressions:
        if node.reducible:
            continue
        
        method = "Emit"

        # special case certain unary/binary expressions
        if node.kind in ["AndAlso", "OrElse", "Quote", "Coalesce", "Unbox", "Throw", "Assign"]:
            method += node.kind
        elif node.kind in ["Convert", "ConvertChecked"]:
            method += "Convert"

        cw.write("case ExpressionType." + node.kind + ":")
        if node.kind in ["Coalesce", "Constant", "Lambda", "ListInit", "Loop", "MemberAccess", "MemberInit",
                          "New", "NewArrayInit", "NewArrayBounds", "Parameter", "Quote", "TypeIs", 
                          "Assign", "DebugInfo", "Dynamic", "Default", "Extension", "Index", "RuntimeVariables",
                          "Throw", "Try", "Unbox", "TypeEqual"]:
            cw.write("    " + method + node.type + "(node);")
        else:
            cw.write("    " + method + node.type + "(node, flags);")
        cw.write("    break;")
    
def gen_op_validator(type, cw):
    for node in expressions:
        if node.interop and node.type == type:
             cw.write("case ExpressionType.%s:" % node.kind)

def gen_binop_validator(cw):
    gen_op_validator("BinaryExpression", cw)
    
def gen_unop_validator(cw):
    gen_op_validator("UnaryExpression", cw)

def gen_checked_ops(cw):
    for node in expressions:
        if node.kind.endswith("Checked"):
            cw.write("case ExpressionType.%s:" % node.kind)

def get_type_name(t):
    if not t.IsGenericType:
        return t.Name

    name = t.Name[:t.Name.IndexOf("`")]
    name += "<" + ", ".join(map(lambda g: g.Name, t.GetGenericArguments())) + ">"
    return name

def gen_debug_proxy(cw, e):
    name = e.Name + "Proxy"
    cw.enter_block("internal class %(name)s", name=name)
    cw.write("""private readonly %(expression)s _node;

public %(name)s(%(expression)s node) {
    _node = node;
}
""", name = name, expression = e.Name)
    import System.Reflection
    bf = System.Reflection.BindingFlags

    # properties
    def get_properties(e):
        properties = []
        atom = set()
        def add(l):
            for p in l:
                if not p.Name in atom:
                    atom.add(p.Name)
                    properties.append(p)
        while e:
            add(e.GetProperties(bf.Instance | bf.Public))
            add(e.GetProperties(bf.Instance | bf.NonPublic))
            e = e.BaseType

        properties.sort(None, lambda p: p.Name)
        return properties
        
    properties = get_properties(e)

    for p in properties:
        if p.Name == "Dump": continue
        get = p.GetGetMethod(True)
        if not get: continue
        if not get.IsPublic and p.Name != "DebugView": continue

        cw.write("public %(type)s %(name)s { get { return _node.%(name)s; } }", type = get_type_name(p.PropertyType), name = p.Name)

    cw.exit_block()


def gen_debug_proxies(cw):
    import clr
    msc = clr.LoadAssemblyByPartialName("Microsoft.Scripting.Core")
    expr = msc.GetType("Microsoft.Scripting.Ast.Expression")
    custom = [ 'SwitchCase', 'CatchBlock' ]
    ignore = [ 'Expression' ]
    def expression_filter(e):
        if not e.IsPublic: return False
        if not e.Namespace.StartsWith("Microsoft.Scripting.Ast"): return False
        if e.IsGenericType: return False
        if e.Name in ignore: return False
        if expr.IsAssignableFrom(e): return True
        if e.Name in custom: return True

        return False

    expressions = filter(expression_filter, msc.GetTypes())
    expressions.sort(None, lambda e: e.Name)
    
    first = True
    for e in expressions:
       if not first: cw.write("")
       else: first = False
       gen_debug_proxy(cw, e)

def main():
    temp_list = [   ("Expression Tree Node Types", gen_tree_nodes),
                    ("Checked Operations", gen_checked_ops),
                    ("Binary Operation Binder Validator", gen_binop_validator),
                    ("Unary Operation Binder Validator", gen_unop_validator),
                    ("StackSpiller Switch", gen_stackspiller_switch),
                    ("Expression Compiler", gen_compiler)
                    ]
    import System
    if System.Environment.Version.Major<4:
        temp_list.append(("Expression Debugger Proxies", gen_debug_proxies))
    
    return generate(*temp_list)

if __name__ == "__main__":
    main()
