# Feature Symbols

Feature Symbols (named FEATURE_{feature name}, all caps) are compilation symbols defined for features whose availability vary across platforms that IronPython supports. The symbols are defined in Build/{framework}.props file, which get included by all .csproj files that contribute to IronPython.

**The following list needs a major update**

The following symbols are currently used:
### FEATURE_ANSICP
System.Globalization.TextInfo.ANSICodePage

### FEATURE_APARTMENTSTATE
System.Threading.ApartmentState

### FEATURE_APPLICATIONEXCEPTION
System.ApplicationException

### FEATURE_ASSEMBLY_CODEBASE
System.Reflection.Assembly.CodeBase

### FEATURE_ASSEMBLY_LOCATION
System.Reflection.Assembly.Location

### FEATURE_ASSEMBLY_RESOLVE
Runtime assembly resolution (System.AppDomain.AssemblyResolve event).

### FEATURE_ASSEMBLYBUILDER_DEFINEDYNAMICASSEMBLY
System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly

### FEATURE_ASYNC
(currently unused)

### FEATURE_BASIC_CONSOLE
Basic Console features like Console.WriteLine, Console.ReadLine.  

### FEATURE_CODEDOM
System.CodeDom

### FEATURE_COM

### FEATURE_CONFIGURATION
Configuration files (System.Configuration).

### FEATURE_CUSTOM_MODIFIERS
Reflection of required and optional custom modifiers.

### FEATURE_CUSTOM_TYPE_DESCRIPTOR
System.ComponentModel.ICustomTypeDescriptor interface.

### FEATURE_DBNULL
System.DBNull type.

### FEATURE_DRIVENOTFOUNDEXCEPTION
System.IO.DriveNotFoundException

### FEATURE_DYNAMIC_EXPRESSION_VISITOR
System.Linq.Expressions.DynamicExpressionVisitor

### FEATURE_EXCEPTION_STATE
System.Threading.ThreadAbortException.ExceptionState

### FEATURE_FILESYSTEM
Full file system (Directory, File, Path, FileStream, etc.)

### FEATURE_FULL_CONSOLE
Full Console APIs including stdin, stdout, stderr streams, colors, etc. 

### FEATURE_FULL_CRYPTO

### FEATURE_FULL_NET

### FEATURE_ICLONEABLE
System.ICloneable

### FEATURE_IPV6
System.Net.Sockets.SocketOptionName.IPv6Only

### FEATURE_LCG

### FEATURE_LOADWITHPARTIALNAME
System.Reflection.Assembly.LoadWithPartialName

### FEATURE_METADATA_READER
DLR metadata reader available.

### FEATURE_MMAP
System.IO.MemoryMappedFiles

### FEATURE_NATIVE
Native code interop: P/Invokes, CTypes, etc.

### FEATURE_OS_SERVICEPACK
System.OperatingSystem.ServicePack

### FEATURE_PDBEMIT
Ability to emit PDB files. 

### FEATURE_PROCESS
Processes, AppDomains, process-wide environment variables.

### FEATURE_READONLY_COLLECTION_INTERFACE
System.Collections.Generic.IReadOnlyList

### FEATURE_READONLY_DICTIONARY
System.Collections.ObjectModel.ReadOnlyDictionary

### FEATURE_REFEMIT
Reflection.Emit.

### FEATURE_REGISTRY

### FEATURE_REMOTING
Remoting (MarshalByRefObject).

### FEATURE_SECURITY_RULES
System.Security.SecurityRuleSet and related (e.g. System.Security.SecurityRulesAttribute)

### FEATURE_SERIALIZATION
Serialization - Serializable attribute, ISerializable interface.

### FEATURE_SORTKEY
System.Globalization.SortKey

### FEATURE_STACK_TRACE
System.Diagnostics.StackTrace, System.Diagnostics.StackFrame.

### FEATURE_SYNC_SOCKETS
System.Net.Sockets.

### FEATURE_THREAD
Threads, ThreadAbortException.

### FEATURE_TYPE_EQUIVALENCE
System.Type.IsEquivalentTo

### FEATURE_TYPE_INFO
System.Reflection.TypeInfo

### FEATURE_TYPECONVERTER
System.ComponentModel.TypeConverter and TypeConverterAttribute types.

### FEATURE_VARIANCE
Covariance and contravariance of generic interface and delegate parameters.

### FEATURE_WARNING_EXCEPTION
System.ComponentModel.WarningException

### FEATURE_WIN32EXCEPTION
System.ComponentModel.Win32Exception

### FEATURE_WPF

### FEATURE_XMLDOC
XML documentation available at runtime.
