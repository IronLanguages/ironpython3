// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
#if FEATURE_REGISTRY //Registry not available in silverlight and we require .NET 4.0 APIs for implementing this.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Text;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("winreg", typeof(IronPython.Modules.PythonWinReg), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    public static class PythonWinReg {
        public const string __doc__ = "Provides access to the Windows registry.";

        public static PythonType error = PythonExceptions.OSError;

#region Constants

        public static BigInteger HKEY_CLASSES_ROOT = 0x80000000L;
        public static BigInteger HKEY_CURRENT_USER = 0x80000001L;
        public static BigInteger HKEY_LOCAL_MACHINE = 0x80000002L;
        public static BigInteger HKEY_USERS = 0x80000003L;
        public static BigInteger HKEY_PERFORMANCE_DATA = 0x80000004L;
        public static BigInteger HKEY_CURRENT_CONFIG = 0x80000005L;
        public static BigInteger HKEY_DYN_DATA = 0x80000006L;

        public const int KEY_QUERY_VALUE = 0X1;
        public const int KEY_SET_VALUE = 0X2;
        public const int KEY_CREATE_SUB_KEY = 0X4;
        public const int KEY_ENUMERATE_SUB_KEYS = 0X8;
        public const int KEY_NOTIFY = 0X10;
        public const int KEY_CREATE_LINK = 0X20;

        public const int KEY_ALL_ACCESS = 0XF003F;
        public const int KEY_EXECUTE = 0X20019;
        public const int KEY_READ = 0X20019;
        public const int KEY_WRITE = 0X20006;
        public const int KEY_WOW64_64KEY = 0X100;
        public const int KEY_WOW64_32KEY = 0X200;

        public const int REG_CREATED_NEW_KEY = 0X1;
        public const int REG_OPENED_EXISTING_KEY = 0X2;

        public const int REG_NONE = 0X0;
        public const int REG_SZ = 0X1;
        public const int REG_EXPAND_SZ = 0X2;
        public const int REG_BINARY = 0X3;
        public const int REG_DWORD = 0X4;
        public const int REG_DWORD_LITTLE_ENDIAN = 0X4;
        public const int REG_DWORD_BIG_ENDIAN = 0X5;
        public const int REG_LINK = 0X6;
        public const int REG_MULTI_SZ = 0X7;
        public const int REG_RESOURCE_LIST = 0X8;
        public const int REG_FULL_RESOURCE_DESCRIPTOR = 0X9;
        public const int REG_RESOURCE_REQUIREMENTS_LIST = 0XA;

        public const int REG_NOTIFY_CHANGE_NAME = 0X1;
        public const int REG_NOTIFY_CHANGE_ATTRIBUTES = 0X2;
        public const int REG_NOTIFY_CHANGE_LAST_SET = 0X4;
        public const int REG_NOTIFY_CHANGE_SECURITY = 0X8;

        public const int REG_OPTION_RESERVED = 0X0;
        public const int REG_OPTION_NON_VOLATILE = 0X0;
        public const int REG_OPTION_VOLATILE = 0X1;
        public const int REG_OPTION_CREATE_LINK = 0X2;
        public const int REG_OPTION_BACKUP_RESTORE = 0X4;
        public const int REG_OPTION_OPEN_LINK = 0X8;

        public const int REG_NO_LAZY_FLUSH = 0X4;
        public const int REG_REFRESH_HIVE = 0X2;
        public const int REG_LEGAL_CHANGE_FILTER = 0XF;
        public const int REG_LEGAL_OPTION = 0XF;
        public const int REG_WHOLE_HIVE_VOLATILE = 0X1;

#endregion

