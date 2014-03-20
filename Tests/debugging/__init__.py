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
-------------------------------------------------------------------------------
INTRODUCTION
This test plan addresses debugging test coverage for the 2.x series of 
IronPython interpreter releases.

EDITORS NOTES
Throughout this document you will find references to documentation in
other packages similar to "See documentation for debugging.pdb_module".  This simply
means that relative to this package (i.e., debugging), you should follow
the 'pdb_module (package)' link at the bottom of this page.

-------------------------------------------------------------------------------
FEATURE GOALS
The goals of IP's debugging feature is to provide IronPython users with 
complete use of CPython's existing debugger functionality, the "pdb" and
"bdb" modules respectively, while also providing a great interactive debugging 
experience using Microsoft tools such as mdbg and the Visual Studio IDE.

-------------------------------------------------------------------------------
PRIMARY TESTING CONCERNS
- new builtin module attributes added to IP to support CPython's "bdb" and "pdb"
  modules
- compatibility with CPython's use of the "bdb" and "pdb" modules
- what happens breakpoint-wise when IP steps into .NET code from "pdb"?
- .NET applications hosting IP should be able to debug Python code
- the experience of debugging IP under Microsoft's mdbg and Visual 
  Studio tools is what existing CSharp and VB.Net would expect; but still
  Pythonic
- "-D" command-line flag

-------------------------------------------------------------------------------
PRIMARY TESTING FOCUS
Our principal testing focus will be on testing that IP's support of the
"pdb" module is completely compatible with that of CPython by running existing
CPython tests, and also by creating a new generic side-by-side testing framework.

The secondardy focus will be testing that debugging IP under mdbg is a
pleasant experience.  This will initially be acheived by using the CLR Team's
existing debugging test framework, but in the long run we will move to a
new generic, side-by-side testing framework which can be reused for other 
side-by-side testing areas.

-------------------------------------------------------------------------------
REFERENCES
* IronPython Test Plan              Internal IP SharePoint site
* Feature Specifications            None...
* Development Documents             None...
* Schedule Documents                None...
* CPython pdb module                http://docs.python.org/library/pdb.html
* CPython bdb module                http://docs.python.org/library/bdb.html
* pdb tutorial                      http://onlamp.com/pub/a/python/2005/09/01/debugger.html
* MDbg.exe                          http://msdn.microsoft.com/en-us/library/ms229861.aspx
* Debugging IP in Visual Studio     http://devhawk.net/2008/05/08/Debugging+IronPython+Code+In+Visual+Studio.aspx
* Bug queries                       http://ironpython.codeplex.com/WorkItem/AdvancedList.aspx
                                    and set the component to "Debugging"
* Build drop location               Updated at http://ironpython.codeplex.com/SourceControl/ListDownloadableCommits.aspx
                                    on weekdays
