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

using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace IronPythonTest {
    /// <summary>
    /// Test cases for verifying IronPython plays nicely with reflection and you
    /// can reflect over python objects in the proper way.
    /// </summary>
    public class TypeDescTests {
        public object TestProperties(object totest, IList shouldContain, IList shouldntContain) {
            if (shouldContain != null) {
                foreach (object o in shouldContain) {
                    bool fFound = false;
                    foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(totest)) {
                        if ((string)o == pd.Name) {
                            fFound = true; break;
                        }
                    }

                    if (!fFound) return false;
                }
            }

            if (shouldntContain != null) {
                foreach (object o in shouldntContain) {
                    bool fFound = false;
                    foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(totest)) {
                        if ((string)o == pd.Name) {
                            fFound = true;
                            break;
                        }
                    }

                    if (fFound) return false;
                }
            }/*
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(totest)) {
                if (shouldContain != null && !shouldContain.Contains(pd.Name)) {
                    Console.WriteLine("No {0}", pd.Name);
                    return Ops.FALSE;
                } else if (shouldntContain != null && shouldntContain.Contains(pd.Name)) {
                    return Ops.FALSE;
                }
            }*/
            return true;
        }

        public object GetClassName(object totest) {
            return TypeDescriptor.GetClassName(totest);
        }

        public object GetComponentName(object totest) {
            return TypeDescriptor.GetComponentName(totest);
        }

        public object GetConverter(object totest) {
            return TypeDescriptor.GetConverter(totest);
        }

        public object GetDefaultEvent(object totest) {
            return TypeDescriptor.GetDefaultEvent(totest);
        }

        public object GetDefaultProperty(object totest) {
            return TypeDescriptor.GetDefaultProperty(totest);
        }

        public object GetEditor(object totest, Type editorBase) {
            return TypeDescriptor.GetEditor(totest, editorBase);
        }

        public object GetEvents(object totest) {
            return TypeDescriptor.GetEvents(totest);
        }
        public object GetEvents(object totest, Attribute[] attributes) {
            return TypeDescriptor.GetEvents(totest, attributes);
        }

        public object GetProperties(object totest) {
            return TypeDescriptor.GetProperties(totest);
        }

        public object GetProperties(object totest, Attribute[] attributes) {
            return TypeDescriptor.GetProperties(totest, attributes);
        }

        public object CallCanConvertToForInt(object totest) {
            return TypeDescriptor.GetConverter(totest).CanConvertTo(typeof(int));
        }
    }

    /// <summary>
    /// Registration-free COM activation
    /// </summary>

    [ComImport]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IClassFactory {
        [return: MarshalAs(UnmanagedType.Interface)]
        object CreateInstance(
            [MarshalAs(UnmanagedType.IUnknown)]object pOuterUnk,
            ref Guid iid);

        void LockServer(bool Lock);
    }

    public class ScriptPW {
        [DllImport("scriptpw.dll", PreserveSig = false)]
        static extern void DllGetClassObject(
            [In] ref Guid rclsid,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)][Out] out object ppv);


        [DllImport("ole32.dll", PreserveSig = false)]
        public static extern void CoRegisterClassObject(
            [In] ref Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            /* CLSCTX */ int dwClsContext,
            /* REGCLS */ int flags,
            out uint lpdwRegister
            );

        [DllImport("ole32.dll", PreserveSig = false)]
        public static extern void CoRevokeClassObject(uint dwRegister);

        static Guid ProgID_IPassword = new Guid("{834C5A62-E0BB-4FB4-87B9-F37C869C976B}");
        static Guid IID_IClassFactory = new Guid("{00000001-0000-0000-C000-000000000046}");
        static Guid IID_IUnknown = new Guid("{00000000-0000-0000-C000-000000000046}");

        public static object CreatePassword() {
            object classObject;
            DllGetClassObject(ref ProgID_IPassword, ref IID_IClassFactory, out classObject);

            uint dwRegister;
            CoRegisterClassObject(ref IID_IClassFactory, classObject, 0, 0, out dwRegister);

            IClassFactory classFactory = classObject as IClassFactory;
            object password = classFactory.CreateInstance(null, ref IID_IUnknown);
            return password;
        }

    }
}