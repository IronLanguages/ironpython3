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

from generate import generate
import collections
import operator

# Add new keywords to the end of the list to preserve existing Enum values
kwlist = [
    'and', 'assert', 'break', 'class', 'continue', 'def', 'del', 'elif', 'else', 'except',
    'finally', 'for', 'from', 'global', 'if', 'import', 'in', 'is', 'lambda', 'not', 'or', 'pass',
    'raise', 'return', 'try', 'while', 'yield', 'as', 'with', 'async', 'nonlocal'
]

class Symbol:
    def __init__(self, symbol, name, titleName = None):
        self.symbol = symbol
        self.name = name
        self.titleName = titleName

    def cap_name(self):
        return self.name.title().replace(' ', '')

    def upper_name(self):
        return self.name.upper().replace(' ', '_')

    def title_name(self):
        if not self.titleName: return self.name.title().replace(' ', '')
        return self.titleName

    def simple_name(self):
        return self.name[0] + self.cap_name()[1:]

    def symbol_name(self):
        return "Operator" + self.title_name()

    def reverse_symbol_name(self):
        return "OperatorReverse" + self.title_name()

    def inplace_symbol_name(self):
        return "OperatorInPlace" + self.title_name()

    def __repr__(self):
        return 'Symbol(%s)' % self.symbol
        
    def is_comparison(self):
        return self.symbol in (sym for sym, name, rname,clrName,opposite, bool1, bool2, bool3 in compares)
        
    def is_bitwise(self):
        return self.symbol in ['^', '&', '|', '<<', '>>']

