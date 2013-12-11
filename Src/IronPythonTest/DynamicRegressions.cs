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

#if !CLR2
namespace IronPythonTest {
    public static class DynamicRegressions {
        
        public static string cp24117(dynamic inputObj){
            return inputObj.ToString();
        }

        public static void cp24118(dynamic pythonCode) {
            dynamic testObj = pythonCode.GetMethodTest();

            // calling method with normal params
            NamedMethod01(testObj);
            // calling method with optional params
            NamedMethod02(testObj);
        }

        private static void NamedMethod01(dynamic testObj) {
            try
            {
                System.Console.WriteLine("1)-1 Exp=33, Act={0}", testObj.Normal01(a: 11, b: 22));
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("1) a:v, b:v => {0}", e);
                System.Console.WriteLine("=====================================");
            }

            System.Console.WriteLine("1)-2 Exp=38, Act={0}", testObj.Normal01(b: 33, a: 5)); // OK
            System.Console.WriteLine("=====================================");
        }

        private static void NamedMethod02(dynamic testObj) {
            // b=1
            System.Console.WriteLine("2)-1 Exp=11, Act={0}", testObj.Optional01(a: 10)); // OK
            try
            {
                System.Console.WriteLine("2)-2 Exp=33, Act={0}", testObj.Optional01(a: 11, b: 22));
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("2) a:v, b:v => {0}", e);
                System.Console.WriteLine("=====================================");
            }
            System.Console.WriteLine("2)-3 Exp=38, Act={0}", testObj.Optional01(b: 33, a: 5)); // OK
        }
    

        public static void cp24115(dynamic testObj) {
            try {
                testObj.x();
                throw new Exception("Invoking non-existent method 'x' should have thrown");
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                if (!ex.Message.Contains("'x'")) {
                    throw new Exception("Error message didn't contain 'x'", ex);
                }
            }
        }

        public static bool cp24111(dynamic testObj)
        {
            return !testObj;
        }

        public static void cp24088(dynamic testObj)
        {
            testObj += 3;
        }
    }
}
#endif
