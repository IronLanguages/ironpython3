/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if CLR2
using Microsoft.Scripting.Math; 
#else
using System.Numerics;
using Microsoft.Scripting.Utils;
#endif

namespace IronPythonTest {
    public static class System_Scripting_Math {
        public static BigInteger CreateBigInteger(int sign, params uint[] data) {
#if CLR2
            return new BigInteger(sign, data);
#else
            ContractUtils.RequiresNotNull(data, "data");
            ContractUtils.Requires(sign >= -1 && sign <= +1, "sign");
            int length = data.Length - 1;
            while (length >= 0 && data[length] == 0) length--;
            length++;
            ContractUtils.Requires(length == 0 || sign != 0, "sign");

            if (length == 0) {
                return BigInteger.Zero;
            }

            bool highest = (data[length - 1] & 0x80000000) != 0;
            byte[] dataBytes = new byte[length * 4 + (highest ? 1 : 0)];
            int j = 0;
            for (int i = 0; i < length; i++) {
                ulong w = data[i];
                dataBytes[j++] = (byte)(w & 0xff);
                dataBytes[j++] = (byte)((w >> 8) & 0xff);
                dataBytes[j++] = (byte)((w >> 16) & 0xff);
                dataBytes[j++] = (byte)((w >> 24) & 0xff);
            }

            BigInteger res = new BigInteger(dataBytes);
            return sign < 0 ? -res : res;
#endif
        }
    }
}