class Operator(Symbol):
    def __init__(self, symbol, name, rname, clrName=None,prec=-1, opposite=None, bool1=None, bool2=None, bool3=None, dotnetOp=False):
        Symbol.__init__(self, symbol, name)
        self.rname = rname
        self.clrName = clrName
        self.dotnetOp = dotnetOp
        self.meth_name = "__" + name + "__"
        self.rmeth_name = "__" + rname + "__"
        if name in kwlist:
            name = name + "_"
        #self.op = getattr(operator, name)
        self.prec = prec
        self.opposite = opposite
        self.bool1 = bool1
        self.bool2 = bool2
        self.bool3 = bool3

    def clrInPlaceName(self):
        return "InPlace" + self.clrName

    def title_name(self):
        return self.clrName

    def isCompare(self):
        return self.prec == -1

    def __repr__(self):
        return 'Operator(%s,%s,%s)' % (self.symbol, self.name, self.rname)

    def genOperatorTable_Mapping(self, cw):
        titleName = self.title_name()
        if titleName.endswith('Equals'):
            titleName = titleName[:-1]
        cw.writeline("pyOp[\"__%s__\"] = PythonOperationKind.%s;" % (self.name, titleName))
        
        if self.isCompare(): return

        cw.writeline("pyOp[\"__r%s__\"] = PythonOperationKind.Reverse%s;" % (self.name, titleName))
        cw.writeline("pyOp[\"__i%s__\"] = PythonOperationKind.InPlace%s;" % (self.name, titleName))

    def genOperatorReversal_Forward(self, cw):
        if self.isCompare(): return
        
        cw.writeline("case Operators.%s: return Operators.Reverse%s;" % (self.title_name(), self.title_name()))
        
    def genOperatorReversal_Reverse(self, cw):    
        if self.isCompare(): return

        cw.writeline("case Operators.Reverse%s: return Operators.%s;" % (self.title_name(), self.title_name()))
        
    def genOperatorTable_Normal(self, cw):
        cw.writeline("///<summary>Operator for performing %s</summary>" % self.name)
        cw.writeline("%s," % (self.title_name()))        
        
    def genOperatorTable_Reverse(self, cw):
        if self.isCompare(): return
        
        cw.writeline("///<summary>Operator for performing reverse %s</summary>" % self.name)
        cw.writeline("Reverse%s," % (self.title_name()))

    def genOperatorTable_InPlace(self, cw):
        if self.isCompare(): return
        
        cw.writeline("///<summary>Operator for performing in-place %s</summary>" % self.name)
        cw.writeline("InPlace%s," % (self.title_name()))
    
    def genOperatorTable_NormalString(self, cw):
        cw.writeline("///<summary>Operator for performing %s</summary>" % self.name)
        titleName = self.title_name()
        if titleName.endswith('Equals'):
            titleName = titleName[:-1]
        cw.writeline('public const string %s = "%s";' % (titleName, titleName))
        
    def genOperatorTable_InPlaceString(self, cw):
        if self.isCompare(): return
        
        cw.writeline("///<summary>Operator for performing in-place %s</summary>" % self.name)
        cw.writeline('public const string InPlace%s = "InPlace%s";' % (self.title_name(), self.title_name()))

    def genOperatorToSymbol(self, cw): 
        cw.writeline("case Operators.%s: return \"__%s__\";" % (self.title_name(), self.name))
        
        if self.isCompare(): return
        
        cw.writeline("case Operators.Reverse%s: return \"__r%s__\";" % (self.title_name(), self.name))
        cw.writeline("case Operators.InPlace%s: return \"__i%s__\";" % (self.title_name(), self.name))

    def genStringOperatorToSymbol(self, cw):
        titleName = self.title_name()
        if titleName.endswith('Equals'):
            titleName = titleName[:-1]
        cw.writeline("case PythonOperationKind.%s: return \"__%s__\";" % (titleName, self.name))
        
        if self.isCompare(): return
        
        cw.writeline("case PythonOperationKind.Reverse%s: return \"__r%s__\";" % (titleName, self.name))
        cw.writeline("case PythonOperationKind.InPlace%s: return \"__i%s__\";" % (titleName, self.name))

    def genOldStyleOp(self, cw):
        if self.isCompare(): return

        cw.writeline('[return: MaybeNotImplemented]')
        if self.dotnetOp:
            cw.enter_block("public static object operator %s([NotNull]OldInstance self, object other)" % self.symbol)            
        else:
            cw.writeline('[SpecialName]')
            cw.enter_block("public static object %s([NotNull]OldInstance self, object other)" % self.title_name())
        cw.writeline('object res = InvokeOne(self, other, \"__%s__\");' % self.name)
        cw.writeline('if (res != NotImplementedType.Value) return res;')

        cw.writeline()
        cw.writeline("OldInstance otherOc = other as OldInstance;")
        cw.enter_block("if (otherOc != null)")
        cw.writeline('return InvokeOne(otherOc, self, \"__r%s__\");' % self.name)
        cw.exit_block() # end of otherOc != null        
        
        cw.writeline("return NotImplementedType.Value;")
        cw.exit_block() # end method
        cw.writeline()
        
        cw.writeline('[return: MaybeNotImplemented]')
        if self.dotnetOp:
            cw.enter_block("public static object operator %s(object other, [NotNull]OldInstance self)" % self.symbol)            
        else:
            cw.writeline('[SpecialName]')
            cw.enter_block("public static object %s(object other, [NotNull]OldInstance self)" % self.title_name())
        cw.writeline("return InvokeOne(self, other, \"__r%s__\");" % self.name)
        cw.exit_block() # end method
        cw.writeline()
        
        cw.writeline('[return: MaybeNotImplemented]')
        cw.writeline('[SpecialName]')
        cw.enter_block("public object InPlace%s(object other)" % self.title_name())
        cw.writeline("return InvokeOne(this, other, \"__i%s__\");" % self.name)
        cw.exit_block() # end method
        cw.writeline()

    def genWeakRefOperatorNames(self, cw):        
        cw.writeline('[SlotField] public static PythonTypeSlot __%s__ = new SlotWrapper(\"__%s__\", ProxyType);' % (self.name, self.name))
        
        if self.isCompare(): return

        cw.writeline('[SlotField] public static PythonTypeSlot __r%s__ = new SlotWrapper(\"__r%s__\", ProxyType);' % (self.name, self.name))
        cw.writeline('[SlotField] public static PythonTypeSlot __i%s__ = new SlotWrapper(\"__i%s__\", ProxyType);' % (self.name, self.name))

    def genWeakRefCallableProxyOperatorNames(self, cw):        
        cw.writeline('[SlotField] public static PythonTypeSlot __%s__ = new SlotWrapper(\"__%s__\", CallableProxyType);' % (self.name, self.name))
        
        if self.isCompare(): return

        cw.writeline('[SlotField] public static PythonTypeSlot __r%s__ = new SlotWrapper(\"__r%s__\", CallableProxyType);' % (self.name, self.name))
        cw.writeline('[SlotField] public static PythonTypeSlot __i%s__ = new SlotWrapper(\"__i%s__\", CallableProxyType);' % (self.name, self.name))
    
    def genConstantFolding(self, cw, type):
        # Exclude bitwise ops on Double and Complex, and exclude comparisons
        # on Complex. Also exclude Complex floordiv and because they need to
        # issue warnings during runtime.
        if type == 'Complex' and (self.clrName in ['Mod', 'FloorDivide']):
            return
        if self.isCompare():
            if type != 'Complex' and self.symbol != '<>':
                cw.writeline('case PythonOperator.%s: return new ConstantExpression(ScriptingRuntimeHelpers.BooleanToObject(%sOps.Compare((%s)constLeft.Value, (%s)constRight.Value) %s 0));' % (self.clrName, type, type, type, self.symbol))
        elif (type !='Double' and type != 'Complex') or not self.is_bitwise():
            cw.writeline('case PythonOperator.%s: return new ConstantExpression(%sOps.%s((%s)constLeft.Value, (%s)constRight.Value));' % (self.clrName, type, self.clrName, type, type))

