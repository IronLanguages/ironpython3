# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Ensures we can import from .NET 2.0 namespaces and types
'''

import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, run_test, skipUnlessIronPython

if is_cli and not is_netcoreapp:
    import clr
    clr.AddReference("System.Configuration")
    clr.AddReference("System.Configuration.Install")
    clr.AddReference("System.Data")
    clr.AddReference("System.Data.OracleClient")
    if is_mono:
        clr.AddReference("System.Data.SqlClient")
    else:
        clr.AddReference("System.Data.SqlXml")
    clr.AddReference("System.Deployment")
    clr.AddReference("System.Design")
    clr.AddReference("System.DirectoryServices")
    clr.AddReference("System.DirectoryServices.Protocols")
    clr.AddReference("System.Drawing.Design")
    clr.AddReference("System.Drawing")
    clr.AddReference("System.EnterpriseServices")
    clr.AddReference("System.Management")
    clr.AddReference("System.Messaging")
    clr.AddReference("System.Runtime.Remoting")
    clr.AddReference("System.Runtime.Serialization.Formatters.Soap")
    clr.AddReference("System.Security")
    clr.AddReference("System.ServiceProcess")
    clr.AddReference("System.Transactions")
    clr.AddReference("System.Web")
    clr.AddReference("System.Web.Mobile")
    clr.AddReference("System.Web.RegularExpressions")
    clr.AddReference("System.Web.Services")
    clr.AddReference("System.Windows.Forms")
    clr.AddReference("System.Xml")

    from System import *
    from System.CodeDom import *
    from System.CodeDom.Compiler import *
    from System.Collections import *
    from System.Collections.Generic import *
    from System.Collections.ObjectModel import *
    from System.Collections.Specialized import *
    from System.ComponentModel import *
    from System.ComponentModel.Design import *
    from System.ComponentModel.Design.Data import *
    from System.ComponentModel.Design.Serialization import *
    from System.Configuration import *
    from System.Configuration.Assemblies import *
    from System.Configuration.Install import *
    from System.Configuration.Internal import *
    from System.Configuration.Provider import *
    from System.Data import *
    from System.Data.Common import *
    from System.Data.Design import *
    from System.Data.Odbc import *
    from System.Data.OleDb import *
    from System.Data.OracleClient import *
    from System.Data.Sql import *
    from System.Data.SqlClient import *
    from System.Data.SqlTypes import *
    if not is_mono:
        from System.Deployment.Application import *
        from System.Deployment.Internal import *
    from System.Diagnostics import *
    from System.Diagnostics.CodeAnalysis import *
    from System.Diagnostics.Design import *
    from System.Diagnostics.SymbolStore import *
    from System.DirectoryServices import *
    from System.DirectoryServices.ActiveDirectory import *
    from System.DirectoryServices.Protocols import *
    from System.Drawing import *
    from System.Drawing.Design import *
    from System.Drawing.Drawing2D import *
    from System.Drawing.Imaging import *
    from System.Drawing.Printing import *
    from System.Drawing.Text import *
    from System.EnterpriseServices import *
    from System.EnterpriseServices.CompensatingResourceManager import *
    from System.EnterpriseServices.Internal import *
    from System.Globalization import *
    from System.IO import *
    from System.IO.Compression import *
    from System.IO.IsolatedStorage import *
    from System.IO.Ports import *
    from System.Management import *
    from System.Management.Instrumentation import *
    from System.Media import *
    from System.Messaging import *
    from System.Messaging.Design import *
    from System.Net import *
    from System.Net.Cache import *
    from System.Net.Configuration import *
    from System.Net.Mail import *
    from System.Net.Mime import *
    from System.Net.NetworkInformation import *
    from System.Net.Security import *
    from System.Net.Sockets import *
    from System.Reflection import *
    from System.Reflection.Emit import *
    from System.Resources import *
    from System.Resources.Tools import *
    from System.Runtime import *
    from System.Runtime.CompilerServices import *
    from System.Runtime.ConstrainedExecution import *
    from System.Runtime.Hosting import *
    from System.Runtime.InteropServices import *
    from System.Runtime.InteropServices.ComTypes import *
    from System.Runtime.InteropServices.Expando import *
    from System.Runtime.Remoting import *
    from System.Runtime.Remoting.Activation import *
    from System.Runtime.Remoting.Channels import *
    from System.Runtime.Remoting.Channels.Http import *
    from System.Runtime.Remoting.Channels.Ipc import *
    from System.Runtime.Remoting.Channels.Tcp import *
    from System.Runtime.Remoting.Contexts import *
    from System.Runtime.Remoting.Lifetime import *
    from System.Runtime.Remoting.Messaging import *
    from System.Runtime.Remoting.Metadata import *
    from System.Runtime.Remoting.Metadata.W3cXsd2001 import *
    from System.Runtime.Remoting.MetadataServices import *
    from System.Runtime.Remoting.Proxies import *
    from System.Runtime.Remoting.Services import *
    from System.Runtime.Serialization import *
    from System.Runtime.Serialization.Formatters import *
    from System.Runtime.Serialization.Formatters.Binary import *
    from System.Runtime.Serialization.Formatters.Soap import *
    from System.Runtime.Versioning import *
    from System.Security import *
    from System.Security.AccessControl import *
    from System.Security.Authentication import *
    from System.Security.Cryptography import *
    from System.Security.Cryptography.Pkcs import *
    from System.Security.Cryptography.X509Certificates import *
    from System.Security.Cryptography.Xml import *
    from System.Security.Permissions import *
    from System.Security.Policy import *
    from System.Security.Principal import *
    from System.ServiceProcess import *
    from System.ServiceProcess.Design import *
    from System.Text import *
    from System.Text.RegularExpressions import *
    from System.Threading import *
    from System.Timers import *
    from System.Transactions import *
    from System.Transactions.Configuration import *
    from System.Web import *
    from System.Web.Caching import *
    from System.Web.Compilation import *
    from System.Web.Configuration import *
    from System.Web.Configuration.Internal import *
    from System.Web.Handlers import *
    from System.Web.Hosting import *
    from System.Web.Mail import *
    from System.Web.Management import *
    if not is_mono:
        from System.Web.Mobile import *
        from System.Web.RegularExpressions import *
    from System.Web.Profile import *
    from System.Web.Security import *
    from System.Web.Services import *
    from System.Web.Services.Configuration import *
    from System.Web.Services.Description import *
    from System.Web.Services.Discovery import *
    from System.Web.Services.Protocols import *
    from System.Web.SessionState import *
    from System.Web.UI import *
    from System.Web.UI.Adapters import *
    from System.Web.UI.Design import *
    if not is_mono:
        from System.Web.UI.Design.MobileControls import *
        from System.Web.UI.Design.MobileControls.Converters import *
        from System.Web.UI.MobileControls import *
        from System.Web.UI.MobileControls.Adapters import *
        from System.Web.UI.MobileControls.Adapters.XhtmlAdapters import *
        from System.Web.UI.Design.WebControls.WebParts import *
    from System.Web.UI.Design.WebControls import *
    from System.Web.UI.HtmlControls import *
    from System.Web.UI.WebControls import *
    from System.Web.UI.WebControls.Adapters import *
    from System.Web.UI.WebControls.WebParts import *
    from System.Web.Util import *
    from System.Windows.Forms import *
    from System.Windows.Forms.ComponentModel.Com2Interop import *
    from System.Windows.Forms.Design import *
    from System.Windows.Forms.Design.Behavior import *
    from System.Windows.Forms.Layout import *
    from System.Windows.Forms.PropertyGridInternal import *
    from System.Windows.Forms.VisualStyles import *
    from System.Xml import *
    from System.Xml.Schema import *
    from System.Xml.Serialization import *
    from System.Xml.Serialization.Advanced import *
    from System.Xml.Serialization.Configuration import *
    from System.Xml.XPath import *
    from System.Xml.Xsl import *
    from System.Xml.Xsl.Runtime import *

broken_types = []

def deep_dive(in_name, in_type):
    if is_cli:
        import System
    stuff_list = dir(in_type)
    
    for member in stuff_list:
        member_type = eval("type(%s.%s)" % (in_name, member))
        member_fullname = in_name + "." + member
        if (member_type in [type, type(System)]) and (member not in ["__class__"]):
            if member_fullname in broken_types:
                print "SKIPPING", member_fullname
                continue
                        
            net_type = Type.GetType(member_fullname)
            #We can only import * from static classes.
            if not net_type or (not (net_type.IsAbstract and net_type.IsSealed) and not net_type.IsEnum):
                continue
                
            print member_fullname
            exec "from " + member_fullname + " import *"
            
            
            deep_dive(member_fullname, member_type)


@unittest.skipIf(is_netcoreapp, 'references are different')
@skipUnlessIronPython()
class SystemNamespacesTest(IronPythonTestCase):
    def test_system_deep(self):
        import System
        deep_dive("System", System)
    
run_test(__name__)