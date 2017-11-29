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

'''
This module is in place to test the interoperatbility between CPython and CLR numerical
types.

TODO:
- at the moment the test cases in place are simple sanity checks to ensure the
  appropriate operator overloads have been implemented.  This needs to be extended
  quite a bit (e.g., see what happens with OverFlow cases).
- a few special cases aren't covered yet - comlex numbers, unary ops, System.Char,

'''

import unittest
from iptest import is_cli, run_test

#Test Python/CLR number interop.
clr_integer_types = [ "System.Byte",
                      "System.SByte",
                      "System.Int16",
                      "System.UInt16",
                      "System.Int32",
                      "System.UInt32",
                      "System.Int64",
                      "System.UInt64"]

clr_float_types = [ "System.Single",
                    "System.Double",
                    "System.Decimal",
                  ]

#TODO - char???
clr_types = clr_integer_types + clr_float_types


py_integer_types = ["int"]
py_float_types   = ["float"]

#TODO - special case complex???
py_types = py_integer_types + py_float_types

bug_operands = []
unsupported_operands = [
                         #System.Decimal +: System.Single, System.Double, long, float
                         "System.Decimal+int",
                         "int+System.Decimal",
                         
                         #System.Decimal -: System.Single, System.Double, long, float
                         "System.Decimal-int",
                         "int-System.Decimal",
                         
                         #System.Decimal *: System.Single, System.Double, long, float
                         "System.Decimal*int",
                         "int*System.Decimal",
                         
                         #System.Decimal /:System.Single, System.Double, long, float
                         "System.Decimal/int",
                         "int/System.Decimal",
                         
                         #System.Decimal //:System.Byte System.SByte
                         "System.Decimal//System.Byte",
                         "System.Decimal//System.SByte",
                         "System.Decimal//System.Int16",
                         "System.Decimal//System.UInt16",
                         "System.Decimal//System.Int32",
                         "System.Decimal//System.UInt32",
                         "System.Decimal//System.Int64",
                         "System.Decimal//System.UInt64",
                         "System.Decimal//System.Decimal",
                         "System.Decimal//int",
                         "System.Decimal//long",
                         "System.Byte//System.Decimal",
                         "System.SByte//System.Decimal",
                         "System.Int16//System.Decimal",
                         "System.UInt16//System.Decimal",
                         "System.Int32//System.Decimal",
                         "System.UInt32//System.Decimal",
                         "System.Int64//System.Decimal",
                         "System.UInt64//System.Decimal",
                         "System.Decimal//System.Decimal",
                         "int//System.Decimal",
                         "long//System.Decimal",
                         
                         "System.Decimal**System.Byte",
                         "System.Decimal**System.SByte",
                         "System.Decimal**System.Int16",
                         "System.Decimal**System.UInt16",
                         "System.Decimal**System.Int32",
                         "System.Decimal**System.UInt32",
                         "System.Decimal**System.Int64",
                         "System.Decimal**System.UInt64",
                         "System.Decimal**System.Decimal",
                         "System.Decimal**int",
                         "System.Decimal**long",
                         "System.Byte**System.Decimal",
                         "System.SByte**System.Decimal",
                         "System.Int16**System.Decimal",
                         "System.UInt16**System.Decimal",
                         "System.Int32**System.Decimal",
                         "System.UInt32**System.Decimal",
                         "System.Int64**System.Decimal",
                         "System.UInt64**System.Decimal",
                         "System.Decimal**System.Decimal",
                         "int**System.Decimal",
                         "long**System.Decimal",
                         
                         "System.Decimal%long",
                         "long%System.Decimal",
                            ] + bug_operands


known_bugs = []