class Grouping(Symbol):
    def __init__(self, symbol, name, side, titleName=None):
        Symbol.__init__(self, symbol, side+" "+name, titleName)
        self.base_name = name
        self.side = side

ops = []

"""
0 expr: xor_expr ('|' xor_expr)*
1 xor_expr: and_expr ('^' and_expr)*
2 and_expr: shift_expr ('&' shift_expr)*
3 shift_expr: arith_expr (('<<'|'>>') arith_expr)*
4 arith_expr: term (('+'|'-') term)*
5 term: factor (('*'|'/'|'%'|'//') factor)*
"""
            # op,  pyname,   prec, .NET name, .NET op      op,    pyname,   prec, .NET name,  .NET op overload
binaries = [('+',  'add',      4, 'Add',         True),   ('-',  'sub',    4, 'Subtract',   True), 
            ('**', 'pow',      6, 'Power',       False),  ('*',  'mul',    5, 'Multiply',   True),
            ('//', 'floordiv', 5, 'FloorDivide', False),
            ('/',  'truediv',  5, 'TrueDivide',  False),  ('%',  'mod',    5, 'Mod',        True), 
            ('<<', 'lshift',   3, 'LeftShift',   False),   ('>>', 'rshift', 3, 'RightShift', False),
            ('&',  'and',      2, 'BitwiseAnd',  True),   ('|',  'or',     0, 'BitwiseOr',  True), 
            ('^',  'xor',      1, 'ExclusiveOr', True)]

def add_binaries(list):
    for sym, name, prec,clrName, netOp in list:
        ops.append(Operator(sym, name, 'r'+name, clrName, prec, dotnetOp = netOp))
        ops.append(Symbol(sym+"=", name+" Equal", clrName+ "Equal"))

add_binaries(binaries)

compares = [('<', 'lt', 'ge', 'LessThan', 'GreaterThan', "false", "true", "false"), 
            ('>', 'gt', 'le', 'GreaterThan', 'LessThan', "false", "false", "true"),
            ('<=', 'le', 'gt', 'LessThanOrEqual', 'GreaterThanOrEqual', "true", "true", "false"), 
            ('>=', 'ge', 'lt', 'GreaterThanOrEqual', 'LessThanOrEqual', "true", "false", "true"),
            ('==', 'eq', 'eq', 'Equals', 'NotEquals', 'a', 'a', 'a'), 
            ('!=', 'ne', 'ne', 'NotEquals', 'Equals', 'a', 'a', 'a')]
for sym, name, rname,clrName,opposite, bool1, bool2, bool3 in compares:
    ops.append(Operator(sym, name, rname,clrName, opposite=opposite, bool1=bool1, bool2=bool2, bool3=bool3))

groupings = [('(', ')', 'Paren', 'Parenthesis'), ('[', ']', 'Bracket', 'Bracket'), ('{', '}', 'Brace', 'Brace')]
for sym, rsym, name, fullName in groupings:
    ops.append(Grouping(sym, name, 'l', 'Left' + fullName))
    ops.append(Grouping(rsym, name, 'r', 'Right' + fullName))

simple = [(',', 'comma'), (':', 'colon'), (';', 'semicolon'),
        ('=', 'assign'), ('~', 'twiddle'), ('@', 'at'), ('=>', 'rarrow', 'RightArrow')]
for info in simple:
    if len(info) == 2:
        sym, name = info
        title = None
    else:
        sym, name, title = info
    
    ops.append(Symbol(sym, name, title))

