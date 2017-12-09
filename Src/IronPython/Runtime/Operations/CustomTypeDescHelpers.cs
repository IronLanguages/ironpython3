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

#if FEATURE_CUSTOM_TYPE_DESCRIPTOR

using System;
using System.Collections.Generic;
using System.ComponentModel;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions.Calls;

namespace IronPython.Runtime.Operations {
    /// <summary>
    /// Helper class that all custom type descriptor implementations call for
    /// the bulk of their implementation.
    /// </summary>
    public static class CustomTypeDescHelpers {
        #region ICustomTypeDescriptor helper functions

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self")]
        public static AttributeCollection GetAttributes(object self) {
            return AttributeCollection.Empty;
        }

        public static string GetClassName(object self) {
            object cls;
            if (PythonOps.TryGetBoundAttr(DefaultContext.DefaultCLS, self, "__class__", out cls)) {
                return PythonOps.GetBoundAttr(DefaultContext.DefaultCLS, cls, "__name__").ToString();
            }
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self")]
        public static string GetComponentName(object self) {
            return null;
        }

        public static TypeConverter GetConverter(object self) {
            return new TypeConv(self);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self")]
        public static EventDescriptor GetDefaultEvent(object self) {
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self")]
        public static PropertyDescriptor GetDefaultProperty(object self) {
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "editorBaseType")]
        public static object GetEditor(object self, Type editorBaseType) {
            return null;
        }

        public static EventDescriptorCollection GetEvents(object self, Attribute[] attributes) {
            if (attributes == null || attributes.Length == 0) return GetEvents(self);
            //!!! update when we support attributes on python types

            // you want things w/ attributes?  we don't have attributes!
            return EventDescriptorCollection.Empty;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "self")]
        public static EventDescriptorCollection GetEvents(object self) {
            return EventDescriptorCollection.Empty;
        }

        public static PropertyDescriptorCollection GetProperties(object self) {
            return new PropertyDescriptorCollection(GetPropertiesImpl(self, new Attribute[0]));
        }

        public static PropertyDescriptorCollection GetProperties(object self, Attribute[] attributes) {
            return new PropertyDescriptorCollection(GetPropertiesImpl(self, attributes));
        }

        static PropertyDescriptor[] GetPropertiesImpl(object self, Attribute[] attributes) {
            IList<object> attrNames = PythonOps.GetAttrNames(DefaultContext.DefaultCLS, self);
            List<PropertyDescriptor> descrs = new List<PropertyDescriptor>();
            if (attrNames != null) {
                foreach (object o in attrNames) {
                    string s = o as string;
                    if (s == null) continue;

                    PythonTypeSlot attrSlot;
                    PythonType dt = DynamicHelpers.GetPythonType(self);
                    dt.TryResolveSlot(DefaultContext.DefaultCLS, s, out attrSlot);
                    object attrVal = ObjectOps.__getattribute__(DefaultContext.DefaultCLS, self, s);

                    Type attrType = (attrVal == null) ? typeof(NoneTypeOps) : attrVal.GetType();

                    if ((attrSlot != null && ShouldIncludeProperty(attrSlot, attributes)) ||
                        (attrSlot == null && ShouldIncludeInstanceMember(s, attributes))) {
                        descrs.Add(new SuperDynamicObjectPropertyDescriptor(s, attrType, self.GetType()));
                    }
                }
            }

            return descrs.ToArray();
        }

        private static bool ShouldIncludeInstanceMember(string memberName, Attribute[] attributes) {
            bool include = true;
            foreach (Attribute attr in attributes) {
                if (attr.GetType() == typeof(BrowsableAttribute)) {
                    if (memberName.StartsWith("__") && memberName.EndsWith("__")) {
                        include = false;
                    }
                } else {
                    // unknown attribute, Python doesn't support attributes, so we
                    // say this doesn't have that attribute.
                    include = false;
                }
            }
            return include;
        }

        private static bool ShouldIncludeProperty(PythonTypeSlot attrSlot, Attribute[] attributes) {
            bool include = true;
            foreach (Attribute attr in attributes) {
                ReflectedProperty rp;

                if ((rp = attrSlot as ReflectedProperty) != null && rp.Info != null) {
                    include &= rp.Info.IsDefined(attr.GetType(), true);
                } else if (attr.GetType() == typeof(BrowsableAttribute)) {
                    PythonTypeUserDescriptorSlot userSlot = attrSlot as PythonTypeUserDescriptorSlot;
                    if (userSlot == null) {
                        if (!(attrSlot is PythonProperty)) {
                            include = false;
                        }
                    } else if (!(userSlot.Value is IPythonObject)) {
                        include = false;
                    }
                } else {
                    // unknown attribute, Python doesn't support attributes, so we
                    // say this doesn't have that attribute.
                    include = false;
                }
            }
            return include;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "pd")]
        public static object GetPropertyOwner(object self, PropertyDescriptor pd) {
            return self;
        }

        #endregion

        class SuperDynamicObjectPropertyDescriptor : PropertyDescriptor {
            string _name;
            Type _propertyType;
            Type _componentType;
            internal SuperDynamicObjectPropertyDescriptor(
                string name,
                Type propertyType,
                Type componentType)
                : base(name, null) {
                _name = name;
                _propertyType = propertyType;
                _componentType = componentType;
            }

            public override object GetValue(object component) {
                return PythonOps.GetBoundAttr(DefaultContext.DefaultCLS, component, _name);
            }
            public override void SetValue(object component, object value) {
                PythonOps.SetAttr(DefaultContext.DefaultCLS, component, _name, value);
            }

            public override bool CanResetValue(object component) {
                return true;
            }

            public override Type ComponentType {
                get { return _componentType; }
            }

            public override bool IsReadOnly {
                get { return false; }
            }

            public override Type PropertyType {
                get { return _propertyType; }
            }

            public override void ResetValue(object component) {
                PythonOps.DeleteAttr(DefaultContext.DefaultCLS, component, _name);
            }

            public override bool ShouldSerializeValue(object component) {
                object o;
                return PythonOps.TryGetBoundAttr(component, _name, out o);
            }
        }

        private class TypeConv : TypeConverter {
            object convObj;

            public TypeConv(object self) {
                convObj = self;
            }

            #region TypeConverter overrides
            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) {
                object result;
                return Converter.TryConvert(convObj, destinationType, out result);
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
                return Converter.CanConvertFrom(sourceType, convObj.GetType(), NarrowingLevel.All);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value) {
                return Converter.Convert(value, convObj.GetType());
            }

            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType) {
                return Converter.Convert(convObj, destinationType);
            }

            public override bool GetCreateInstanceSupported(ITypeDescriptorContext context) {
                return false;
            }

            public override bool GetPropertiesSupported(ITypeDescriptorContext context) {
                return false;
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) {
                return false;
            }

            public override bool IsValid(ITypeDescriptorContext context, object value) {
                object result;
                return Converter.TryConvert(value, convObj.GetType(), out result);
            }
            #endregion
        }
    }
}

#endif
