# Package compatibility

## pytest (4.6.11)

To install:
```
ipy -m pip install pytest==4.6.11
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