start_symbols = {}
for op in ops:
    ss = op.symbol[0]
    if ss not in start_symbols: start_symbols[ss] = []
    start_symbols[ss].append(op)


def gen_tests(ops, pos, indent=1):
    ret = []

    default_match = []
    future_matches = collections.OrderedDict()
    for sop in ops:
        if len(sop.symbol) == pos:
            default_match.append(sop)
        elif len(sop.symbol) > pos:
            ch = sop.symbol[pos]
            if ch in future_matches:
                future_matches[ch].append(sop)
            else:
                future_matches[ch] = [sop]
    #assert len(default_match) <= 1
    for ch, sops in future_matches.items():
        ret.append("if (NextChar('%s')) {" % ch)
        ret.extend(gen_tests(sops, pos+1))
        ret.append("}")
    if default_match:
        op = default_match[0]
        if isinstance(op, Grouping):
            if op.side == 'l':
                ret.append("_state.%sLevel++;" % op.base_name);
            else:
                ret.append("_state.%sLevel--;" % op.base_name);
        ret.append("return Tokens.%sToken;" %
                   op.title_name())
    else:
        ret.append("return BadChar(ch);")

    return ["    "*indent + l for l in ret]

def tokenize_generator(cw):
    ret = []
    done = {}
    for op in ops:
        ch = op.symbol[0]
        if ch in done: continue
        sops = start_symbols[ch]
        cw.write("case '%s':" % ch)
        for t in gen_tests(sops, 1):
            cw.write(t)
        done[ch] = True
    return ret

friendlyOverload = {'elif':"ElseIf"}
def keywordToFriendly(kw):
    if kw in friendlyOverload:
        return friendlyOverload[kw]
    
    return kw.title()

class unique_checker:
    def __init__(self):
        self.__unique = {}
    def unique(self, op):
        if not op.symbol in self.__unique:
            self.__unique[op.symbol] = op
            return True
        else: return False

def tokenkinds_generator(cw):
    i = 32
    uc = unique_checker()
    for op in ops:
        if not uc.unique(op): continue
        cw.write("%s = %d," % (op.title_name(), i))
        i += 1

    cw.writeline()
    keyword_list = list(kwlist)
    
    cw.write("FirstKeyword = Keyword%s," % keywordToFriendly(keyword_list[0]));
    cw.write("LastKeyword = Keyword%s," % keywordToFriendly(keyword_list[len(keyword_list) - 1]));
    
    for kw in keyword_list:
        cw.write("Keyword%s = %d," % (keywordToFriendly(kw), i))
        i += 1

def gen_mark_end(cw, keyword):
    cw.write('MarkTokenEnd();')
    if keyword == 'None':
        cw.write('return Tokens.NoneToken;')
    else:
        cw.write('return Tokens.Keyword%sToken;' % keywordToFriendly(keyword))
    cw.exit_block()

def gen_token_tree(cw, tree, keyword):
    cw.write('ch = NextChar();')
    for i, (k, (v, end)) in enumerate(tree.items()):
        if i == 0:
            cw.enter_block("if (ch == '%c')" % k)            
        else:
            cw.else_block("if (ch == '%c')" % k)
            
        if end and v:
            cw.enter_block('if (!IsNamePart(Peek()))')
            gen_mark_end(cw, keyword + k)
        
        if not v:
            cw.enter_block('if (!IsNamePart(Peek()))')
            gen_mark_end(cw, keyword + k)
        else:
            cur_tree = v
            word = []
            while True:
                if not cur_tree:
                    cw.enter_block("if (" + ' && '.join(["NextChar() == '%c'" % letter for letter in word]) + ' && !IsNamePart(Peek()))')
                    gen_mark_end(cw, keyword + k + ''.join(word))
                    break
                elif len(cur_tree) == 1:
                    word.append(next(iter(cur_tree.keys())))
                    cur_tree = next(iter(cur_tree.values()))[0]
                else:
                    gen_token_tree(cw, v, keyword + k)
                    break
                
    cw.exit_block()
    
def keyword_lookup_generator(cw):
    cw.write('int ch;')
    cw.write('BufferBack();')
    
    keyword_list = list(kwlist)
    keyword_list.append('None')
    keyword_list.sort()
    
    tree = collections.OrderedDict()
    for kw in keyword_list:
        prev = cur = tree
        for letter in kw:
            val = cur.get(letter)
            if val is None:
                val = cur[letter] = (collections.OrderedDict(), False)
                
            prev = cur
            cur = val[0]
        prev[kw[-1:]] = (cur, True)
    
    gen_token_tree(cw, tree, '')
    return

