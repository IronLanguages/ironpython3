using IronPython.Runtime;

[assembly: PythonModule("fcntl", typeof(IronPython.Modules.PythonFcntl), PythonModuleAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules {
    public static class PythonFcntl {
    }
}