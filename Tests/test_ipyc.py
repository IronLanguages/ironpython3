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

##
## Testing IronPython Compiler
##

from iptest.assert_util import *
skiptest("silverlight")
skiptest("win32")
from iptest.file_util import *
from iptest.process_util import *

import sys
import nt

import System
from   System.Collections.Generic import List

remove_ironpython_dlls(testpath.public_testdir)
load_iron_python_dll()

import IronPython

if False: #Needs to be updated or removed for DLR
    from IronPython.Hosting import PythonCompiler

load_iron_python_test()

def CompileAsDll(fileName, assemblyName):
    sources = List[str]()
    sources.Add(fileName)
    pc = PythonCompiler(sources, assemblyName)
    pc.TargetKind = System.Reflection.Emit.PEFileKinds.Dll
    pc.Compile()
    
def CompileOneFileAsConsoleApp1(fileName, assemblyName, setMainFile) :
    sources = List[str]()
    sources.Add(fileName)
    pc = PythonCompiler(sources, assemblyName)
    if setMainFile:
        pc.MainFile = fileName
    pc.Compile()

def CompileOneFileAsConsoleApp2(fileName, assemblyName):
    sources = List[str]()
    sources.Add(fileName)
    pc = PythonCompiler(sources, assemblyName)
    pc.MainFile = "NotExistFile"
    pc.Compile()

def CompileTwoFilesAsConsoleApp(fileName1, fileName2, assemblyName, setMainFile):
    sources = List[str]()
    sources.Add(fileName1)
    sources.Add(fileName2)
    pc = PythonCompiler(sources, assemblyName)
    if (setMainFile):
        pc.MainFile = fileName1
    pc.Compile()

def UsingReference(fileName, typeName, assemblyName):
    sources = List[str]()
    sources.Add(fileName)
    pc = PythonCompiler(sources, assemblyName)
    pc.MainFile = fileName
    refAsms = List[str]()
    refAsms.Add(System.Type.GetType(typeName).Assembly.FullName)
    pc.ReferencedAssemblies = refAsms
    pc.Compile()
    
def CheckIncludeDebugInformation(fileName, assemblyName, include):
    sources = List[str]()
    sources.Add(fileName)
    pc = PythonCompiler(sources, assemblyName)
    pc.IncludeDebugInformation = include
    pc.Compile()
    

def FileExists(file):
    return System.IO.File.Exists(file)

def DeleteFile(file):
    for i in range(5):
        try:
            System.IO.File.Delete(file)
            break
        except:
            System.Threading.Thread.Sleep(1000)

def FileRemoval(*files):
    for file in files:
        DeleteFile(file)
   
def GetFullPath(file):
    return System.IO.Path.GetFullPath(file).ToLower()

def RunPythonExe(file, *args):
    fullpath = GetFullPath(file)
    temppath = System.IO.Path.Combine(sys.prefix, System.IO.FileInfo(fullpath).Name).ToLower()

    if (fullpath != temppath):
        System.IO.File.Copy(fullpath, temppath, True)

    realargs = [temppath]
    realargs.extend(args)
    try:
        retval = nt.spawnv(0, temppath, realargs)
    except:
        retval = 1

    # hack
    if (fullpath != temppath):
        DeleteFile(temppath)
    Assert(not retval)

## compile as dll
source, assembly, pdbfile = "tempFile1.tpy", "tempFile1.dll", "tempFile1.pdb"

write_to_file(source, '''
class B:
    def M1(self):
        return 20
''')

@disabled("Needs to be updated or removed for DLR")
def test_sanity():

    FileRemoval(assembly, pdbfile);
    CompileAsDll(source, assembly)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))

@disabled("Needs to be updated or removed for DLR")
def test_one_source_consoleapp():
    ## compile as exe
    ## if only one source file, you do not necessarily specify the main file
    source, assembly, pdbfile = "tempFile1.tpy", "tempFile1.exe", "tempFile1.pdb"

    FileRemoval(assembly, pdbfile);
    CompileOneFileAsConsoleApp1(source, assembly, True)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))

    FileRemoval(assembly, pdbfile);
    CompileOneFileAsConsoleApp1(source, assembly, False)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))

    ## compile as exe, but main file is INVALID
    AssertError(Exception, CompileOneFileAsConsoleApp2, source, assembly)

@disabled("Needs to be updated or removed for DLR")
def test_two_source_consoleapp():
    ## compile 2 files as exe
    source1, source2, assembly, pdbfile = "tempFile2.tpy", "tempFile1.tpy", "tempFile2.exe", "tempFile2.pdb"
    write_to_file(source1, '''
    import tempFile1
    class D(tempFile1.B):
        def M2(self):
            return 100
    b = tempFile1.B()
    if (b.M1() != 20) :
        raise AssertionError("failed 1")
    d= D()
    if (d.M2() !=  100):
        raise AssertionError("failed 2")
    ''')

    FileRemoval(assembly, pdbfile);
    CompileTwoFilesAsConsoleApp(source1, source2, assembly, True)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))
    RunPythonExe(assembly)
    
    ## compile 2 files as exe, but main file is not set
    AssertError(Exception, CompileTwoFilesAsConsoleApp, source1, source2, assembly, False)