#region Module Methods

        public static void CloseKey(HKEYType key) {
            key.Close();
        }

        public static HKEYType CreateKey(object key, string sub_key) {
            // the .NET APIs don't work with a key name of length 256, use CreateKeyEx which PInvokes instead
            if (sub_key.Length == 256)
                return CreateKeyEx(key, sub_key, 0, KEY_ALL_ACCESS);

            HKEYType rootKey = GetRootKey(key);

            //if key is a system key and no subkey is specified return that.
            if (key is BigInteger && string.IsNullOrEmpty(sub_key))
                return rootKey;
            
            HKEYType subKey = new HKEYType(rootKey.GetKey().CreateSubKey(sub_key));
            return subKey;
        }

        private static string FormatError(int errorCode) {
            return new Win32Exception(errorCode).Message;
        }

        public static HKEYType CreateKeyEx(object key, string sub_key, int reserved = 0, int access = KEY_ALL_ACCESS) {
            HKEYType rootKey = GetRootKey(key);

            //if key is a system key and no subkey is specified return that.
            if (key is BigInteger && string.IsNullOrEmpty(sub_key))
                return rootKey;

            SafeRegistryHandle handle;
            int disposition;
            
            int result = RegCreateKeyEx(
                rootKey.GetKey().Handle,
                sub_key,
                0,
                null,
                RegistryOptions.None,
                (RegistryRights)access,
                IntPtr.Zero,
                out handle,
                out disposition
            );
            if (result != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(error, result, FormatError(result));
            }

            return new HKEYType(RegistryKey.FromHandle(handle));
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCreateKeyEx(
                    SafeRegistryHandle hKey,
                    string lpSubKey,
                    int Reserved,
                    string lpClass,
                    RegistryOptions dwOptions,
                    RegistryRights samDesired,
                    IntPtr lpSecurityAttributes,
                    out SafeRegistryHandle phkResult,
                    out int lpdwDisposition);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int RegQueryValueEx(
              SafeRegistryHandle hKey,
              string lpValueName,
              IntPtr lpReserved,
              out int lpType,
              byte[] lpData,
              ref uint lpcbData
            );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegEnumKeyEx(
            SafeRegistryHandle hKey,
            int dwIndex,
            StringBuilder lpName,
            ref int lpcbName,
            IntPtr lpReserved,
            IntPtr lpClass,
            IntPtr lpcbClass,
            IntPtr lpftLastWriteTime);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegSetValueEx(
            SafeRegistryHandle hKey,
            string lpValueName,
            int Reserved,
            int dwType,
            byte[] lpData,
            int cbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int RegDeleteKey(
            SafeRegistryHandle hKey,
            string lpSubKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int RegDeleteKeyEx(
            SafeRegistryHandle hKey,
            string lpSubKey,
            int samDesired,
            int Reserved);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int RegDisableReflectionKey(
            SafeRegistryHandle hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int RegEnableReflectionKey(
            SafeRegistryHandle hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int RegQueryReflectionKey(
            SafeRegistryHandle hBase,
            out bool bIsReflectionDisabled);

        public static void DeleteKey(object key, string sub_key) {
            HKEYType rootKey = GetRootKey(key);

            if (key is BigInteger && string.IsNullOrEmpty(sub_key))
                throw new InvalidCastException("DeleteKey() argument 2 must be string, not None");

            // the .NET APIs don't work with a key name of length 256, use a PInvoke instead
            // unlike in CreateKey(), `subKeyName.Length` on deletion can be greater than 256 if combined with parent key name.
            if (sub_key.Length >= 256) {
                return;
            }

            try {
                rootKey.GetKey().DeleteSubKey(sub_key);
            } catch (ArgumentException e) {
                throw new ExternalException(e.Message);
            }
        }

        public static void DeleteKeyEx(object key, string sub_key, int access=KEY_WOW64_64KEY, int reserved=0) {
            HKEYType rootKey = GetRootKey(key);

            if (key is BigInteger && string.IsNullOrEmpty(sub_key))
                throw new InvalidCastException("DeleteKeyEx() argument 2 must be string, not None");

            int result = RegDeleteKeyEx(rootKey.GetKey().Handle, sub_key, access, reserved);
            if (result != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, result);
            }
        }

        public static void DeleteValue(object key, string value) {
            HKEYType rootKey = GetRootKey(key);

            rootKey.GetKey().DeleteValue(value, true);
        }

        public static string EnumKey(object key, int index) {
            HKEYType rootKey = GetRootKey(key);

            int len = 257; // maximum key name length is 256
            StringBuilder name = new StringBuilder(len);
            int ret = RegEnumKeyEx(rootKey.GetKey().Handle, index, name, ref len, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (ret != ERROR_SUCCESS) {
                Debug.Assert(ret == ERROR_NO_MORE_ITEMS);
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_BAD_COMMAND, "No more data is available", null, PythonExceptions._OSError.ERROR_BAD_COMMAND);
            }
            return name.ToString();
        }

        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_MORE_DATA = 234;
        private const int ERROR_SUCCESS = 0;

        public static PythonTuple EnumValue(object key, int index) {
            HKEYType rootKey = GetRootKey(key);
            if (index >= rootKey.GetKey().ValueCount) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_BAD_COMMAND, "No more data is available", null, PythonExceptions._OSError.ERROR_BAD_COMMAND);
            }

            var nativeRootKey = rootKey.GetKey();
            string valueName = nativeRootKey.GetValueNames()[index];

            // it looks like nativeRootKey.Handle fails on HKEY_PERFORMANCE_DATA so manually create the handle instead
            var handle = rootKey.hkey == HKEY_PERFORMANCE_DATA ? new SafeRegistryHandle(new IntPtr(unchecked((int)0x80000004)), true) : nativeRootKey.Handle;

            int valueKind;
            object value;
            QueryValueExImpl(handle, valueName, out valueKind, out value);
            return PythonTuple.MakeTuple(valueName, value, valueKind);
        }

        private static void QueryValueExImpl(SafeRegistryHandle handle, string valueName, out int valueKind, out object value) {
            valueName = valueName ?? ""; // it looks like RegQueryValueEx can fail with null, use empty string instead
            valueKind = 0;
            int dwRet;
            byte[] data = new byte[128];
            uint length = (uint)data.Length;

            // query the size first, reading the data as we query...
            dwRet = RegQueryValueEx(handle, valueName, IntPtr.Zero, out valueKind, data, ref length);
            while (dwRet == ERROR_MORE_DATA) {
                data = new byte[data.Length * 2];
                length = (uint)data.Length;
                dwRet = RegQueryValueEx(handle, valueName, IntPtr.Zero, out valueKind, data, ref length);
            }

            if (dwRet == PythonExceptions._OSError.ERROR_FILE_NOT_FOUND) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND, "The system cannot find the file specified", null, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND);
            }
            if (dwRet != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, dwRet);
            }

            // convert the result into a Python object

            switch (valueKind) {
                case REG_MULTI_SZ:
                    PythonList list = new PythonList();
                    int curIndex = 0;
                    while (curIndex < length) {
                        for (int dataIndex = curIndex; dataIndex < length; dataIndex += 2) {
                            if (data[dataIndex] == 0 && data[dataIndex + 1] == 0) {
                                // got a full string
                                list.Add(ExtractString(data, curIndex, dataIndex));
                                curIndex = dataIndex + 2;

                                if (curIndex + 2 <= length && data[curIndex] == 0 && data[curIndex + 1] == 0) {
                                    // double null terminated
                                    curIndex = data.Length;
                                    break;
                                }
                            }
                        }

                        if (curIndex != data.Length) {
                            // not null terminated
                            list.Add(ExtractString(data, curIndex, data.Length));
                        }
                    }
                    value = list;
                    break;
                case REG_BINARY:
                    value = length == 0 ? null : PythonOps.MakeString(data, (int)length);
                    break;
                case REG_EXPAND_SZ:
                case REG_SZ:
                    if (length >= 2 && data[length - 1] == 0 && data[length - 2] == 0) {
                        value = ExtractString(data, 0, (int)length - 2);
                    } else {
                        value = ExtractString(data, 0, (int)length);
                    }
                    break;
                case REG_DWORD:
                    if (BitConverter.IsLittleEndian) {
                        value = (uint)((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
                    } else {
                        value = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
                    }
                    break;
                default:
                    value = null;
                    break;
            }

        }

        public static string ExpandEnvironmentStrings(string value)
        {
            if (value == null)
                throw PythonExceptions.CreateThrowable(PythonExceptions.TypeError,  "must be unicode, not None");

            return Environment.ExpandEnvironmentVariables(value);
        }

        private static string ExtractString(byte[] data, int start, int end) {
            if (end <= start) {
                return String.Empty;
            }
            char[] chars = new char[(end - start) / 2];
            for (int i = 0; i < chars.Length; i++) {
                chars[i] = (char)((data[i*2 + start]) | (data[i*2 + start + 1] << 8));
            }
            return new string(chars);
        }

        public static void FlushKey(object key) {
            HKEYType rootKey = GetRootKey(key);
            rootKey.GetKey().Flush();
        }

        public static HKEYType OpenKey(object key, string sub_key) {
            return OpenKey(key, sub_key, 0, KEY_READ);
        }

        public static HKEYType OpenKey(object key, string sub_key, int reserved=0, int access=KEY_READ) {
            HKEYType rootKey = GetRootKey(key);
            RegistryKey newKey = null;

            // I'm assuming that the masks that CPy uses are the same as the Win32 API one mentioned here-
            // http://msdn2.microsoft.com/en-us/library/ms724878(VS.85).aspx

            // KEY_WRITE is a combination of KEY_SET_VALUE and KEY_CREATE_SUB_KEY. We'll open with write access
            // if any of this is set.
            // KEY_READ is a combination of KEY_QUERY_VALUE, KEY_ENUMERATE_SUB_KEYS and KEY_NOTIFY. We'll open
            // with read access for all of these. 

            var nativeRootKey = rootKey.GetKey();
            try {
                if ((access & KEY_SET_VALUE) == KEY_SET_VALUE ||
                    (access & KEY_CREATE_SUB_KEY) == KEY_CREATE_SUB_KEY) {
                        if (reserved != 0) {
                            newKey = nativeRootKey.OpenSubKey(sub_key, RegistryKeyPermissionCheck.Default, (RegistryRights)reserved);
                        } else {
                            newKey = nativeRootKey.OpenSubKey(sub_key, true);
                        }
                } else if ((access & KEY_QUERY_VALUE) == KEY_QUERY_VALUE ||
                           (access & KEY_ENUMERATE_SUB_KEYS) == KEY_ENUMERATE_SUB_KEYS ||
                           (access & KEY_NOTIFY) == KEY_NOTIFY) {
                               if (reserved != 0) {
                                   newKey = nativeRootKey.OpenSubKey(sub_key, RegistryKeyPermissionCheck.ReadSubTree, (RegistryRights)reserved);
                               } else {
                                   newKey = nativeRootKey.OpenSubKey(sub_key, false);
                               }
                } else {
                    throw new Win32Exception("Unexpected mode");
                }
            } catch (SecurityException) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_ACCESS_DENIED, "Access is denied", null, PythonExceptions._OSError.ERROR_ACCESS_DENIED);
            }


            if (newKey == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND, "The system cannot find the file specified", null, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND);
            }

            return new HKEYType(newKey);
        }

        public static HKEYType OpenKeyEx(object key, string sub_key, int reserved=0, int access=KEY_READ) {
            return OpenKey(key, sub_key, reserved, access);
        }

        public static PythonTuple QueryInfoKey(object key) {
            HKEYType rootKey = null;
            //The key can also be a handle. If it is, then retrieve it from the cache.
            if (key is int) {
                if (HKeyHandleCache.cache.ContainsKey((int)key)) {
                    if (HKeyHandleCache.cache[(int)key].IsAlive) {
                        rootKey = HKeyHandleCache.cache[(int)key].Target as HKEYType;
                    }
                }
            } else {
                rootKey = GetRootKey(key);
            }

            if (rootKey == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, "key has been closed");
            }

            try {
                var nativeRootKey = rootKey.GetKey();
                return PythonTuple.MakeTuple(nativeRootKey.SubKeyCount, nativeRootKey.ValueCount, 0);
            } catch (ObjectDisposedException e) {
                throw new ExternalException(e.Message);
            }
        }

        public static object QueryValue(object key, string sub_key) {
            HKEYType pyKey = OpenKey(key, sub_key);
            return pyKey.GetKey().GetValue(null);
        }

        public static PythonTuple QueryValueEx(object key, string value_name) {
            HKEYType rootKey = GetRootKey(key);

            // it looks like rootKey.GetKey().Handle fails on HKEY_PERFORMANCE_DATA so manually create the handle instead
            var handle = rootKey.hkey == HKEY_PERFORMANCE_DATA ? new SafeRegistryHandle(new IntPtr(unchecked((int)0x80000004)), true) : rootKey.GetKey().Handle;

            int valueKind;
            object value;
            QueryValueExImpl(handle, value_name, out valueKind, out value);

            return PythonTuple.MakeTuple(value, valueKind);
        }

        public static void SetValue(object key, string sub_key, int type, string value) {
            HKEYType pyKey = CreateKey(key, sub_key);
            pyKey.GetKey().SetValue(null, value);
        }

        public static void SetValueEx(object key, string value_name, object reserved, int type, object value) {
            HKEYType rootKey = GetRootKey(key);
            RegistryValueKind regKind = (RegistryValueKind)type;

            // null is a valid value but RegistryKey.SetValue doesn't like it so PInvoke to set it
            if (value == null) {
                RegSetValueEx(rootKey.GetKey().Handle, value_name, 0, type, null, 0);
                return;
            }

            if (regKind == RegistryValueKind.MultiString) {
                int size = ((PythonList)value)._size;
                string[] strArray = new string[size];
                Array.Copy(((PythonList)value)._data, strArray, size);
                rootKey.GetKey().SetValue(value_name, strArray, regKind);
            } else if (regKind == RegistryValueKind.Binary) {
                byte[] byteArr = null;
                if (value is string) {
                    string strValue = value as string;
                    ASCIIEncoding encoding = new ASCIIEncoding();
                    byteArr = encoding.GetBytes(strValue);
                }
                rootKey.GetKey().SetValue(value_name, byteArr, regKind);
            } else if (regKind == RegistryValueKind.DWord) {
                // DWORDs are uint but the .NET API requires int
                if (value is BigInteger) {
                    var val = (uint)(BigInteger)value;
                    value = unchecked((int)val);
                }
                rootKey.GetKey().SetValue(value_name, value, regKind);
            } else if (regKind == RegistryValueKind.QWord) {
                // QWORDs are ulong but the .NET API requires long
                if (value is BigInteger) {
                    var val = (ulong)(BigInteger)value;
                    value = unchecked((long)val);
                }
                rootKey.GetKey().SetValue(value_name, value, regKind);
            } else {
                rootKey.GetKey().SetValue(value_name, value, regKind);
            }

        }

        public static HKEYType ConnectRegistry(string computer_name, BigInteger key) {
            if (string.IsNullOrEmpty(computer_name))
                computer_name = string.Empty;

            RegistryKey newKey = null;
            try {
                if (computer_name == string.Empty) {
                    newKey = RegistryKey.OpenBaseKey(MapSystemKey(key), RegistryView.Default);
                }
                else {
                    newKey = RegistryKey.OpenRemoteBaseKey(MapSystemKey(key), computer_name);
                }
            }
            catch (IOException ioe) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonExceptions._OSError.ERROR_BAD_NETPATH, ioe.Message, null, PythonExceptions._OSError.ERROR_BAD_NETPATH);
            } catch (Exception e) {
                throw new ExternalException(e.Message);
            }
            return new HKEYType(newKey);
        }

        public static void DisableReflectionKey(object key) {
            HKEYType rootKey = GetRootKey(key);

            if (!Environment.Is64BitOperatingSystem) {
                throw new NotImplementedException("not implemented on this platform");
            }

            int dwRet = RegDisableReflectionKey(rootKey.GetKey().Handle);
            if (dwRet != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, dwRet);
            }
        }

        public static void EnableReflectionKey(object key) {
            HKEYType rootKey = GetRootKey(key);

            if (!Environment.Is64BitOperatingSystem) {
                throw new NotImplementedException("not implemented on this platform");
            }

            int dwRet = RegEnableReflectionKey(rootKey.GetKey().Handle);
            if (dwRet != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, dwRet);
            }
        }

        public static bool QueryReflectionKey(object key) {
            HKEYType rootKey = GetRootKey(key);
            bool isDisabled;

            if(!Environment.Is64BitOperatingSystem) {
                throw new NotImplementedException("not implemented on this platform");
            }

            int dwRet = RegQueryReflectionKey(rootKey.GetKey().Handle, out isDisabled);
            if (dwRet != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, dwRet);
            }
            return isDisabled;
        }

