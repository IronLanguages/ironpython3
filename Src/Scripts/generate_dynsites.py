# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate
import clr
from System import *

MaxSiteArity = 14

def gsig(n):
    return ", ".join(["T%d" % i for i in range(n)] + ["TRet"])

def gsig_noret(n):
    return ", ".join(["T%d" % i for i in range(n)])

def gparms(size):
    if size == 0: return ''
    return ", " + ", ".join(["T%d arg%d" % (i,i) for i in range(size)])

def gargs(size):
    if size == 0: return ''
    return ", " + ", ".join(["arg%d" % i for i in range(size)])

def gargs_index(size):
    if size == 0: return ''
    return ", " + ", ".join(["args[%d]" % i for i in range(size)])

def gargs_indexwithcast(size):
    if size == 0: return ''
    return ", " + ", ".join(["(T%d)args[%d]" % (i, i) for i in range(size)])

def gonlyargs(size):
    if size == 0: return ''
    return ", ".join(["arg%d" % i for i in range(size)])

numbers = {
     0 : ( 'no',        ''            ),
     1 : ( 'one',       'first'       ),
     2 : ( 'two',       'second'      ),
     3 : ( 'three',     'third'       ),
     4 : ( 'four',      'fourth'      ),
     5 : ( 'five',      'fifth'       ),
     6 : ( 'six',       'sixth'       ),
     7 : ( 'seven',     'seventh'     ),
     8 : ( 'eight',     'eighth'      ),
     9 : ( 'nine',      'ninth'       ),
    10 : ( 'ten',       'tenth'       ),
    11 : ( 'eleven',    'eleventh'    ),
    12 : ( 'twelve',    'twelfth'     ),
    13 : ( 'thirteen',  'thirteenth'  ),
    14 : ( 'fourteen',  'fourteenth'  ),
    15 : ( 'fifteen',   'fifteenth'   ),
    16 : ( 'sixteen',   'sixteenth'   ),
}

def gsig_1(n, varianceAnnotated):
    if varianceAnnotated: annotation = "in "
    else: annotation = ""
    return ", ".join([annotation + "T%d" % i for i in range(1, n + 1)])
def gparams_1(n):
    return ", ".join(["T%d arg%d" % (i, i) for i in range(1, n + 1)])
def gsig_1_result(n, varianceAnnotated):
    if varianceAnnotated: 
        contraAnnotation = "in "
        coAnnotation = "out "
    else: 
        contraAnnotation = ""
        coAnnotation = ""
    return ", ".join([contraAnnotation + "T%d" % i for i in range(1, n + 1)] + [coAnnotation + 'TResult'])

def generate_one_action_type(cw, n, varianceAnnotated):
    cw.write("""
/// <summary>
/// Encapsulates a method that takes %(alpha)s parameters and does not return a value.
/// </summary>""", alpha = numbers[n][0])

    for i in range(1, n + 1):
        cw.write('/// <typeparam name="T%(number)d">The type of the %(alphath)s parameter of the method that this delegate encapsulates.</typeparam>', number = i, alphath = numbers[i][1])
    for i in range(1, n + 1):
        cw.write('/// <param name="arg%(number)d">The %(alphath)s parameter of the method that this delegate encapsulates.</param>', number = i, alphath = numbers[i][1])
    if n > 2:
        cw.write('[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes")]')
    cw.write("public delegate void Action<%(gsig)s>(%(gparms)s);", gsig = gsig_1(n, varianceAnnotated), gparms = gparams_1(n))

