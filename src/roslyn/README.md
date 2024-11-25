# `src/roslyn`

This directory contains the IronPython Roslyn analyzer which allows for rich language analysis in using IronPython.

- `IPY01`: Non-nullable reference type parameters should have the `NotNoneAttribute`.
- `IPY02`: Nullable reference type parameters should not have the `NotNoneAttribute`.
- `IPY03`: `BytesLikeAttribute` is only allowed on parameters of type `IReadOnlyList<byte>`, or `IList<byte>`.
- `IPY04`: To obtain a name of a python type of a given object to display to a user, use `PythonOps.GetPythonTypeName`.
- `IPY05`: `NotNullAttribute` is ambiguous between `System.Diagnostics.CodeAnalysis.NotNullAttribute` and `Microsoft.Scripting.Runtime.NotNullAttribute`. The latter should be accesses as `NotNoneAttribute`.
