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
    public class NestedClass {
        public string Field;

        public string CallMe() {
            return " Hello World";
        }

        public string Property {
            get {
                return Field;
            }
            set {
                Field = value;
            }
        }

        public class InnerClass {
            public string InnerField;

            public string CallMeInner() {
                return "Inner Hello World";
            }

            public string InnerProperty {
                get {
                    return InnerField;
                }
                set {
                    InnerField = value;
                }
            }

            public class TripleNested {
                public string TripleField;

                public string CallMeTriple() {
                    return "Triple Hello World";
                }

                public string TripleProperty {
                    get {
                        return TripleField;
                    }
                    set {
                        TripleField = value;
                    }
                }
            }
        }

        public class InnerGenericClass<T> {
            public string InnerGenericField;

            public string CallMeInnerGeneric() {
                return "InnerGeneric Hello World";
            }

            public string InnerGenericProperty {
                get {
                    return InnerGenericField;
                }
                set {
                    InnerGenericField = value;
                }
            }
        }

        public class InnerGenericClass<TA, TB> {
            public string InnerGenericField;

            public string CallMeInnerGeneric() {
                return "InnerGeneric Hello World";
            }

            public string InnerGenericProperty {
                get {
                    return InnerGenericField;
                }
                set {
                    InnerGenericField = value;
                }
            }
        }
    }
}
