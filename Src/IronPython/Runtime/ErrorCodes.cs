// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Hosting {
    public static class ErrorCodes {
        // The error flags
        public const int IncompleteMask = 0x000F;

        /// <summary>
        /// The error involved an incomplete statement due to an unexpected EOF.
        /// </summary>
        public const int IncompleteStatement = 0x0001;

        /// <summary>
        /// The error involved an incomplete token.
        /// </summary>
        public const int IncompleteToken = 0x0002;

        /// <summary>
        /// The mask for the actual error values 
        /// </summary>
        public const int ErrorMask = 0x7FFFFFF0;

        /// <summary>
        /// The error was a general syntax error
        /// </summary>
        public const int SyntaxError = 0x0010;              

        /// <summary>
        /// The error was an indentation error.
        /// </summary>
        public const int IndentationError = 0x0020;      

        /// <summary>
        /// The error was a tab error.
        /// </summary>
        public const int TabError = 0x0030;

        /// <summary>
        /// syntax error shouldn't include a caret (no column offset should be included)
        /// </summary>
        public const int NoCaret = 0x0040;

    }
}
