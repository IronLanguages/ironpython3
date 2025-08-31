// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