def generate_one_func_type(cw, n, varianceAnnotated):
    if n != 1: plural = "s"
    else: plural = ""
    cw.write("""
/// <summary>
/// Encapsulates a method that has %(alpha)s parameter%(plural)s and returns a value of the type specified by the TResult parameter.
/// </summary>""", alpha = numbers[n][0], plural = plural)

    for i in range(1, n + 1):
        cw.write('/// <typeparam name="T%(number)d">The type of the %(alphath)s parameter of the method that this delegate encapsulates.</typeparam>', number = i, alphath = numbers[i][1])
    cw.write('/// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>')
    for i in range(1, n + 1):
        cw.write('/// <param name="arg%(number)d">The %(alphath)s parameter of the method that this delegate encapsulates.</param>', number = i, alphath = numbers[i][1])
    cw.write("""/// <returns>The return value of the method that this delegate encapsulates.</returns>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes")]""")
    cw.write('public delegate TResult Func<%(gsig)s>(%(gparms)s);', gsig = gsig_1_result(n, varianceAnnotated), gparms = gparams_1(n))

def gen_func_action(cw, lo, med, hi, func):
    cw.write("#if !FEATURE_VARIANCE")
    for i in range(lo, hi):
        func(cw, i, False)
    cw.write("#else")
    #generate the func times with covariance and contravairance annotations
    for i in range(med, hi):
        func(cw, i, True)
    cw.write("\n#endif")

def gen_func_types(cw):
    gen_func_action(cw, 5, 9, 17, generate_one_func_type)

def gen_action_types(cw):
    gen_func_action(cw, 5, 9, 17, generate_one_action_type)

#
# Pregenerated UpdateAndExecute methods for Func, Action delegate types
#
# Sample argument values:
#
# * methodDeclaration:
#       "TRet UpdateAndExecute1<T0, T1, TRet>(CallSite site, T0 arg0, T1 arg1)"
#       "void UpdateAndExecuteVoid1<T0, T1>(CallSite site, T0 arg0, T1 arg1)"
#
# * matchMakerDeclaration:
#       "TRet Fallback1<T0, T1, TRet>(CallSite site, T0 arg0, T1 arg1)"
#       "void FallbackVoid1<T0, T1>(CallSite site, T0 arg0, T1 arg1)"
#
# * funcType:
#       "Func<CallSite, T0, T1, TRet>"
#       "Action<CallSite, T0, T1>"
#
# * setResult
#       "result =" or ""
#
# * returnResult
#       "return result" or "return"
#
# * returnDefault
#       "return default(TRet)" or "return"
#
# * declareResult
#       Either "TRet result;\n" or ""
#
# * matchmakerArgs
#       "mm_arg0, mm_arg1"
#
# * args
#       "arg0, arg1"
#
# * argsTypes
#       "GetTypeForBinding(arg0), GetTypeForBinding(arg1)"
#
# * typeArgs
#       T0, T1, TRet
#       T0, T1
#
# * fallbackMethod
#       Fallback1
#       FallbackVoid1
#

def gen_update_targets(cw):
    maxArity = 11

    def argList(size):
        if (size == 0):
            return ""
        else:
            return ", " + ", ".join(["arg%d" % i for i in range(size)])

    def argElems(size):
        return ", ".join(["arg%d" % i for i in range(size)])
  
    def mmArgList(size):
        return ", ".join(["mm_arg%d" % i for i in range(size)])
    
    #def argTypeList(size):
    #    return ", ".join(["GetTypeForBinding(arg%d)" % i for i in range(size)])

    replace = {}
    for n in xrange(0, maxArity):
        replace['setResult'] = 'result ='
        replace['returnResult'] = 'return result'
        replace['returnDefault'] = 'return default(TRet)'
        replace['declareResult'] = 'TRet result;\n'
        replace['args'] = argList(n)
        replace['argelems'] = argElems(n)
        #replace['argTypes'] = argTypeList(n)
        replace['matchmakerArgs'] = mmArgList(n)
        replace['funcType'] = 'Func<CallSite, %s>' % gsig(n)
        replace['methodDeclaration'] = 'TRet UpdateAndExecute%d<%s>(CallSite site%s)' % (n, gsig(n), gparms(n))
        replace['matchMakerDeclaration'] = 'TRet NoMatch%d<%s>(CallSite site%s)' % (n, gsig(n), gparms(n))
        replace['fallbackMethod'] = 'Fallback%d' % (n, )
        replace['typeArgs'] = gsig(n)
        cw.write(updateAndExecute, **replace)

    for n in xrange(1, maxArity):
        replace['setResult'] = ''
        replace['returnResult'] = 'return'
        replace['returnDefault'] = 'return'
        replace['declareResult'] = ''
        replace['args'] = argList(n)
        replace['argelems'] = argElems(n)
        #replace['argTypes'] = argTypeList(n)
        replace['matchmakerArgs'] = mmArgList(n)
        replace['funcType'] = 'Action<CallSite, %s>' % gsig_noret(n)
        replace['methodDeclaration'] = 'void UpdateAndExecuteVoid%d<%s>(CallSite site%s)' % (n, gsig_noret(n), gparms(n))
        replace['matchMakerDeclaration'] = 'void NoMatchVoid%d<%s>(CallSite site%s)' % (n, gsig_noret(n), gparms(n))
        replace['fallbackMethod'] = 'FallbackVoid%d' % (n, )
        replace['typeArgs'] = gsig_noret(n)
        cw.write(updateAndExecute, **replace)

