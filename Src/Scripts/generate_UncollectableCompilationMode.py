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

# Number of static fields for each kind of allocation before array fallback
n_statics = 50 # number of static fields per storage type
n_context_types = 1
n_constant_types = 5
n_symbol_types = 7
n_global_types = 20
n_site_types = 30

# Default number of slots in fallback array
# NOTE: Because we periodically double the size, this must be nonzero.
n_fallback_slots = 64

# Max number of arguments in the various Dynamic overloads
max_dynamic_args = 4

def _gen_indexer(cw, name, n_types, isGeneric=False):
    storage_prefix = "StorageData." if name == "Site" else ""
    cw.write("")
    cw.write("/// <summary>Ensures the underlying array is long enough to accomodate the given index</summary>")
    cw.write("/// <returns>The %s storage type corresponding to the given index</returns>" % name.lower())
    cw.enter_block("public static Type/*!*/ %sStorageType(int index)" % name)
    cw.write("Debug.Assert(index >= 0);")
    cw.write("")
    if n_types == 1:
        cw.enter_block("if (index < %sStaticFields)" % storage_prefix)
        cw.write("return typeof(%sStorage000%s);" % (name, "<T>" if isGeneric else ""))
        cw.else_block()
    elif n_types > 0:
        cw.enter_block("switch (index / %sStaticFields)" % storage_prefix)
        for i in xrange(n_types):
            cw.write("case %.3d: return typeof(%sStorage%.3d%s);" %
                     (i, name, i, "<T>" if isGeneric else ""))
        cw.write("")
        cw.case_label("default:")
    cw.write("int len = checked(index - %sStaticFields * %s%sTypes + 1);" % (storage_prefix, storage_prefix, name))
    cw.enter_block("if (%ss.Length < len)" % name)
    cw.write("int newLen = %ss.Length;" % name)
    cw.enter_block("while (newLen < len)")
    cw.write("newLen *= 2;")
    cw.exit_block()
    cw.write("Array.Resize(ref %ss, newLen);" % name)
    cw.exit_block()
    if isGeneric:
        cw.write("return typeof(%sStorage<T>);" % name)
    else:
        cw.write("return typeof(StorageData);")
    if n_types == 1:
        cw.exit_block()
    elif n_types > 0:
        cw.dedent()
        cw.exit_block()
    cw.exit_block()

def gen_static_storage(cw):
    # Generate storage metadata and fallback arrays
    
    cw.enter_block("public static class StorageData")
    cw.write("// Amount of data currently allocated")
    cw.write("internal static int ContextCount;")
    cw.write("internal static int ConstantCount;")
    cw.write("internal static int SymbolCount;")
    cw.write("internal static int GlobalCount;")
    cw.write("")
    cw.write("// Number of static fields per storage type")
    cw.write("public const int StaticFields = %d;" % n_statics)
    cw.write("")
    cw.write("// Number of storage types for each kind of data")
    cw.write("public const int ContextTypes = %d;" % n_context_types)
    cw.write("public const int ConstantTypes = %d;" % n_constant_types)
    cw.write("public const int SymbolTypes = %d;" % n_symbol_types)
    cw.write("public const int GlobalTypes = %d;" % n_global_types)
    cw.write("public const int SiteTypes = %d;" % n_site_types)
    cw.write("")
    cw.write("// Fallback array storage and locking objects")
    cw.write("public static CodeContext[] Contexts = new CodeContext[%d];" % n_fallback_slots)
    cw.write("public static object[] Constants = new object[%d];" % n_fallback_slots)
    cw.write("public static PythonGlobal[] Globals = new PythonGlobal[%d];" % n_fallback_slots)
    cw.write("")
    cw.write("// Locking object for site storage")
    cw.write("public static readonly object SiteLockObj = new object();")
    cw.write("")
    cw.write("// Indexing helpers")
    _gen_indexer(cw, "Context", n_context_types)
    _gen_indexer(cw, "Constant", n_constant_types)
    _gen_indexer(cw, "Global", n_global_types)
    cw.exit_block()
    
    # Generate storage types
    
    def gen_storage(cw, type, name, n_types):
        cw.write("")
        for i in xrange(n_types):
            cw.enter_block("public static class %sStorage%.3d" % (name, i))
            for j in xrange(n_statics):
                cw.write("public static %s %s%.3d;" % (type, name, j))
            cw.exit_block()
    
    gen_storage(cw, "CodeContext", "Context", n_context_types)
    gen_storage(cw, "object", "Constant", n_constant_types)
    gen_storage(cw, "PythonGlobal", "Global", n_global_types)

