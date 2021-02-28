// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_NATIVE

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("winsound", typeof(IronPython.Modules.PythonWinsoundModule), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    [SupportedOSPlatform("windows")]
    public static class PythonWinsoundModule {
        public static readonly string __doc__ = @"PlaySound(sound, flags) - play a sound
SND_FILENAME - sound is a wav file name
SND_ALIAS - sound is a registry sound association name
SND_LOOP - Play the sound repeatedly; must also specify SND_ASYNC
SND_MEMORY - sound is a memory image of a wav file
SND_PURGE - stop all instances of the specified sound
SND_ASYNC - PlaySound returns immediately
SND_NODEFAULT - Do not play a default beep if the sound can not be found
SND_NOSTOP - Do not interrupt any sounds currently playing
SND_NOWAIT - Return immediately if the sound driver is busy

Beep(frequency, duration) - Make a beep through the PC speaker.";

        public const int SND_SYNC = 0x0000;  /* play synchronously (default) */
        public const int SND_ASYNC = 0x0001;  /* play asynchronously */
        public const int SND_NODEFAULT = 0x0002;  /* silence (!default) if sound not found */
        public const int SND_MEMORY = 0x0004;  /* pszSound points to a memory file */
        public const int SND_LOOP = 0x0008;  /* loop the sound until next sndPlaySound */
        public const int SND_NOSTOP = 0x0010;  /* don't stop any currently playing sound */

        public const int SND_NOWAIT = 0x00002000; /* don't wait if the driver is busy */
        public const int SND_ALIAS = 0x00010000; /* name is a registry alias */
        public const int SND_ALIAS_ID = 0x00110000; /* alias is a predefined ID */
        public const int SND_FILENAME = 0x00020000; /* name is file name */
        public const int SND_RESOURCE = 0x00040004; /* name is resource name or atom */
        public const int SND_PURGE = 0x0040;  /* purge non-static events for task */
        public const int SND_APPLICATION = 0x0080;  /* look for application specific association */


        public const int MB_OK = 0x00000000;
        public const int MB_ICONASTERISK = 0x00000040;
        public const int MB_ICONEXCLAMATION = 0x00000030;
        public const int MB_ICONHAND = 0x00000010;
        public const int MB_ICONQUESTION = 0x00000020;

        #region Private Implementation Details

        [DllImport("winmm.dll")]
        private static extern bool PlaySound(string fileName, IntPtr hMod, int flags);

        [DllImport("winmm.dll")]
        private static extern bool PlaySound(byte[] bytes, IntPtr hMod, int flags);

        [DllImport("winmm.dll")]
        private static extern bool PlaySound(IntPtr input, IntPtr hMod, int flags);

        [DllImport("kernel32.dll")]
        private static extern bool Beep(int dwFreq, int dwDuration);

        [DllImport("user32.dll")]
        private static extern bool MessageBeep(int uType);

        #endregion

        #region Public API

        [Documentation(@"PlaySound(sound, flags) - a wrapper around the Windows PlaySound API

The sound argument can be a filename, data, or None.
For flag values, ored together, see module documentation.")]
        public static void PlaySound(CodeContext/*!*/ context, string sound, int flags) {
            if (sound is null) {
                if (!PlaySound(IntPtr.Zero, IntPtr.Zero, flags)) {
                    throw PythonOps.RuntimeError("Failed to play sound");
                }
            } else {
                if (((flags & SND_ASYNC) == SND_ASYNC) && ((flags & SND_MEMORY) == SND_MEMORY)) throw PythonOps.RuntimeError("Cannot play asynchronously from memory");
                if ((flags & SND_MEMORY) == SND_MEMORY) throw PythonOps.TypeError($"a bytes-like object is required, not '{PythonOps.GetPythonTypeName(sound)}'");

                if (sound.IndexOf((char)0) != -1) throw PythonOps.ValueError("embedded null character");

                if (!PlaySound(sound, IntPtr.Zero, flags)) {
                    throw PythonOps.RuntimeError("Failed to play sound");
                }
            }
        }

        [Documentation(@"PlaySound(sound, flags) - a wrapper around the Windows PlaySound API

The sound argument can be a filename, data, or None.
For flag values, ored together, see module documentation.")]
        public static void PlaySound(CodeContext/*!*/ context, [NotNull] IBufferProtocol sound, int flags) {
            if (((flags & SND_ASYNC) == SND_ASYNC) && ((flags & SND_MEMORY) == SND_MEMORY)) throw PythonOps.RuntimeError("Cannot play asynchronously from memory");
            if ((flags & SND_MEMORY) == 0) throw PythonOps.TypeError($"'{nameof(sound)}' must be str or None, not '{PythonOps.GetPythonTypeName(sound)}'");

            using var buffer = sound.GetBuffer();

            if (!PlaySound(buffer.ToArray(), IntPtr.Zero, flags)) {
                throw PythonOps.RuntimeError("Failed to play sound");
            }
        }

        [Documentation(@"Beep(frequency, duration) - a wrapper around the Windows Beep API

The frequency argument specifies frequency, in hertz, of the sound.
This parameter must be in the range 37 through 32,767.
The duration argument specifies the number of milliseconds.
")]
        public static void Beep(CodeContext/*!*/ context, int freq, int dur) {
            if (freq < 37 || freq > 32767) {
                throw PythonOps.ValueError("frequency must be in 37 thru 32767");
            }

            bool ok = Beep(freq, dur);
            if (!ok) {
                throw PythonOps.RuntimeError("Failed to beep");
            }
        }

        [Documentation("MessageBeep(x) - call Windows MessageBeep(x). x defaults to MB_OK.")]
        public static void MessageBeep(CodeContext/*!*/ context, int x=MB_OK) {
            MessageBeep(x);
        }

        #endregion
    }
}

#endif
