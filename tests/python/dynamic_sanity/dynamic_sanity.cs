// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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


