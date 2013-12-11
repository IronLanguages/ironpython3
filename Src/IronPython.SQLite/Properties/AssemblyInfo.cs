using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("IronPython.SQLite")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration(BuildInfo.Configuration)]
[assembly: AssemblyProduct("IronPython")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("225ca84b-ef0f-409e-a3d4-42ab1fd899cc")]

#if FEATURE_APTCA
[assembly: AllowPartiallyTrustedCallers]
#endif

#if FEATURE_SECURITY_RULES
[assembly: SecurityRules(SecurityRuleSet.Level1)]
#endif
