# TDD

Bug fixes should be accompanied by a test that shows that the bug has been fixed. If the bug fix is fixing something that is covered by a test in the C Python test suite (Src\StdLib\Lib\test) and that test is not currently enabled, try enabling the test in Src\IronPythonTest\Cases\*.ini depending on the type of test it is. 

Most PR's will not be accepted if there is not a test included.

# Coding conventions

 * We have a .editorconfig file with the coding conventions used in the project. Please use an editor that honors these settings.

 * Use [.NET Framework conventions for all identifiers](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines).
   * There is no specific guideline for naming private fields in this document; we prefix field names with underscores (e.g. <code>private string _fooBar;</code>) so that use of the fields is easily distinguishable as a field access as opposed to a local variable access. 
   * If you're not sure about some convention, try to find out in the rest of the IronPython code or ask in the list.
 * Use `/*!*/` for method parameters and instance fields that should never be null. [Spec# annotations](http://research.microsoft.com/specsharp).
 * Do not use public fields (Base::algorithm, buffer). Use properties if it is necessary to expose the field or private/internal visibility otherwise.
 * Use `readonly` if the field is not mutated after the object is constructed.
 * Auto properties are to be used when possible instead of private fields with wrapping properties
 * String interpolation should use used instead of calls to `String.Format`

# Validating the changes

The following commands will build and run all the tests that are required to pass. If you get any failures, do report them to the mailing-list to see if they are expected or not. This command can usually run without any failures.

```
./make.ps1 test-all
```