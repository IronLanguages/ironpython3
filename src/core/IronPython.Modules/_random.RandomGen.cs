// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace IronPython.Modules {
    public static partial class PythonRandom {
        /// <summary>
        /// Generator based on the .NET Core implementation of System.Random
        /// </summary>
        private class RandomGen {
            //
            // Private Constants
            //
            private const int MBIG = int.MaxValue;
            private const int MSEED = 161803398;

            //
            // Member Variables
            //
            private int _inext;
            private int _inextp;
            private int[] _seedArray = new int[56];

            //
            // Constructors
            //

            /*=========================================================================================
            **Action: Initializes a new instance of the Random class, using a specified seed value
            ===========================================================================================*/
            public RandomGen(int Seed) {
                int ii = 0;
                int mj, mk;

                //Initialize our Seed array.
                int subtraction = (Seed == int.MinValue) ? int.MaxValue : Math.Abs(Seed);
                mj = MSEED - subtraction;
                _seedArray[55] = mj;
                mk = 1;
                for (int i = 1; i < 55; i++) {  //Apparently the range [1..55] is special (Knuth) and so we're wasting the 0'th position.
                    if ((ii += 21) >= 55) ii -= 55;
                    _seedArray[ii] = mk;
                    mk = mj - mk;
                    if (mk < 0) mk += MBIG;
                    mj = _seedArray[ii];
                }
                for (int k = 1; k < 5; k++) {
                    for (int i = 1; i < 56; i++) {
                        int n = i + 30;
                        if (n >= 55) n -= 55;
                        _seedArray[i] -= _seedArray[1 + n];
                        if (_seedArray[i] < 0) _seedArray[i] += MBIG;
                    }
                }
                _inext = 0;
                _inextp = 21;
                Seed = 1;
            }

            //
            // Package Private Methods
            //

            private int InternalSample() {
                int retVal;
                int locINext = _inext;
                int locINextp = _inextp;

                if (++locINext >= 56) locINext = 1;
                if (++locINextp >= 56) locINextp = 1;

                retVal = _seedArray[locINext] - _seedArray[locINextp];

                if (retVal == MBIG) retVal--;
                if (retVal < 0) retVal += MBIG;

                _seedArray[locINext] = retVal;

                _inext = locINext;
                _inextp = locINextp;

                return retVal;
            }

            //
            // Public Instance Methods
            //

            /*==================================NextBytes===================================
            **Action:  Fills the byte array with random bytes [0..0x7f].  The entire array is filled.
            **Returns:Void
            **Arguments:  buffer -- the array to be filled.
            **Exceptions: None
            ==============================================================================*/
            public void NextBytes(byte[] buffer) {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                for (int i = 0; i < buffer.Length; i++) {
                    buffer[i] = (byte)InternalSample();
                }
            }

            internal object[] GetState() {
                var state = new object[58];
                Array.Copy(_seedArray, state, _seedArray.Length);
                state[56] = _inext;
                state[57] = _inextp;
                return state;
            }

            internal void SetState(int[] state) {
                Array.Copy(state, _seedArray, _seedArray.Length);
                _inext = state[56];
                _inextp = state[57];
            }
        }
    }
}
