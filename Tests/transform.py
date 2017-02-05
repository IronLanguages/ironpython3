def codegen_helper():
    import clr
    clr.AddReference("IronPython")
    from IronPython.Compiler import Ast

    def get_Subclass_of(rt):
        for y in [getattr(Ast, x) for x in dir(Ast)]:
            yt = clr.GetClrType(y)
            if rt == yt: continue
            if yt.IsAbstract: continue
            if yt.IsSubclassOf(rt):
                yield yt.Name

    all_types = []
    all_exprs = []
    all_stmts = []
    
    nodeRt = clr.GetClrType(Ast.Node)
    exprRt = clr.GetClrType(Ast.Expression)
    stmtRt = clr.GetClrType(Ast.Statement)

    all_exprs = [x for x in get_Subclass_of(exprRt)]
    all_stmts = [x for x in get_Subclass_of(stmtRt)]
    all_types = [x for x in get_Subclass_of(nodeRt)] 
    other_types = list(set(['Expression', 'Statement']) | set(all_types) - set(all_exprs) - set(all_stmts))

    print("        public override Node Visit(Node node) {")
    print("            if (node == null) return null;")
    print("            Type type = node.GetType();")
    
    for i in range(len(other_types)):
        tn = other_types[i]
        if i != 0: print(" else", end=' ') 
        print("    if (type == typeof(%s)) {"  % tn)
        print("         return Visit%s(node as %s);" % (tn, tn))
        print("}", end=' ') 
    print() 
    print("throw new ApplicationException(\"unknown node\");")
    print("        }")
    
    for (n, ts) in [('Expression', all_exprs), ('Statement', all_stmts)]:        
        print("        public virtual Node Visit%s(%s node) {" % (n, n))
        print("            if (node == null) return null;")
        print("            Type type = node.GetType();")
        for i in range(len(ts)):
            tn = ts[i]
            if i != 0: print(" else", end=' ') 
            print("    if (type == typeof(%s)) {"  % tn)
            print("         return Visit%s(node as %s);" % (tn, tn))
            print("}", end=' ') 
        print() 
        print("throw new ApplicationException(\"unknown %s\");" % n.lower())
        print("        }")

import nt, sys
import clr
import System
import pickle
clr.AddReference("PythonStyle")

from PythonStyle import *
from System.IO import *

testdir = r"C:\Workspaces\dlr\Languages\IronPython\Tests"
cpy_testdir = r"C:\Workspaces\dlr\Languages\IronPython\Tests\CPy_Testcases"

if System.IO.Directory.Exists(testdir+r"\Transformed"):
    System.IO.Directory.Delete(testdir+r"\Transformed", True)
    
System.IO.Directory.CreateDirectory(testdir+r"\Transformed")

filename_to_g = lambda f: Path.Combine(testdir, "Transformed\\g_" + Path.GetFileName(f))
filename_to_3 = lambda f: f.lower().replace("merlin2", "merlin3")

generated = filename_to_g

def get_test_file(directory, pattern): 
    for x in System.IO.Directory.GetFiles(directory, pattern):
        yield x
    
    for x in System.IO.Directory.GetDirectories(directory):
        for y in get_test_file(x, pattern):
            yield y

def try_one(visitor, test_file, stdout=False):
    test_file = testdir + "\\" + test_file
    rw = Rewriter(visitor)    

    if stdout:
        rw.Convert(test_file)
    else: 
        rw.Convert(test_file, generated(test_file))