#endregion

#region Helpers
        private static HKEYType GetRootKey(object key) {
            HKEYType rootKey;
            rootKey = key as HKEYType;
            if (rootKey == null) {
                if (key is BigInteger) {
                    rootKey = new HKEYType(RegistryKey.OpenBaseKey(MapSystemKey((BigInteger)key), RegistryView.Default), (BigInteger)key);
                } else {
                    throw new InvalidCastException("The object is not a PyHKEY object");
                }
            }
            return rootKey;
        }

        private static RegistryHive MapSystemKey(BigInteger hKey) {
            const RegistryHive RegistryHiveDynData = (RegistryHive)unchecked((int)0x80000006);
            if (hKey == HKEY_CLASSES_ROOT)
                return RegistryHive.ClassesRoot;
            else if (hKey == HKEY_CURRENT_CONFIG)
                return RegistryHive.CurrentConfig;
            else if (hKey == HKEY_CURRENT_USER)
                return RegistryHive.CurrentUser;
            else if (hKey == HKEY_DYN_DATA)
                return RegistryHiveDynData;
            else if (hKey == HKEY_LOCAL_MACHINE)
                return RegistryHive.LocalMachine;
            else if (hKey == HKEY_PERFORMANCE_DATA)
                return RegistryHive.PerformanceData;
            else if (hKey == HKEY_USERS)
                return RegistryHive.Users;
            else
                throw new ValueErrorException("Unknown system key");
        }

        private static int MapRegistryValueKind(RegistryValueKind registryValueKind) {
            return (int)registryValueKind;
        }