def tokens_generator(cw):
    uc = unique_checker()
    for op in ops:
        if not uc.unique(op): continue

        if isinstance(op, Operator):
            creator = 'new OperatorToken(TokenKind.%s, "%s", %d)' % (
                op.title_name(), op.symbol, op.prec)
        else:
            creator = 'new SymbolToken(TokenKind.%s, "%s")' % (
                op.title_name(), op.symbol)
        cw.write("private static readonly Token sym%sToken = %s;" %
                   (op.title_name(), creator))
                       

    cw.writeline()
    
    uc = unique_checker()
    for op in ops:
        if not uc.unique(op): continue

        cw.enter_block("public static Token %sToken" % op.title_name())
        cw.write("get { return sym%sToken; }" % op.title_name())
        cw.exit_block()
        cw.write("")
    
    keyword_list = list(kwlist)
    keyword_list.sort()

    for kw in keyword_list:
        creator = 'new SymbolToken(TokenKind.Keyword%s, "%s")' % (
            keywordToFriendly(kw), kw)
        cw.write("private static readonly Token kw%sToken = %s;" %
               (keywordToFriendly(kw), creator))

    cw.write("")
    cw.write("")
    for kw in keyword_list:
        cw.enter_block("public static Token Keyword%sToken" % keywordToFriendly(kw))
        cw.write("get { return kw%sToken; }" % keywordToFriendly(kw))
        cw.exit_block()
        cw.write("")

    cw.write("")

UBINOP = """
public static object %(name)s(object x, object y) {
    throw new NotImplementedException("%(name)s");
}
"""
ADD_EXTRA_VARS = """        string sx, sy;
        ExtensibleString es = null;
"""

ADD_EXTRA_EARLY = """       } else if ((sx = x as string) != null && ((sy = y as string) != null || (es = y as ExtensibleString) != null)) {
            if (sy != null) return sx + sy;
            return sx + es.Value;"""

ADD_EXTRA = """                
        ISequence seq = x as ISequence;
        if (seq != null) { return seq.AddSequence(y); }
"""

INPLACE_ADD_EXTRA = """
        if (x is string && y is string) {
            return ((string)x) + ((string)y);
        }
    
        if (x is ReflectedEvent.EventTarget) {
            return ((ReflectedEvent.EventTarget)x).InPlaceAdd(y);
        }
"""


MUL_EXTRA = """
        if (x is ISequence) {
            return ((ISequence)x).MultiplySequence(y);
        } else if (y is ISequence) {
            return ((ISequence)y).MultiplySequence(x);
        }
"""

