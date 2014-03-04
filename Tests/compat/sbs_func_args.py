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

from common import *
import sys

def f1(arg0, arg1, arg2, arg3): return "same## %s %s %s %s" % (arg0, arg1, arg2, arg3)
def f2(arg0, arg1, arg2=6, arg3=7): return "same## %s %s %s %s" % (arg0, arg1, arg2, arg3)
def f3(arg0, arg1, arg2, *arg3): return "same## %s %s %s %s" % (arg0, arg1, arg2, arg3)

if is_cli: 
    from iptest.process_util import run_csc 
    run_csc("/nologo /target:library /out:sbs_library.dll sbs_library.cs")
    import clr
    clr.AddReference("sbs_library")
    from SbsTest import C
    o = C()
    g1 = o.M1
    g2 = o.M2
    g3 = o.M3
    
    #for peverify runs
    from System.IO import Path, File, Directory
    if File.Exists(Path.GetTempPath() + r"\sbs_library.dll"):
        try:
            File.Delete(Path.GetTempPath() + r"\sbs_library.dll")
        except:
            pass
    if not File.Exists(Path.GetTempPath() + r"\sbs_library.dll"):
        File.Copy(Directory.GetCurrentDirectory() + r"\sbs_library.dll", Path.GetTempPath() + r"\sbs_library.dll")
    
else:
    g1 = f1
    g2 = f2
    g3 = f3

# combinations
choices = [(), (0,), (1,), (2,), (3,), (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3), (0, 1, 2), (0, 1, 3), (0, 2, 3), (1, 2, 3), (0, 1, 2, 3) ]

def to_str(l): return [str(x) for x in l] 

def replace_B(line):
    count = 0
    while True:
        pos = line.find("B")
        if pos == -1: break
        line = line[:pos] + str(1 + count) + line[pos+1:]
        count += 1
    return line
    
class func_arg(object):
    def _test_always_try_4(self, f_str):
        for simple in range(4):
        
            simple_string = ",".join(['B'] * simple)
            
            for keyword in choices:
                keyword_string = ",".join(["arg%s = B" % x for x in keyword])
                
                for tuple in range(4):
                    tuple_string = ",".join(['B'] * tuple)
                    
                    for dict in choices:
                        dict_string = ",".join(["'arg%s' : B" % x for x in dict])
                        
                        s = f_str
                        s += "("
                        
                        if simple_string: 
                            s += simple_string + ","
                        if keyword_string:
                            s += keyword_string + ","
                        
                        s += "*(" + tuple_string + "),"
                        s += "**{" + dict_string + "}"
                        s += ")"
                        
                        s = replace_B(s)
                        
                        try: 
                            printwith("case", s)
                            x = eval(s)
                            print x
                        except: 
                            printwith("same", sys.exc_type)
                            
    def test_pure_normal(self): self._test_always_try_4("f1")
    def test_pure_keyword(self): self._test_always_try_4("f2")
    def test_pure_params(self): self._test_always_try_4("f3")
    
    def test_cli_normal(self): self._test_always_try_4("g1")
    def test_cli_keyword(self): self._test_always_try_4("g2")
    def test_cli_params(self): self._test_always_try_4("g3")

runtests(func_arg)