bool_test_cases = [
                    #x==x
                    ("0","==","0", True),
                    ("0.0","==","0", True),
                    ("1","==","1", True),
                    ("3","==","3", True),
                    ("-1","==","-1", True),
                    
                    #! x==x
                    ("10","==","0", False),
                    ("10.0","==","0", False),
                    ("11","==","1", False),
                    ("31","==","3", False),
                    ("-11","==","-1", False),
                    
                    #x!=x
                    ("10","!=","0", True),
                    ("10.0","!=","0", True),
                    ("11","!=","1", True),
                    ("31","!=","3", True),
                    ("-11","!=","-1", True),
                    
                    #! x!=x
                    ("0","!=","0", False),
                    ("0.0","!=","0", False),
                    ("1","!=","1", False),
                    ("3","!=","3", False),
                    ("-1","!=","-1", False),
                    
                    #x<=x
                    ("0","<=","0", True),
                    ("0.0","<=","0", True),
                    ("1","<=","1", True),
                    ("3","<=","3", True),
                    ("-1","<=","-1", True),
                        
                    #! x<=x
                    ("10","<=","0", False),
                    ("10.0","<=","0", False),
                    ("11","<=","1", False),
                    ("13","<=","3", False),
                    ("10","<=","-1", False),
                    
                    #x>=x
                    ("0",">=","0", True),
                    ("0.0",">=","0", True),
                    ("1",">=","1", True),
                    ("3",">=","3", True),
                    ("-1",">=","-1", True),
                   
                    #! x>=x
                    ("0",">=","10", False),
                    ("0.0",">=","10", False),
                    ("1",">=","11", False),
                    ("3",">=","13", False),
                    ("-1",">=","11", False),
                    
                    #x<=/<y
                    ("0", "<=", "1", True),
                    ("0", "<", "1", True),
                    ("3.14", "<=", "19", True),
                    ("3.14", "<", "19", True),
                    
                    #!x<=/<y
                    ("10", "<=", "1", False),
                    ("10", "<", "1", False),
                    ("31.14", "<=", "19", False),
                    ("31.14", "<", "19", False),
                         
                    #x>=/.y
                    ("10", ">=", "1", True),
                    ("10", ">", "1", True),
                    ("31.14", ">=", "19", True),
                    ("31.14", ">", "19", True),
                    
                    #! x>=/.y
                    ("0", ">=", "1", False),
                    ("0", ">", "1", False),
                    ("3.14", ">=", "19", False),
                    ("3.14", ">", "19", False),
                   ]

arith_test_cases = [
                    #add
                    ("0", "+", "0", 0),
                    ("0", "+", "1", 1),
                    ("1", "+", "-1", 0),
                    ("2", "+", "-1", 1),
                    
                    #sub
                    ("0", "-", "0", 0),
                    ("0", "-", "1", -1),
                    ("1", "-", "-1", 2),
                    ("2", "-", "-1", 3),
                    
                    #mult
                    ("0", "*", "0", 0),
                    ("0", "*", "1", 0),
                    ("2", "*", "1", 2),
                    ("1", "*", "-1", -1),
                    ("2", "*", "-1", -2),
                    
                    #div
                    ("0", "/", "1", 0),
                    ("4", "/", "2", 2),
                    ("2", "/", "1", 2),
                    ("1", "/", "-1", -1),
                    ("2", "/", "-1", -2),
                    
                    #trun div
                    ("0", "//", "1", 0),
                    ("4", "//", "2", 2),
                    ("2", "//", "1", 2),
                    ("1", "//", "-1", -1),
                    ("2", "//", "-1", -2),
                    ("3", "//", "2", 1),
                    
                    #power
                    ("0", "**", "1", 0),
                    ("4", "**", "2", 16),
                    ("2", "**", "1", 2),
                    ("1", "**", "-1", 1),
                    
                    #mod
                    ("0", "%", "1", 0),
                    ("5", "%", "2", 1),
                    ("2", "%", "1", 0),
                    ("1", "%", "-1", 0),
                    ("2", "%", "-1", 0),
                   ]

