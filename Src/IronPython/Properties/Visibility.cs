/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System;

#if DEBUG
[assembly: InternalsVisibleTo("IronPython.Modules, PublicKey=002400000480000094000000060200000024000052534131000400000100010077bd8a9eac4c59da8abe9ca7708d869e11006040943a6acd03d50fb0e797e977a2f6d09b7fce40170ed1bfc3a45ffe4cfe4a3d6187f83fadaee44268c3e8b22417a955d0c435816d3743ba824aab12ea68aeafa31e4f3ac72bc5cded79512c18989fb9a5e8a75ba3f4f3bf90e8f970b53d5bd89f044c308e369e77b215be52bf")]
[assembly: InternalsVisibleTo("IronPythonTest, PublicKey=002400000480000094000000060200000024000052534131000400000100010077bd8a9eac4c59da8abe9ca7708d869e11006040943a6acd03d50fb0e797e977a2f6d09b7fce40170ed1bfc3a45ffe4cfe4a3d6187f83fadaee44268c3e8b22417a955d0c435816d3743ba824aab12ea68aeafa31e4f3ac72bc5cded79512c18989fb9a5e8a75ba3f4f3bf90e8f970b53d5bd89f044c308e369e77b215be52bf")]
#else
[assembly: InternalsVisibleTo("IronPython.Modules, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c10ce00dd2e0ce5046d68183d3ad035b47e92bf0ce7bcf8a03a217ca5d0b0c7db973fdf97579b52b502a23d4069dbf043389e1ab65a1d6c508a9837f3e2350f15e05cc63c0fc4b0410867a51919090e4c33f80203e9b0035b21c32bae20f98b068f90d99a50133a5336480d94039b176519f5fd8524765f33be43da65c4b68ba")]
[assembly: InternalsVisibleTo("IronPythonTest, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c10ce00dd2e0ce5046d68183d3ad035b47e92bf0ce7bcf8a03a217ca5d0b0c7db973fdf97579b52b502a23d4069dbf043389e1ab65a1d6c508a9837f3e2350f15e05cc63c0fc4b0410867a51919090e4c33f80203e9b0035b21c32bae20f98b068f90d99a50133a5336480d94039b176519f5fd8524765f33be43da65c4b68ba")]
#endif

#if FEATURE_APTCA
[assembly: AllowPartiallyTrustedCallers]
#endif
