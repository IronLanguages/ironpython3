// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Scripting.Utils;

namespace IronPythonTest {
    public static class System_Scripting_Math {
        public static BigInteger CreateBigInteger(int sign, params uint[] data) {
            ContractUtils.RequiresNotNull(data, nameof(data));
            ContractUtils.Requires(sign >= -1 && sign <= +1, nameof(sign));
            int length = data.Length - 1;
            while (length >= 0 && data[length] == 0) length--;
            length++;
            ContractUtils.Requires(length == 0 || sign != 0, nameof(sign));

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
        }
    }
}
