/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

public class dynamic_sanity
{
    private delegate string SanityDelegate(object p1);

    static void Main()
    {
        var py_run = Python.CreateRuntime();
        var py_eng = py_run.GetEngine("py");
        var scope1 = py_run.CreateScope();

        dynamic mock = py_run.UseFile("mock.py");
        dynamic m = mock.m;
        dynamic who_knows = m.the_csharp_compiler_cannot_possibly_know_this_member_exists_at_compile_time;

        var src = py_eng.CreateScriptSourceFromString(@"
def py_hello(some_object):
   return 'Hello ' + str(some_object)", Microsoft.Scripting.SourceCodeKind.Statements);
        src.Execute(scope1);
        SanityDelegate sd = scope1.GetVariable<SanityDelegate>("py_hello");
        System.Console.WriteLine(sd(who_knows));
    }
}


