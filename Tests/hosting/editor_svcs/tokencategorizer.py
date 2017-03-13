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

##
## Testing TokenCategorizer
##
from iptest.assert_util import *
skiptest("win32")

load_iron_python_test()

from System import Enum
import Microsoft.Scripting.Hosting
from Microsoft.Scripting import TokenTriggers, TokenInfo, TokenCategory, SourceLocation, SourceSpan
from Microsoft.Scripting.Hosting import ScriptRuntime, TokenCategorizer
from IronPython.Hosting import Python

#------------------------------------------------------------------------------
# Globals
engine = Python.CreateEngine()

#------------------------------------------------------------------------------
# Utils
def pt(tokens):
    for token in tokens:
        print("t.%s(From(%d,%d,%d), To(%d,%d,%d)%s)," % \
        (token.Category.ToString(), token.SourceSpan.Start.Index,
        token.SourceSpan.Start.Line, token.SourceSpan.Start.Column,
        token.SourceSpan.End.Index, token.SourceSpan.End.Line,
        token.SourceSpan.End.Column,
        "" if token.Trigger == TokenTriggers.None else token.Trigger))

def get_tokens(src, charcount = -1):
    if charcount == -1:
        charcount = len(src)
    source = engine.CreateScriptSourceFromString(src)
    categorizer = engine.GetService[TokenCategorizer]()
    categorizer.Initialize(None, source, SourceLocation.MinValue)
    return categorizer.ReadTokens(charcount)

class TokenBuilder(object):
    def __getattr__(self, name):
        def callable(*args): 
            triggers = args[2] if len(args)>2  else TokenTriggers.None
            return TokenInfo(SourceSpan(args[0], args[1]),
                    Enum.Parse(TokenCategory.None.GetType(), name), triggers)
        return callable

From, To, t = SourceLocation, SourceLocation, TokenBuilder()

#------------------------------------------------------------------------------
# Tests
def test_categorizer_print():
    expected = [
        t.Keyword(From(0,1,1), To(5,1,6)),
        t.StringLiteral(From(6,1,7), To(11,1,12)),
        t.Comment(From(12,1,13), To(20,1,21)),
    ]
    actual = list(get_tokens("print 'foo' #comment"))
    AreEqual(actual, expected)

