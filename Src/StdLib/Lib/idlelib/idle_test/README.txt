README FOR IDLE TESTS IN IDLELIB.IDLE_TEST

0. Quick Start

Automated unit tests were added in 3.3 for Python 3.x.
To run the tests from a command line:

python -m test.test_idle

Human-mediated tests were added later in 3.4.

python -m idlelib.idle_test.htest


1. Test Files

The idle directory, idlelib, has over 60 xyz.py files. The idle_test
subdirectory should contain a test_xyz.py for each, where 'xyz' is
lowercased even if xyz.py is not. Here is a possible template, with the
blanks after '.' and 'as', and before and after '_' to be filled in.

import unittest
from test.support import requires
import idlelib. as

class _Test(unittest.TestCase):

    def test_(self):

if __name__ == '__main__':
    unittest.main(verbosity=2)

Add the following at the end of xyy.py, with the appropriate name added
after 'test_'. Some files already have something like this for htest.
If so, insert the import and unittest.main lines before the htest lines.

if __name__ == "__main__":
    import unittest
    unittest.main('idlelib.idle_test.test_', verbosity=2, exit=False)



2. GUI Tests

When run as part of the Python test suite, Idle GUI tests need to run
test.support.requires('gui').  A test is a GUI test if it creates a
tkinter.Tk root or master object either directly or indirectly by
instantiating a tkinter or idle class.  GUI tests cannot run in test
processes that either have no graphical environment available or are not
allowed to use it.

To guard a module consisting entirely of GUI tests, start with

from test.support import requires
requires('gui')

To guard a test class, put "requires('gui')" in its setUpClass function.

To avoid interfering with other GUI tests, all GUI objects must be destroyed and
deleted by the end of the test.  The Tk root created in a setUpX function should
be destroyed in the corresponding tearDownX and the module or class attribute
deleted.  Others widgets should descend from the single root and the attributes
deleted BEFORE root is destroyed.  See https://bugs.python.org/issue20567.

    @classmethod
    def setUpClass(cls):
        requires('gui')
        cls.root = tk.Tk()
        cls.text = tk.Text(root)

    @classmethod
    def tearDownClass(cls):
        del cls.text
        cls.root.update_idletasks()
        cls.root.destroy()
        del cls.root

The update_idletasks call is sometimes needed to prevent the following warning
either when running a test alone or as part of the test suite (#27196).
  can't invoke "event" command: application has been destroyed
  ...
  "ttk::ThemeChanged"

Requires('gui') causes the test(s) it guards to be skipped if any of
these conditions are met:

 - The tests are being run by regrtest.py, and it was started without
   enabling the "gui" resource with the "-u" command line option.

 - The tests are being run on Windows by a service that is not allowed
   to interact with the graphical environment.

 - The tests are being run on Linux and X Windows is not available.

 - The tests are being run on Mac OSX in a process that cannot make a
   window manager connection.

 - tkinter.Tk cannot be successfully instantiated for some reason.

 - test.support.use_resources has been set by something other than
   regrtest.py and does not contain "gui".

Tests of non-GUI operations should avoid creating tk widgets. Incidental
uses of tk variables and messageboxes can be replaced by the mock
classes in idle_test/mock_tk.py. The mock text handles some uses of the
tk Text widget.


3. Running Unit Tests

Assume that xyz.py and test_xyz.py both end with a unittest.main() call.
Running either from an Idle editor runs all tests in the test_xyz file
with the version of Python running Idle.  Test output appears in the
Shell window.  The 'verbosity=2' option lists all test methods in the
file, which is appropriate when developing tests. The 'exit=False'
option is needed in xyx.py files when an htest follows.

The following command lines also run all test methods, including
GUI tests, in test_xyz.py. (Both '-m idlelib' and '-m idlelib.idle'
start Idle and so cannot run tests.)

python -m idlelib.xyz
python -m idlelib.idle_test.test_xyz

The following runs all idle_test/test_*.py tests interactively.

>>> import unittest
>>> unittest.main('idlelib.idle_test', verbosity=2)

The following run all Idle tests at a command line.  Option '-v' is the
same as 'verbosity=2'.

python -m unittest -v idlelib.idle_test
python -m test -v -ugui test_idle
python -m test.test_idle

The idle tests are 'discovered' by
idlelib.idle_test.__init__.load_tests, which is also imported into
test.test_idle. Normally, neither file should be changed when working on
individual test modules. The third command runs unittest indirectly
through regrtest. The same happens when the entire test suite is run
with 'python -m test'. So that command must work for buildbots to stay
green. Idle tests must not disturb the environment in a way that makes
other tests fail (issue 18081).

To run an individual Testcase or test method, extend the dotted name
given to unittest on the command line.

python -m unittest -v idlelib.idle_test.test_xyz.Test_case.test_meth


4. Human-mediated Tests

Human-mediated tests are widget tests that cannot be automated but need
human verification. They are contained in idlelib/idle_test/htest.py,
which has instructions.  (Some modules need an auxiliary function,
identified with "# htest # on the header line.)  The set is about
complete, though some tests need improvement. To run all htests, run the
htest file from an editor or from the command line with:

python -m idlelib.idle_test.htest
