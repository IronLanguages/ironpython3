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

namespace IronPythonTest {
    // The sole purpose of this class is to provide many different methods of the same name that
    // differ only in their signature and/or their type parameters. Each returns a string that
    // uniquely identifies it so that we can validate the correct binding from the python side.
    // The instance and static methods have different names simply because C# doesn't allow two
    // methods to differ only by their "static-ness".
    public class GenMeth {
        public string InstMeth() {
            return "InstMeth()";
        }

        public string InstMeth<T>() {
            return "InstMeth<" + typeof(T).Name + ">()";
        }

        public string InstMeth<T, U>() {
            return "InstMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">()";
        }

        public string InstMeth(int arg1) {
            return "InstMeth(Int32)";
        }

        public string InstMeth(string arg1) {
            return "InstMeth(String)";
        }

        public string InstMeth<T>(int arg1) {
            return "InstMeth<" + typeof(T).Name + ">(Int32)";
        }

        public string InstMeth<T>(string arg1) {
            return "InstMeth<" + typeof(T).Name + ">(String)";
        }

        public string InstMeth<T, U>(int arg1) {
            return "InstMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">(Int32)";
        }

        public string InstMeth<T>(T arg1) {
            return "InstMeth<" + typeof(T).Name + ">(" + typeof(T).Name + ")";
        }

        public string InstMeth<T, U>(T arg1, U arg2) {
            return "InstMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">(" + typeof(T).Name + ", " + typeof(U).Name + ")";
        }

        public static string StaticMeth() {
            return "StaticMeth()";
        }

        public static string StaticMeth<T>() {
            return "StaticMeth<" + typeof(T).Name + ">()";
        }

        public static string StaticMeth<T, U>() {
            return "StaticMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">()";
        }

        public static string StaticMeth(int arg1) {
            return "StaticMeth(Int32)";
        }

        public static string StaticMeth(string arg1) {
            return "StaticMeth(String)";
        }

        public static string StaticMeth<T>(int arg1) {
            return "StaticMeth<" + typeof(T).Name + ">(Int32)";
        }

        public static string StaticMeth<T>(string arg1) {
            return "StaticMeth<" + typeof(T).Name + ">(String)";
        }

        public static string StaticMeth<T, U>(int arg1) {
            return "StaticMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">(Int32)";
        }

        public static string StaticMeth<T>(T arg1) {
            return "StaticMeth<" + typeof(T).Name + ">(" + typeof(T).Name + ")";
        }

        public static string StaticMeth<T, U>(T arg1, U arg2) {
            return "StaticMeth<" + typeof(T).Name + ", " + typeof(U).Name + ">(" + typeof(T).Name + ", " + typeof(U).Name + ")";
        }
    }
}
