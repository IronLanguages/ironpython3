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
#if FEATURE_REGISTRY //Registry not available in silverlight and we require .NET 4.0 APIs for implementing this.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

[assembly: PythonModule("_winreg", typeof(IronPython.Modules.PythonWinReg))]
namespace IronPython.Modules {
    public static class PythonWinReg {
        public const string __doc__ = "Provides access to the Windows registry.";

        public static PythonType error = PythonExceptions.WindowsError;

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

        public static HKEYType CreateKey(object key, string subKeyName) {
            HKEYType rootKey = GetRootKey(key);

            //if key is a system key and no subkey is specified return that.
            if (key is BigInteger && string.IsNullOrEmpty(subKeyName))
                return rootKey;
            
            HKEYType subKey = new HKEYType(rootKey.GetKey().CreateSubKey(subKeyName));
            return subKey;
        }


        public static HKEYType CreateKeyEx(object key, string subKeyName, int res, int sam) {
            HKEYType rootKey = GetRootKey(key);

            //if key is a system key and no subkey is specified return that.
            if (key is BigInteger && string.IsNullOrEmpty(subKeyName))
                return rootKey;

            SafeRegistryHandle handle;
            int disposition;
            
            int result = RegCreateKeyEx(
                rootKey.GetKey().Handle,
                subKeyName,
                0,
                null,
                RegistryOptions.None,
                (RegistryRights)sam,
                IntPtr.Zero,
                out handle,
                out disposition
            );
            if (result != ERROR_SUCCESS) {
                throw PythonExceptions.CreateThrowable(error, result, CTypes.FormatError(result));
            }

            return new HKEYType(RegistryKey.FromHandle(handle));
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int RegCreateKeyEx(
                    SafeRegistryHandle hKey,
                    string lpSubKey,
                    int Reserved,
                    string lpClass,
                    RegistryOptions dwOptions,
                    RegistryRights samDesired,
                    IntPtr lpSecurityAttributes,
                    out SafeRegistryHandle phkResult,
                    out int lpdwDisposition);

        [DllImport("advapi32.dll", SetLastError = true, CharSet=CharSet.Auto)]
        static extern int RegQueryValueEx(
              SafeRegistryHandle hKey,
              string lpValueName,
              IntPtr lpReserved,
              out int lpType,
              byte[] lpData,
              ref uint lpcbData
            );


        public static void DeleteKey(object key, string subKeyName) {
            HKEYType rootKey = GetRootKey(key);
            if (key is BigInteger && string.IsNullOrEmpty(subKeyName))
                throw new InvalidCastException("DeleteKey() argument 2 must be string, not None");

            try {
                rootKey.GetKey().DeleteSubKey(subKeyName);
            } catch (ArgumentException e) {
                throw new ExternalException(e.Message);
            }
        }

        public static void DeleteValue(object key, string value) {
            HKEYType rootKey = GetRootKey(key);

            rootKey.GetKey().DeleteValue(value, true);
        }

        public static string EnumKey(object key, int index) {
            HKEYType rootKey = GetRootKey(key);
            if (index >= rootKey.GetKey().SubKeyCount) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, PythonExceptions._WindowsError.ERROR_BAD_COMMAND, "No more data is available");
            }
            return rootKey.GetKey().GetSubKeyNames()[index];
        }

        const int ERROR_MORE_DATA = 234;
        const int ERROR_SUCCESS = 0;

