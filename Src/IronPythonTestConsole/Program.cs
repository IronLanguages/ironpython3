using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnitLite.Runner;

namespace IronPythonTest.Desktop {
    class Program {
        // The main program executes the tests. Output may be routed to
        // various locations, depending on the arguments passed.
        //
        // Arguments:
        //
        //  Arguments may be names of assemblies or options prefixed with '/'
        //  or '-'. Normally, no assemblies are passed and the calling
        //  assembly (the one containing this Main) is used. The following
        //  options are accepted:
        //
        //    -test:<testname>  Provides the name of a test to be exected.
        //                      May be repeated. If this option is not used,
        //                      all tests are run.
        //
        //    -out:PATH         Path to a file to which output is written.
        //                      If omitted, Console is used, which means the
        //                      output is lost on a platform with no Console.
        //
        //    -full             Print full report of all tests.
        //
        //    -result:PATH      Path to a file to which the XML test result is written.
        //
        //    -explore[:Path]   If specified, list tests rather than executing them. If a
        //                      path is given, an XML file representing the tests is written
        //                      to that location. If not, output is written to tests.xml.
        //
        //    -noheader,noh     Suppress display of the initial message.
        //
        //    -wait             Wait for a keypress before exiting.
        //
        //    -include:categorylist 
        //             If specified, nunitlite will only run the tests with a category 
        //             that is in the comma separated list of category names. 
        //             Example usage: -include:category1,category2 this command can be used
        //             in combination with the -exclude option also note that exlude takes priority
        //             over all includes.
        //
        //    -exclude:categorylist 
        //             If specified, nunitlite will not run any of the tests with a category 
        //             that is in the comma separated list of category names. 
        //             Example usage: -exclude:category1,category2 this command can be used
        //             in combination with the -include option also note that exclude takes priority
        //             over all includes
        static void Main(string[] args) {
            new TextUI().Execute(args);
        }
    }
}