def try_all(visitor):
    rw = Rewriter(visitor)
    
    if (ipy_only):
        if System.IO.Directory.Exists(testdir+r"\CPy_Testcases"):
            System.IO.Directory.Delete(testdir+r"\CPy_Testcases", True)

    for f in get_test_file(testdir, "test_*.py"):
        #list of files to skip
        f_name = Path.GetFileName(f)
        if (f_name=="test_builds.py"):
            pass
        elif (f_name=="test_complex.py"):          
            pass 
        elif (f_name=="test_dllsite.py"):          
            pass
        elif (f_name=="test_formatting.py"):          
            pass
        elif (f_name=="test_namebinding.py"):          
            pass
        elif (f_name=="test_nofuture.py"):          
            pass
        elif (f_name=="test_nt.py"):          
            pass
        elif (f_name=="test_number.py"):          
            pass
        elif (f_name=="test_numtypes.py"):          
            pass
        elif (f_name=="test_superconsole.py"):          
            pass
        elif (f_name=="test_threadsafety.py"):          
            pass
        elif (f_name=="test_traceback.py"):          
            pass
        else:
            print("Attempting to transform: ", f_name)
            rw.Convert(f, generated(f))
           
    
    print("\nNumber of test cases transformed: ", rw.transformCount, "/", rw.fileCount)
    
    _f_list = open('Transformed\\untrans_list.txt', 'w')
    
    _f_list.write("Files not transformed:\n")
    for f in rw.PrintFileList():
        f_name = Path.GetFileName(f)
        _f_list.write(f_name+'\n')
        System.IO.File.Delete(testdir+"\\Transformed\\g_"+Path.GetFileName(f))
    _f_list.close()
        
def cpy_try_all(visitor):
    rw = Rewriter(visitor)

    for f in get_test_file(cpy_testdir, "test_*.py"):
        #list of files to skip
        f_name = Path.GetFileName(f)
        if (f_name=="test_builds.py"):
            pass
        else:
            print("Attempting to transform: ", f_name)
            rw.Convert(f, generated(f))
            
    print("\nNumber of test cases transformed: ", rw.transformCount, "/", rw.fileCount)
     
    _f_list = open('Transformed\\untrans_list.txt', 'w')
    _f_list.write("Files not transformed:\n")
    for f in rw.PrintFileList():
        f_name = Path.GetFileName(f)
        _f_list.write(f_name+'\n')
        System.IO.File.Delete(testdir+"\\Transformed\\g_"+Path.GetFileName(f))

    _f_list.close()
def compile_all():
    for f in get_test_file(testdir, "test_*.py"):
        f = generated(f)
        fo = file(f)
        lines = fo.readlines()
        fo.close()
        try:
            print("compiling", f, end=' ') 
            compile("\n".join(lines), f, "exec")
            print("pass")
        except: 
            print("fail")
            print(sys.exc_info()[1])

if sys.argv[1]=="try_one":
    exec("t_obj = " + sys.argv[2] + "()") 
    try_one(t_obj, sys.argv[3])
elif sys.argv[1]=="try_ipy":
    exec("t_obj = " + sys.argv[2] + "()")
    ipy_only = True
    try_all(t_obj)
elif sys.argv[1]=="try_cpy":
    exec("t_obj = " + sys.argv[2] + "()")
    cpy_try_all(t_obj)
elif sys.argv[1]=="try_all":
    exec("t_obj = " + sys.argv[2] + "()")
    ipy_only = False
    try_all(t_obj)
else:
    print("Error: unrecognized argument")
    
            


#codegen_helper()
#try_all(CallableClassVisitor())
#try_all(DynamicMethodVisitor())
#try_all(NestedFunctionVisitor())
#try_all(PropertyVisitor())
#try_all(DecoratorVisitor())
#cpy_try_all(CallableClassVisitor())
#cpy_try_all(DynamicMethodVisitor())
#cpy_try_all(NestedFunctionVisitor())
#cpy_try_all(PropertyVisitor())
#cpy_try_all(DecoratorVisitor())
#compile_all()
#try_one(DecoratorVisitor(), "test_CPickle.py")
#try_one(NestedFunctionVisitor(), "test_methodbinder2.py")
#try_one(CallableClassVisitor(), "kevin_test.py")
#try_one(StandardVisitor(), "kevin_test.py", False)
#try_one(YieldReturnVisitor(), "test_isinstance.py")
#try_one(LambdaExprVisitor(), "test_generator_throw.py")
#try_one(DummyInterfaceVisitor(), "test_class.py")
#try_one(SubClassingVisitor(), "test_class.py")
#try_one(ParameterDefaultValueVisitor(), "test_class.py")
#try_one(PropertyVisitor(), "kevin_test.py")
#try_one(PropertyVisitor(), "test_class.py")
