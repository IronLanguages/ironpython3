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

import logmodule
import sys
from System.IO import File, Directory
from System.Diagnostics import Process

BUG_REPORT_PRE = """Implement rest of %s module


IP VERSION AFFECTED: %s
FLAGS PASSED TO IPY.EXE: None
OPERATING SYSTEMS AFFECTED: All

DESCRIPTION
"""    
    
def generate(exe, mod_name):
    proc = Process()
    proc.StartInfo.FileName = exe
    proc.StartInfo.Arguments = "logmodule.py " + mod_name
    proc.StartInfo.UseShellExecute = False
    proc.StartInfo.RedirectStandardOutput = True
    if not proc.Start():
        raise Exception("process failed to start")
    
    return proc.StandardOutput.ReadToEnd()

    
def add_diff(type, diffDict, path, line):
    curDict = diffDict
    for item in path:
        nextDict = curDict.get(item, None)
        if nextDict is None:
            nextDict = curDict[item] = {}
        
        curDict = nextDict
    
    curList = curDict.get(type, None)
    if curList is None:
        curDict[type] = curList = []
    curList.append(line)


def first_non_whitespace(s):
    spaceCnt = 0
    for c in s:
        if c != ' ':
            break
        spaceCnt += 1
    return spaceCnt
    
class ModuleDiffer(object):
    def __init__(self, mod_name, cpy_path):
        self.mod_name = mod_name
        self.cpy_path = cpy_path
        self.cpyout = generate(cpy_path, mod_name)
        self.ipyout = generate(sys.executable, mod_name)
        
        self.path = []
        self.cur_indent = ''
        self.next_indent = '  '
        self.prevline, self.previpyline = '', ''
        self.new_indent = True
        self.diffs = {}
        self.ipyindex, self.cpyindex = 0, 0
        self.ipytext, self.cpytext = self.ipyout.split('\n'), self.cpyout.split('\n')
    
    def process(self):
        while self.cpyindex < len(self.cpytext) and self.ipyindex < len(self.ipytext):
            self.cpyline, self.ipyline = self.cpytext[self.cpyindex], self.ipytext[self.ipyindex]
            self.cspaceCnt = first_non_whitespace(self.cpyline)
            self.ispaceCnt = first_non_whitespace(self.ipyline)
            
            # update our path to this line if the indentation level has changed.
            if self.cspaceCnt > self.ispaceCnt:
                self.basic_line = self.cpyline
            else:
                self.basic_line = self.ipyline
            
            self.process_indent()
            
            if self.cspaceCnt  == self.ispaceCnt:
                if self.compare_lines_same_indent():
                    continue
            else:
                self.compare_lines_different_indent()
                    
            self.prevline, self.previpyline = self.cpyline, self.ipyline            
                
        if self.cpyindex != len(self.cpytext):
            # we don't hit this, so we don't support this yet
            raise Exception()
        elif self.ipyindex != len(self.ipytext):
            # we don't hit this, so we don't support this yet
            raise Exception()
        
        return self.diffs
    
    def process_indent(self):
        if self.basic_line.startswith(self.next_indent):
            self.cur_indent = self.next_indent
            self.next_indent = self.next_indent + '  '
            newpath, newipypath = self.prevline.strip(), self.previpyline.strip()
            if newpath.startswith('Module: '): newpath = newpath[8:]
            if newipypath.startswith('Module: '): newipypath = newipypath[8:]
            if newpath != newipypath and self.cspaceCnt == self.ispaceCnt:
                self.path.append(newpath + '/' + newipypath)
            else:
                self.path.append(newpath)
            self.new_indent = True
        else:
            while not self.basic_line.startswith(self.cur_indent):
                self.next_indent = self.cur_indent
                self.cur_indent = self.cur_indent[:-2]
                self.path.pop()
                self.new_indent = True
    
    def compare_lines_same_indent(self):
        self.basic_line = self.cpyline
    
        if self.cpyline == self.ipyline or self.cspaceCnt == 0:
            # no difference
            self.ipyindex += 1
            self.cpyindex += 1
            self.prevline, self.previpyline = self.cpyline, self.ipyline
            return True
            
        # same indentation level
        if self.cpyline > self.ipyline:
            # ipy has an extra attribute
            add_diff('+', self.diffs , self.path, self.ipyline)
            self.ipyindex += 1
        else:
            # ipy is missing an attribute
            add_diff('-', self.diffs, self.path, self.cpyline)
            self.cpyindex += 1
    
    def compare_lines_different_indent(self):
        if self.cspaceCnt > self.ispaceCnt:
            while first_non_whitespace(self.cpytext[self.cpyindex]) > self.ispaceCnt:
                add_diff('-', self.diffs , self.path, self.cpytext[self.cpyindex])
                self.cpyindex += 1
        else:
            while first_non_whitespace(self.ipytext[self.ipyindex]) > self.cspaceCnt:
                add_diff('+', self.diffs , self.path, self.ipytext[self.ipyindex])
                self.ipyindex += 1


