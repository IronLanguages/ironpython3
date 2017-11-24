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

import unittest


from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

# from System import Enum
# import Microsoft.Scripting.Hosting
# from Microsoft.Scripting import TokenTriggers, TokenInfo, TokenCategory, SourceLocation, SourceSpan
# from Microsoft.Scripting.Hosting import ScriptRuntime, TokenCategorizer

def pt(tokens):
    from Microsoft.Scripting import TokenTriggers
    for token in tokens:
        print "t.%s(From(%d,%d,%d), To(%d,%d,%d)%s)," % \
        (token.Category.ToString(), token.SourceSpan.Start.Index,
        token.SourceSpan.Start.Line, token.SourceSpan.Start.Column,
        token.SourceSpan.End.Index, token.SourceSpan.End.Line,
        token.SourceSpan.End.Column,
        "" if token.Trigger == TokenTriggers.None else token.Trigger)

def get_tokens(engine, src, charcount = -1):
    from Microsoft.Scripting import SourceLocation
    from Microsoft.Scripting.Hosting import TokenCategorizer
    if charcount == -1:
        charcount = len(src)
    source = engine.CreateScriptSourceFromString(src)
    categorizer = engine.GetService[TokenCategorizer]()
    categorizer.Initialize(None, source, SourceLocation.MinValue)
    return categorizer.ReadTokens(charcount)

class TokenBuilder(object):
    def __getattr__(self, name):
        def callable(*args):
            from Microsoft.Scripting import SourceSpan, TokenCategory, TokenInfo, TokenTriggers
            from System import Enum
            triggers = args[2] if len(args)>2  else TokenTriggers.None
            return TokenInfo(SourceSpan(args[0], args[1]),
                    Enum.Parse(TokenCategory.None.GetType(), name), triggers)
        return callable

