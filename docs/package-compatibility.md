# Package compatibility

Note that IronPython does not currently support packages which are C extension modules.

## numpy

:warning: Not supported - C extension module.

## pandas

:warning: Not supported - requires `numpy`.

## pytest (4.6.11)

To install:
```
ipy -m pip install pytest==4.6.11 typing==3.10.0.0
```

The package fails with (probably the same issue as https://github.com/IronLanguages/ironpython3/issues/1212):
```
SystemError: Cannot access a closed file.
```

Edit `_pytest/capture.py` and replace:
```py
with f:
    tmpfile = safe_text_dupfile(f, mode="wb+")
```
with:
```py
tmpfile = EncodedFile(f, getattr(f, "encoding", "utf-8"))
```

## requests (2.21.0)

To install:
```
ipy -m pip install requests==2.21.0
```

## sympy (1.4)

To install:
```
ipy -m pip install sympy==1.4
```

The above will also automatically install `mpmath` 1.2.1 if not yet installed.

`sympy` comes with an interactive console, which can be started with:
```
ipy -m isympy
```