def diff_module(mod_name, cpy_path):
    return ModuleDiffer(mod_name, cpy_path).process()

def collect_diffs(diffs, type):
    res = []
    collect_diffs_worker(res, '', diffs, type)
    return res
        

def collect_diffs_worker(res, path, diffs, diffType):
    for key in sorted(list(diffs)):
        if (key == '+' or key == '-') and key != diffType:
            continue
        value = diffs[key]
        if type(value) is dict:
            if path:
                newpath = path + '.' + key
            else:
                newpath = key
            collect_diffs_worker(res, newpath, value, diffType)
        else:
            for name in value:
                res.append(path + name)


def gen_bug_report(mod_name, diffs, outdir):
    needs_to_be_implemented = collect_diffs(diffs, '-')
    needs_to_be_removed = collect_diffs(diffs, '+')
    
    if not needs_to_be_implemented and not needs_to_be_removed:
        return
    
    bug_report_name = outdir + "\\%s.log" % mod_name
    bug_report = open(bug_report_name, "w")
    bug_report.write(BUG_REPORT_PRE % (mod_name, str(sys.winver)))

    #--unfiltered list of attributes to be added
    if len(needs_to_be_implemented)>0:
        bug_report.write("-------------------------------------------------------\n")
        bug_report.write("""Complete list of module attributes IronPython is still 
missing implementations for:
""")
        for x in needs_to_be_implemented:
            bug_report.write("    " + x + '\n')
        bug_report.write("\n\n\n")
    
    #--unfiltered list of attributes to be removed
    if len(needs_to_be_removed)>0:
        bug_report.write("-------------------------------------------------------\n")
        bug_report.write("""Complete list of module attributes that should be removed 
from IronPython:
""")
        for x in needs_to_be_removed:
            bug_report.write("    " + x + '\n')
    
    bug_report.close()
    return bug_report_name


def check_baseline(bugdir, baselinedir, module):
    bugfile = file(bugdir + '\\' + module + '.log')
    basefile = file(baselinedir + '\\' + module + '.log')
    fail = False
    for bug, base in zip(bugfile, basefile):
        comment = base.find('#')
        if comment != -1:
            base = base[:comment]
        base, bug = base.strip(), bug.strip()
        if base != bug:
            print 'Differs from baseline!', base, bug
            fail = True
    return fail


def gen_one_report(module, cpy_path, outdir = 'baselines'):
    if not Directory.Exists(outdir):
        Directory.CreateDirectory(outdir)
    
    print 'processing', module
    diffs = diff_module(module, cpy_path)
    return gen_bug_report(module, diffs, outdir)


def gen_all(cpy_path):
    for module in ['types_only'] + logmodule.BUILTIN_MODULES:
        gen_one_report(module, cpy_path)


def diff_all(cpy_path):
    differs = False
    for module in ['types_only'] + logmodule.BUILTIN_MODULES:
        outfile = gen_one_report(module, cpy_path, 'check')
        if not outfile:
            # TODO: Make sure we don't have an existing baseline
            pass
        elif check_baseline('check', 'baselines', module):
            print 'Module differs', module
            differs = True
            
    # if a module gets implemented we need to add a new baseline for it
    for mod in logmodule.MISSING_MODULES:
        try:
            __import__(mod)
        except ImportError:
            pass
        else:
            print """module %s has been implemented.  
            
It needs to be moved into the BUILTIN_MODULES list and a baseline should be generated using:

ipy modulediff.py C:\\Python27\\python.exe %s""" % (mod, mod)
            differs = 1
    
    sys.exit(differs)


if __name__ == '__main__':
    if len(sys.argv) < 2 or not File.Exists(sys.argv[1]):
        print 'usage: '
        print '       ipy modulediff.py C:\\Python27\\python.exe module_name [module_name ...]'
        print '            generates a report for an indivdual module into the baselines directory'
        print 
        print '       ipy modulediff.py C:\\Python27\\python.exe --'
        print '            generates reports for all modules into the baselines directory'
        print
        print '       ipy modulediff.py C:\\Python27\\python.exe'
        print '            generates reports into check directory, compares with baselines directory, if there'
        print '            are differences exits with a non-zero exit code.'
        print 
        print 'For rapdily iteration of finding and fixing issues a good idea is to run:'
        print '  ipy logmodule.py > out.txt'
        print '  cpy logmodule.py > outcpy.txt'
        print '  windiff outcpy.txt out.txt'
        print 
        print 'instead of using this script directly.  This will produce all of the diffs in one big file in a matter'
        print 'of seconds and can avoid the overhead of re-generating the CPython results each run.'
        
        sys.exit(1)
    
    if len(sys.argv) > 2:
        if sys.argv[2] == '--':
            gen_all(sys.argv[1])
        else:
            for module in sys.argv[2:]:
                gen_one_report(module, sys.argv[1])
    else:
        diff_all(sys.argv[1])