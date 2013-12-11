using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython {
    /// <summary>
    /// Provides more specific type information for Python dictionaries which are not strongly typed.
    /// 
    /// This attribute can be applied to fields, parameters, proeprties, and return values.  It can be
    /// inspected to get type information about the types of the keys and values of the expected 
    /// dictionary or the returned dictionary.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple=false)]
    public sealed class DictionaryTypeInfoAttribute : Attribute {
        private readonly Type _keyType, _valueType;

        public DictionaryTypeInfoAttribute(Type keyType, Type valueType) {
            _keyType = keyType;
            _valueType = valueType;
        }

        public Type KeyType {
            get{
                return _keyType;
            }
        }

        public Type ValueType {
            get{
                return _valueType;
            }
        }
    }
}