        public static PythonTuple EnumValue(object key, int index) {
            HKEYType rootKey = GetRootKey(key);
            if (index >= rootKey.GetKey().ValueCount) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, PythonExceptions._WindowsError.ERROR_BAD_COMMAND, "No more data is available");
            }

            var nativeRootKey = rootKey.GetKey();
            string valueName = nativeRootKey.GetValueNames()[index];

            int valueKind;
            object value;
            QueryValueExImpl(nativeRootKey, valueName, out valueKind, out value);
            return PythonTuple.MakeTuple(valueName, value, valueKind);
        }

        private static void QueryValueExImpl(RegistryKey nativeRootKey, string valueName, out int valueKind, out object value) {
            valueKind = 0;
            int dwRet;
            byte[] data = new byte[128];
            uint length = (uint)data.Length;
            // query the size first, reading the data as we query...
            dwRet = RegQueryValueEx(nativeRootKey.Handle, valueName, IntPtr.Zero, out valueKind, data, ref length);
            while (dwRet == ERROR_MORE_DATA) {
                data = new byte[data.Length * 2];
                length = (uint)data.Length;
                dwRet = RegQueryValueEx(nativeRootKey.Handle, valueName, IntPtr.Zero, out valueKind, data, ref length);
            }

            // convert the result into a Python object

            switch (valueKind) {
                case REG_MULTI_SZ:
                    List list = new List();
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
                    value = PythonOps.MakeString(data, (int)length);
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
                        value = ((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
                    } else {
                        value = ((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
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

        public static HKEYType OpenKey(object key, string subKeyName) {
            return OpenKey(key, subKeyName, 0, KEY_READ);
        }

        public static HKEYType OpenKey(object key, string subKeyName, [DefaultParameterValue(0)]int res, [DefaultParameterValue(KEY_READ)]int sam) {
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
                if ((sam & KEY_SET_VALUE) == KEY_SET_VALUE ||
                    (sam & KEY_CREATE_SUB_KEY) == KEY_CREATE_SUB_KEY) {
                        if (res != 0) {
                            newKey = nativeRootKey.OpenSubKey(subKeyName, RegistryKeyPermissionCheck.Default, (RegistryRights)res);
                        } else {
                            newKey = nativeRootKey.OpenSubKey(subKeyName, true);
                        }
                } else if ((sam & KEY_QUERY_VALUE) == KEY_QUERY_VALUE ||
                           (sam & KEY_ENUMERATE_SUB_KEYS) == KEY_ENUMERATE_SUB_KEYS ||
                           (sam & KEY_NOTIFY) == KEY_NOTIFY) {
                               if (res != 0) {
                                   newKey = nativeRootKey.OpenSubKey(subKeyName, RegistryKeyPermissionCheck.ReadSubTree, (RegistryRights)res);
                               } else {
                                   newKey = nativeRootKey.OpenSubKey(subKeyName, false);
                               }
                } else {
                    throw new Win32Exception("Unexpected mode");
                }
            } catch (SecurityException) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, PythonExceptions._WindowsError.ERROR_ACCESS_DENIED, "Access is denied");
            }


            if (newKey == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, PythonExceptions._WindowsError.ERROR_FILE_NOT_FOUND, "The system cannot find the file specified");
            }

            return new HKEYType(newKey);
        }

        public static HKEYType OpenKeyEx(object key, string subKeyName, [DefaultParameterValue(0)]int res, [DefaultParameterValue(KEY_READ)]int sam) {
            return OpenKey(key, subKeyName, res, sam);
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
                throw PythonExceptions.CreateThrowable(PythonExceptions.EnvironmentError, "key has been closed");
            }

            try {
                var nativeRootKey = rootKey.GetKey();
                return PythonTuple.MakeTuple(nativeRootKey.SubKeyCount, nativeRootKey.ValueCount, 0);
            } catch (ObjectDisposedException e) {
                throw new ExternalException(e.Message);
            }
        }

        public static object QueryValue(object key, string subKeyName) {
            HKEYType pyKey = OpenKey(key, subKeyName);
            return pyKey.GetKey().GetValue(null);
        }

        public static PythonTuple QueryValueEx(object key, string valueName) {
            HKEYType rootKey = GetRootKey(key);

            int valueKind;
            object value;
            QueryValueExImpl(rootKey.GetKey(), valueName, out valueKind, out value);

            return PythonTuple.MakeTuple(value, valueKind);
        }

        public static void SetValue(object key, string subKeyName, int type, string value) {
            HKEYType pyKey = CreateKey(key, subKeyName);
            pyKey.GetKey().SetValue(null, value);
        }

        public static void SetValueEx(object key, string valueName, int reserved, int type, object value) {
            HKEYType rootKey = GetRootKey(key);
            RegistryValueKind regKind = (RegistryValueKind)type;

            if (regKind == RegistryValueKind.MultiString) {
                int size = ((List)value)._size;
                string[] strArray = new string[size];
                Array.Copy(((List)value)._data, strArray, size);
                rootKey.GetKey().SetValue(valueName, strArray, regKind);
            } else if (regKind == RegistryValueKind.Binary) {
                byte[] byteArr = null;
                if (value is string) {
                    string strValue = value as string;
                    ASCIIEncoding encoding = new ASCIIEncoding();
                    byteArr = encoding.GetBytes(strValue);
                }
                rootKey.GetKey().SetValue(valueName, byteArr, regKind);
            } else {
                rootKey.GetKey().SetValue(valueName, value, regKind);
            }

        }

        public static HKEYType ConnectRegistry(string computerName, BigInteger key) {
            if (string.IsNullOrEmpty(computerName))
                computerName = string.Empty;

            RegistryKey newKey;
            try {
                newKey = RegistryKey.OpenRemoteBaseKey(MapSystemKey(key), computerName);
            }catch(IOException ioe) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, PythonExceptions._WindowsError.ERROR_BAD_NETPATH, ioe.Message);
            } catch (Exception e) {
                throw new ExternalException(e.Message);
            }
            return new HKEYType(newKey);
        }
#endregion

#region Helpers
        private static HKEYType GetRootKey(object key) {
            HKEYType rootKey;
            rootKey = key as HKEYType;
            if (rootKey == null) {
                if (key is BigInteger) {
                    rootKey = new HKEYType(RegistryKey.OpenRemoteBaseKey(MapSystemKey((BigInteger)key), string.Empty));
                } else {
                    throw new InvalidCastException("The object is not a PyHKEY object");
                }
            }
            return rootKey;
        }

        private static RegistryHive MapSystemKey(BigInteger hKey) {
            if (hKey == HKEY_CLASSES_ROOT)
                return RegistryHive.ClassesRoot;
            else if (hKey == HKEY_CURRENT_CONFIG)
                return RegistryHive.CurrentConfig;
            else if (hKey == HKEY_CURRENT_USER)
                return RegistryHive.CurrentUser;
            else if (hKey == HKEY_DYN_DATA)
                return RegistryHive.DynData;
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

            public void Close() {
                lock (this) {
                    if (key != null) {
                        HKeyHandleCache.cache.Remove(key.GetHashCode());
                        key.Close();
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
                        throw PythonExceptions.CreateThrowable(PythonExceptions.EnvironmentError, "key has been closed");
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