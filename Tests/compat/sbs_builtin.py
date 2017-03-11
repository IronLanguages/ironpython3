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
import testdata

import sys

def complex_case_repr(*args):    
    ret = "complex with " 
    for x in args:
        ret += "'%s (%s)'" % (str(x), type(x))
    return ret

class test_builtin(object): 
    ''' test built-in type, etc '''
    
    def test_slice(self):
        ''' currently mainly test 
                del list[slice]
        '''
        test_str = testdata.long_string
        str_len  = len(test_str)
        
        choices = ['', 0]
        numbers = [1, 2, 3, str_len/2-1, str_len/2, str_len/2+1, str_len-3, str_len-2, str_len-1, str_len, str_len+1, str_len+2, str_len+3, str_len*2]
        numbers = numbers[::3] # Temporary approach to speed things up...
        choices.extend(numbers)
        choices.extend([-1 * x for x in numbers])
        
        for x in choices:
            for y in choices:
                for z in choices:
                    if z == 0:  continue

                    line = "l = list(test_str); del l[%s:%s:%s]" % (str(x), str(y), str(z))
                    exec(line)                
                    printwith("case", "del l[%s:%s:%s]" % (str(x), str(y), str(z)))
                    printwith("same", eval("l"), eval("len(l)"))

    def test_xrange(self):
        ''' test xrange with corner cases'''
        import sys
        maxint = sys.maxsize
        numbers = [1, 2, maxint/2, maxint-1, maxint, maxint+1, maxint+2]
        choices = [0]
        choices.extend(numbers)
        choices.extend([-1 * x for x in numbers])
        
        for x in choices:
            for y in choices:
                for z in choices:
                    line = "xrange(%s, %s, %s)" % (str(x), str(y), str(z))
                    printwith("case", line)
                    try: 
                        xr = eval(line)
                        xl = len(xr)
                        cnt = 0
                        first = last = first2 = last2 = "n/a"
                        # testing XRangeIterator
                        if xl < 10:
                            for x in xr: 
                                if cnt == 0: first = x
                                if cnt == xl -1 : last = x
                                cnt += 1
                        # testing this[index]
                        if xl == 0: first2 = xr[0]
                        if xl > 1 : first2, last2 = xr[0], xr[xl - 1]
                        
                        printwith("same", xr, xl, first, last, first2, last2)
                    except: 
                        printwith("same", sys.exc_info()[0])

    def test_complex_ctor_str(self):
        l = [ "-1", "0", "1", "+1", "+1.1", "-1.01", "-.101", ".234", "-1.3e3", "1.09e-3", "33.2e+10"] #, " ", ""] #http://ironpython.codeplex.com/workitem/28385
        
        
        for s in l:
            try: 
                printwith("case", complex_case_repr(s))
                c = complex(s)
                printwithtype(c)
            except: 
                printwith("same", sys.exc_info()[0], sys.exc_info()[1])
            
            s += "j"
            try: 
                printwith("case", complex_case_repr(s))
                c = complex(s)
                printwithtype(c)
            except: 
                printwith("same", sys.exc_info()[0], sys.exc_info()[1])
        
        for s1 in l:
            for s2 in l:
                try:
                    if s2.startswith("+") or s2.startswith("-"):
                        s = "%s%sJ" % (s1, s2)
                    else:
                        s = "%s+%sj" % (s1, s2)
                    
                    printwith("case", complex_case_repr(s))
                    c = complex(s)
                    printwithtype(c)
                except: 
                    printwith("same", sys.exc_info()[0], sys.exc_info()[1])
                    
    def test_complex_ctor(self):
        # None is not included due to defaultvalue issue
        ln = [-1, 1, 1.5, 1.5e+5, 1+2j, -1-9.3j ]
        ls = ["1", "1L", "-1.5", "1.5e+5", "-34-2j"]
        
        la = []
        la.extend(ln)
        la.extend(ls)
            
        for s in la:
            try:                
                printwith("case", complex_case_repr(s))
                c = complex(s)
                printwithtype(c)
            except:
                printwith("same", sys.exc_info()[0], sys.exc_info()[1])
        
        for s in la:
            try:                
                printwith("case", "real only", complex_case_repr(s))
                c = complex(real=s)
                printwithtype(c)
            except:
                printwith("same", sys.exc_info()[0], sys.exc_info()[1])

        for s in la:
            try:                
                printwith("case", "imag only", complex_case_repr(s))
                c = complex(imag=s)
                printwithtype(c)
            except:
                printwith("same", sys.exc_info()[0], sys.exc_info()[1])
                     
        for s1 in la:
            for s2 in ln:
                try:                
                    printwith("case", complex_case_repr(s1, s2))
                    c = complex(s1, s2)
                    printwithtype(c)
                except:
                    printwith("same", sys.exc_info()[0], sys.exc_info()[1])
    
    def test_bigint(self):
        s = '1234567890' 
        for x in range(10): s += str(x) * x
        s = s * 10

        l = [7, 1001, 5.89, True]
        for start in range(1, 50, 7):
            startx = start
            for length in [1, 20, 50, 60, 100]:
                startx += 1
                l.append(int(s[startx:startx + length]))
        
        for x in l:
            for y in l:
                print(x, y)
                printwith('case', '%s, %s' % (x, y))
                printwith('same', x+y)
                printwith('same', x-y)
                printwith('same', x*y)
                if y: 
                    printwith('same', x/y)
                    t = divmod(x, y)
                    printwithtype(t[0])
                    printwithtype(t[1])
        
        l.remove(5.89)
        l.remove(True) #
        for a in range(1, 100, 7):            
            for x in l:
                for y in l:
                    if x and y: 
                        printwith('case', a, x, y)
                        printwith('same', pow(a, x, y))
       
    def test_file_mode(self):
        disabled_modes = ['Ut+', 'rUt+', 'Urt+']
        disabled_modes += ['Ut', 'U+t', 'rUt', 'rU+t', 'Urt', 'Ur+t'] #http://ironpython.codeplex.com/workitem/28386
                          
        arw = ['', 'a', 'r', 'w', 'U', 'rU', 'Ur', 'wU', 'Uw', 'Ua', 'aU']
        bt = ['', 'b', 't']
        plus = ['', '+']
        modes = []

        for x in arw:
            for y in bt:
                for z in plus:
                    modes.append(x + y + z)
            for y in plus:
                for z in bt:
                    modes.append(x + y + z)
        
        modes = [x for x in modes if x not in disabled_modes]
        
        filename = 'tempfile.txt'
        for m in modes: 
            printwith('case', m)
            try:
                f = file(filename, m)
                s = str(f)
                atPos = s.find('at')
                printwith('same', s[:atPos])
                f.close()
            except: 
                printwith("same", 'throw')

runtests(test_builtin)