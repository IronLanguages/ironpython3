# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_scope from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_scope

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_scope.ScopeTests('testBoundAndFree'))
        suite.addTest(test.test_scope.ScopeTests('testCellIsKwonlyArg'))
        suite.addTest(test.test_scope.ScopeTests('testCellLeak'))
        suite.addTest(test.test_scope.ScopeTests('testClassAndGlobal'))
        suite.addTest(unittest.expectedFailure(test.test_scope.ScopeTests('testClassNamespaceOverridesClosure'))) # TODO: figure out
        suite.addTest(test.test_scope.ScopeTests('testComplexDefinitions'))
        suite.addTest(test.test_scope.ScopeTests('testEvalExecFreeVars'))
        suite.addTest(test.test_scope.ScopeTests('testEvalFreeVars'))
        suite.addTest(test.test_scope.ScopeTests('testExtraNesting'))
        suite.addTest(test.test_scope.ScopeTests('testFreeVarInMethod'))
        suite.addTest(test.test_scope.ScopeTests('testFreeingCell'))
        suite.addTest(test.test_scope.ScopeTests('testGlobalInParallelNestedFunctions'))
        suite.addTest(test.test_scope.ScopeTests('testInteractionWithTraceFunc'))
        suite.addTest(test.test_scope.ScopeTests('testLambdas'))
        #suite.addTest(unittest.expectedFailure(test.test_scope.ScopeTests('testLeaks'))) # https://github.com/IronLanguages/ironpython3/issues/1056
        suite.addTest(test.test_scope.ScopeTests('testListCompLocalVars'))
        suite.addTest(test.test_scope.ScopeTests('testLocalsClass'))
        suite.addTest(test.test_scope.ScopeTests('testLocalsClass_WithTrace'))
        suite.addTest(test.test_scope.ScopeTests('testLocalsFunction'))
        suite.addTest(test.test_scope.ScopeTests('testMixedFreevarsAndCellvars'))
        suite.addTest(test.test_scope.ScopeTests('testNearestEnclosingScope'))
        suite.addTest(test.test_scope.ScopeTests('testNestedNonLocal'))
        suite.addTest(test.test_scope.ScopeTests('testNestingGlobalNoFree'))
        suite.addTest(test.test_scope.ScopeTests('testNestingPlusFreeRefToGlobal'))
        suite.addTest(test.test_scope.ScopeTests('testNestingThroughClass'))
        suite.addTest(test.test_scope.ScopeTests('testNonLocalClass'))
        suite.addTest(test.test_scope.ScopeTests('testNonLocalFunction'))
        suite.addTest(test.test_scope.ScopeTests('testNonLocalGenerator'))
        suite.addTest(test.test_scope.ScopeTests('testNonLocalMethod'))
        suite.addTest(test.test_scope.ScopeTests('testRecursion'))
        suite.addTest(test.test_scope.ScopeTests('testScopeOfGlobalStmt'))
        suite.addTest(test.test_scope.ScopeTests('testSimpleAndRebinding'))
        suite.addTest(test.test_scope.ScopeTests('testSimpleNesting'))
        suite.addTest(test.test_scope.ScopeTests('testTopIsNotSignificant'))
        suite.addTest(test.test_scope.ScopeTests('testUnboundLocal'))
        suite.addTest(test.test_scope.ScopeTests('testUnboundLocal_AfterDel'))
        suite.addTest(test.test_scope.ScopeTests('testUnboundLocal_AugAssign'))
        suite.addTest(test.test_scope.ScopeTests('testUnoptimizedNamespaces'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_scope, pattern)

run_test(__name__)
