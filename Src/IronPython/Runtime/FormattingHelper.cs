﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace IronPython.Runtime {
    internal static class FormattingHelper {
        private static NumberFormatInfo _invariantUnderscoreSeperatorInfo;

        /// <summary>
        /// Helper NumberFormatInfo for use by int/BigInteger __format__ routines
        /// for width specified leading zero support that contains '_'s every 3 digits.
        /// i.e. For use by d/g/G format specifiers. NOT for use by n format specifiers.
        /// </summary>
        public static NumberFormatInfo InvariantUnderscoreNumberInfo {
            get {
                if (_invariantUnderscoreSeperatorInfo == null) {
                    Interlocked.CompareExchange(
                        ref _invariantUnderscoreSeperatorInfo,
                        new NumberFormatInfo() {
                            NumberGroupSeparator = "_",
                            NumberDecimalSeparator = ".",
                            NumberGroupSizes = new int[] { 3 }
                        },
                        null
                    );
                }
                return _invariantUnderscoreSeperatorInfo;
            }
        }

        public static string/*!*/ ToCultureString<T>(T/*!*/ val, NumberFormatInfo/*!*/ nfi, StringFormatSpec spec, int? overrideWidth = null) {
            string separator = nfi.NumberGroupSeparator;
            int[] separatorLocations = nfi.NumberGroupSizes;
            string digits = val.ToString();

            // If we're adding leading zeros, we need to know how
            // many we need.
            int width = overrideWidth ?? spec.Width ?? 0;
            int fillerLength = Math.Max(width - digits.Length, 0);
            bool addLeadingZeros = (spec.Fill ?? '\0') == '0' && width > digits.Length;
            int beginningOfDigits = fillerLength;
            int remainingWidth = 0;

            if (addLeadingZeros) {
                // If we're adding leading zeros, add more than necessary
                // we'll trim off the extra (if any) later.
                digits = digits.Insert(0, new string('0', fillerLength));
            }

            if (separatorLocations.Length > 0) {
                StringBuilder res = new StringBuilder(digits);

                int curGroup = 0, curDigit = digits.Length - 1;
                while (curDigit > 0) {
                    // insert the separator
                    int groupLen = separatorLocations[curGroup];
                    if (groupLen == 0) {
                        break;
                    }
                    curDigit -= groupLen;

                    if (curDigit >= 0) {
                        res.Insert(curDigit + 1, separator);
                        // Once we have advanced left of the last of
                        // the original digits, we need to adjust the
                        // index that tracks the first original digit index.
                        if (addLeadingZeros && curDigit < fillerLength) {
                            beginningOfDigits++;
                            // The remaining width is the format width minus the length
                            // of the expanded original digits:
                            remainingWidth = Math.Max(width - (res.Length - beginningOfDigits), 0);
                            // If we've run out of room, then no need to insert
                            // anymore commas into leading zeros.
                            if (remainingWidth == 0) {
                                break;
                            }
                        }
                    }

                    // advance the group
                    if (curGroup + 1 < separatorLocations.Length) {
                        if (separatorLocations[curGroup + 1] == 0) {
                            // last value doesn't propagate
                            break;
                        }

                        curGroup++;
                    }
                }
                if (addLeadingZeros && res.Length > width) {
                    // The remaining width is the format width minus the length
                    // of the expanded original digits:
                    remainingWidth = Math.Max(width - (res.Length - beginningOfDigits), 0);
                    if (remainingWidth > 0) {
                        // Index that points at the beginning of the requested width:
                        var beginningOfMaximumWidth = beginningOfDigits - remainingWidth;
                        // If the maximum width stops at a character that is part of the
                        // separator then keep looking to the left until we find a character
                        // that isn't. After all, it would be pretty weird to produce:
                        // 000,xxx,xxx,xxx. So, produce 0,000,xxx,xxx,xxx instead.
                        // (Just like CPython)
                        if (separator.IndexOf(res[beginningOfMaximumWidth]) != -1) {
                            for (int i = beginningOfMaximumWidth - 1; i >= 0; i--) {
                                if (separator.IndexOf(res[i]) == -1) {
                                    res.Remove(0, i);
                                    break;
                                }
                            }
                        } else {
                            res.Remove(0, beginningOfMaximumWidth);
                        }
                    } else {
                        // If we ran out of remainingWidth just formatting
                        // the actual digits, then remove any extra leading zeros
                        // we added.
                        res.Remove(0, beginningOfDigits);
                    }
                }
                digits = res.ToString();
            }

            return digits;
        }
    }
}
