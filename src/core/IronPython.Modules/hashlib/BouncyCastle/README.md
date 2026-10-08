# Bouncy Castle digests

The files in this folder implement the hash algorithms used by the `_md5`, `_sha1`, `_sha256`, `_sha512`, `_sha3` and `_blake2` modules. They are copied from [Bouncy Castle C#](https://github.com/bcgit/bc-csharp) and are covered by its MIT license, see [LICENSE.md](LICENSE.md).

## Provenance

Copied from commit [`4ccf1e6`](https://github.com/bcgit/bc-csharp/tree/4ccf1e6d2785aa8c3745723c21a4aa027eace7df).

| File | Upstream path |
|---|---|
| `Avx2.cs` | `crypto/src/runtime/intrinsics/x86/Avx2.cs` |
| `Blake2b_X86.cs` | `crypto/src/crypto/digests/Blake2b_X86.cs` |
| `Blake2bDigest.cs` | `crypto/src/crypto/digests/Blake2bDigest.cs` |
| `Blake2s_X86.cs` | `crypto/src/crypto/digests/Blake2s_X86.cs` |
| `Blake2sDigest.cs` | `crypto/src/crypto/digests/Blake2sDigest.cs` |
| `GeneralDigest.cs` | `crypto/src/crypto/digests/GeneralDigest.cs` |
| `IDigest.cs` | `crypto/src/crypto/IDigest.cs` |
| `IMemoable.cs` | `crypto/src/util/IMemoable.cs` |
| `IXof.cs` | `crypto/src/crypto/IXof.cs` |
| `KeccakDigest.cs` | `crypto/src/crypto/digests/KeccakDigest.cs` |
| `LongDigest.cs` | `crypto/src/crypto/digests/LongDigest.cs` |
| `MD5Digest.cs` | `crypto/src/crypto/digests/MD5Digest.cs` |
| `Pack.cs` | `crypto/src/crypto/util/Pack.cs` |
| `Sha1Digest.cs` | `crypto/src/crypto/digests/Sha1Digest.cs` |
| `Sha224Digest.cs` | `crypto/src/crypto/digests/Sha224Digest.cs` |
| `Sha256Digest.cs` | `crypto/src/crypto/digests/Sha256Digest.cs` |
| `Sha384Digest.cs` | `crypto/src/crypto/digests/Sha384Digest.cs` |
| `SHA3Digest.cs` | `crypto/src/crypto/digests/SHA3Digest.cs` |
| `Sha512Digest.cs` | `crypto/src/crypto/digests/Sha512Digest.cs` |
| `ShakeDigest.cs` | `crypto/src/crypto/digests/ShakeDigest.cs` |
| `Sse41.cs` | `crypto/src/runtime/intrinsics/x86/Sse41.cs` |
| `Vector.cs` | `crypto/src/runtime/intrinsics/Vector.cs` |
| `LICENSE.md` | `LICENSE.md` |

### Local changes

The copied files are unmodified, except that every top-level `public` type (class or interface) is changed to `internal`.

The copied files depend on a few Bouncy Castle utility classes which are implemented in [`../BouncyCastleSupport.cs`](../BouncyCastleSupport.cs) (outside this folder so the repository style is enforced).
