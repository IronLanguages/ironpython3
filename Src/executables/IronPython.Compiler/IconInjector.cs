// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace IronPython.Compiler {

    /// <remarks>
    /// Fork from <see href="https://github.com/INotGreen/SharpThief/blob/main/IconInjector.cs"/>.
    /// </remarks>
    internal static class IconInjector {

        private const nint RT_ICON = 3;
        private const nint RT_GROUP_ICON = 14;

        public static void InjectIcon(string exeFileName, string iconFileName, nint iconGroupID = 1, ushort iconBaseID = 1) {
            IconFile iconFile = IconFile.FromFile(iconFileName);
            nint hUpdate = BeginUpdateResourceW(exeFileName, false);
            byte[] data = iconFile.CreateIconGroupData(iconBaseID);
            UpdateResourceW(hUpdate, RT_GROUP_ICON, iconGroupID, 0, data, data.Length);
            for (int i = 0; i <= iconFile.ImageCount - 1; i++) {
                byte[] image = iconFile[i];
                UpdateResourceW(hUpdate, RT_ICON, iconBaseID + i, 0, image, image.Length);
            }
            EndUpdateResourceW(hUpdate, false);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern nint BeginUpdateResourceW([In] string fileName, [In][MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateResourceW([In] nint hUpdate, [In] nint type, [In] nint name, [In] short language, [In, Optional][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] data, [In] int dataSize);

        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndUpdateResourceW([In] nint hUpdate, [In][MarshalAs(UnmanagedType.Bool)] bool discard);

        // The first structure in an ICO file lets us know how many images are in the file.
        [StructLayout(LayoutKind.Sequential)]
        private struct ICONDIR {
            // Reserved, must be 0
            public ushort Reserved;
            // Resource type, 1 for icons.
            public ushort Type;
            // How many images.
            public ushort Count;
            // The native structure has an array of ICONDIRENTRYs as a final field.
        }

        // Each ICONDIRENTRY describes one icon stored in the ico file. The offset says where the icon image data
        // starts in the file. The other fields give the information required to turn that image data into a valid
        // bitmap.
        [StructLayout(LayoutKind.Sequential)]
        private struct ICONDIRENTRY {
            /// <summary>
            /// The width, in pixels, of the image.
            /// </summary>
            public byte Width;
            /// <summary>
            /// The height, in pixels, of the image.
            /// </summary>
            public byte Height;
            /// <summary>
            /// The number of colors in the image; (0 if >= 8bpp)
            /// </summary>
            public byte ColorCount;
            /// <summary>
            /// Reserved (must be 0).
            /// </summary>
            public byte Reserved;
            /// <summary>
            /// Color planes.
            /// </summary>
            public ushort Planes;
            /// <summary>
            /// Bits per pixel.
            /// </summary>
            public ushort BitCount;
            /// <summary>
            /// The length, in bytes, of the pixel data.
            /// </summary>
            public int BytesInRes;
            /// <summary>
            /// The offset in the file where the pixel data starts.
            /// </summary>
            public int ImageOffset;
        }

        // Each image is stored in the file as an ICONIMAGE structure:
        //typdef struct
        //{
        //   BITMAPINFOHEADER   icHeader;      // DIB header
        //   RGBQUAD         icColors[1];   // Color table
        //   BYTE            icXOR[1];      // DIB bits for XOR mask
        //   BYTE            icAND[1];      // DIB bits for AND mask
        //} ICONIMAGE, *LPICONIMAGE;


        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        // The icon in an exe/dll file is stored in a very similar structure:
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct GRPICONDIRENTRY {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitCount;
            public int BytesInRes;
            public ushort ID;
        }

        private class IconFile {
            private ICONDIR iconDir;
            private ICONDIRENTRY[] iconEntry;

            private byte[][] iconImage;

            public ushort ImageCount => iconDir.Count;

            public byte[] this[int index] => iconImage[index];

            public static unsafe IconFile FromFile(string filename) {
                IconFile instance = new IconFile();
                // Read all the bytes from the file.
                byte[] fileBytes = System.IO.File.ReadAllBytes(filename);
                // First struct is an ICONDIR
                // Pin the bytes from the file in memory so that we can read them.
                // If we didn't pin them then they could move around (e.g. when the
                // garbage collector compacts the heap)
                fixed (byte* pinnedBytes = fileBytes) {
                    ICONDIR* iconDirs = (ICONDIR*)pinnedBytes;
                    // Read the ICONDIR
                    instance.iconDir = iconDirs[0];
                    // which tells us how many images are in the ico file. For each image, there's a ICONDIRENTRY, and associated pixel data.
                    instance.iconEntry = new ICONDIRENTRY[instance.iconDir.Count];
                    instance.iconImage = new byte[instance.iconDir.Count][];
                    // The first ICONDIRENTRY will be immediately after the ICONDIR, so the offset to it is the size of ICONDIR
                    ICONDIRENTRY* array = (ICONDIRENTRY*)(iconDirs + 1);
                    // After reading an ICONDIRENTRY we step forward by the size of an ICONDIRENTRY
                    for (int i = 0; i < instance.iconDir.Count; i++) {
                        // Grab the structure.
                        ICONDIRENTRY entry = array[i];
                        instance.iconEntry[i] = entry;
                        // Grab the associated pixel data.
                        byte[] image = new byte[entry.BytesInRes];
                        Buffer.BlockCopy(fileBytes, entry.ImageOffset, image, 0, entry.BytesInRes);
                        instance.iconImage[i] = image;
                    }
                }
                return instance;
            }

            public unsafe byte[] CreateIconGroupData(ushort iconBaseID) {
                // This will store the memory version of the icon.
                int sizeOfIconGroupData = sizeof(ICONDIR) +
                                          sizeof(GRPICONDIRENTRY) * ImageCount;
                byte[] data = new byte[sizeOfIconGroupData];
                fixed (byte* pinnedData = data) {
                    ICONDIR* iconDirs = (ICONDIR*)pinnedData;
                    iconDirs[0] = iconDir;
                    GRPICONDIRENTRY* array = (GRPICONDIRENTRY*)(iconDirs + 1);
                    for (ushort i = 0; i < ImageCount; i++) {
                        byte[] image = iconImage[i];
                        fixed (byte* pinnedBitmapInfoHeader = image) {
                            BITMAPINFOHEADER bitmapHeader = *(BITMAPINFOHEADER*)pinnedBitmapInfoHeader;
                            GRPICONDIRENTRY grpEntry = new GRPICONDIRENTRY {
                                Width = iconEntry[i].Width,
                                Height = iconEntry[i].Height,
                                ColorCount = iconEntry[i].ColorCount,
                                Reserved = iconEntry[i].Reserved,
                                Planes = bitmapHeader.Planes,
                                BitCount = bitmapHeader.BitCount,
                                BytesInRes = iconEntry[i].BytesInRes,
                                ID = checked((ushort)(iconBaseID + i))
                            };
                            array[i] = grpEntry;
                        }
                    }
                }
                return data;
            }
        }
    }
}
