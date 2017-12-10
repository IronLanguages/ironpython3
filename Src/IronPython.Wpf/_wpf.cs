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

#if FEATURE_WPF

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using System.Windows.Threading;

[assembly: PythonModule("_wpf", typeof(IronPython.Modules.Wpf))]
namespace IronPython.Modules {
    /// <summary>
    /// Provides helpers for interacting with Windows Presentation Foundation applications.
    /// </summary>
    public static class Wpf {
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.DomainManager.LoadAssembly(typeof(XamlReader).Assembly);            // PresentationFramework
            context.DomainManager.LoadAssembly(typeof(Clipboard).Assembly);             // PresentationCore
            context.DomainManager.LoadAssembly(typeof(DependencyProperty).Assembly);    // WindowsBase
            context.DomainManager.LoadAssembly(typeof(System.Xaml.XamlReader).Assembly);// System.Xaml
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided module.  Any named objects are assigned to the object.
        /// 
        /// The provided object is expected to be the same type as the root of the XAML element.
        /// </summary>
        public static object LoadComponent(CodeContext context, object self, string filename) {
            if (filename == null) {
                throw PythonOps.TypeError("expected str, got None");
            } else if (self == null) {
                throw PythonOps.TypeError("expected module, got None");
            }

            return DynamicXamlReader.LoadComponent(self, context.LanguageContext.Operations, filename, XamlReader.GetWpfSchemaContext());
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided module.  Any named objects are assigned to the object.
        /// 
        /// The provided object is expected to be the same type as the root of the XAML element.
        /// </summary>
        public static object LoadComponent(CodeContext context, object self, [NotNull]Stream stream) {
            if (self == null) {
                throw PythonOps.TypeError("expected module, got None");
            }

            return DynamicXamlReader.LoadComponent(self, context.LanguageContext.Operations, stream, XamlReader.GetWpfSchemaContext());
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided module.  Any named objects are assigned to the object.
        /// 
        /// The provided object is expected to be the same type as the root of the XAML element.
        /// </summary>
        public static object LoadComponent(CodeContext context, object self, [NotNull]XmlReader xmlReader) {
            if (self == null) {
                throw PythonOps.TypeError("expected module, got None");
            }

            return DynamicXamlReader.LoadComponent(self, context.LanguageContext.Operations, xmlReader, XamlReader.GetWpfSchemaContext());
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided module.  Any named objects are assigned to the object.
        /// 
        /// The provided object is expected to be the same type as the root of the XAML element.
        /// </summary>
        public static object LoadComponent(CodeContext context, object self, [NotNull]TextReader filename) {
            if (self == null) {
                throw PythonOps.TypeError("expected module, got None");
            }
            return DynamicXamlReader.LoadComponent(self, context.LanguageContext.Operations, filename, XamlReader.GetWpfSchemaContext());
        }

        /// <summary>
        /// Loads XAML from the specified XmlReader and returns the deserialized object.  Any event handlers
        /// are bound to methods defined in the provided module.  Any named objects are assigned to the object.
        /// 
        /// The provided object is expected to be the same type as the root of the XAML element.
        /// </summary>
        public static object LoadComponent(CodeContext context, object self, [NotNull]System.Xaml.XamlXmlReader reader) {
            if (self == null) {
                throw PythonOps.TypeError("expected module, got None");
            }
            return DynamicXamlReader.LoadComponent(self, context.LanguageContext.Operations, reader);
        }
    }
}

#endif
