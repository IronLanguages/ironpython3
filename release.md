IronPython 3.4.0-alpha1 Released
================================

On behalf of the IronPython team, I'm very happy to announce the release of [IronPython 3.4.0-alpha1](https://github.com/IronLanguages/ironpython3/releases/tag/v3.4.0-alpha1). The runtime targets are .NET Framework 4.6, .NET Core 2.1, .NET Core 3.1 and .NET 5. The baseline for this release is Python 3.4.

Huge thanks to @BCSharp, @slide and other contributors @gpetrou, @jdhardy, @paweljasinski, @gfmcknight, @jameslan, @moto-timo, @rtzoeller, @in-code-i-trust, @hackf5, @dc366, @simplicbe, @AlexKubiesa, @isaiah, @ivanbakel, @syn2083, @komodo472, @yuhan0, @michaelblyons, @simonwyatt, @alanmbarr, @ShahneRodgers.

Upgrading from IronPython 2 to 3
--------------------------------

IronPython 3.4 uses Python 3.4 syntax and standard libraries and so your Python code will need to be updated accordingly. There are numerous tools and guides available on the web to help porting from Python 2 to 3.

In an effort to improve compatibility, `sys.platform` no longer returns `cli`. If you wish to check if you're running on IronPython the recommended pattern is to check that `sys.implementation.name` is equal to `ironpython`.

Notable differences with Python 3.4
-----------------------------------

The differences below should be treated as implementations detail and may change in the future.

- `int` and `long` types are still separate types (mapped to `System.Int32` and `System.Numerics.BigInteger` respectively). This may cause issues when using `type` to check if a variable is an `int` (e.g. `type(1<<64) != type(int)`). In this case the recommended mitigation is to use `isinstance` instead (e.g. `isinstance(1<<64, int)`). Tracked at [IronLanguages/ironpython3#52](https://github.com/IronLanguages/ironpython3/issues/52).
- `str` (mapped to `System.String`) are UTF-16. Because of this, IronPython string will behave like pre-Python 3.3 strings (e.g. `len("\U00010000") == 2`). Tracked at [IronLanguages/ironpython3#252](https://github.com/IronLanguages/ironpython3/issues/252)
