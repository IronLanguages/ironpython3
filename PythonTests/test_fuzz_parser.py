MAX_DEPTH = 2

#class StatementGenerator(BaseGenerator): 
#    def generate(self, depth): raise NotImplementedError

######################################################################
# Expressions

class ExpressionGenerator(object):
    def generate(self, depth): raise NotImplementedError

class BinaryExpression(ExpressionGenerator):
    def __init__(self, op):
        self.op = op
    def generate(self, depth):  
        exprs = list(MakeExpressionGenerator(depth + 1))
        for x in exprs:
            for y in exprs:
                yield x + ' ' + self.op + ' ' + y

class UnaryExpression(ExpressionGenerator):
    def __init__(self, op):
        self.op = op
    def generate(self, depth):  
        for x in MakeExpressionGenerator(depth + 1):
            yield self.op + ' ' + x
                
class EmptyExpression(ExpressionGenerator):
    def generate(self, depth):
        yield ' '

class ConstantExpression(ExpressionGenerator):
    def __init__(self, value):
        self.value = value
    def generate(self, depth):
        yield repr(self.value)

class NameExpression(ExpressionGenerator):
    def __init__(self, value):
        self.value = value
    def generate(self, depth):
        yield self.value
    
class InvalidExpression(ExpressionGenerator):
    def __init__(self, value):
        self.value = value
    def generate(self, depth):
        yield self.value

class ParenthForm(ExpressionGenerator):
    def __init__(self, trailingComma):
        self.trailingComma = trailingComma
    def generate(self, depth):
        subexprs = list(MakeExpressionGenerator(depth + 1))
        head = '('
        
        if self.trailingComma:
            tail = ', )'
        else:
            tail = ')'
        
        args3 = [x + ', ' + y + ', ' + z for x in subexprs for y in subexprs for z in subexprs]
        args2 = [x + ', ' + y for x in subexprs for y in subexprs]
        args1 = [x for x in subexprs]
        
        yield head + tail
        for values in args1 + args2 + args3:
            yield head + values + tail