def gen_site_storage(cw):
    # Generate SiteStorage<T>
    cw.enter_block("public static class SiteStorage<T> where T : class")
    cw.write("public static int SiteCount;")
    cw.write("")
    cw.write("public static CallSite<T>[] Sites = new CallSite<T>[%d];" % n_fallback_slots)
    _gen_indexer(cw, "Site", n_site_types, True)
    cw.exit_block()
    
    # Generate SiteStorage000..NNN<T>
    for i in xrange(n_site_types):
        cw.write("")
        cw.enter_block("public static class SiteStorage%.3d<T> where T : class" % i)
        for j in xrange(n_statics):
            cw.write("public static CallSite<T> Site%.3d;" % j)
        cw.exit_block()

def _list_args(n, prefix=""):
    return ", ".join(map(lambda i: "%sarg%d" % (prefix, i), range(n)))

def _list_type_args(n, transform=(lambda x: x)):
    return ", ".join(map(lambda i: transform("T%d" % i), range(n)))

def gen_dynamic(cw):
    def gen_main(n):
        cw.enter_block("public override MSAst.Expression/*!*/ Dynamic("
                       "DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, %s)" %
                       _list_args(n, "MSAst.Expression/*!*/ "))
        if n <= 2:
            cw.write("Assert.NotNull(binder, retType, %s);" % _list_args(n))
        else:
            cw.write("Assert.NotNull(binder, retType);")
            if n <= 4:
                cw.write("Assert.NotNull(%s);" % _list_args(n))
            else:
                cw.write("Assert.NotNullItems(new[] { %s });" % _list_args(n))
        cw.write("")
        cw.write("DelegateCache info;")
        cw.enter_block("lock (_delegateCache)")
        line = "info = DelegateCache.FirstCacheNode(arg0.Type)"
        for i in xrange(1, n):
            line += ".NextCacheNode(arg%d.Type)" % i
        cw.write(line + '.NextCacheNode(retType);')
        cw.enter_block("if (info.DelegateType == null)")
        cw.write("info.MakeDelegateType(retType, %s);" % _list_args(n))
        cw.exit_block()
        cw.exit_block()
        cw.write("")
        cw.write("SiteInfo si = info.NextSite(binder);")
        cw.write("")
        cw.write("return MakeDynamicExpression(binder, si.Expression, "
                 "info.TargetField, info.InvokeMethod, %s);" %
                 _list_args(n))
        cw.exit_block()
    
    def gen_helper(n):
        cw.enter_block("private static MSAst.Expression/*!*/ MakeDynamicExpression("
                       "DynamicMetaObjectBinder/*!*/ binder, MSAst.Expression/*!*/ expr, "
                       "FieldInfo/*!*/ targetField, MethodInfo/*!*/ invokeMethod, %s)" %
                       _list_args(n, "MSAst.Expression/*!*/ "))
        if n >= 4:
            cw.write("var builder = new ReadOnlyCollectionBuilder<MSAst.Expression>(%s);" % (n + 1))
            cw.write("builder.Add(expr);")
            for i in xrange(n):
                cw.write("builder.Add(arg%s);" % i)
            cw.write("")
        cw.write("return new ReducableDynamicExpression(")
        cw.indent()
        cw.write("Ast.Call(")
        cw.indent()
        cw.write("Ast.Field(expr, targetField),")
        cw.write('invokeMethod,')
        if n >= 4:
            cw.write("builder.ToReadOnlyCollection()")
        else:
            cw.write("expr, %s" % _list_args(n))
        cw.dedent()
        cw.write("),")
        cw.write("binder,")
        cw.write("new[] { %s }" % _list_args(n))
        cw.dedent()
        cw.write(");")
        cw.exit_block()
    
    for n_args in xrange(1, max_dynamic_args + 1):
        gen_main(n_args)
        cw.write("")
        gen_helper(n_args)
        if n_args < max_dynamic_args:
            cw.write("")

def main():
    return generate(
        ("Storage", gen_static_storage),
        ("CallSite Storage", gen_site_storage),
        ("Cached Site Support", gen_dynamic),
    )

if __name__ == "__main__":
    main()
