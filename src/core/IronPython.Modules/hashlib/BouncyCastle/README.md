# Bouncy Castle digests

The files in this folder implement the SHA-3, SHAKE and BLAKE2 hash algorithms used by the `_sha3` and `_blake2` modules. They are copied from [Bouncy Castle C#](https://github.com/bcgit/bc-csharp) and are covered by its MIT license, see [LICENSE.md](LICENSE.md).

## Provenance

Copied from commit [`4ccf1e6`](https://github.com/bcgit/bc-csharp/tree/4ccf1e6d2785aa8c3745723c21a4aa027eace7df).

| File | Upstream path |
|---|---|
| `Blake2bDigest.cs` | `crypto/src/crypto/digests/Blake2bDigest.cs` |
| `Blake2b_X86.cs` | `crypto/src/crypto/digests/Blake2b_X86.cs` |
| `Blake2sDigest.cs` | `crypto/src/crypto/digests/Blake2sDigest.cs` |
| `Blake2s_X86.cs` | `crypto/src/crypto/digests/Blake2s_X86.cs` |
| `IDigest.cs` | `crypto/src/crypto/IDigest.cs` |
| `IMemoable.cs` | `crypto/src/util/IMemoable.cs` |
| `IXof.cs` | `crypto/src/crypto/IXof.cs` |
| `KeccakDigest.cs` | `crypto/src/crypto/digests/KeccakDigest.cs` |
| `SHA3Digest.cs` | `crypto/src/crypto/digests/SHA3Digest.cs` |
| `ShakeDigest.cs` | `crypto/src/crypto/digests/ShakeDigest.cs` |
| `LICENSE.md` | `LICENSE.md` |

### Local changes

The copied files are unmodified, except that every top-level `public` type (class or interface) is changed to `internal`.

The copied files depend on a few Bouncy Castle utility classes which are implemented in [`../BouncyCastleSupport.cs`](../BouncyCastleSupport.cs) (outside this folder so the repository style is enforced).