class GeneratorExpression(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            for target in MakeExpressionGenerator(depth + 1):
                for inTest in MakeExpressionGenerator(depth + 1):
                    yield '(' + expr + ' for ' + target + ' in ' + inTest + ')'
                    for cond in MakeExpressionGenerator(depth + 1):
                        yield '(' + expr + ' for ' + target + ' in ' + inTest + ' if ' + cond + ')'
                

class DictionaryDisplay(ExpressionGenerator):
    def generate(self, depth):
        subexprs = list(MakeExpressionGenerator(depth + 1))
        
        args = [x + ': ' + y for x in subexprs for y in subexprs]
        yield '{}'
        
        for x in args:
            yield '{' + x + '}'
            yield '{' + x + ', }'
        
        for x, y in zip(args, args):
            yield '{' + x + ', ' + y + '}'
            yield '{' + x + ', ' + y + ', }'

class StringConversion(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            yield '`' + expr + '`'

class YieldExpression(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            yield 'yield ' + expr
            yield '( yield ' + expr + ')'

class AttributeExpression(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            for name in MakeIdentifierGenerator(depth + 1):
                yield expr + '.' + name

class SubscriptionExpression(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                yield expr + '[' + subscript + ']'
                yield expr + '[' + subscript + ', ]'

        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                for subscript2 in MakeExpressionGenerator(depth + 1):            
                    yield expr + '[' + subscript + ', ' + subscript2 + ']'
                    yield expr + '[' + subscript + ', ' + subscript2 + ', ]'

class SlicingExpression(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                yield expr + '[' + subscript + ':]'
                #yield expr + '[:' + subscript + ']'
                #yield expr + '[' + subscript + '::]'
                #yield expr + '[::' + subscript + ']'

        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                for subscript2 in MakeExpressionGenerator(depth + 1):            
                    yield expr + '[' + subscript + ': ' + subscript2 + ']'
                    #yield expr + '[' + subscript + ', ' + subscript2 + ': ]'
                    #yield expr + '[ :' + subscript + ', ' + subscript2 + ']'

        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                for subscript2 in MakeExpressionGenerator(depth + 1):            
                    for subscript3 in MakeExpressionGenerator(depth + 1):            
                        yield expr + '[' + subscript + ': ' + subscript2 + ':' + subscript3 + ']'

class CallExpression(object):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                yield expr + '(' + subscript + ')'
                yield expr + '(' + subscript + ', )'
                yield expr + '(*' + subscript + ')'
                yield expr + '(**' + subscript + ', )'
                for name in MakeExpressionGenerator(depth + 1):
                    yield expr + '(' + name + ' = ' + subscript + ')'                

        for expr in MakeExpressionGenerator(depth + 1):
            for subscript in MakeExpressionGenerator(depth + 1):            
                for subscript2 in MakeExpressionGenerator(depth + 1):            
                    yield expr + '(' + subscript + ', ' + subscript2 + ')'
                    yield expr + '(' + subscript + ', ' + subscript2 + ', )'
                    yield expr + '(*' + subscript + ', ' + subscript2 + ')'
                    yield expr + '(**' + subscript + ', ' + subscript2 + ', )'
                    yield expr + '(*' + subscript + ', **' + subscript2 + ')'
                    yield expr + '(**' + subscript + ', *' + subscript2 + ')'
                    for name in MakeExpressionGenerator(depth + 1):
                        yield expr + '(' + name + ' = ' + subscript + ', ' + subscript2 + ')'
                        yield expr + '(' + subscript + ', ' + name + ' = ' + subscript2 + ')'

######################################################################
# Statements

class AssertStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield 'assert ' + expr + '\n'
            for expr2 in MakeExpressionGenerator(0):
                yield 'assert ' + expr + ', ' + expr2 + '\n'

class AssignmentStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            for expr2 in MakeExpressionGenerator(0):
                yield expr + ' = ' + expr2 + '\n'
                yield expr + ' += ' + expr2 + '\n'

class DelStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield 'del ' + expr + '\n'

class PrintStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield 'print ' + expr + '\n'
            yield 'print ' + expr + ', ' + '\n'

class ReturnStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield 'return ' + expr + '\n'

class RaiseStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield 'raise ' + expr + '\n'

class PassStatement(ExpressionGenerator):
    def generate(self, depth):
        yield 'pass\n'
    
class IfStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            for body in MakeStatementGenerator(depth + 1):
                yield 'if ' + expr + ':\n' + body
                yield 'if ' + expr + ':\n' + body + 'else:\n' + body
                yield 'if ' + expr + ':\n' + body + 'elif ' + expr + ':\n' + body
            yield 'if ' + expr + '\n'

class WhileStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            for body in MakeStatementGenerator(depth + 1):
                yield 'while ' + expr + ':\n' + body
                yield 'while ' + expr + ':\n' + body + 'else:\n' + body
            yield 'while' + expr + '\n'

class ForStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            for body in MakeStatementGenerator(depth + 1):
                yield 'for ' + expr + 'in ' + expr + ':\n' + body
                yield 'for ' + expr + 'in ' + expr + ':\n' + body + 'else:\n' + body
            yield 'for ' + expr + 'in ' + expr + '\n'

class ExpressionStatement(ExpressionGenerator):
    def generate(self, depth):
        for expr in MakeExpressionGenerator(0):
            yield expr + '\n'
    
######################################################################
# Factories

class BinaryMaker(object):
    def __init__(self, value):
        self.value = value        
    def __call__(self):
        return BinaryExpression(self.value)
    def __repr__(self):
        return 'BinaryMaker(' + repr(self.value) + ')'

class UnaryMaker(object):
    def __init__(self, value):
        self.value = value        
    def __call__(self):
        return UnaryExpression(self.value)
    def __repr__(self):
        return 'UnaryMaker(' + repr(self.value) + ')'

class ConstantMaker(object):
    def __init__(self, value):
        self.value = value
    def __call__(self):
        return ConstantExpression(self.value)
    def __repr__(self):
        return 'ConstantMaker(' + repr(self.value) + ')'

class NameMaker(object):
    def __init__(self, value):
        self.value = value
    def __call__(self):
        return NameExpression(self.value)
    def __repr__(self):
        return 'NameMaker(' + self.value + ')'

class InvalidMaker(object):
    def __init__(self, value):
        self.value = value
    def __call__(self):
        return InvalidExpression(self.value)
    def __repr__(self):
        return 'InvalidMaker(' + self.value + ')'

class ParenthFormMaker(object):
    def __init__(self, trailingComma):
        self.trailingComma = trailingComma
    def __call__(self):
        return ParenthForm(self.trailingComma)
    def __repr__(self):
        return 'ParenthFormMaker(' + self.trailingComma + ')'

identifier_exprs = [NameMaker('foo'), NameMaker('...'), NameMaker('reallylongname' * 200)] # NameMaker('_bar'), 

expr_gens = identifier_exprs + \
            [BinaryMaker('+'), BinaryMaker('**'), BinaryMaker('<<'), BinaryMaker('&'), BinaryMaker('=='), BinaryMaker('>'), BinaryMaker(' in '),  # BinaryMaker('-'), BinaryMaker('|'), BinaryMaker('^'), 
             UnaryMaker('~'), UnaryMaker(' not '),  # UnaryMaker('-'), UnaryMaker('+'), 
             EmptyExpression,
             GeneratorExpression,
             DictionaryDisplay,
             CallExpression,
             AttributeExpression,
             SubscriptionExpression,
             SlicingExpression,
             ConstantMaker(1), ConstantMaker('abc'), ConstantMaker(1), ConstantMaker(1.0), ConstantMaker(1j),
             ParenthFormMaker(False), ParenthFormMaker(True), 
             StringConversion,
             YieldExpression,
             InvalidMaker('$'), InvalidMaker('@'), InvalidMaker('!'), InvalidMaker('\\'), #InvalidMaker('\0'),
            ]


stmt_gens = [IfStatement, PassStatement, ExpressionStatement, WhileStatement, ForStatement, AssertStatement, AssignmentStatement, DelStatement, PrintStatement, ReturnStatement, RaiseStatement]

def MakeExpressionGenerator(depth):
    """yields all possible expressions"""
    if depth == MAX_DEPTH: 
        yield '' # empty expr
    else: 
        for exprGen in expr_gens:        
            for value in exprGen().generate(depth):
                yield value

def Indent(value, depth):
    if depth == 0: 
        return value
    indent = '    ' * depth
    return indent + value.replace('\n', '\n' + indent)

def MakeStatementGenerator(depth):
    """yields all possible expressions"""
    if depth == MAX_DEPTH: 
        yield '' # empty expr
    else: 
        for stmtGen in stmt_gens:        
            for value in stmtGen().generate(depth):
                yield Indent(value, depth)

def MakeIdentifierGenerator(depth):
    for exprGen in identifier_exprs:        
        for value in exprGen().generate(depth):
            yield value
    
i = 0
for x in MakeStatementGenerator(0):    
    i += 1
    if i % 1000 == 0:
        print('.', end=' ')
    try:
        compile(x, 'foo', 'exec')
    except SyntaxError:
        pass

print('Ran %d tests', i)