#
# WARNING: If you're changing the generated C# code, make sure you update the
# expression tree representation as well, which lives in UpdateDelegate.cs
# The two implementations *must* be kept in sync!
#
updateAndExecute = '''
[Obsolete("pregenerated CallSite<T>.Update delegate", true)]
internal static %(methodDeclaration)s {
    //
    // Declare the locals here upfront. It actually saves JIT stack space.
    //
    var @this = (CallSite<%(funcType)s>)site;
    %(funcType)s[] applicable;
    %(funcType)s rule, originalRule = @this.Target;
    %(declareResult)s

    //
    // Create matchmaker and its site. We'll need them regardless.
    //
    site = CallSiteOps.CreateMatchmaker(@this);

    //
    // Level 1 cache lookup
    //
    if ((applicable = CallSiteOps.GetRules(@this)) != null) {
        for (int i = 0; i < applicable.Length; i++) {
            rule = applicable[i];

            //
            // Execute the rule
            //

            // if we've already tried it skip it...
            if ((object)rule != (object)originalRule) {
                @this.Target = rule;
                %(setResult)s rule(site%(args)s);

                if (CallSiteOps.GetMatch(site)) {
                    CallSiteOps.UpdateRules(@this, i);
                    %(returnResult)s;
                }        
                        
                // Rule didn't match, try the next one
                CallSiteOps.ClearMatch(site);            
            }
        }
    }

    //
    // Level 2 cache lookup
    //

    //
    // Any applicable rules in level 2 cache?
    //
    
    var cache = CallSiteOps.GetRuleCache(@this);

    applicable = cache.GetRules();
    for (int i = 0; i < applicable.Length; i++) {
        rule = applicable[i];

        //
        // Execute the rule
        //
        @this.Target = rule;

        try {
            %(setResult)s rule(site%(args)s);
            if (CallSiteOps.GetMatch(site)) {
                %(returnResult)s;
            }
        } finally {
            if (CallSiteOps.GetMatch(site)) {
                //
                // Rule worked. Add it to level 1 cache
                //
                CallSiteOps.AddRule(@this, rule);
                // and then move it to the front of the L2 cache
                CallSiteOps.MoveRule(cache, rule, i);
            }
        }
        
        // Rule didn't match, try the next one
        CallSiteOps.ClearMatch(site);
    }

    //
    // Miss on Level 0, 1 and 2 caches. Create new rule
    //

    rule = null;
    var args = new object[] { %(argelems)s };
   
    for (; ; ) {
        @this.Target = originalRule;
        rule = @this.Target = @this.Binder.BindCore(@this, args);

        //
        // Execute the rule on the matchmaker site
        //

        try {
            %(setResult)s rule(site%(args)s);
            if (CallSiteOps.GetMatch(site)) {
                %(returnResult)s;
            }
        } finally {
            if (CallSiteOps.GetMatch(site)) {
                //
                // The rule worked. Add it to level 1 cache.
                //
                CallSiteOps.AddRule(@this, rule);
            }
        }

        // Rule we got back didn't work, try another one
        CallSiteOps.ClearMatch(site);
    }
}

[Obsolete("pregenerated CallSite<T>.Update delegate", true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters")]
internal static %(matchMakerDeclaration)s {
    site._match = false;
    %(returnDefault)s;
}

'''