bitwise_test_cases = [
                        #left shift
                        ("0", "<<", "1", 0),
                        ("3", "<<", "1", 6),
                        ("-3", "<<", "1", -6),
                        
                        #right shift
                        ("0", ">>", "1", 0),
                        ("6", ">>", "1", 3),
                        ("-3", ">>", "1", -2),
                        
                        #bitwise AND
                        ("0", "&", "1", 0),
                        ("1", "&", "1", 1),
                        ("7", "&", "2", 2),
                        ("-1", "&", "1", 1),
                        
                        #bitwise OR
                        ("0", "|", "1", 1),
                        ("1", "|", "1", 1),
                        ("4", "|", "2", 6),
                        ("-1", "|", "1", -1),
                        
                        #bitwise XOR
                        ("0", "^", "1", 1),
                        ("1", "^", "1", 0),
                        ("7", "^", "2", 5),
                        ("-1", "^", "1", -2),
                     ]

@unittest.skipUnless(is_cli, 'IronPython specific test case')
class ClrNumInteropTest(unittest.TestCase):
    def num_ok_for_type(self, number, proposed_type):
        '''Helper function returns true if the number param is within the range of valid values for the proposed type'''

        import clr
        import System
        #handle special cases first
        if proposed_type=="long":
            #arbitrary precision
            return True
        if proposed_type=="float":
            #arbitrary precision
            return True
        
        
        if number >= eval(proposed_type + ".MinValue") and number <= eval(proposed_type + ".MaxValue"):
            return True
        #must give it another shot...maybe the operator is broken
        if eval(proposed_type + ".MinValue") <= number and eval(proposed_type + ".MaxValue") >= number:
            return True
            
        return False
            

    def _test_interop_set(self, clr_types, py_types, test_cases):
        '''Helper function which permutes Python/CLR types onto test cases'''
        global unsupported_operands
        global known_bugs

        import clr
        import System
        
        #each test case
        for leftop, op, rightop, expected_value in test_cases:
            
            #get the left operand as a Python type
            py_left = eval(leftop)
            
            #------------------------------------------------------------------
            #create a list of values where each element is the lefthand operand
            #converted to a CLR type
            leftop_clr_types = [x for x in clr_types if self.num_ok_for_type(py_left, x)]
            leftop_clr_values = [eval(x + "(" + leftop + ")") for x in leftop_clr_types]
                        
            #------------------------------------------------------------------
            #create a list of values where each element is the lefthand operand
            #converted to a Python type
            leftop_py_types = [x for x in py_types if self.num_ok_for_type(py_left, x)]
            leftop_py_values = [eval(x + "(" + leftop + ")") for x in leftop_py_types]
            
            #------------------------------------------------------------------
            #get the right operand as a Python type
            py_right = eval(rightop)
            rightop_clr_types = [x for x in clr_types if self.num_ok_for_type(py_right, x)]
            rightop_clr_values = [eval(x + "(" + rightop + ")") for x in rightop_clr_types]
                        
            #------------------------------------------------------------------
            #create a list of values where each element is the righthand operand
            #converted to a Python type
            rightop_py_types = [x for x in py_types if self.num_ok_for_type(py_right, x)]
            rightop_py_values = [eval(x + "(" + rightop + ")") for x in rightop_py_types]
                    
            #------------------------------------------------------------------
            #Comparisons between CPython/CLR types
            def assertionHelper(left_type, left_op, op, right_type, right_op, expected):
                '''Helper function used to figure out which test cases fail without blowing up the rest of the test.'''

                import clr
                import System

                expression_str = '{0}({1}) {2} {3}({4})'.format(left_type, left_op, str(op), right_type, right_op)
                
                #if it's supposedly unsupported...make sure
                if unsupported_operands.count(left_type + op + right_type)>0:
                    self.assertRaises(TypeError, eval, expression_str)
                    return
                    
                try:
                    expression = eval(expression_str)
                except TypeError as e:
                    self.fail("TYPE BUG: %s" % expression_str)
                
                try:
                    self.assertEqual(expression, expected)
                    if known_bugs.count(left_type + op + right_type)>0:
                        self.fail("NO BUG FOR: %s" % expression_str)
                except:
                    if known_bugs.count(left_type + op + right_type)>0:
                        return
                    self.fail(expression_str)
                
                
            #CLR-CLR
            for x in leftop_clr_types:
                for y in rightop_clr_types:
                    assertionHelper(x, leftop, op, y, rightop, expected_value)
                    
            #CLR-PY
            for x in leftop_clr_types:
                for y in rightop_py_types:
                    assertionHelper(x, leftop, op, y, rightop, expected_value)
                                
            #PY-CLR
            for x in leftop_py_types:
                for y in rightop_clr_types:
                    assertionHelper(x, leftop, op, y, rightop, expected_value)
                
            #PY-PY
            for x in leftop_py_types:
                for y in rightop_py_types:
                    assertionHelper(x, leftop, op, y, rightop, expected_value)


    def test_boolean(self):
        '''Test boolean operations involving a left and right operand'''
        self._test_interop_set(clr_types, py_types, bool_test_cases)

    @unittest.expectedFailure
    def test_arithmetic(self):
        '''Test general arithmetic operations.'''
        self._test_interop_set(clr_types, py_types, arith_test_cases)

    def test_bitwiseshift(self):
        '''Test bitwise and shifting operations.'''
        self._test_interop_set(clr_integer_types, py_integer_types, bitwise_test_cases)


    def test_sanity(self):
        '''Make sure that numbers within the constraints of the numerical types are allowed.'''
        import clr
        import System
        temp_list = [   ["System.Byte", 0, 255],
                        ["System.SByte", -128, 127],
                        ["System.Byte", 0, 255],
                        ["System.Int16", -32768, 32767],
                        ["System.UInt16", 0, 65535],
                        ["System.Int32", -2147483648, 2147483647],
                        ["System.UInt32", 0, 4294967295],
                        ["System.Int64", -9223372036854775808, 9223372036854775807],
                        ["System.UInt64", 0, 18446744073709551615],
                        ["System.Single", -3.40282e+038, 3.40282e+038],
                        ["System.Double", -1.79769313486e+308, 1.79769313486e+308],
                        ["System.Decimal", -79228162514264337593543950335, 79228162514264337593543950335],
                        ["int", -2147483648, 2147483647]
                    ]
                    
        for num_type, small_val, large_val in temp_list:
            self.assertTrue(self.num_ok_for_type(1, num_type))
            self.assertTrue(self.num_ok_for_type(1.0, num_type))
            
        
            #Minimum value
            self.assertTrue(self.num_ok_for_type(small_val, num_type))
            self.assertTrue(self.num_ok_for_type(small_val + 1, num_type))
            self.assertTrue(self.num_ok_for_type(small_val + 2, num_type))
            
            #Maximum value
            self.assertTrue(self.num_ok_for_type(large_val, num_type))
            self.assertTrue(self.num_ok_for_type(large_val - 1, num_type))
            self.assertTrue(self.num_ok_for_type(large_val - 2, num_type))
            
            #Negative cases
            if num_type!="System.Single" and num_type!="System.Double" and num_type!="System.Decimal":
                self.assertTrue(not self.num_ok_for_type(small_val - 1, num_type))
                self.assertTrue(not self.num_ok_for_type(small_val - 2, num_type))
                self.assertTrue(not self.num_ok_for_type(large_val + 1, num_type))
                self.assertTrue(not self.num_ok_for_type(large_val + 2, num_type))
            
        #Special cases
        self.assertTrue(self.num_ok_for_type(0, "long"))
        self.assertTrue(self.num_ok_for_type(1, "long"))
        self.assertTrue(self.num_ok_for_type(-1, "long"))
        self.assertTrue(self.num_ok_for_type(5, "long"))
        self.assertTrue(self.num_ok_for_type(-92233720368547758080000, "long"))
        self.assertTrue(self.num_ok_for_type( 18446744073709551615000, "long"))
        
        self.assertTrue(self.num_ok_for_type(0.0, "float"))
        self.assertTrue(self.num_ok_for_type(1.0, "float"))
        self.assertTrue(self.num_ok_for_type(-1.0, "float"))
        self.assertTrue(self.num_ok_for_type(3.14, "float"))
        self.assertTrue(self.num_ok_for_type(-92233720368547758080000.0, "float"))
        self.assertTrue(self.num_ok_for_type( 18446744073709551615000.0, "float"))
    

run_test(__name__)
