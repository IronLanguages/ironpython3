# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
AREA BREAKDOWN

  - COMMANDS (see http://msdn.microsoft.com/en-us/library/ms229861.aspx)
    Below is a list of all commands offered by the mdbg tool. Obviously not all
    commands will be relevant for testing IP's debugging feature, but 
    they are called out here for brevity.
    - ap[rocess] [number]
    - a[ttach] [pid]
    - b[reak] [ClassName.Method | FileName:LineNo]
      COVERED IN IP 2.0
    - ca[tch] [exceptionType]
      COVERED IN IP 2.0
    - conf[ig] [option value]
    - del[ete]
      COVERED IN IP 2.0
    - de[tach]
    - d[own] [frames]
    - echo
    - ex[it] [exitcode]
    - fo[reach] [OtherCommand]
    - f[unceval] [-ad Num] functionName [args ... ]
    - g[o]
      COVERED IN IP 2.0
    - h[elp] [command] or ? [command]
    - ig[nore] [event]
      COVERED IN IP 2.0
    - int[ercept] FrameNumber
    - k[ill]
    - l[ist] [modules|appdomains|assemblies]
    - lo[ad] assemblyName
    - mo[de] [option on/off]
      Sets different debugger options. The option parameter should be a 
      two-letter pair.
    - newo[bj] typeName [arguments...]
    - n[ext]
      COVERED IN IP 2.0
    - o[ut]
    - pa[th] [pathName]
    - p[rint] [var] | [-d]
    - pro[cessenum]
    - q[uit] [exitcode]
    - re[sume] [*|[~]threadNumber]
    - r[un] [-d(ebug) | -o(ptimize) | -enc] [[path_to_exe] [args_to_exe]]
      COVERED IN IP 2.0
    - Set variable=value
    - Setip [-il] number
    - sh[ow] [lines]
      COVERED IN IP 2.0
    - s[tep]
    - su[spend] [*|[~]threadNumber]
    - sy[mbol] commandName [commandValue]
    - t[hread] [newThread][-nick name]
    - u[p]
    - when
    - w[here] [-v] [-c depth] [threadID]
    - x [-c numSymbols] [module[!pattern]]
  - SCENARIOS
    TODO (7/30/2009): this section needs to be revisited.
    - MdbgClassMethod
      able to break into/step through new-style class's __init__ and other class
      method
    - MdbgDeclarations
      the step-through behavior when defining function, nested function and class
    - MdbgImport
      able to break into the imported module file
    - MdbgInheritance
      able to break into the base class's __init__ and other method when calling 
      the derived class's corresponding methods
    - MdbgInterop (not enabled yet)
      able to break into the C# code from the python code
      able to break back into the python code from the C# code
    - MdbgNameError
      able to catch runtime exception 
      verify the behavior when the runtime exception not caught 
    - MdbgNested
      able to break into simple function call, and python function calls another 
      python function
    - MdbgPyLang
      verify the step-through behavior on the following language structures
      * generator
      * list comprehension 
      * "exec"
      * Decorator
      * Lambda call
    - MdbgRecursive
      Able to break into the python code of a recursive function
    - MdbgRegression
      Some regression scenarios: 
      * 225309: unable to set bp on augmented assignment statements
      * 227218: the last line again when the nest try does not catch the 
        exception
    - MdbgStepThrough
      More step-through coverage on if /while /for /try-statement and their 
      combinations
      Step-through behavior for parallel assignment (aka. multiple-target 
      assignments)
    - MdbgTryCatch 
      The step-through behavior when exception is not thrown, and when 
      exception is thrown(again)
'''