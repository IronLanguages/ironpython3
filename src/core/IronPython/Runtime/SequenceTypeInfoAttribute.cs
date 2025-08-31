using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides more specific type information for Python lists which are not strongly typed.
    /// 
    /// This attribute can be applied to fields, parameters, proeprties, and return values.  It can be
    /// inspected to get type information about the types of the values of the expected 
    /// list or the returned list.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments"), AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class SequenceTypeInfoAttribute : Attribute {
        private readonly ReadOnlyCollection<Type> _types;
        
        public SequenceTypeInfoAttribute(params Type[] types) {
            _types = new ReadOnlyCollection<Type>(types);
        }

        public ReadOnlyCollection<Type> Types {
            get{
                return _types;
            }
        }
    }
}
