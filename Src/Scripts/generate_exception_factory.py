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
import generate

class ExceptionItem:
    def __init__(self, type, ID, text, param_num):
        self.type = type
        self.ID = ID
        self.text = text
        self.param_num = param_num

class StringItem:
    def __init__(self, ID, text, param_num):
        self.ID = ID
        self.text = text
        self.param_num = param_num

def make_string_item(line1):
    splitted = line1.split('=', 1)
    ID = splitted[0]
    text = splitted[1]
    num = text.count('{')
    string_item = StringItem(ID, text ,num)
    return string_item


def make_exception_item(line1, line2):
    type = line1.rsplit('=', 1)[1]
    splitted = line2.split('=', 1)
    ID = splitted[0]
    text = splitted[1]
    num = text.count('{')
    exception_item = ExceptionItem(type, ID, text ,num)
    return exception_item

def collect_exceptions(filename):
	thefile = open(filename)
	text = thefile.read()
	lines = text.splitlines()
	items = []
	for i in range(len(lines)):
		line1 = lines[i]
		if line1.startswith("## "):
			line2 = lines[i+1]
			items.append(make_exception_item(line1, line2))			
	return items

def collect_strings(filename):
	thefile = open(filename)
	text = thefile.read()
	lines = text.splitlines()
	items = []
	for i in range(len(lines)):
		line1 = lines[i]
		if (not (line1.startswith("#") or line1.startswith(";")) and line1.find("=") > 0 ):
			items.append(make_string_item(line1))			
	return items


def make_signature(cnt):
	if cnt == 0:
		return "()"
		
	sig = "("
	for i in range(cnt - 1):
		sig += "object p" + str(i) + ", "
	
	sig += "object p" + str(cnt - 1) + ")"
	return sig


def make_args(cnt):
	if cnt == 0:
		return "()"
		
	sig = "("
	for i in range(cnt - 1):
		sig += "p" + str(i) + ", "
	
	sig += "p" + str(cnt - 1) + ")"
	return sig


def make_format(text, cnt):
	if cnt == 0:
		return '"' + text + '"'
		
	format = 'FormatString("' + text + '", '
	for i in range(cnt - 1):
		format += "p" + str(i) + ", "
	
	format += "p" + str((cnt - 1)) + ")"
	return format

def escape(s):
	s = s.replace('"', '\\"')
	return s
	
def escape_xml(s):
    s = s.replace("<", "&lt;")
    s = s.replace(">", "&gt;")
    return s
	
def gen_expr_factory(cw, source):
	strings = collect_strings(source)
	exceptions = collect_exceptions(source)

	result = ""
	result += "/// <summary>" + "\n"
	result += "///    Strongly-typed and parameterized string factory." + "\n"
	result += "/// </summary>" + "\n"
	
	cw.write(result)
	
	cw.enter_block("internal static partial class Strings")

	for ex in strings:
		result = ""
		result += "/// <summary>" + "\n"
		result += '/// A string like  "' + escape_xml(ex.text) + '"\n'
		result += "/// </summary>" + "\n"
		
		if (ex.param_num > 0):
			result += "internal static string " + str(ex.ID) + make_signature(ex.param_num) + " {" + "\n"
			result += "    return " + make_format(escape(ex.text), ex.param_num) + ";" + "\n"
			result += "}" + "\n"
		else:
			result += "internal static string " + str(ex.ID) + " {" + "\n"
			result += "    get {" + "\n"
  			result += '        return "' + escape(ex.text) + '";' + '\n'
			result += "    }" + "\n"
			result += "}" + "\n"
			
		cw.write(result)
		
	cw.exit_block()
	
	result = "/// <summary>" + "\n"
	result += "///    Strongly-typed and parameterized exception factory." + "\n"
	result += "/// </summary>" + "\n"
	
	cw.write(result)

	cw.enter_block("internal static partial class Error")
	
	for ex in exceptions:
		result = ""
		if (ex.type == "System.Runtime.InteropServices.COMException"):
			result += "#if !SILVERLIGHT" + "\n"
		result += "/// <summary>" + "\n"
		result += "/// " + ex.type + ' with message like "' + escape_xml(ex.text) + '"\n'
		result += "/// </summary>" + "\n"
		if (ex.type == "System.Runtime.InteropServices.COMException"):
			result += '[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")]'  + '\n'
		
		result += "internal static Exception " + str(ex.ID) + make_signature(ex.param_num) + " {" + "\n"
		if (ex.param_num == 0):
			result += "    return new " + ex.type + "(Strings." + ex.ID + ");" + "\n"
		else:
			result += "    return new " + ex.type + "(Strings." + ex.ID + make_args(ex.param_num) + ");" + "\n"
		result += "}" + "\n"

		if (ex.type == "System.Runtime.InteropServices.COMException"):
			result += "#endif" + "\n"
			
		cw.write(result)
			
	cw.exit_block()

def gen_expr_factory_core(cw):
    gen_expr_factory(cw, generate.root_dir + "\\..\\..\\ndp\\fx\\src\\Core\\System\\Linq\\Expressions\\System.Linq.Expressions.txt")

def gen_expr_factory_com(cw):
    gen_expr_factory(cw, generate.root_dir + "\\..\\..\\ndp\\fx\\src\\Dynamic\\System\\Dynamic\\System.Dynamic.txt")

def gen_expr_factory_scripting(cw):
    gen_expr_factory(cw, generate.root_dir + "\\Runtime\\Microsoft.Scripting\\Microsoft.Scripting.txt")

def main():
    return generate.generate(
        ("Exception Factory", gen_expr_factory_core),
        ("Com Exception Factory", gen_expr_factory_com),
        ("Microsoft.Scripting Exception Factory", gen_expr_factory_scripting),
    )

if __name__ == "__main__":
    main()
