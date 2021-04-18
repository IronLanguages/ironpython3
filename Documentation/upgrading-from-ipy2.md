# Upgrading from IronPython 2 to 3

IronPython 3.4 uses Python 3.4 syntax and standard libraries and so your Python code will need to be updated accordingly. There are numerous tools and guides available on the web to help porting from Python 2 to 3.

## Binary Compatibility

The IronPython 3 binaries are not compatible with the IronPython 2 binaries. Modules compiled with `clr.CompileModules` using IronPython 2 are not compatible and will need to be recompiled using IronPython 3.

## Checking for IronPython

In an effort to improve compatibility, `sys.platform` no longer returns `cli`. If you wish to check if you're running on IronPython the recommended pattern is to check that `sys.implementation.name` is equal to `ironpython`:

```Python
if sys.implementation.name == "ironpython":
    print("IronPython!")
```