* source file tree                  $/.../dlr/Debugging/ and also $/.../dlr/.../IronPython/*

-------------------------------------------------------------------------------
PERSONNEL
Program Manager:    hpierson
Developer:          dinov
Tester:             dfugate

-------------------------------------------------------------------------------
TESTING SCHEDULE
    
    Phase I - Test Plan and Enabling Existing Tests
        - Write test plan for IronPython Debugging feature
        - Enable any existing CPython test cases for "pdb" and "bdb" in SNAP
        - Enable any existing CPython test cases for Python features supporting 
          "pdb" and "bdb" in SNAP
        
    Phase II - Examine Coverage of Existing Cases and Write High-priority Cases
        - Verify that developer tests for new additions to IP 
          supporting "pdb" and "bdb" modules are sufficient. Write more test
          cases if not
        - Write additional test cases for the "pdb" module if CPython's test cases 
          are insufficient
        - Examine our existing mdbg test (cases) looking for holes. Write new test
          cases if needed
        - Author a few manual sanity test cases for verifying IP can 
          be debugged under the Visual Studio IDE. These should be folded into
          the CodePlex test pass signoff procedure for major releases
          
    Phase III - Design and Implement Side-by-Side Testing Framework
        - Based on the needs of test cases added in the previous phase either:
            1. Design a side-by-side testing infrastructure to be used with testing
               IP's CPython debugging compatibility OR
            2. Look for existing side-by-side testing frameworks we could reuse.
               The pexpect package might be useful here
        - Implement (or add existing) side-by-side testing framework
    
    Phase IV - Implement High-priority Cases
        - Implement test cases for additions to IP which support the
          "pdb" module IF NEEDED
        - Implement test cases for the "pdb" module IF NEEDED
        - Implement test cases for mdbg IF NEEDED
        
    Phase IV - Write Lower Priority Cases
        - Write test cases around stepping into (hosted) Python code from a 
          .NET application
        - Write test cases around stepping into .NET code from an ipy.exe
          process
        - Write performance test cases
                    
    Phase V - Implement Lower Priority Cases
        - Implement test cases around stepping into (hosted) Python code from a 
          .NET application
        - Implement test cases around stepping into .NET code from an ipy.exe
          process
        - Implement performance test cases
    
    Phase VI - Future
        - Develop test cases for the "bdb" module if CPython's test cases are
          insufficient
        - Automate manual sanity Visual Studio debugging tests
        - Migrate existing mdbg tests to the new side-by-side testing framework
        - Low-resource test cases and implementation
        - Reliability test cases and implementation
        - Scalability test cases and implementation
        - Stress test cases and implementation
        - Internationalization test cases and implementation

    Phase P - All Public Releases
        - Verify all user documentation with respect to debugging
        - Setup testing - verify pdb.py and all supporting modules are installed by the MSI
        - Generate code coverage report and verify block coverage as greater
          than 80%

-------------------------------------------------------------------------------
FEATURE HISTORY
2.0:     Tests implemented for IronPython under mdbg. Scenarios which still 
         need to be covered are:
         * Attach scenario with and without the -D option
         * Step into/out when there is no pdb file for IP 
           binaries
         * Multiple statements in one line (e.g., "x = 1;y=2"
         * if x and \\n y: print 1
         * dict/tuple spanning multiple lines
         * break/continue, explicit raise
         * other multi-line statement scenarios such as:
           if True: print 1
           if True:\\n    print 1
         * MdbgInterop
           able to break into the C# code from Python
           able to break back into Python code from C#

2.6B2:   First release of IronPython to include support of CPython's pdb module.
         Only works if -X:Frames or -X:FullFrames option supplied to ipy.exe.
         No pdb-related tests running in SNAP at this point, but we are running
         all of CPython's tests WRT supporting functions in the sys module.
         Debugging test plan completely rewritten in the form of pydoc strings,
         and made public.

-------------------------------------------------------------------------------
FEATURES:
- sys.settrace, sys.gettrace, and sys.call_tracing.  NOTE: while each of these
  three builtin module members needs to be tested within unit tests on their
  own, the real use case here is through the "bdb" and "pdb" modules
- ipy.exe's generation of PDBs
- ipy.exe's PDB visualizers

-------------------------------------------------------------------------------
FILES AND MODULES:

- FILES LIST:
    * everything under Microsoft.Scripting.Debugging, although the DLR 
      officially owns this
    * sys.cs
    * IronPython.Runtime.PythonTracebackListener
- REGISTRY, INI SETTINGS: None
- SETUP PROCEDURES: 
    CPython standard library must be installed and present in sys.path to be 
    usable
- DE-INSTALLATION PROCEDURES
    N/A
- DATABASE SETUP AND PROCEDURES
    N/A
- NETWORK DOMAIN/TOPOLOGIES CONFIGURATION PROCEDURES
    N/A
- PERFORMANCE MONITORING COUNTERS SETUP AND CONFIGURATIONS
    N/A

-------------------------------------------------------------------------------
OPERATIONAL ISSUES
N/A.  This feature of IP is not being monitored/maintained by operational 
staff, and IP is provided on an as-is basis.

-------------------------------------------------------------------------------
SCOPE OF TEST CASES
We'll be getting most of our test coverage on the debugging feature of 
IP by unit testing the CPython 'pdb' module.  Doing this thoroughly
should guarantee we hit nearly all blocks of code supporting debugging in 
IP DLLs. This will be validated and verified through monthly code 
coverage runs.  As an aside, this should in theory hit most of the DLR's
Microsoft.Scripting.Debugging.dll as well.

As IP currently has no tie-in into the Visual Studio IDE, this is 
arguably far less important to test than our support of the 'pdb' module or 
even mdbg.  As a result, the Microsoft toolset testing emphasis will involve
exahaustively covering all aspects of mdbg. That said, some of our users do in
fact have VS Pro installed implying that we should perform at least minimal,
manual sanity tests before every major public release of IP.  Should we
ever provide visualizers for our debug symbols we'll need much more 
comprehensive and automated tests for VS of course.
 
-------------------------------------------------------------------------------
ACCEPTANCE CRITERIA
- no debugging related feature should be checked into the internal source 
  repository without some form of unit test hooked into SNAP checked in as
  well
- we should not publically advertise the existance of any new debugging features
  in an IP release unless:
    - if the feature is CPython-based; the majority of it's corresponding 
      CPython unit test cases have been enabled in SNAP and pass
    - if the feature is novel to IP; block coverage should be greater
      than 70%
    - the feature needs to be documented in some form other than a blog
    - performance of the feature needs to be "within reason"
    - if the feature is not CPython-based; it needs a Pythonic feel to it
    - at least half of all bugs opened on the feature since its inception are
      fixed

-------------------------------------------------------------------------------
KEY FEATURE ISSUES
- no spec on debugging IP from mdbg, VS, or what debugging symbols 
  should be generated for various Python constructs
- insufficient test resources to adequately test this feature
- insufficient resources to implement all debugging features called out in this
  test plan
- existing mdbg tests are based on CLR infrastructure and require a Perl 
  installation. We cannot redistribute this publically
- existing mdbg tests in SNAP are flakey

-------------------------------------------------------------------------------
TEST APPROACH
- DESIGN VALIDATION
    We have no part in the design of the "pdb" and "bdb" standard modules. 
    
    As for debugging under mdbg...there is no current design document on 
    debugging Python sessions.  Generally speaking test will simply ensure that
    whatever mdbg functionality exists with regard to IP is "Pythonic",
    but at the same time familiar to CSharp and VB developers.

- DATA VALIDATION
    pdb/bdb: at a very low-level, all we need to validate is that parameters passed
    to a trace function we set via sys.settrace are as expected.  In particular,
    the first parameter passed in is the current stack frame which we have a 
    very solid chance of getting wrong.

    mdbg/VS: need to validate that generation of PDB symbols "make sense"
    with respect to whatever Python source is being compiled. There are at 
    least two ways to accomplish this:
    1. Create a regression test in which we compare the generated PDB of a 
       known Python script to an expected PDB file.  The Python script should
       exhaustively cover everything that's possible in the Python grammar.
       Testing in this manner is not really maintainable in the long run as we
       expect the PDB generated for a given Python script to change over time
       reflecting optimizations and new features added to IP
    2. Test that the behavior of mdbg, given a known Python script, does not 
       regress.  Again, this Python script should exhaustively cover everything
       that's possible in Python's grammar, but this time around we'll also
       need to cover everything that's possible under mdbg

- API TESTING
    We'll exhaustively cover the entire "pdb" standard module without ever 
    touching the CSharp APIs directly.  Prior coverage runs have shown that 
    (generally speaking) any block of code that is not directly hittable through 
    the Python API is likely dead code.  Also, testing "bdb" module is lower 
    priority as "pdb" uses this for its own implementation.  The final 
    justification for this is that there is actually very little in terms of 
    debugging APIs in IP DLLs.  Most of the debugging support is built
    directly into the DLR's Microsoft.Scripting.Debugging.dll.
    
    For mdbg/VS, we'll exhaustively cover all mdbg commands.
    
- CONTENT TESTING
  All debugging documentation should be thoroughly reviewed by Test before any
  public release of IronPython.  Careful attention will be given to duplicating
  exactly any command fed into ipy.exe or mdbg.exe, and this should be 
  automated entirely if feasible.
  
- LOW-RESOURCE TESTING
  Use of the debugging feature has a few side effects which might be interesting
  from a limited resources perspective.  First and foremost, use of the -X:Frames
  and -X:FullFrames IP console flags imply that IP will consume more 
  memory than under normal circumstances.  We should check that IP does not 
  consume "too much" extra memory.  Next, use of the debugging flag, '-D', in
  conjunction with the '-X:SaveAssemblies' flag will generate 
  Snippets.debug.scripting.pdb in the %TMP% directory. What occurs if the drive
  containing %TMP% is full?

  NOTE: while this is an interesting test area to explore, there are no current
  plans to test it.  We should perform general low-resource testing of 
  IP before tackling it for this specific feature.
  
- SETUP TESTING
  The only option of IP's MSI installer capable of affecting
  the debugging feature is the ability to selectively install CPython's standard
  library.  As we generate the list of CPython modules to include in the MSI
  dynamically, it would be worthwhile to always check that pdb.py is included.
  
  NOTE: IronPython currently has no automated tests for the MSI. Once we
  do, we need to add a check to ensure pdb.py is always included.  For the time
  being this will have to be done manually.
  
- MODES AND RUNTIME OPTIONS
  We should focus our efforts on testing IP with the following modes:
  * -D -X:Frames
  * -D -X:FullFrames
  * -D -X:Frames -X:SaveAssemblies
  * -D
  * -X:Frames
  * 
  using both debug and release IP assemblies.  Also note that this
  needs to be performed for both Python interactive sessions and Python scripts
  passed into ipy.exe (e.g., "ipy.exe test_str.py").  Last but
  not least, there's the %PYTHONDEBUG% environment variable to consider.
  
- INTEROPERABILITY
  Of primary concern is that IP produces an identical experience as
  CPython when given input which utilizes the pdb module. This will be 
  accomplished via side-by-side testing and verification that the output of 
  IP is the same as CPython given an identical input.
  
  We will also verify that IP can be debugged from mdbg and the Visual
  Studio IDE.
  
- INTEGRATION TESTING
  * Silverlight support of debugging?
  * breaking into .NET code from pdb
  * breaking into COM from pdb
  * breaking into IronRuby from pdb
  * breaking into Python code from DLR hosting APIs
  * breaking into Python code from IronRuby

- BETA TESTING
  The second beta release of IronPython 2.6 will support the debugging feature
  in the form of very limited pdb module support with the -X:Frames flag 
  passed to ipy.exe. As with all beta releases of IP, this will be 
  released on CodePlex to the general public. We will then use feedback
  from the IronPython Community to determine the amount of effort that goes 
  into fully testing this feature.
  
- ENVIRONMENT/SYSTEM - GENERAL
  The %PYTHONDEBUG% environment variable may have some impact on this feature.
  Other than this, the VS IDE will of course need to be installed for testing
  the VS debugging experience.
  
- CONFIGURATION
  Need .NET 2.0 Service Pack 1 installed to run IP, and .NET 3.5
  Service Pack 1 to build the feature.
  
- USER INTERFACE
  IP provides no user interfaces.
  
- PERFORMANCE & CAPACITY TESTING
  Minimally, we need to run one or more tests in the perf lab under the -X:Frames
  and -X:FullFrames test modes.  We should also measure the end-to-end run time
  of a complete, yet minimal, debugging sample utilizing the pdb module in the 
  lab.  The focus here will simply be on ensuring there are no perf regressions
  which are greater than 15% for any given checkin, and also that overall 
  performance of the debugging feature remains palatable to our userbase.
  
  In terms of capacity testing we should look at:
  - if perf gets affected when breaking into deeply nested functions (e.g., 
    recursive functions)
  - anything else?
  
- PRIVACY
  Does %TMP%\Snippets.debug.scripting.pdb contain sensitive customer data? If 
  so, does IP disclose the existence of this file?
  Is remote debugging supported? If so, what safeguards are in place?
  
- RELIABILITY
  Most end-users are expected to enter into debugging sessions for short 
  periods of time to diagnose issues with Python code.  From this perspective
  the so-called "up time" of debugging sessions is less important than the 
  overall sporadic failure frequency. We can get a good sense of how often 
  sporadic failures occur by automatically running debugging tests in SNAP 
  for every IP checkin.
  
- SCALABILITY
  Does debugging still work correctly when stepping into deeply nested (e.g., 1000)
  functions?
  
- STRESS TESTING
  - gcstress
  - MDA
  - assemblies in the GAC
  - call pdb functions multiple times and look for memory leaks
  - what happens when stepping into unreasonably deeply nested functions?
  
- VOLUME TESTING
  App building exercise or try utilizing pdb from some of our internal Python 
  tools. It would be interesting to use this from gopackage when exceptions 
  are encountered. 
  
- INTERNATIONAL ISSUES
  As Python is not a localized language and available only in English, we 
  simply need to confirm that nothing gets localized.  This can be done by
  running a simple debugbing test against non-English OSes such as Deutsche
  Vista.
  
- ROBUSTNESS
  We'll need a dedicated test or two in the stress lab to ensure there are no
  memory leaks.  Also, we'll need to keep an eye out for sporadic failures of
  debugging tests in SNAP.
  
- ERROR TESTING
  What happens when invalid commands are fed to the pdb debugger? Is this 
  handled identically to CPython?
  
  Is there any conceivable way to fully break debugging without actually 
  breaking ipy.exe in general?  What happens under the mode 
  "-D -X:SaveAssemblies" when the current user doesn't have write 
  permissions on %TMP%?  What happens if MS.Scripting.Debugging.dll is 
  removed outright?
  
- USABILITY
  There are three major usability issues with debugging:
  * IP offers no visualizer for the VS IDE
  * we're largely incompatible with CPython's pdb module
  * debugging Python code from .NET apps isn't currently possible
  
  Test will depend on feedback from the IronPython Mailing List and blogs to
  determine more usability issues. Resource permitting, we may also do 
  app building exercises.
  
  The usability goal of this feature is that there are no complaints about
  the IP debugging experience from our users.
  
- ACCESSIBILITY
  As this feature provides no new user interfaces to IronPython. and simply 
  emits output to the stdout stream of command prompts, there should be no 
  need to invest into accessibility testing.
  
- USER SCENARIOS
  It's anticipated that users of this feature will be limited almost exclusively to
  developers. Expert Python users will likely prefer using the pdb
  module or debugging their applications directly via the "-i" ipy.exe 
  option.  Existing Microsoft customers with little Python background will 
  probably be more comfortable with the VS IDE debugging experience. Due to 
  this we must make sure both experiences are great!
  
  The types of applications being debugged will likely:
  * be non-trivial
  * consist of many different modules/packages
  * make heavy use of the CPython standard library and/or third party Python
    packages
  * be debugged often during the application development phase
  * be debugged infrequently in a production environment when something goes 
    awry
  
  With these constraints in mind, we should try to debug existing major third
  party Python applications and/or add debugging support to our own internal
  Python tools.
  
- BOUNDARIES AND LIMITS
  What happens when stepping into a recursive function at the maximum recursion
  level?
  
- OPERATIONAL ISSUES
  None
  
- SPECIAL CODE PROFILING AND OTHER METRICS
  Overall block coverage of all assemblies with "ipy" or "IronPython" in their
  names should be above 80% and file coverage should stay above 97%. Visual 
  Studio's mstest will be used to measure code coverage.

-------------------------------------------------------------------------------
TEST ENVIRONMENT

- OPERATING SYSTEMS
  32-bit Windows XP
  32-bit Windows 2003
  64-bit Windows 2003
  32-bit Windows Vista
  64-bit Windows Vista
  32-bit Windows Vista (Deutsche)
- NETWORKS
  General Intranet network connection required for SNAP.
  May be other special network needs if remote debugging is/becomes a supported scenario.
- HARDWARE
  - MACHINES
    At least two machines of every OS variety called out above with at least
    10 gigs of free hard disk space, 2 gigs of RAM, and a modern CPU
- SOFTWARE
  * Visual Studio 2008 Team System Service Pack 1 installed to run the 
    test suite
  * PowerShell 1.0 to run any supporting test scripts
  * CPython for side-by-side tests
  * mdbg

-------------------------------------------------------------------------------
UNIQUE TESTING CONCERNS FOR SPECIFIC FEATURES
It should be relatively easy to drive pdb or mdbg via the command line in an
automated fashion, but the same cannot be said about the VS IDE debugging 
experience.  We'll need to reuse the VS team's automation infrastructure and 
ensure any machines running these tests in SNAP are never locked at the 
username/password screen which would cause the test to fail.  Also, should 
visualization ever be a supported scenario, we may have no other option than to 
test this manually (note - this needs to be investigated).

-------------------------------------------------------------------------------
AREA BREAKDOWN
- CPython's "pdb" module
  See debugging.pdb_mod.

- mdbg tool support
  See debugging.mdbg_tool.
      
- VS IDE
  See debugging.vs.
  
- sys module extensions
  - sys.settrace
    settrace(function) 
    Set the system's trace function, which allows you to implement a Python 
    source code debugger in Python. The function is thread-specific; for a 
    debugger to support multiple threads, it must be registered using 
    settrace() for each thread being debugged.

    Trace functions should have three arguments: frame, event, and arg. frame 
    is the current stack frame. event is a string: 'call', 'line', 'return', 
    'exception', 'c_call', 'c_return', or 'c_exception'. arg depends on the 
    event type.

    The trace function is invoked (with event set to 'call') whenever a new 
    local scope is entered; it should return a reference to a local trace 
    function to be used that scope, or None if the scope shouldn't be traced.

    The local trace function should return a reference to itself (or to another 
    function for further tracing in that scope), or None to turn off tracing 
    in that scope.

    The events have the following meaning:
    'call'
        A function is called (or some other code block entered). The global 
        trace function is called; arg is None; the return value specifies the 
        local trace function.
    'line'
        The interpreter is about to execute a new line of code (sometimes 
        multiple line events on one line exist). The local trace function is 
        called; arg is None; the return value specifies the new local trace 
        function.
    'return'
        A function (or other code block) is about to return. The local trace 
        function is called; arg is the value that will be returned. The trace 
        function's return value is ignored.
    'exception'
        An exception has occurred. The local trace function is called; arg is 
        a tuple (exception, value, traceback); the return value specifies the 
        new local trace function.
    'c_call'
        A C function is about to be called. This may be an extension function 
        or a builtin. arg is the C function object.
    'c_return'
        A C function has returned. arg is None.
    'c_exception'
        A C function has thrown an exception. arg is None.
    
    Note that as an exception is propagated down the chain of callers, an 
    'exception' event is generated at each level.
  
  - sys.gettrace
    Get the trace function as set by settrace().
    
  - sys.call_tracing
    call_tracing(func, args) -> object

    Call func(*args), while tracing is enabled.  The tracing state is
    saved, and restored afterwards.  This is intended to be called from
    a debugger from a checkpoint, to recursively debug some other code.

-------------------------------------------------------------------------------
TEST CASE STRUCTURE
Test cases will be stored directly in the pydoc strings of the functions 
implementing the tests.  In turn, the modules containing these test case 
functions can be found under the "debugging" test package. Generating pydoc for
the "debugging" test package and all sub-modules/sub-packages will generate 
this test plan.

-------------------------------------------------------------------------------
SPEC REVIEW ISSUES
There is no IronPython spec for this feature.  Any issues observed in the pdb 
module, which acts as a spec, and reproducible under CPython should be reported 
to http://bugs.python.org.

-------------------------------------------------------------------------------
TEST TOOLS
One of the following:
- reuse CLR's debugging test infrastructure.  NOTE: this is only suitable for 
  mdbg tests
- write our own new side-by-side testing infrastructure entirely in Python
  so that we can eventually contribute pdb tests back to CPython
- write our own new side-by-side testing infrastructure in any
  language using any framework. http://sourceforge.net/projects/pexpect/ could
  be useful as would be PowerShell
- re-use some internal tool that might not be redistributable on CodePlex

which will minimally need to support the following features:
- ability to ignore lines (matching some regular expression) entirely
- ability to substitute strings in lines
- ability to run arbitrary commands (e.g., not limited to "ipy.exe ..." or "mdbg ...")
- ability to match the current output against a reference output stored on disk
- ability to run two or more arbitrary commands and compare their output against each other

As per usual, the plan is to automate all tests using the SNAP checkin system
and VS's mstest tool.

-------------------------------------------------------------------------------
SMOKE TEST (ACCEPTANCE TEST, BUILD VERIFICATION, ETC.)

pdb acceptance test
    ipy.exe -c "import pdb"

mdbg
    TODO (July 30, 2009)
    
Visual Studio IDE
    TODO (July 30, 2009)


-------------------------------------------------------------------------------
AUTOMATED TESTS
pdb and mdbg tests will be 100% automated in SNAP.


-------------------------------------------------------------------------------
MANUAL TESTS
Setup testing will be manual for now, and we'll eventually move to automated 
testing in SNAP using PowerShell support scripts.

Visual Studio IDE testing is also manual for now.  Sometime in the 
future we'll migrate to using the VS team's automation technologies.

Manual testing will only occur for major IP releases and the 
instructions will be documented on the internal IP website under the 
"Release Process" wiki.


-------------------------------------------------------------------------------
REGRESSION TESTS
Regression tests will be added to existing test modules under the "debugging" 
package before any CodePlex or internal bug dealing with debugging is closed.
As such, the majority of these regressions will be automated and run on every
developer or test checkin.  Developers should preferably add a regression test
for every bug fix, and Test will verify the sufficiency of the test, extending
it if necessary.

-------------------------------------------------------------------------------
BUG BASHES
While considered to be extremely useful, we have no current plans for internal 
bug bashes on this feature.  Quite simply put, we need more IronPython headcount 
and/or interest from the rest of Visual Studio Languages to accomplish this.

-------------------------------------------------------------------------------
BUG REPORTING
All bug reports on this feature which do not include information consided to be
Microsoft confidential are to be filed at 
http://ironpython.codeplex.com/WorkItem/AdvancedList.aspx under the "Debugging"
component using the bug template found at 
http://ironpython.codeplex.com/Wiki/View.aspx?title=IronPython%20Bug%20Template.
Bugs will be triaged on a weekly basis by the entire IronPython Team.

-------------------------------------------------------------------------------
PLAN CONTINGENCIES
Without at least one additional SDET or other test tasks getting dropped, only 
phases I and II of this plan are implementable prior to the release of 
IP 2.6 RTM.

-------------------------------------------------------------------------------
EXTERNAL DEPENDENCIES
We depend on the DLR for their implementation of Microsoft.Scripting.Debugging.dll 
which forms the basis of our support of the CPython pdb module.  This plan 
assumes that this DLL has been adequately tested.

There are no teams/projects we know about which are taking a dependency on 
IP's debugging functionality.

-------------------------------------------------------------------------------
HEADCOUNT REQUIREMENTS
At least two FTE IronPython SDETs will be needed to implement this test plan 
fully: one to handle general, day-to-day IP test operations
(i.e., passes/investigations/issues/regressions, etc), and another to work exclusively on the 
debugging feature.  A loose approximation is that the SDET working full-time
on debugging would need anywhere from one to three months to completely 
implement all aspects of this plan.  This estimate varies considerably 
depending upon how much emphasis is to be given to pdb versus mdbg versus the 
VS IDE.  Having a second full-time SDET on the debugging feature would have a
not positive effect as the division of labor between testing command-line IP
debugging support and VS IDE support is quite clear.

-------------------------------------------------------------------------------
PRODUCT SUPPORT
IP is not a supported product in the conventional Microsoft sense. 
Support for IP is freely given directly by the IP Team via the 
IronPython Mailing List.

-------------------------------------------------------------------------------
DROP PROCEDURES
All developer checkins must go through the SNAP checkin system => IronPython
sources are always in a "good" state.  Test will build IP from sources
using developer instructions.

-------------------------------------------------------------------------------
RELEASE PROCEDURES
See the IP "Release Process" wiki on the internal IP website.

-------------------------------------------------------------------------------
ALIAS/NEWSGROUPS AND COMMUNICATION CHANNELS
Major changes around IP debugging support should be announced on the 
IronPython Mailing List, users@lists.ironpython.com.

-------------------------------------------------------------------------------
REGULAR MEETINGS
- FEATURE TEAM MEETINGS
  There is no feature team for debugging.
- PROJECT TEST TEAM MEETINGS
  IronPython weekly team meeting. Day and time are subject to change.
  We'll triage debugging work items during this meeting and discuss IP 
  debugging issues as necessary.
- FEATURE TEAM TEST MEETINGS
  There is no feature test team for debugging.

-------------------------------------------------------------------------------
DECISION MAKING PROCEDURES
Decisions on this feature will be driven by the following criteria:
- Microsoft business needs
- IronPython Community feature requests
- Previous decisions made by CPython. E.g., the pdb module
- Existing debugging support in Microsoft products such as VS
In the event a conscensus cannot be reached by the team on something, Jim
Hugunin's input should be sought.

Miscellaneous Procedures:
- the gopackage tool is responsible for validating all source pushes to 
  CodePlex
- at least two IP team members need to sanity check public builds
  before they can be released
- full test pass signoff on this feature will be needed for every public, signed
  release
- bug triages will be performed with all three software disciplines in attendance
- development design should be reviewed both by the PM and Test disciplines

-------------------------------------------------------------------------------
NOTES

MDBG TEST CASE WRITING TIPS
- use different language structures as the last line in the Python file
- when only one line lives inside a block of code, but the code should not be
  stepped through
- correct step-through: no extra step-throughs or lack thereof
- 
'''