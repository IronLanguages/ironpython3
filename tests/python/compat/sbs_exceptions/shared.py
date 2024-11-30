# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import runtests, printwith

import sys
import os

class CodeHolder(object):
    def __init__(self):
        self.text = ''
        self.depth = 0
    def Add(self, text):
        self.text += text
       
class try_finally_generator(object):
    def __init__(self, codeHolder, tryBody, finallyBody):
        self.code = codeHolder
        self.tryBody = tryBody
        self.finallyBody = finallyBody
    def generate(self, indent=1):
        self.code.Add('    '*indent); self.code.Add('try:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="try"\n')
        self.tryBody.generate(indent+1)
        self.code.Add('    '*indent); self.code.Add('finally:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="finally"\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+=dump_exc_info()\n')
        self.finallyBody.generate(indent+1)
    
class try_except_generator(object):
    def __init__(self, codeHolder, tryBody, exceptBody):
        self.code = codeHolder
        self.tryBody = tryBody
        self.exceptBody = exceptBody
    def generate(self, indent=1):
        self.code.Add('    '*indent); self.code.Add('try:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="try"\n')
        self.tryBody.generate(indent+1)
        self.code.Add('    '*indent); self.code.Add('except:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="except"\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+=dump_exc_info()\n')
        self.exceptBody.generate(indent+1)
    
class try_except_else_generator(object):        
    def __init__(self, codeHolder, tryBody, exceptBody, elseBody):
        self.code = codeHolder
        self.tryBody = tryBody
        self.exceptBody = exceptBody
        self.elseBody = elseBody
    def generate(self, indent=1):
        self.code.Add('    '*indent); self.code.Add('try:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="try"\n')
        self.tryBody.generate(indent+1)
        self.code.Add('    '*indent); self.code.Add('except:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="except"\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+=dump_exc_info()\n')
        self.exceptBody.generate(indent+1)
        self.code.Add('    '*indent); self.code.Add('else:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="else"\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+=dump_exc_info()\n')
        self.elseBody.generate(indent+1)    
        
class for_loop_generator(object):
    def __init__(self, codeHolder, var, items, body):
        self.code = codeHolder
        self.var = var
        self.items = items
        self.body = body
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="preloop"\n')
        self.code.Add('    '*indent);self.code.Add('for %s in %s:\n' % (self.var+str(indent), self.items))
        self.code.Add('    '*(indent+1));self.code.Add('log+="inloop"\n')
        self.body.generate(indent+1)

class while_loop_generator(object):
    def __init__(self, codeHolder, body):
        self.code = codeHolder
        self.body = body
    def generate(self, indent=1):
        global uniqueCount
        self.code.Add('    '*indent);self.code.Add('log+="preloop"\n')
        self.code.Add('    '*indent);self.code.Add('whilevar%d_%d = 0\n' % (indent, uniqueCount))
        self.code.Add('    '*indent);self.code.Add('while whilevar%d_%d < 3:\n' % (indent, uniqueCount))
        self.code.Add('    '*(indent+1));self.code.Add('whilevar%d_%d += 1\n' % (indent, uniqueCount))
        self.code.Add('    '*(indent+1));self.code.Add('log+="inloop"\n')
        uniqueCount += 1
        self.body.generate(indent+1)

class pass_generator(object):
    def __init__(self, codeHolder):
        self.code = codeHolder
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="pass"\n')
        self.code.Add('    '*indent);self.code.Add('pass\n')
        
class break_generator(object):
    def __init__(self, codeHolder):
        self.code = codeHolder
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="break"\n')
        self.code.Add('    '*indent);self.code.Add('break\n')

class continue_generator(object):
    def __init__(self, codeHolder):
        self.code = codeHolder
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="continue"\n')
        self.code.Add('    '*indent);self.code.Add('continue\n')

class return_generator(object):
    def __init__(self, codeHolder, state):
        self.code = codeHolder
        self.state = state
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="return"\n')
        self.code.Add('    '*indent);self.code.Add('return %s\n' % self.state)

class if_false_generator(object):
    def __init__(self, codeHolder, body):
        self.code = codeHolder
        self.body = body
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="preif"\n')
        self.code.Add('    '*indent);self.code.Add('if False:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="huh?"\n')
        self.body.generate(indent+1)

class if_true_generator(object):
    def __init__(self, codeHolder, body):
        self.code = codeHolder
        self.body = body
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="preif"\n')
        self.code.Add('    '*indent);self.code.Add('if True:\n')
        self.code.Add('    '*(indent+1));self.code.Add('log+="true!"\n')
        self.body.generate(indent+1)

class yield_generator(object):
    def __init__(self, codeHolder, state):
        self.code = codeHolder
        self.state = state
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('log+="yield"\n')
        self.code.Add('    '*indent);self.code.Add('yield %s\n' % self.state)
        
class raise_generator(object):
    def __init__(self, codeHolder, state):
        self.code = codeHolder
        self.state = state
    def generate(self, indent=1):
        self.code.Add('    '*indent);self.code.Add('raise Exception("%s")\n' % self.state)

class define_generator(object):
    def __init__(self, codeHolder, body):
        self.code = codeHolder
        self.body = body
    def generate(self, indent=1):
        global uniqueCount
        saved = uniqueCount
        uniqueCount += 1
        self.code.Add('    '*indent);self.code.Add('log+="predefine"\n')
        self.code.Add('    '*indent);self.code.Add('def func%d_%d():\n' % (indent, saved))
        self.code.Add('    '*(indent+1));self.code.Add('global log\n')
        self.body.generate(indent+1)
        self.code.Add('    '*indent);self.code.Add('func%d_%d()\n' % (indent, saved))