@disabled("Needs to be updated or removed for DLR")
def test_debug_consoleapp():
    ## IncludeDebugInformation
    source, assembly, pdbfile = "tempFile1.tpy", "tempFile1.dll", "tempFile1.pdb"
    FileRemoval(assembly, pdbfile);
    CheckIncludeDebugInformation(source, assembly, True)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))

    FileRemoval(assembly, pdbfile);
    CheckIncludeDebugInformation(source, assembly, False)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile) == False)

@disabled("Needs to be updated or removed for DLR")
def test_referenced_assemblies_consoleapp():
    ## Test Using referenced assemblies
    source, assembly, pdbfile = "tempFile3.tpy", "tempFile3.exe", "tempFile3.pdb"

    import clr
    clr.AddReferenceByPartialName("System.Xml")

    # sys.LoadAssembly...("System.xml") is emitted because of referenced assemblies specified
    write_to_file(source, '''
    import System
    import System.Xml
    tw = System.Xml.XmlTextWriter("tempResult.xml",  System.Text.Encoding.ASCII)
    tw.WriteStartDocument()
    tw.WriteStartElement("PythonCompiler")
    tw.WriteEndElement()
    tw.WriteEndDocument()
    tw.Close()
    ''')

    fullTypeName = System.Type.GetType("System.Int32").AssemblyQualifiedName.split(',', 2)
    UsingReference(source, "System.Xml.XmlTextReader, System.Xml," + fullTypeName[2], assembly)

    tempXml = "tempResult.xml"
    ## BE CLEAN
    FileRemoval(tempXml)
    ## RUN
    RunPythonExe(assembly)
    ## CHECK
    Assert(FileExists(tempXml), "File was not generated after running the exe")
    f = open(tempXml)
    Assert(f.read().find("PythonCompiler") != -1, "The specified word is not found in the file")
    f.close()

    FileRemoval(tempXml)


    for filename in ['tempFile1', 'tempFile2', 'tempFile3']:
        for suffix in [ 'tpy', 'dll', 'exe', 'pdb']:
            FileRemoval(filename + '.' + suffix)

#
# verify that generated exe will run stand alone.
#
@disabled("Needs to be updated or removed for DLR")
def test_exe_standalone():
    tempFile1 = '''
class B:
    def M1(self):
        return 20
'''

    tempFile2 = '''
import tempFile1
class D(tempFile1.B):
    def M2(self):
        return 100
b = tempFile1.B()
if (b.M1() != 20) :
    raise AssertionError("failed 1")
d= D()
if (d.M2() !=  100):
    raise AssertionError("failed 2")
'''

    tempFileName1 = GetFullPath("tempFile1.py")
    tempFileName2 = GetFullPath("tempFile2.py")
    tempExeName1 = GetFullPath("tempFile1.exe")
    tempExeName2 = GetFullPath("tempFile2.exe")
    tempPdbName1 = GetFullPath("tempFile1.pdb")
    tempPdbName2 = GetFullPath("tempFile2.pdb")
    
    write_to_file(tempFileName1, tempFile1)
    write_to_file(tempFileName2, tempFile2)
    
    AreEqual(launch_ironpython_changing_extensions(tempFileName2, ["-X:SaveAssemblies"], ["-X:LightweightScopes", "-X:AssembliesDir"]), 0)
    RunPythonExe(tempExeName2)

    FileRemoval(tempFileName1, tempFileName2, tempExeName1, tempExeName2, tempPdbName1, tempPdbName2)

    #
    # Verify that the executable doesn't get generated
    #

    tempFile1 = """
import System
files = map(lambda extension: System.IO.Path.ChangeExtension(__file__, extension), [".dll", ".exe", ".pdb"])
for file in files:
    if System.IO.File.Exists(file):
        print file, "exists"
        raise AssertionError(file + " exists")
"""

    write_to_file(tempFileName1, tempFile1)
    AreEqual(launch_ironpython_changing_extensions(tempFileName1, [], ["-X:SaveAssemblies"]), 0)
    FileRemoval(tempFileName1, tempExeName1, tempPdbName1)

    source1  = "tempFile1.tpy"
    source2  = "tempFile2.tpy"
    assembly = "tempFile1.exe"
    pdbfile  = "tempFile1.pdb"

    write_to_file(source1, """
import tempFile2
if tempFile2.value != 8.0:
    raise AssertionError("failed import built-in")
""")
    
    write_to_file(source2, """
import math
value = math.pow(2, 3)
""")

    CompileTwoFilesAsConsoleApp(source1, source2, assembly, True)
    Assert(FileExists(assembly))
    Assert(FileExists(pdbfile))
    RunPythonExe(assembly)
    FileRemoval(source1, source2, assembly)

    # verify arguments are passed through...
    write_to_file(source1, """
import sys
def CustomAssert(c):
    if not c: raise AssertionError("Assertin Failed")

CustomAssert(sys.argv[0].lower() == sys.argv[4].lower())
sys.exit(int(sys.argv[1]) + int(sys.argv[2]) + int(sys.argv[3]))
""")

    CompileOneFileAsConsoleApp1(source1, assembly, False)
    RunPythonExe(assembly, 24, -22, -2, System.IO.Path.Combine(sys.prefix, assembly))
    RunPythonExe(".\\" + assembly, 24, -22, -2, System.IO.Path.Combine(sys.prefix, assembly))
    FileRemoval(source1, assembly)