@skipUnlessIronPython()
class TokenCategorizerTest(IronPythonTestCase):
    def setUp(self):
        super(TokenCategorizerTest, self).setUp()
        self.load_iron_python_test()

        from IronPython.Hosting import Python
        from Microsoft.Scripting import SourceLocation

        self.engine = Python.CreateEngine()
        self.From, self.To, self.t = SourceLocation, SourceLocation, TokenBuilder()

    def test_categorizer_print(self):
        expected = [
            self.t.Keyword(self.From(0,1,1), self.To(5,1,6)),
            self.t.StringLiteral(self.From(6,1,7), self.To(11,1,12)),
            self.t.Comment(self.From(12,1,13), self.To(20,1,21)),
        ]
        actual = list(get_tokens(self.engine, "print 'foo' #comment"))
        self.assertEqual(actual, expected)

    def test_categorizer_for_loop(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Keyword(self.From(0,1,1), self.To(3,1,4)),
            self.t.Identifier(self.From(4,1,5), self.To(8,1,9)),
            self.t.Keyword(self.From(9,1,10), self.To(11,1,12)),
            self.t.Identifier(self.From(12,1,13), self.To(16,1,17)),
            self.t.Grouping(self.From(16,1,17), self.To(17,1,18),
                    TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.StringLiteral(self.From(17,1,18), self.To(26,1,27)),
            self.t.Grouping(self.From(26,1,27), self.To(27,1,28),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
            self.t.Delimiter(self.From(27,1,28), self.To(28,1,29)),
            self.t.WhiteSpace(self.From(28,1,29), self.To(33,2,5)),
            self.t.Operator(self.From(28,1,29), self.To(33,2,5)),
            self.t.Keyword(self.From(33,2,5), self.To(38,2,10)),
            self.t.Identifier(self.From(39,2,11), self.To(43,2,15)),
            self.t.Delimiter(self.From(43,2,15), self.To(44,2,16), TokenTriggers.ParameterNext)
        ]
        actual = list(get_tokens(self.engine, "for line in open(\"foo.txt\"):\n    print line,"))
        self.assertEqual(actual, expected)

    def test_categorizer_string_literals(self):
        expected = [
            self.t.Identifier(self.From(0,1,1), self.To(1,1,2)),
            self.t.Operator(self.From(1,1,2), self.To(2,1,3)),
            self.t.StringLiteral(self.From(2,1,3), self.To(9,1,10)),
            self.t.WhiteSpace(self.From(9,1,10), self.To(10,2,1)),
            self.t.Identifier(self.From(10,2,1), self.To(11,2,2)),
            self.t.Operator(self.From(11,2,2), self.To(12,2,3)),
            self.t.StringLiteral(self.From(12,2,3), self.To(18,2,9)),
            self.t.WhiteSpace(self.From(18,2,9), self.To(19,3,1)),
            self.t.Identifier(self.From(19,3,1), self.To(20,3,2)),
            self.t.Operator(self.From(20,3,2), self.To(21,3,3)),
            self.t.StringLiteral(self.From(21,3,3), self.To(33,3,15)),
        ]
        actual = list(get_tokens(self.engine, "a=\"Hello\"\nb='iron'\nc=\"\"\"python\"\"\""))
        self.assertEqual(actual, expected)

    def test_categorizer_list(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Identifier(self.From(0,1,1), self.To(5,1,6)),
            self.t.Operator(self.From(6,1,7), self.To(7,1,8)),
            self.t.Grouping(self.From(8,1,9), self.To(9,1,10), TokenTriggers.MatchBraces),
            self.t.StringLiteral(self.From(9,1,10), self.To(15,1,16)),
            self.t.Delimiter(self.From(15,1,16), self.To(16,1,17), TokenTriggers.ParameterNext),
            self.t.StringLiteral(self.From(17,1,18), self.To(23,1,24)),
            self.t.Delimiter(self.From(23,1,24), self.To(24,1,25), TokenTriggers.ParameterNext),
            self.t.Grouping(self.From(25,1,26), self.To(26,1,27), TokenTriggers.MatchBraces),
            self.t.StringLiteral(self.From(26,1,27), self.To(32,1,33)),
            self.t.Delimiter(self.From(32,1,33), self.To(33,1,34), TokenTriggers.ParameterNext),
            self.t.StringLiteral(self.From(34,1,35), self.To(39,1,40)),
            self.t.Grouping(self.From(39,1,40), self.To(40,1,41), TokenTriggers.MatchBraces),
            self.t.Grouping(self.From(40,1,41), self.To(41,1,42), TokenTriggers.MatchBraces),
        ]
        actual = list(get_tokens(self.engine, "names = [\"Dave\", \"Mark\", [\"Jeff\", \"Ann\"]]"))
        self.assertEqual(actual, expected)

    def test_categorizer_tuple(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Identifier(self.From(0,1,1), self.To(1,1,2)),
            self.t.Operator(self.From(2,1,3), self.To(3,1,4)),
            self.t.Grouping(self.From(4,1,5), self.To(5,1,6),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.NumericLiteral(self.From(5,1,6), self.To(6,1,7)),
            self.t.Delimiter(self.From(6,1,7), self.To(7,1,8), TokenTriggers.ParameterNext),
            self.t.Operator(self.From(7,1,8), self.To(8,1,9)),
            self.t.NumericLiteral(self.From(8,1,9), self.To(9,1,10)),
            self.t.Grouping(self.From(9,1,10), self.To(10,1,11),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
            self.t.WhiteSpace(self.From(10,1,11), self.To(11,2,1)),
            self.t.Identifier(self.From(11,2,1), self.To(12,2,2)),
            self.t.Operator(self.From(13,2,3), self.To(14,2,4)),
            self.t.Grouping(self.From(15,2,5), self.To(16,2,6),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.NumericLiteral(self.From(16,2,6), self.To(17,2,7)),
            self.t.Delimiter(self.From(17,2,7), self.To(18,2,8),
                        TokenTriggers.ParameterNext),
            self.t.Grouping(self.From(18,2,8), self.To(19,2,9),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
        ]
        actual = list(get_tokens(self.engine, "a = (1,-9)\nb = (7,)"))
        self.assertEqual(actual, expected)

    def test_categorizer_dict(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Identifier(self.From(0,1,1), self.To(1,1,2)),
            self.t.Operator(self.From(2,1,3), self.To(3,1,4)),
            self.t.Grouping(self.From(4,1,5), self.To(5,1,6), TokenTriggers.MatchBraces),
            self.t.StringLiteral(self.From(10,2,5), self.To(20,2,15)),
            self.t.Delimiter(self.From(21,2,16), self.To(22,2,17)),
            self.t.StringLiteral(self.From(23,2,18), self.To(32,2,27)),
            self.t.Grouping(self.From(37,3,5), self.To(38,3,6), TokenTriggers.MatchBraces),
        ]
        actual = list(get_tokens(self.engine, "a = {\n    \"username\" : \"beazley\"\n    }"))
        self.assertEqual(actual, expected)

    def test_categorizer_if_else(self):
        expected = [
            self.t.Keyword(self.From(0,1,1), self.To(2,1,3)),
            self.t.Identifier(self.From(3,1,4), self.To(7,1,8)),
            self.t.Delimiter(self.From(7,1,8), self.To(8,1,9)),
            self.t.WhiteSpace(self.From(8,1,9), self.To(13,2,5)),
            self.t.Operator(self.From(8,1,9), self.To(13,2,5)),
            self.t.Keyword(self.From(13,2,5), self.To(17,2,9)),
            self.t.WhiteSpace(self.From(17,2,9), self.To(18,3,1)),
            self.t.Operator(self.From(17,2,9), self.To(18,3,1)),
            self.t.Keyword(self.From(18,3,1), self.To(22,3,5)),
            self.t.Delimiter(self.From(22,3,5), self.To(23,3,6)),
            self.t.WhiteSpace(self.From(23,3,6), self.To(28,4,5)),
            self.t.Operator(self.From(23,3,6), self.To(28,4,5)),
            self.t.Keyword(self.From(28,4,5), self.To(32,4,9)),
        ]
        actual = list(get_tokens(self.engine, "if True:\n    pass\nelse:\n    pass"))
        self.assertEqual(actual, expected)

    def test_categorizer_def(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Keyword(self.From(0,1,1), self.To(3,1,4)),
            self.t.Identifier(self.From(4,1,5), self.To(7,1,8)),
            self.t.Grouping(self.From(7,1,8), self.To(8,1,9),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.Identifier(self.From(8,1,9), self.To(9,1,10)),
            self.t.Delimiter(self.From(9,1,10), self.To(10,1,11),
                        TokenTriggers.ParameterNext),
            self.t.Identifier(self.From(10,1,11), self.To(11,1,12)),
            self.t.Grouping(self.From(11,1,12), self.To(12,1,13),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
            self.t.Delimiter(self.From(12,1,13), self.To(13,1,14)),
            self.t.WhiteSpace(self.From(13,1,14), self.To(18,2,5)),
            self.t.Operator(self.From(13,1,14), self.To(18,2,5)),
            self.t.Keyword(self.From(18,2,5), self.To(24,2,11)),
            self.t.Identifier(self.From(25,2,12), self.To(26,2,13)),
            self.t.Operator(self.From(26,2,13), self.To(27,2,14)),
            self.t.Identifier(self.From(27,2,14), self.To(28,2,15)),
        ]
        actual = list(get_tokens(self.engine, "def foo(a,b):\n    return a+b"))

        self.assertEqual(actual, expected)

    def test_categorizer_class(self):
        from Microsoft.Scripting import TokenTriggers
        expected = [
            self.t.Keyword(self.From(0,1,1), self.To(5,1,6)),
            self.t.Identifier(self.From(6,1,7), self.To(11,1,12)),
            self.t.Grouping(self.From(11,1,12), self.To(12,1,13),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.Identifier(self.From(12,1,13), self.To(18,1,19)),
            self.t.Grouping(self.From(18,1,19), self.To(19,1,20),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
            self.t.Delimiter(self.From(19,1,20), self.To(20,1,21)),
            self.t.WhiteSpace(self.From(20,1,21), self.To(25,2,5)),
            self.t.Operator(self.From(20,1,21), self.To(25,2,5)),
            self.t.Keyword(self.From(25,2,5), self.To(28,2,8)),
            self.t.Identifier(self.From(29,2,9), self.To(37,2,17)),
            self.t.Grouping(self.From(37,2,17), self.To(38,2,18),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterStart),
            self.t.Identifier(self.From(38,2,18), self.To(42,2,22)),
            self.t.Grouping(self.From(42,2,22), self.To(43,2,23),
                        TokenTriggers.MatchBraces|TokenTriggers.ParameterEnd),
            self.t.Delimiter(self.From(43,2,23), self.To(44,2,24)),
            self.t.WhiteSpace(self.From(44,2,24), self.To(45,3,1)),
            self.t.Operator(self.From(44,2,24), self.To(45,3,1)),
            self.t.Keyword(self.From(45,3,1), self.To(49,3,5)),
        ]
        actual = list(get_tokens(self.engine, "class Stack(object):\n    def __init__(self):\npass"))
        self.assertEqual(actual, expected)

    def test_tokenizer_restartable(self):
        from Microsoft.Scripting.Hosting import TokenCategorizer
        categorizer = self.engine.GetService[TokenCategorizer]()
        self.assertTrue(categorizer.IsRestartable)

    def test_tokenizer_restart_multistring(self):
        from Microsoft.Scripting import SourceLocation
        from Microsoft.Scripting.Hosting import TokenCategorizer
        expected1 = [
            self.t.Identifier(self.From(0,1,1), self.To(1,1,2)),
            self.t.Operator(self.From(2, 1, 3), self.To(3, 1, 4)),
            self.t.StringLiteral(self.From(4, 1, 5), self.To(10, 1, 11)),
        ]
        expected2 = [
            self.t.StringLiteral(self.From(0, 1, 1), self.To(6, 1, 7)),
        ]

        categorizer = self.engine.GetService[TokenCategorizer]()

        source = self.engine.CreateScriptSourceFromString('''s = """abc''')
        categorizer.Initialize(None, source, SourceLocation.MinValue)
        actual = list(categorizer.ReadTokens(len(source.GetCode())))
        self.assertEqual(actual, expected1)

        state = categorizer.CurrentState

        source = self.engine.CreateScriptSourceFromString('''def"""''')
        categorizer.Initialize(state, source, SourceLocation.MinValue)
        actual = list(categorizer.ReadTokens(len(source.GetCode())))

        self.assertEqual(actual, expected2)

    def test_tokenizer_unterminated_string_literal(self):
        from Microsoft.Scripting import SourceLocation
        from Microsoft.Scripting.Hosting import TokenCategorizer
        expected1 = [
            self.t.Identifier(self.From(0,1,1), self.To(1,1,2)),
            self.t.Operator(self.From(2, 1, 3), self.To(3, 1, 4)),
            self.t.StringLiteral(self.From(4, 1, 5), self.To(8, 1, 9)),
        ]
        expected2 = [
            self.t.StringLiteral(self.From(0, 1, 1), self.To(4, 1, 5)),
        ]

        categorizer = self.engine.GetService[TokenCategorizer]()

        source = self.engine.CreateScriptSourceFromString('''s = "abc''')
        categorizer.Initialize(None, source, SourceLocation.MinValue)
        actual = list(categorizer.ReadTokens(len(source.GetCode())))
        self.assertEqual(actual, expected1)

        state = categorizer.CurrentState

        source = self.engine.CreateScriptSourceFromString('''def"''')
        categorizer.Initialize(state, source, SourceLocation.MinValue)
        actual = list(categorizer.ReadTokens(len(source.GetCode())))

        self.assertEqual(actual, expected2)

run_test(__name__)