ch = CodeHolder()

def for_loop_maker(ch, body):
    return for_loop_generator(ch, 'x', 'range(3)', body)
    
def while_loop_maker(ch, body):
    return while_loop_generator(ch, body)
    
def try_except_maker1(ch, body):
    return try_except_generator(ch, pass_generator(ch), body)

def try_except_maker2(ch, body):
    return try_except_generator(ch, body, pass_generator(ch))

def try_except_maker3(ch, body):
    return try_except_generator(ch, body, body)

def try_finally_maker1(ch, body):
    return try_finally_generator(ch, pass_generator(ch), body)

def try_finally_maker2(ch, body):
    return try_finally_generator(ch, body, pass_generator(ch))

def try_finally_maker3(ch, body):
    return try_finally_generator(ch, body, body)

def try_else_maker1(ch, body):
    return try_except_else_generator(ch, pass_generator(ch), body, body)

def pass_maker(ch, body):
    return pass_generator(ch)

def break_maker(ch, body):
    return break_generator(ch)
    
def continue_maker(ch, body):
    return continue_generator(ch)

def define_maker(ch, body):
    return define_generator(ch, body)

def generator(): yield 2
generator_type = type(generator())

loopCnt = 0
generatorDepth = 0
yieldState = 0
finallyCnt = 0
tryOrCatchCount = 0

uniqueCount = 0

def dump_exc_info():
    tb = sys.exc_info()[2]
    tb_list = []
    while tb is not None :
        f = tb.tb_frame
        co = f.f_code
        filename = co.co_filename
        #Shrink the filename a bit
        if filename.count("\\"):
            filename = filename.rsplit("\\", 1)[1]
        name = co.co_name
        tb_list.append((tb.tb_lineno, filename, name))
        tb = tb.tb_next
    return str(tb_list)


#------------------------------------------------------------------------------
allGenerators = []
    
def setGenerator(generator):
    global allGenerators
    allGenerators = [ generator ]


knownFailures = []

def setKnownFailures(failures):
    global knownFailures
    knownFailures = failures
    

class test_exceptions(object):
    
    def test_exceptions(self):    
        if len(allGenerators)==0:
            raise Exception("Need at least one generator from test_exceptions")
    
        stateful = [raise_generator, return_generator]
        
        ch = CodeHolder()
        
        curTest = 0 # a counter so that trace backs have unique method names and we check left over info properly
        for depth in range(3):
            def do_generate(test):
                global loopCnt, yieldState, finallyCnt, tryOrCatchCount, uniqueCount
                yieldState += 1
                
                if test in (for_loop_maker,  while_loop_maker):
                    loopCnt += 1
                if test in (try_finally_maker1, try_finally_maker2, try_finally_maker3):
                    finallyCnt += 1
                if test in (try_except_maker1, try_except_maker2, try_except_maker3, try_else_maker1):
                    tryOrCatchCount += 1
                 

                genSet = [  for_loop_maker, while_loop_maker,
                            try_except_maker1, try_except_maker2, try_except_maker3, 
                            try_finally_maker1, try_finally_maker2, try_finally_maker3, 
                            try_else_maker1, 
                            if_false_generator, if_true_generator, 
                            define_maker,
                            ]    

                if loopCnt > 0: 
                    if finallyCnt > 0: genSet = genSet + [break_maker]        
                    else: genSet = genSet + [break_maker, continue_maker]        
                
                if ch.depth > depth:
                    yield test(ch, pass_generator(ch))
                else:
                    for testCase in genSet:
                        ch.depth += 1
        
                        x = do_generate(testCase)
                        for body in x:                            
                            yield test(ch, body)
        
                        ch.depth -= 1
                
                for statefulGuy in stateful:
                    yield test(ch, statefulGuy(ch, yieldState))
                
                if finallyCnt == 0:
                    yield test(ch, yield_generator(ch, yieldState))
                
                if test in (for_loop_maker,  while_loop_maker):
                    loopCnt -= 1        
                if test in (try_finally_maker1, try_finally_maker2, try_finally_maker3):
                    finallyCnt -= 1
                if test in (try_except_maker1, try_except_maker2, try_except_maker3, try_else_maker1):
                    tryOrCatchCount -= 1
                        
            for testCase in allGenerators:
                x = do_generate(testCase)
                        
                for y in x:
                    curTest += 1
                    
                    if curTest in knownFailures:
                        continue
                    
                    if 'IRONPYTHON_RUNSLOWTESTS' in os.environ:
                        uniqueCount = 0
    
                        # run without a function                    
                        y.code.text = ''
                        y.generate(0)
                        y.code.text += 'print log'
                        d = {'log': '', 'dump_exc_info': dump_exc_info}
                        try: 
                            #printwith(y.code.text)
                            exec(y.code.text, d, d)
                        except Exception as e:
                            printwith('same', sys.exc_info()[0])

                    uniqueCount = 0
                    
                    # run within a function
                    y.code.text = 'def test' + str(curTest) + '():\n'
                    y.code.text += '    global log\n'
                    y.generate()
                    d = {'log' : '', 'dump_exc_info': dump_exc_info}
                    
                    try:
                        printwith(y.code.text)
                        exec(y.code.text, d, d)
                    except SyntaxError:
                        printwith("same", sys.exc_info()[0])
                        continue
                    
                    retval = None
                    try:
                        retval = d['test' + str(curTest)]()
                        if isinstance(retval, generator_type):
                            for it in retval: printwith('same', it)
                        else:
                            printwith('same', retval)
                    except: 
                        printwith("same", sys.exc_info()[0])
                    if isinstance(retval, generator_type):
                        retval.close()
                    printwith('same', d['log'])