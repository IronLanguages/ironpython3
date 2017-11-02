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

import sys
import clr
import System

sys.path.append(sys.exec_prefix)
clr.AddReference("Microsoft.Scripting")
clr.AddReference("Microsoft.Dynamic")
if clr.IsNetCoreApp:
    clr.AddReference("System.IO.FileSystem")
    clr.AddReference("System.Runtime.Extensions")
else:
    clr.AddReference("System.Core")
    
clr.AddReference("IronPython")
clr.AddReference("IronPython.Modules")

from Microsoft.Scripting import SourceCodeKind, ErrorSink
from Microsoft.Scripting.Hosting import ScriptRuntime
from Microsoft.Scripting.Hosting.Providers import HostingHelpers
from Microsoft.Scripting.Runtime import CompilerContext
from IronPython import PythonOptions
from IronPython.Hosting import Python
from IronPython.Runtime import PythonContext, ModuleOptions, Symbols
from IronPython.Compiler import Parser, PythonCompilerOptions
from IronPython.Compiler.Ast import SuiteStatement, FunctionDefinition
from System import Type, Array, UriBuilder
from System.Reflection import Assembly
from System.IO import Directory, Path, File

#--------------------------------------------------------------------------------------
# Class that takes a file and runs it in interactive mode using the hosting APIs

class FileConsole(object):
    def __init__(self, fileName):
        scriptEnv = Python.CreateRuntime()
        self.fileName = fileName
        self.engine = scriptEnv.GetEngine("python")        
        self.context = HostingHelpers.GetLanguageContext(self.engine) 
        scriptEnv.LoadAssembly(Type.GetType("System.String").Assembly) #mscorlib.dll
        scriptEnv.LoadAssembly(UriBuilder().GetType().Assembly)  #System.dll
                
        self.InitializePath()
        
        executable = Assembly.GetEntryAssembly().Location
        prefix = Path.GetDirectoryName(executable)
        
        self.context.SystemState.executable = executable
        self.context.SystemState.exec_prefix = self.context.SystemState.prefix = prefix
        
        import imp
        mod = imp.new_module('__main__')
        mod.__file__ = fileName
        mod.__builtins__ = sys.modules['__builtin__']
        self.context.SystemState.modules['__main__'] = mod
        self.mainScope = scriptEnv.CreateScope(mod.__dict__)
        
    def InitializePath(self):
        searchPath = []
        currentDir = Directory.GetCurrentDirectory()
        searchPath.append(currentDir)
        filePathDir = Path.GetDirectoryName(Path.Combine(currentDir, self.fileName))
        searchPath.append(filePathDir)
        entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
        searchPath.append(entryDir)
        siteDir = Path.Combine(entryDir, "Lib")
        searchPath.append(siteDir)
        devStdLibDir = Path.Combine(entryDir, '../../External.LCA_RESTRICTED/Languages/IronPython/27/Lib')
        searchPath.append(devStdLibDir)
        dllsDir = Path.Combine(entryDir, "DLLs")
        if Directory.Exists(dllsDir):
            searchPath.append(dllsDir)

        self.engine.SetSearchPaths(Array[str](searchPath))
        
    def CreateASTFromFile(self, fileName):
        completeCode = self.engine.CreateScriptSourceFromFile(fileName)
        sourceUnit = HostingHelpers.GetSourceUnit(completeCode)
        cc = CompilerContext(sourceUnit, PythonCompilerOptions(), ErrorSink.Default)
        parser = Parser.CreateParser(cc, PythonOptions())
        return parser.ParseFile(False), sourceUnit.GetCode()
        
    def GetCodeForStatement(self, codeText, statement):
        decoratorStart, decoratorLength = -1, 0
        if isinstance(statement, FunctionDefinition):            
            if (statement.Decorators != None and len(statement.Decorators) != 0):
                decoratorStart = min([x.Start.Index for x in statement.Decorators])                
                decoratorLength = statement.Start.Index - decoratorStart                
        return codeText.Substring( statement.Start.Index if decoratorStart == -1 else decoratorStart, statement.Span.Length + decoratorLength)

    def Run(self):
        ast, codeText = self.CreateASTFromFile(self.fileName)
                        
        if isinstance(ast.Body, SuiteStatement):            
            suiteStatement = ast.Body
            for statement in suiteStatement.Statements:                
                code = self.GetCodeForStatement(codeText, statement)
                codeUnit = self.engine.CreateScriptSourceFromString(code + '\n\n', SourceCodeKind.InteractiveCode)                
                codeProps = codeUnit.GetCodeProperties()
                codeUnit.Execute(self.mainScope)                
                
#--------------------------------------------------------------------------------------

def run_interactive_main():
    #if the commandline was invoked so: ipy run_interactive.py test_x.py then run just that one test.
    testName = sys.argv[1] if len(sys.argv) > 1 else None
    
    if testName:
        testsToRun = Directory.GetFiles(Directory.GetCurrentDirectory() , testName)        
    else:
	    print "No test name provided"
	    sys.exit(-1)
	    
    allErrors = []
    for test in testsToRun:
        try:
            print "\nRunning test in interactive mode - ", test
            con = FileConsole(test)        
            con.Run()
        except Exception, e:
            print e, e.clsException
            allErrors.append((test, sys.exc_info()[0], sys.exc_info()[1]))

    if(allErrors):
        print "\n##################################################################################"
        print "Summary of all errors"
        for file, type, message in allErrors:
            print file, type, message
        sys.exit(len(allErrors))

#--------------------------------------------------------------------------------------
if __name__ == "__main__":
    run_interactive_main()
#--------------------------------------------------------------------------------------