def gen_OperatorTable(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorTable_Normal(cw)
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorTable_InPlace(cw)
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorTable_Reverse(cw)
        
def gen_OperatorStringTable(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorTable_NormalString(cw)
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorTable_InPlaceString(cw)

def gen_operatorMapping(cw):
    for op in ops:
        if isinstance(op, Operator): op.genOperatorTable_Mapping(cw)

def gen_OperatorToSymbol(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOperatorToSymbol(cw)
        
def gen_StringOperatorToSymbol(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genStringOperatorToSymbol(cw)

def weakref_operators(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        if op.is_comparison(): continue
        op.genWeakRefOperatorNames(cw)

def weakrefCallabelProxy_operators(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        if op.is_comparison(): continue
        op.genWeakRefCallableProxyOperatorNames(cw)

def oldinstance_operators(cw):
    for op in ops:
        if not isinstance(op, Operator): continue
        op.genOldStyleOp(cw)

def operator_reversal(cw):
    for op in ops:
        if not isinstance(op, Operator): continue

        op.genOperatorReversal_Forward(cw)
        op.genOperatorReversal_Reverse(cw)
        

def fast_op_ret_bool_chooser(cw):
    for curType in ('int', ):
        cw.enter_block('if (CompilerHelpers.GetType(args[0]) == typeof(%s) && CompilerHelpers.GetType(args[1]) == typeof(%s))' % (curType, curType))
        _else = ''
        for x in (curType, 'object'):
            for y in (curType, 'object'):                
                cw.enter_block('%sif (typeof(T) == typeof(Func<CallSite, %s, %s, bool>))' % (_else, x, y))
                cw.write('res = (T)(object)Get%s%s%sDelegate(opBinder);' % (curType.title(), x.title(), y.title()))
                cw.exit_block()
                _else = 'else '
        cw.exit_block()

def fast_op_ret_bool(cw):
    """public bool IntEqualRetBool(CallSite site, object self, object other) {
    if (self != null && self.GetType() == typeof(int) &&
        other != null && other.GetType() == typeof(int)) {
        return (int)self == (int)other;
    }

    return ((CallSite<Func<CallSite, object, object, bool>>)site).Update(site, self, other);
}"""
    for curPyType, curPascalType in (('int', 'Int'), ):
        for x in ('object', curPyType):
            for y in ('object', curPyType):
                cw.enter_block('private Func<CallSite, %s, %s, bool> Get%s%s%sDelegate(BinaryOperationBinder opBinder)' % (x, y, curPascalType, x.title(), y.title()))
                cw.enter_block('switch (opBinder.Operation)')
                cw.write('case ExpressionType.Equal: return %sEqualRetBool;' % (curPascalType, ))
                cw.write('case ExpressionType.NotEqual: return %sNotEqualRetBool; ' % (curPascalType, ))
                cw.write('case ExpressionType.GreaterThan: return %sGreaterThanRetBool;' % (curPascalType, ))
                cw.write('case ExpressionType.LessThan: return %sLessThanRetBool;' % (curPascalType, ))
                cw.write('case ExpressionType.GreaterThanOrEqual: return %sGreaterThanOrEqualRetBool;' % (curPascalType, ))
                cw.write('case ExpressionType.LessThanOrEqual:return %sLessThanOrEqualRetBool;' % (curPascalType, ))
                cw.exit_block()
                cw.write('return null;')
                cw.exit_block()
                
                cw.write('')
                
                for op in (('==', 'Equal'), ('!=', 'NotEqual'), ('>', 'GreaterThan'), ('<', 'LessThan'), ('>=', 'GreaterThanOrEqual'), ('<=', 'LessThanOrEqual')):                   
                    cw.enter_block('public bool %s%sRetBool(CallSite site, %s self, %s other)' % (curPascalType, op[1], x, y))
                    if x == 'object':
                        cw.enter_block('if (self != null && self.GetType() == typeof(%s))' % curPyType)
                        
                    if y == 'object':
                        cw.enter_block('if (other != null && other.GetType() == typeof(%s))' % curPyType)
                        
                    cw.write('return (%s)self %s (%s)other;' % (curPyType, op[0], curPyType))
                    
                    if x == 'object':
                        cw.exit_block()
                        
                    if y == 'object':
                        cw.exit_block()
                    
                    if x == 'object' or y == 'object':
                        cw.write('return ((CallSite<Func<CallSite, %s, %s, bool>>)site).Update(site, self, other);' % (x, y))

                    cw.exit_block()
                    cw.write('')

def gen_constant_folding(cw):
    types = ['Int32', 'Double', 'BigInteger', 'Complex']
    for cur_type in types:
        cw.enter_block('if (constLeft.Value.GetType() == typeof(%s))' % (cur_type, ))
        cw.enter_block('switch (_op)')
        for op in ops:           
            gen = getattr(op, 'genConstantFolding', None)
            if gen is not None:            
                gen(cw, cur_type)
        cw.exit_block()
        cw.exit_block()
    
def main():
    return generate(
        ("Python Keyword Lookup", keyword_lookup_generator),
        ("Python Constant Folding", gen_constant_folding),
        ("Python Fast Ops RetBool Chooser", fast_op_ret_bool_chooser),
        ("Python Fast Ops Ret Bool", fast_op_ret_bool),
        ("Tokenize Ops", tokenize_generator),
        ("Token Kinds", tokenkinds_generator),
        ("Tokens", tokens_generator),
        #("Table of Operators", gen_OperatorTable),
        ("PythonOperator Mapping", gen_operatorMapping),
        #("OperatorToSymbol", gen_OperatorToSymbol),
        ("StringOperatorToSymbol", gen_StringOperatorToSymbol),
        ("WeakRef Operators Initialization", weakref_operators),
        #("Operator Reversal", operator_reversal),
        ("WeakRef Callable Proxy Operators Initialization", weakrefCallabelProxy_operators),
    )

if __name__ == "__main__":
    main()