@disabled("Needs to be updated or removed for DLR")
def test_compilersinktest():
    from IronPythonTest import PythonCompilerSinkTest
    st = PythonCompilerSinkTest()

    for s in ['''
    class Class:zxvc
        "Description of Class"cxvxcvb
    ''',
        '(1 and 1) = 1',
        '(lambda x: x = 1 )',
        '    print 1',
        '(name1 =1) = 1',
        '''d = {}
    for x in d.keys()
        pass
    ''',
        ]:
        Assert(st.CompileWithTestSink(s) > 0)
    
    for s in [
        'name = 1',
        '(name) = 1',
        '(name1,name2) = (1,2)',
        'name, = (1,0)',
        ]:
        Assert(st.CompileWithTestSink(s) == 0)

@disabled("ResourceFile is not available anymore")
def test_ip_hosting_resource_file():
    '''
    Test to hit IronPython.Hosting.ResourceFile.
    '''
    rf_list = [ IronPython.Hosting.ResourceFile("name0", "file0"),
                IronPython.Hosting.ResourceFile("name1", "file1", False),
                ]
    rf_list[0].PublicResource = False
                
                
    for i in range(len(rf_list)):
        AreEqual(rf_list[i].Name, "name" + str(i))
        rf_list[i].Name = "name"
        AreEqual(rf_list[i].Name, "name")
        
        AreEqual(rf_list[i].File, "file" + str(i))
        rf_list[i].File = "file"
        AreEqual(rf_list[i].File, "file")
        
        AreEqual(rf_list[i].PublicResource, False)
        rf_list[i].PublicResource = True
        AreEqual(rf_list[i].PublicResource, True)


@skip("multiple_execute")
def test_compiled_code():
    if System.Environment.GetEnvironmentVariable('DLR_SaveAssemblies'):
        # The SaveAssemblies option is not compatible with saving code to disk
        print('... skipping test if DLR_SaveAssemblies is set...')
        return

    import clr
    
    # make sure we can compile
    clr.CompileModules('test.pyil', testpath.public_testdir + '\\test_class.py')
    
    # make sure we can compile multiple files
    clr.CompileModules('test.pyil', testpath.public_testdir + '\\test_class.py', testpath.public_testdir + '\\test_slice.py')
    
    clr.AddReference('test.pyil')
    import nt
    
    # and make sure we can run some reasonable sophisticated code...
    System.IO.File.Move(testpath.public_testdir + '\\test_class.py', 'old_test_class.py')
    try:
        import test_class
        Assert(test_class.test_oldstyle_getattr.__doc__ != '')
    finally:
        System.IO.File.Move('old_test_class.py', testpath.public_testdir + '\\test_class.py')
        
@skip("multiple_execute")
def test_cached_types():
    import clr
    from System import IComparable, IFormattable, ICloneable
    import IronPythonTest    
    
    # basic sanity test that we can compile...
    clr.CompileSubclassTypes('test', (object, ))
    clr.CompileSubclassTypes('test', object)
    clr.CompileSubclassTypes('test', object, str, int, int, float, complex)
    clr.CompileSubclassTypes('test', (object, IComparable[()]))
    clr.CompileSubclassTypes('test', (object, IComparable[()]), (str, IComparable[()]))
    
    # build an unlikely existing type and make sure construction gives us
    # back the correct type.
    clr.CompileSubclassTypes('cached_type_dll', (object, IComparable[()], IFormattable, ICloneable))
    asm = System.Reflection.Assembly.Load('cached_type_dll')
    clr.AddReference(asm)
    
    class x(object, IComparable[()], IFormattable, ICloneable):
        pass
        
    a = x()
    AreEqual(clr.GetClrType(x).Assembly, asm)

    # collect all types that are available in IronPythonTest and
    # pre-gen them, then run test_inheritance to make sure it all works.
    types = []
    queue = [IronPythonTest]
    while queue:
        cur = queue.pop()
        for name in dir(cur):
            attr = getattr(cur, name)
            if type(attr) is type:
                clrType = clr.GetClrType(attr)
                if clrType.IsEnum or clrType.IsSealed or clrType.IsValueType or clrType.ContainsGenericParameters:
                    continue
                types.append(attr)
            elif type(attr) == type(IronPythonTest):
                queue.append(attr)

    clr.CompileSubclassTypes('InheritanceTypes', *types)
    clr.AddReference('InheritanceTypes')
    import test_inheritance

    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21892
    # verify that GetSubclassedTypes round trips with clr.CompileSubclassTypes
    clr.CompileSubclassTypes('finaltest', *clr.GetSubclassedTypes())
    clr.AddReference('finaltest')

run_test(__name__)
