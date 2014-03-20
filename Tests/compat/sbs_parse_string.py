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

prefixs = ['', 'r', 'u', 'ur', 'R', 'U', 'UR', 'Ur', 'uR']
def generate_str1():
    s = "[ \n"
    for prefix in prefixs :
        for x in range(5):
            s += "  %s\"a%su0020B\", \n" % (prefix, "\\" * x)
    for prefix in prefixs :
        for x in range(5):
            s += "  %s\"A%stb\", \n" % (prefix, "\\" * x)
    for prefix in prefixs :
        for x in range(5):
            s += "  %s\"a%sx34b\", \n" % (prefix, "\\" * x)
    for prefix in prefixs :
        for x in range(5):
            for y in range(5):
                s + "  %s\"A%su0020%stB\", \n" % (prefix, "\\" * x, "\\"*y)
    s += "] \n"
    
    return s

class test_parse_string(object): 
    # always valid \u \t sequence
    def test_scenario1(self):
        list_string = eval(generate_str1())
        
        for x in list_string:
            print len(x)
            print x

    # could be invalid \ sequence
    def test_scenario2(self):
        for prefix in prefixs :
            for template in [
                "%s\"%s\"",         # "\\\\\"
                "%s\'%sa\'",        # '\\\\a'
                "%s\'a%sb\'",       # 'a\\\b'
                "%s\'\u%s\'",  
                "%s\'\u0%s\'",  
                "%s\'\u00%s\'",  
                "%s\'\u002%s\'",  
                "%s\'\u0020%s\'",  
                "%s\'\\u%s\'",   
                "%s\'\\u0%s\'",   
                "%s\'\\u00%s\'",   
                "%s\'\\u002%s\'",   
                "%s\'\\u0020%s\'",   
                "%s\'%s\u\'",    
                "%s\'%s\u0\'",    
                "%s\'%s\u00\'",    
                "%s\'%s\u002\'",    
                "%s\'%s\u0020\'",    
                "%s\'\\u002%s\'",   
                ] :
                for x in range(10):
                    line = template % (prefix, "\\" * x)
                    try:
                        printwith("case", line)
                        str = eval(line)
                        print len(str)
                        print str
                    except: 
                        print "exception"

######################################################################################

def apply_format(s, l, onlypos = False):   
    for y in l:
        for x in (y-1, y, y+1):
            printwith("case", s, x)
            printwith("same", s % x)
            
            if onlypos: continue
                        
            printwith("case", s, -1 * x)
            printwith("same", s % (-1 * x))

class test_formating(object): 
    def test_formating1(self):
        for ss in ['+0', '-0', '+ 0', '- 0', '0', ' 0', '+', '-', '+ ', '- ', ' +', ' -','#', "#0", "+#0"]:
            for i in (1, 2, 3):
                for k in ('d', ):
                    s = "%" + ss + str(i) + k
                    apply_format(s, [0, 10, 100, 1000, 10000, 1234567890123456])
                
                for k in ('u', 'x', 'o', 'X'):
                    s = "%" + ss + str(i) + k
                    apply_format(s, [0, 10, 100, 1000, 10000, 1234567890123456], True)
                    
            for i in ('8.2', '8.3', '7.2', '3.2', '.2'):
                s = "%" + ss + i + "f"
                apply_format(s, [0, 10, 100, 1000, 10000, 0.01, 0.1, 1.01, 100.3204])
            
    # reasonal difference?
    def test_formating2(self):
        for ss in ['+0', '-0', '+ 0', '- 0', '0', ' 0', '+', '-', '+ ', '- ', ' +', ' -','#', "#0", "+#0"]:
            for i in ('8.', '8.0', '8.1'):
                s = "%" + ss + i + "f"
                apply_format(s, [0, 10, 100, 1000, 10000, 0.01, 0.1, 1.01, 100.3204])


runtests(test_parse_string)    
runtests(test_formating)