MaxTypes = MaxSiteArity + 2

def gen_delegate_func(cw):
    for i in range(MaxTypes + 1):
        cw.write("case %(length)d: return typeof(Func<%(targs)s>).MakeGenericType(types);", length = i + 1, targs = "," * i)

def gen_delegate_action(cw):
    for i in range(MaxTypes):
        cw.write("case %(length)d: return typeof(Action<%(targs)s>).MakeGenericType(types);", length = i + 1, targs = "," * i)

def gen_max_delegate_arity(cw):
    cw.write('private const int MaximumArity = %d;' % (MaxTypes + 1))

mismatch = """// Mismatch detection - arity %(size)d
internal static TRet Mismatch%(size)d<%(ts)s>(StrongBox<bool> box, %(params)s) {
    box.Value = false;
    return default(TRet);
}
"""

def gen_matchmaker(cw):
    cw.write("//\n// Mismatch routines for dynamic sites\n//\n")
    for n in range(MaxSiteArity + 2):
        cw.write(mismatch, ts = gsig(n), size = n, params = "CallSite site" + gparms(n))

mismatch_void = """// Mismatch detection - arity %(size)d
internal static void MismatchVoid%(size)d<%(ts)s>(StrongBox<bool> box, %(params)s) {
    box.Value = false;
}
"""

def gen_void_matchmaker(cw):
    cw.write("//\n// Mismatch routines for dynamic sites with void return type\n//\n")
    for n in range(1, MaxSiteArity + 2):
        cw.write(mismatch_void, ts = gsig_noret(n), size = n, params = "CallSite site" + gparms(n))


update_target="""/// <summary>
/// Site update code - arity %(arity)d
/// </summary>
internal static TRet Update%(arity)d<T, %(ts)s>(CallSite site%(tparams)s) where T : class {
    return (TRet)((CallSite<T>)site).UpdateAndExecute(new object[] { %(targs)s });
}
"""

matchcaller_target="""/// Matchcaller - arity %(arity)d
internal static object Call%(arity)d<%(ts)s>(Func<CallSite, %(ts)s> target, CallSite site, object[] args) {
    return (object)target(site%(targs)s);
}
"""

def gen_matchcaller_targets(cw):
    for n in range(1, MaxSiteArity):
        cw.write(matchcaller_target, ts = gsig(n), targs = gargs_indexwithcast(n), arity = n)

matchcaller_target_void = """// Matchcaller - arity %(arity)d
internal static object CallVoid%(arity)d<%(ts)s>(Action<CallSite, %(ts)s> target, CallSite site, object[] args) {
    target(site%(targs)s);
    return null;
}
"""

def gen_void_matchcaller_targets(cw):
    for n in range(1, MaxSiteArity):
        cw.write(matchcaller_target_void, ts = gsig_noret(n), targs = gargs_indexwithcast(n), arity = n)

def main():
    return generate(
        #("Func Types", gen_func_types),
        #("Action Types", gen_action_types),
        #("UpdateAndExecute Methods", gen_update_targets),
        ("Delegate Action Types", gen_delegate_action),
        ("Delegate Func Types", gen_delegate_func),
        ("Maximum Delegate Arity", gen_max_delegate_arity),
# outer ring generators
        ("Delegate Microsoft Scripting Action Types", gen_delegate_action),
        ("Delegate Microsoft Scripting Scripting Func Types", gen_delegate_func),
        
    )

if __name__ == "__main__":
    main()
