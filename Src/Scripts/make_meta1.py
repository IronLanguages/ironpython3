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

import os, re, string

src_dir = r"c:\Documents and Settings\Jim\My Documents\Visual Studio Projects\IronPython"

meth_pat = r"(?P<ret_type>\w+)\s+(?P<name>\w+)\s*\((?P<params>(?:\w|\s|,)*)\)"
meth_pat = re.compile(r"public\s+(?P<mods>static\s+)?(virtual\s+)?"+meth_pat)

class_pat = re.compile(r"public\s+(abstract\s+)?class\s+(?P<name>\w+)\s*(:\s*(?P<super_name>\w+))?")

START = "//BEGIN-GENERATED"
END = "//END-GENERATED"

generated_pat = re.compile(START+".*"+END, re.DOTALL)

from_table = {
    'int':"Py.asInt(%s)",
    'double':"Py.asDouble(%s)",
    'char':"Py.asChar(%s)",
    'PyObject':"%s",
}


def from_any(totype, name):
    pat = from_table.get(totype, None)
    if pat is None:
        return "(%s)%s" % (totype, name)
    else:
        return pat % name

to_table = {
    'int':"PyInteger.make(%s)",
    'double':"PyFloat.make(%s)",
    'bool':"PyBoolean.make(%s)",
    'char':"PyString.make(%s)",
    'PyObject':"%s",
}


def to_any(totype, name):
    pat = to_table.get(totype, None)
    if pat is None:
        return name #"(%s)%s" % (totype, name)
    else:
        return pat % name

BINOP = """
public override PyObject __%(name)s__(PyObject other) {
    %(type)s ov;
    if (!Py.check%(ctype)s(other, out ov)) return Py.NotImplemented;
    return %(ret_type)s.make(%(name)s(_value, ov));
}
public override PyObject __r%(name)s__(PyObject other) {
    %(type)s ov;
    if (!Py.check%(ctype)s(other, out ov)) return Py.NotImplemented;
    return %(ret_type)s.make(%(name)s(ov, _value));
}
"""

UNOP = """
public override PyObject __%(name)s__() {
    return %(ret_type)s.make(%(name)s(_value));
}
"""

class Method:
    def __init__(self, ret_type, name, params, mods=None):
        self.param_list = map(lambda s: s.strip(), string.split(params, ","))
        self.param_list = filter(lambda s: s, self.param_list)
        self.ret_type = ret_type
        self.name = name
        self.mods = mods
        self.is_invokable = True

    def get_public_name(self):
        name = self.name
        if name.endswith("_") and not name.endswith("__"):
            name = name[:-1]
        return name

    def is_any(self):
        if self.ret_type != ANY or self.mods: return False
        for p in self.param_list:
            if not p.startswith(ANY): return False
        return True

    def is_static(self):
        return self.mods is not None

    def is_internal(self):
        return self.name.endswith("__")

    def make_op(self):
        self.is_invokable = False
        type = self.param_list[0].split(" ")[0]
        dict = {
            'name':self.get_public_name(),
            'type':type,
            'ctype':type.capitalize(),
            'ret_type':to_table[self.ret_type].split('.')[0]
            }

        if len(self.param_list) == 2:
            return (BINOP % dict).split("\n")
        else:
            return (UNOP % dict).split("\n")

    def make_any(self, in_module=False):
        if not in_module and self.is_static():
            return self.make_op()

        name = self.get_public_name()
        params = []
        args = []
        for p in self.param_list:
            ptype, pname = string.split(p)
            args.append(from_any(ptype, pname))
            params.append("PyObject %s" % pname)

        if self.is_internal():
            mods = "override "
            self.is_invokable = False
        else:
            mods = ""

        if self.is_static():
            mods += "static "

        ret = ["public %sPyObject %s(%s) {" % (mods, name, string.join(params, ", "))]
        if self.ret_type == 'void':
            ret.append("    %s(%s);" % (self.name, string.join(args, ", ")))
            ret.append("    return Py.None;")
        else:
            value = "%s(%s)" % (self.name, string.join(args, ", "))
            ret.append("    return %s;" % to_any(self.ret_type, value))

        ret.append("}")
        return ret

    def __repr__(self):
        return "Method(%s, %s, %s)" % (self.ret_type, self.name, self.param_list)

class Class:
    def __init__(self, name, super_name, methods):
        self.name = name
        self.super_name = super_name
        self.methods = methods
        self.strings = {}

    def get_constant_string(self, s):
        if not self.strings.has_key(s):
            self.strings[s] = s + "_str"

        return self.strings[s]

    def make_invoke_method(self, nargs):
        params = ["PyString name"]
        args = ["name"]
        for i in range(nargs):
            params.append("PyObject arg%d" % i)
            args.append("arg%d" % i)
        ret = ["public override PyObject invoke(%s) {" % ", ".join(params)]
        ret.append("    if (name.interned) {")

        #TODO create switch clause when more than 3-8 matches
        for method in self.methods:
            if not method.is_invokable or len(method.param_list) != nargs:
                continue
            name = method.get_public_name()
            ret.append("        if (name == %s) return %s(%s);" %
                       (self.get_constant_string(name), name,
                        ", ".join(args[1:])))

        if len(ret) == 2: return []

        ret.append("    }")
        ret.append("    return base.invoke(%s);" % ", ".join(args))
        ret.append("}")
        return ret

    def make_any_methods(self):
        ret = []
        # first generate the any forms of each method
        for method in self.methods:
            if not method.is_any():
                ret.extend(method.make_any(self.super_name == 'PyModule'))

        # now generate the various invoke methods
        for i in range(4):
            ret.extend(self.make_invoke_method(i))
        #TODO invokeN

        for value, name in self.strings.items():
            ret.append('static readonly PyString %s = PyString.intern("%s");' %
                       (name, value))

        return ret

    def __repr__(self):
        return "Class(%s, %s)" % (self.name, self.super_name)

def collect_methods(text):
    text = generated_pat.sub("", text)

    ret = []
    match= class_pat.search(text)
    #print match
    if match is None:
        return None
    cl = Class(match.group('name'), match.group('super_name'), ret)

    for match in meth_pat.finditer(text):
        meth = Method(**match.groupdict())
        if meth.is_static() and meth.name in ['make', 'intern']: continue
        ret.append(meth)

    return cl

base = collect_methods(open(os.path.join(src_dir, "PyObject.cs")).read())
ANY = base.name


for file in os.listdir(src_dir):
    filename = os.path.join(src_dir, file)
    if not filename.endswith(".cs"): continue
    text = open(filename).read()

    if generated_pat.search(text) is None: continue

    c = collect_methods(text)
    assert c is not None and c.name != base.name
    #if c.super_name != 'PyModule':
    #    assert c.super_name == base.name, c.super_name

    print c, c.methods
    code = c.make_any_methods()
    code.insert(0, START)
    code.append(END)
    generated_code = "\n\t\t".join(code)
    new_text = generated_pat.sub(generated_code, text)
    #print new_text
    if text != new_text:
        open(filename, 'w').write(new_text)


#print c, c.methods

#print meth_pat.search("public void m(int a, float d)").groups()