def test_categorizer_for_loop():
    expected = [
        t.Keyword(From(0,1,1), To(3,1,4)),
        t.Identifier(From(4,1,5), To(8,1,9)),
        t.Keyword(From(9,1,10), To(11,1,12)),
        t.Identifier(From(12,1,13), To(16,1,17)),
        t.Grouping(From(16,1,17), To(17,1,18),
                   TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.StringLiteral(From(17,1,18), To(26,1,27)),
        t.Grouping(From(26,1,27), To(27,1,28),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        t.Delimiter(From(27,1,28), To(28,1,29)),
        t.WhiteSpace(From(28,1,29), To(33,2,5)),
        t.Operator(From(28,1,29), To(33,2,5)),
        t.Keyword(From(33,2,5), To(38,2,10)),
        t.Identifier(From(39,2,11), To(43,2,15)),
        t.Delimiter(From(43,2,15), To(44,2,16), TokenTriggers.ParameterNext)
    ]
    actual = list(get_tokens("for line in open(\"foo.txt\"):\n    print line,"))
    AreEqual(actual, expected)

def test_categorizer_string_literals():
    expected = [
        t.Identifier(From(0,1,1), To(1,1,2)),
        t.Operator(From(1,1,2), To(2,1,3)),
        t.StringLiteral(From(2,1,3), To(9,1,10)),
        t.WhiteSpace(From(9,1,10), To(10,2,1)),
        t.Identifier(From(10,2,1), To(11,2,2)),
        t.Operator(From(11,2,2), To(12,2,3)),
        t.StringLiteral(From(12,2,3), To(18,2,9)),
        t.WhiteSpace(From(18,2,9), To(19,3,1)), 
        t.Identifier(From(19,3,1), To(20,3,2)),
        t.Operator(From(20,3,2), To(21,3,3)),
        t.StringLiteral(From(21,3,3), To(33,3,15)),
    ]
    actual = list(get_tokens("a=\"Hello\"\nb='iron'\nc=\"\"\"python\"\"\""))
    AreEqual(actual, expected)
    
def test_categorizer_list():
    expected = [
        t.Identifier(From(0,1,1), To(5,1,6)),
        t.Operator(From(6,1,7), To(7,1,8)),
        t.Grouping(From(8,1,9), To(9,1,10), TokenTriggers.MatchBraces),
        t.StringLiteral(From(9,1,10), To(15,1,16)),
        t.Delimiter(From(15,1,16), To(16,1,17), TokenTriggers.ParameterNext),
        t.StringLiteral(From(17,1,18), To(23,1,24)),
        t.Delimiter(From(23,1,24), To(24,1,25), TokenTriggers.ParameterNext),
        t.Grouping(From(25,1,26), To(26,1,27), TokenTriggers.MatchBraces),
        t.StringLiteral(From(26,1,27), To(32,1,33)),
        t.Delimiter(From(32,1,33), To(33,1,34), TokenTriggers.ParameterNext),
        t.StringLiteral(From(34,1,35), To(39,1,40)),
        t.Grouping(From(39,1,40), To(40,1,41), TokenTriggers.MatchBraces),
        t.Grouping(From(40,1,41), To(41,1,42), TokenTriggers.MatchBraces),
    ]
    actual = list(get_tokens("names = [\"Dave\", \"Mark\", [\"Jeff\", \"Ann\"]]"))
    AreEqual(actual, expected)

def test_categorizer_tuple():
    expected = [
        t.Identifier(From(0,1,1), To(1,1,2)),
        t.Operator(From(2,1,3), To(3,1,4)),
        t.Grouping(From(4,1,5), To(5,1,6),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.NumericLiteral(From(5,1,6), To(6,1,7)),
        t.Delimiter(From(6,1,7), To(7,1,8), TokenTriggers.ParameterNext),
        t.Operator(From(7,1,8), To(8,1,9)),
        t.NumericLiteral(From(8,1,9), To(9,1,10)),
        t.Grouping(From(9,1,10), To(10,1,11),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        t.WhiteSpace(From(10,1,11), To(11,2,1)),
        t.Identifier(From(11,2,1), To(12,2,2)),
        t.Operator(From(13,2,3), To(14,2,4)),
        t.Grouping(From(15,2,5), To(16,2,6),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.NumericLiteral(From(16,2,6), To(17,2,7)),
        t.Delimiter(From(17,2,7), To(18,2,8),
                     TokenTriggers.ParameterNext),
        t.Grouping(From(18,2,8), To(19,2,9),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
    ]
    actual = list(get_tokens("a = (1,-9)\nb = (7,)"))
    AreEqual(actual, expected)

def test_categorizer_dict():
    expected = [
        t.Identifier(From(0,1,1), To(1,1,2)),
        t.Operator(From(2,1,3), To(3,1,4)),
        t.Grouping(From(4,1,5), To(5,1,6), TokenTriggers.MatchBraces),
        t.StringLiteral(From(10,2,5), To(20,2,15)),
        t.Delimiter(From(21,2,16), To(22,2,17)),
        t.StringLiteral(From(23,2,18), To(32,2,27)),
        t.Grouping(From(37,3,5), To(38,3,6), TokenTriggers.MatchBraces),
    ]
    actual = list(get_tokens("a = {\n    \"username\" : \"beazley\"\n    }"))
    AreEqual(actual, expected)

def test_categorizer_if_else():
    expected = [
        t.Keyword(From(0,1,1), To(2,1,3)),  
        t.Identifier(From(3,1,4), To(7,1,8)),
        t.Delimiter(From(7,1,8), To(8,1,9)),
        t.WhiteSpace(From(8,1,9), To(13,2,5)),
        t.Operator(From(8,1,9), To(13,2,5)),
        t.Keyword(From(13,2,5), To(17,2,9)),
        t.WhiteSpace(From(17,2,9), To(18,3,1)),
        t.Operator(From(17,2,9), To(18,3,1)),
        t.Keyword(From(18,3,1), To(22,3,5)),
        t.Delimiter(From(22,3,5), To(23,3,6)),
        t.WhiteSpace(From(23,3,6), To(28,4,5)),
        t.Operator(From(23,3,6), To(28,4,5)),
        t.Keyword(From(28,4,5), To(32,4,9)),
    ]
    actual = list(get_tokens("if True:\n    pass\nelse:\n    pass"))
    AreEqual(actual, expected)

def test_categorizer_def():
    expected = [
        t.Keyword(From(0,1,1), To(3,1,4)),
        t.Identifier(From(4,1,5), To(7,1,8)),
        t.Grouping(From(7,1,8), To(8,1,9),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.Identifier(From(8,1,9), To(9,1,10)),
        t.Delimiter(From(9,1,10), To(10,1,11),
                     TokenTriggers.ParameterNext),
        t.Identifier(From(10,1,11), To(11,1,12)),
        t.Grouping(From(11,1,12), To(12,1,13),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        t.Delimiter(From(12,1,13), To(13,1,14)),
        t.WhiteSpace(From(13,1,14), To(18,2,5)),
        t.Operator(From(13,1,14), To(18,2,5)),
        t.Keyword(From(18,2,5), To(24,2,11)),
        t.Identifier(From(25,2,12), To(26,2,13)),
        t.Operator(From(26,2,13), To(27,2,14)),
        t.Identifier(From(27,2,14), To(28,2,15)),
    ]
    actual = list(get_tokens("def foo(a,b):\n    return a+b"))

    AreEqual(actual, expected)

def test_categorizer_class():
    expected = [
        t.Keyword(From(0,1,1), To(5,1,6)),
        t.Identifier(From(6,1,7), To(11,1,12)),
        t.Grouping(From(11,1,12), To(12,1,13),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.Identifier(From(12,1,13), To(18,1,19)),
        t.Grouping(From(18,1,19), To(19,1,20),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        t.Delimiter(From(19,1,20), To(20,1,21)),
        t.WhiteSpace(From(20,1,21), To(25,2,5)),
        t.Operator(From(20,1,21), To(25,2,5)),
        t.Keyword(From(25,2,5), To(28,2,8)),
        t.Identifier(From(29,2,9), To(37,2,17)),
        t.Grouping(From(37,2,17), To(38,2,18),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
        t.Identifier(From(38,2,18), To(42,2,22)),
        t.Grouping(From(42,2,22), To(43,2,23),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        t.Delimiter(From(43,2,23), To(44,2,24)),
        t.WhiteSpace(From(44,2,24), To(45,3,1)),
        t.Operator(From(44,2,24), To(45,3,1)),
        t.Keyword(From(45,3,1), To(49,3,5)),
    ]
    actual = list(get_tokens("class Stack(object):\n    def __init__(self):\npass"))
    AreEqual(actual, expected)

def test_tokenizer_restartable():
    categorizer = engine.GetService[TokenCategorizer]()
    Assert(categorizer.IsRestartable)

def test_tokenizer_restart_multistring():
    expected1 = [
        t.Identifier(From(0,1,1), To(1,1,2)),
        t.Operator(From(2, 1, 3), To(3, 1, 4)),
        t.StringLiteral(From(4, 1, 5), To(10, 1, 11)),
    ]
    expected2 = [
        t.StringLiteral(From(0, 1, 1), To(6, 1, 7)),
    ]
    
    categorizer = engine.GetService[TokenCategorizer]()

    source = engine.CreateScriptSourceFromString('''s = """abc''')
    categorizer.Initialize(None, source, SourceLocation.MinValue)
    actual = list(categorizer.ReadTokens(len(source.GetCode())))
    AreEqual(actual, expected1)

    state = categorizer.CurrentState
    
    source = engine.CreateScriptSourceFromString('''def"""''')
    categorizer.Initialize(state, source, SourceLocation.MinValue)
    actual = list(categorizer.ReadTokens(len(source.GetCode())))
    
    AreEqual(actual, expected2)

def test_tokenizer_unterminated_string_literal():
    expected1 = [
        t.Identifier(From(0,1,1), To(1,1,2)),
        t.Operator(From(2, 1, 3), To(3, 1, 4)),
        t.StringLiteral(From(4, 1, 5), To(8, 1, 9)),
    ]
    expected2 = [
        t.StringLiteral(From(0, 1, 1), To(4, 1, 5)),
    ]
    
    categorizer = engine.GetService[TokenCategorizer]()

    source = engine.CreateScriptSourceFromString('''s = "abc''')
    categorizer.Initialize(None, source, SourceLocation.MinValue)
    actual = list(categorizer.ReadTokens(len(source.GetCode())))
    AreEqual(actual, expected1)

    state = categorizer.CurrentState
    
    source = engine.CreateScriptSourceFromString('''def"''')
    categorizer.Initialize(state, source, SourceLocation.MinValue)
    actual = list(categorizer.ReadTokens(len(source.GetCode())))

    AreEqual(actual, expected2)

run_test(__name__)
