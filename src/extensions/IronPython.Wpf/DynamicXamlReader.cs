// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Xaml;
using System.Xaml.Schema;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;

using Microsoft.Scripting.Runtime;

namespace Microsoft.Internal.Scripting.Runtime {
    /// <summary>
    /// Provides services for loading XAML and binding events to dynamic language code definitions.
    /// </summary>
    internal static class DynamicXamlReader {
        /// <summary>
        /// Loads XAML from the specified stream and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided Scope and converted using the provided DynamicOperations
        /// object.
        /// </summary>
        public static object LoadComponent(dynamic scope, DynamicOperations operations, Stream stream, XamlSchemaContext schemaContext) {
            return LoadComponent((object)scope, operations, new XamlXmlReader(stream, schemaContext ?? new XamlSchemaContext()));
        }

        /// <summary>
        /// Loads XAML from the specified filename and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided Scope and converted using the provided DynamicOperations
        /// object.
        /// </summary>
        public static object LoadComponent(dynamic scope, DynamicOperations operations, string filename, XamlSchemaContext schemaContext) {
            using (var file = new StreamReader(filename)) {
                return LoadComponent((object)scope, operations, new XamlXmlReader(file, schemaContext ?? new XamlSchemaContext()));
            }
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided Scope and converted using the provided DynamicOperations
        /// object.
        /// </summary>
        public static object LoadComponent(dynamic scope, DynamicOperations operations, XmlReader reader, XamlSchemaContext schemaContext) {
            return LoadComponent((object)scope, operations, new XamlXmlReader(reader, schemaContext ?? new XamlSchemaContext()));
        }

        /// <summary>
        /// Loads XAML from the specified TextReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided Scope and converted using the provided DynamicOperations
        /// object.
        /// </summary>
        public static object LoadComponent(dynamic scope, DynamicOperations operations, TextReader reader, XamlSchemaContext schemaContext) {
            return LoadComponent((object)scope, operations, new XamlXmlReader(reader, schemaContext ?? new XamlSchemaContext()));
        }

        /// <summary>
        /// Loads XAML from the specified XamlXmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided Scope and converted using the provided DynamicOperations
        /// object.
        /// </summary>
        public static object LoadComponent(dynamic scope, DynamicOperations operations, XamlXmlReader reader) {
            var settings = new XamlObjectWriterSettings();
            settings.RootObjectInstance = scope;

            var myWriter = new DynamicWriter((object)scope, operations, reader.SchemaContext, settings);
            while (reader.Read()) {
                myWriter.WriteNode(reader);
            }

            foreach (string name in myWriter.Names) {
                object value = myWriter.RootNameScope.FindName(name);
                if (value != null) {
                    operations.SetMember((object)scope, name, value);
                }
            }
            
            return myWriter.Result;
        }

        private class DynamicWriter : XamlObjectWriter {
            private readonly object _scope;
            private readonly DynamicOperations _operations;
            private readonly Stack<bool> _nameStack = new Stack<bool>();
            private HashSet<string> _names = new HashSet<string>();

            public DynamicWriter(object scope, DynamicOperations operations, XamlSchemaContext context, XamlObjectWriterSettings settings)
                : base(context, settings) {
                _scope = scope;
                _operations = operations;
            }

            /// <summary>
            /// Returns the list of x:Name'd objects that we saw and should set on the root object.
            /// </summary>
            public IEnumerable<string> Names {
                get {
                    return _names;
                }
            }

            /// <summary>
            /// Dummy, should never be called
            /// </summary>
            public static void Adder(object inst, object args) {
                throw new InvalidOperationException();
            }

            private static MethodInfo Dummy = new Action<object, object>(Adder).Method;

            private class DynamicEventMember : XamlMember {
                public DynamicEventMember(DynamicWriter writer, EventInfo eventInfo, XamlSchemaContext ctx)
                    : base(eventInfo.Name, Dummy, ctx, new DynamicEventInvoker(eventInfo, writer)) {
                }
            }

            private class DynamicEventInvoker : XamlMemberInvoker {
                private readonly DynamicWriter _writer;
                private readonly EventInfo _info;

                public DynamicEventInvoker(EventInfo info, DynamicWriter writer) {
                    _writer = writer;
                    _info = info;
                }

                public override void SetValue(object instance, object value) {
                    object target = _writer._operations.GetMember(_writer._scope, (string)value);

                    _info.AddEventHandler(instance, (Delegate)_writer._operations.ConvertTo(target, _info.EventHandlerType));
                }
            }

            public override void WriteValue(object value) {
                string strValue;
                if (_nameStack.Peek() && (strValue = value as string) != null) {
                    // we are writing a x:Name, save it so we can later get the name from the scope
                    _names.Add(strValue);
                }
                base.WriteValue(value);
            }
            
            public override void WriteEndMember() {
                _nameStack.Pop();
                base.WriteEndMember();
            }

            public override void WriteStartMember(XamlMember property) {
                // we don't check the namespace for the property here - it can be x:Name or it can be Name 
                // on the underlying type.  WPF supports either one and so do we.
                if (property.Name == "Name" && property.Type.UnderlyingType == typeof(string)) {
                    _nameStack.Push(true);
                } else {
                    _nameStack.Push(false);
                }

                if (property.UnderlyingMember != null && property.UnderlyingMember.MemberType == System.Reflection.MemberTypes.Event) {
                    base.WriteStartMember(new DynamicEventMember(this, (EventInfo)property.UnderlyingMember, SchemaContext));
                } else {
                    base.WriteStartMember(property);
                }
            }            
        }
    }
}

#endif
