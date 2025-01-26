# Feature Symbols

Feature Symbols (named FEATURE_{feature name}, all caps) are compilation symbols defined for features whose availability vary across platforms that IronPython supports. The symbols are defined in Build/{framework}.props file, which get included by all .csproj files that contribute to IronPython.

**The following list needs a major update**

The following symbols are currently used:
### FEATURE_APARTMENTSTATE
System.Threading.ApartmentState

### FEATURE_ASSEMBLY_GETFORWARDEDTYPES
System.Reflection.Assembly.GetForwardedTypes

### FEATURE_ASSEMBLY_RESOLVE
Runtime assembly resolution (System.AppDomain.AssemblyResolve event).

### FEATURE_ASSEMBLYBUILDER_DEFINEDYNAMICASSEMBLY
System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly

### FEATURE_ASSEMBLYBUILDER_SAVE
System.Reflection.Emit.AssemblyBuilder.Save

### FEATURE_CODEDOM
System.CodeDom

### FEATURE_COM

### FEATURE_CONFIGURATION
Configuration files (System.Configuration).

### FEATURE_CTYPES

### FEATURE_CUSTOM_TYPE_DESCRIPTOR
System.ComponentModel.ICustomTypeDescriptor interface.

### FEATURE_EXCEPTION_STATE
System.Threading.ThreadAbortException.ExceptionState

### FEATURE_FILESYSTEM
Full file system (Directory, File, Path, FileStream, etc.)

### FEATURE_FULL_CRYPTO

### FEATURE_FULL_NET

### FEATURE_LAMBDAEXPRESSION_COMPILETOMETHOD
System.Linq.Expressions.LambdaExpression.CompileToMethod

### FEATURE_LCG

### FEATURE_LOADWITHPARTIALNAME
System.Reflection.Assembly.LoadWithPartialName

### FEATURE_METADATA_READER
DLR metadata reader available.

### FEATURE_MMAP
System.IO.MemoryMappedFiles

### FEATURE_NATIVE
Native code interop: P/Invokes, CTypes, etc.

### FEATURE_OSPLATFORMATTRIBUTE
System.Runtime.Versioning.OSPlatformAttribute

### FEATURE_PDBEMIT
Ability to emit PDB files.

### FEATURE_PIPES

### FEATURE_PROCESS
Processes, AppDomains, process-wide environment variables.

### FEATURE_REFEMIT
Reflection.Emit.

### FEATURE_REFEMIT_FULL

### FEATURE_REGISTRY

### FEATURE_REMOTING
Remoting (MarshalByRefObject).

### FEATURE_RUNTIMEINFORMATION
System.Runtime.InteropServices.RuntimeInformation

### FEATURE_SECURITY_RULES
System.Security.SecurityRuleSet and related (e.g. System.Security.SecurityRulesAttribute)

### FEATURE_SERIALIZATION
Serialization - Serializable attribute, ISerializable interface.

### FEATURE_STACK_TRACE
System.Diagnostics.StackTrace, System.Diagnostics.StackFrame.

### FEATURE_SYNC_SOCKETS
System.Net.Sockets.

### FEATURE_THREAD
Threads, ThreadAbortException.

### FEATURE_TYPE_EQUIVALENCE
System.Type.IsEquivalentTo

### FEATURE_TYPECONVERTER
System.ComponentModel.TypeConverter and TypeConverterAttribute types.

### FEATURE_WPF

### FEATURE_XMLDOC
XML documentation available at runtime.