#endregion


        [PythonType]
        public class HKEYType : IDisposable {
            private RegistryKey key;
            internal HKEYType(RegistryKey key) {
                this.key = key;
                HKeyHandleCache.cache[key.GetHashCode()] = new WeakReference(this);
            }

            internal readonly BigInteger hkey = 0;
            internal HKEYType(RegistryKey key, BigInteger hkey) : this(key) {
                this.hkey = hkey;
            }

            public void Close() {
                lock (this) {
                    if (key != null) {
                        HKeyHandleCache.cache.Remove(key.GetHashCode());
                        key.Dispose();
                        key = null;
                    }
                }
            }

            public int Detach() {
                return 0; //Can't keep handle after the object is destroyed.
            }

            public int handle {
                get {
                    lock (this) {
                        if (key == null) {
                            return 0;
                        }
                        return key.GetHashCode();
                    }
                }
            }

            public static implicit operator int(HKEYType hKey) {
                return hKey.handle;
            }

            /// <summary>
            /// Returns the underlying .NET RegistryKey
            /// </summary>
            /// <returns></returns>
            [PythonHidden]
            public RegistryKey GetKey() {
                lock (this) {
                    if (key == null) {
                        throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, "key has been closed");
                    }
                    return key;
                }
            }

#region IDisposable Members

            void IDisposable.Dispose() {
                Close();
            }

#endregion
        }
    }

    //CPython exposes the native handle for the registry keys as well. Since there is no .NET API to
    //expose the native handle, we return the hashcode of the key as the "handle". To track these handles 
    //and return the right RegistryKey we maintain this cache of the generated handles.
    internal static class HKeyHandleCache {
        internal static Dictionary<int, WeakReference> cache = new Dictionary<int, WeakReference>();

    }

}

#endif
