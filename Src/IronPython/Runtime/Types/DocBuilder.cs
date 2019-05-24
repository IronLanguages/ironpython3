// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_XMLDOC
using System.Xml;
using System.Xml.XPath;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    static class DocBuilder {
        
        internal static string GetDefaultDocumentation(string methodName) {
            switch (methodName) {
                case "__abs__": return "x.__abs__() <==> abs(x)";
                case "__add__": return "x.__add__(y) <==> x+y";
                case "__call__": return "x.__call__(...) <==> x(...)";
                case "__cmp__": return "x.__cmp__(y) <==> cmp(x,y)";
                case "__delitem__": return "x.__delitem__(y) <==> del x[y]";
                case "__eq__": return "x.__eq__(y) <==> x==y";
                case "__floordiv__": return "x.__floordiv__(y) <==> x//y";
                case "__getitem__": return "x.__getitem__(y) <==> x[y]";
                case "__gt__": return "x.__gt__(y) <==> x>y";
                case "__hash__": return "x.__hash__() <==> hash(x)";
                case "__init__": return "x.__init__(...) initializes x; see x.__class__.__doc__ for signature";
                case "__len__": return "x.__len__() <==> len(x)";
                case "__lshift__": return "x.__rshift__(y) <==> x<<y";
                case "__lt__": return "x.__lt__(y) <==> x<y";
                case "__mod__": return "x.__mod__(y) <==> x%y";
                case "__mul__": return "x.__mul__(y) <==> x*y";
                case "__neg__": return "x.__neg__() <==> -x";
                case "__pow__": return "x.__pow__(y[, z]) <==> pow(x, y[, z])";
                case "__reduce__":
                case "__reduce_ex__": return "helper for pickle";
                case "__rshift__": return "x.__rshift__(y) <==> x>>y";
                case "__setitem__": return "x.__setitem__(i, y) <==> x[i]=";
                case "__str__": return "x.__str__() <==> str(x)";
                case "__sub__": return "x.__sub__(y) <==> x-y";
                case "__truediv__": return "x.__truediv__(y) <==> x/y";
            }

            return null;
        }

        private static string DocOneInfoForProperty(Type declaringType, string propertyName, MethodInfo getter, MethodInfo setter, IEnumerable<DocumentationAttribute> attrs) {
            if (!attrs.Any()) {
                StringBuilder autoDoc = new StringBuilder();

                string summary = null;
#if FEATURE_XMLDOC
                string returns = null;
                GetXmlDocForProperty(declaringType, propertyName, out summary, out returns);
#endif

                if (summary != null) {
                    autoDoc.AppendLine(summary);
                    autoDoc.AppendLine();
                }

                if (getter != null) {
                    autoDoc.Append("Get: ");
                    autoDoc.AppendLine(CreateAutoDoc(getter, propertyName, 0));
                }

                if (setter != null) {
                    autoDoc.Append("Set: ");
                    autoDoc.Append(CreateAutoDoc(setter, propertyName, 1));
                    autoDoc.AppendLine(" = value");
                }
                return autoDoc.ToString();
            }

            StringBuilder docStr = new StringBuilder();
            foreach (var attr in attrs) {
                docStr.Append(attr.Documentation);
                docStr.Append(Environment.NewLine);
            }
            return docStr.ToString();
        }

        public static string DocOneInfo(ExtensionPropertyInfo info) {
            return DocOneInfoForProperty(info.DeclaringType, info.Name, info.Getter, info.Setter, Enumerable.Empty<DocumentationAttribute>());
        }

        public static string DocOneInfo(PropertyInfo info) {
            var attrs = info.GetCustomAttributes<DocumentationAttribute>();
            return DocOneInfoForProperty(info.DeclaringType, info.Name, info.GetGetMethod(), info.GetSetMethod(), attrs);
        }

        public static string DocOneInfo(FieldInfo info) {
            var attrs = info.GetCustomAttributes<DocumentationAttribute>();
            return DocOneInfoForProperty(info.DeclaringType, info.Name, null, null, attrs);
        }

        public static string DocOneInfo(MethodBase info, string name) {
            return DocOneInfo(info, name, true);
        }

        public static string DocOneInfo(MethodBase info, string name, bool includeSelf) {
            // Look for methods tagged with [Documentation("doc string for foo")]
            var attr = info.GetCustomAttributes<DocumentationAttribute>().SingleOrDefault();
            if (attr != null) {
                return attr.Documentation;
            }

            string defaultDoc = GetDefaultDocumentation(name);
            if (defaultDoc != null) {
                return defaultDoc;
            }

            return CreateAutoDoc(info, name, 0, includeSelf);
        }

        public static string CreateAutoDoc(MethodBase info) {
            return CreateAutoDoc(info, null, 0);
        }

        public static string CreateAutoDoc(EventInfo info) {
            string summary = null;
#if FEATURE_XMLDOC
            string returns;
            GetXmlDoc(info, out summary, out returns);
#endif
            return summary;
        }

        public static string CreateAutoDoc(Type t) {
            string summary = null;

#if FEATURE_XMLDOC
            GetXmlDoc(t, out summary);
#endif
            if (t.IsEnum) {
                string[] names = Enum.GetNames(t);
                Array values = Enum.GetValues(t);
                for (int i = 0; i < names.Length; i++) {
                    names[i] = String.Concat(names[i],
                        " (",
                        Convert.ChangeType(values.GetValue(i), Enum.GetUnderlyingType(t), null).ToString(),
                        ")");
                }

                Array.Sort<string>(names);

                summary = String.Concat(
                    summary,
                    Environment.NewLine, Environment.NewLine, 
                    "enum ", t.IsDefined(typeof(FlagsAttribute), false) ? "(flags) ": "", GetPythonTypeName(t), ", values: ", 
                    String.Join(", ", names));
            }
            return summary;
        }

        public static OverloadDoc GetOverloadDoc(MethodBase info, string name, int endParamSkip) {
            return GetOverloadDoc(info, name, endParamSkip, true);
        }

        /// <summary>
        /// Creates a DLR OverloadDoc object which describes information about this overload.
        /// </summary>
        /// <param name="info">The method to document</param>
        /// <param name="name">The name of the method if it should override the name in the MethodBase</param>
        /// <param name="endParamSkip">Parameters to skip at the end - used for removing the value on a setter method</param>
        /// <param name="includeSelf">true to include self on instance methods</param>
        public static OverloadDoc GetOverloadDoc(MethodBase info, string name, int endParamSkip, bool includeSelf) {
            string summary = null, returns = null;
            List<KeyValuePair<string, string>> parameters = null;

#if FEATURE_XMLDOC
            GetXmlDoc(info, out summary, out returns, out parameters);
#endif

            StringBuilder retType = new StringBuilder();

            int returnCount = 0;
            MethodInfo mi = info as MethodInfo;
            if (mi != null) {
                if (mi.ReturnType != typeof(void)) {
                    retType.Append(GetPythonTypeName(mi.ReturnType));
                    returnCount++;

                    var typeAttrs = mi.ReturnParameter.GetCustomAttributes(typeof(SequenceTypeInfoAttribute), true);
                    if (typeAttrs.Length > 0) {
                        retType.Append(" (of ");
                        SequenceTypeInfoAttribute typeAttr = (SequenceTypeInfoAttribute)typeAttrs[0];
                        for (int curTypeAttr = 0; curTypeAttr < typeAttr.Types.Count; curTypeAttr++) {
                            if (curTypeAttr != 0) {
                                retType.Append(", ");
                            }

                            retType.Append(GetPythonTypeName(typeAttr.Types[curTypeAttr]));
                        }
                        retType.Append(")");
                    }

                    var dictTypeAttrs = mi.ReturnParameter.GetCustomAttributes(typeof(DictionaryTypeInfoAttribute), true);
                    if (dictTypeAttrs.Length > 0) {
                        var dictTypeAttr = (DictionaryTypeInfoAttribute)dictTypeAttrs[0];
                        retType.Append(String.Format(" (of {0} to {1})", GetPythonTypeName(dictTypeAttr.KeyType), GetPythonTypeName(dictTypeAttr.ValueType)));
                    }
                }

                if (name == null) {
                    var hashIndex = mi.Name.IndexOf('#');
                    if (hashIndex == -1) {
                        name = mi.Name;
                    } else {
                        name = mi.Name.Substring(0, hashIndex);
                    }
                }
            } else if(name == null) {
                name = "__new__";
            }

            // For generic methods display either type parameters (for unbound methods) or
            // type arguments (for bound ones).
            if (mi != null && mi.IsGenericMethod) {
                Type[] typePars = mi.GetGenericArguments();
                bool unbound = mi.ContainsGenericParameters;
                StringBuilder tmp = new StringBuilder();
                tmp.Append(name);
                tmp.Append("[");
                if (typePars.Length > 1)
                    tmp.Append("(");

                bool insertComma = false;
                foreach (Type t in typePars) {
                    if (insertComma)
                        tmp.Append(", ");
                    if (unbound)
                        tmp.Append(t.Name);
                    else
                        tmp.Append(GetPythonTypeName(t));
                    insertComma = true;
                }

                if (typePars.Length > 1)
                    tmp.Append(")");
                tmp.Append("]");
                name = tmp.ToString();
            }

            List<ParameterDoc> paramDoc = new List<ParameterDoc>();
            if (mi == null) {
                if (name == "__new__") {
                    // constructor, auto-insert cls
                    paramDoc.Add(new ParameterDoc("cls", "type"));
                }
            } else if (!mi.IsStatic && includeSelf) {
                paramDoc.Add(new ParameterDoc("self", GetPythonTypeName(mi.DeclaringType)));
            }

            ParameterInfo[] pis = info.GetParameters();
            for (int i = 0; i < pis.Length - endParamSkip; i++) {
                ParameterInfo pi = pis[i];
                if (i == 0 && pi.ParameterType == typeof(CodeContext)) {
                    // hide CodeContext parameters
                    continue;
                }

                if ((pi.Attributes & ParameterAttributes.Out) == ParameterAttributes.Out || pi.ParameterType.IsByRef) {
                    if (returnCount == 1) {
                        retType.Insert(0, "(");
                    }

                    if (returnCount != 0) retType.Append(", ");

                    returnCount++;

                    retType.Append(GetPythonTypeName(pi.ParameterType));

                    if ((pi.Attributes & ParameterAttributes.Out) == ParameterAttributes.Out) continue;
                }

                ParameterFlags flags = ParameterFlags.None;
                if (pi.IsDefined(typeof(ParamArrayAttribute), false)) {
                    flags |= ParameterFlags.ParamsArray;
                } else if (pi.IsDefined(typeof(ParamDictionaryAttribute), false)) {
                    flags |= ParameterFlags.ParamsDict;
                }

                string paramDocString = null;
                if (parameters != null) {
                    foreach (var paramXmlDoc in parameters) {
                        if (paramXmlDoc.Key == pi.Name) {
                            paramDocString = paramXmlDoc.Value;
                            break;
                        }
                    }
                }

                paramDoc.Add(
                    new ParameterDoc(
                        pi.Name ?? "",  // manufactured methods, such as string[].ctor(int) can have no parameter names.
                        pi.ParameterType.IsGenericParameter ? pi.ParameterType.Name : GetPythonTypeName(pi.ParameterType),
                        paramDocString,
                        flags
                    )
                );
            }
            
            if (returnCount > 1) {
                retType.Append(')');
            }
            
            ParameterDoc retDoc = new ParameterDoc(String.Empty, retType.ToString(), returns);

            return new OverloadDoc(
                name,
                summary,
                paramDoc,
                retDoc
            );
        }

        internal static string CreateAutoDoc(MethodBase info, string name, int endParamSkip) {
            return CreateAutoDoc(info, name, endParamSkip, true);
        }

        internal static string CreateAutoDoc(MethodBase info, string name, int endParamSkip, bool includeSelf) {
#if FEATURE_FULL_CONSOLE
            int lineWidth;
            try {
                lineWidth = Console.WindowWidth - 30;
            } catch {
                // console output has been redirected.
                lineWidth = 80;
            }
#else
            int lineWidth = 80;
#endif
            var docInfo = GetOverloadDoc(info, name, endParamSkip, includeSelf);
            StringBuilder ret = new StringBuilder();

            ret.Append(docInfo.Name);
            ret.Append("(");
            string comma = "";
            foreach (var param in docInfo.Parameters) {
                ret.Append(comma);
                if ((param.Flags & ParameterFlags.ParamsArray) != 0) {
                    ret.Append('*');
                } else if ((param.Flags & ParameterFlags.ParamsDict) != 0) {
                    ret.Append("**");
                }
                ret.Append(param.Name);
                if (!String.IsNullOrEmpty(param.TypeName)) {
                    ret.Append(": ");
                    ret.Append(param.TypeName);
                }
                comma = ", ";
            }
            ret.Append(")");

            if (!String.IsNullOrEmpty(docInfo.ReturnParameter.TypeName)) {
                ret.Append(" -> ");
                ret.AppendLine(docInfo.ReturnParameter.TypeName);
            }

            // Append XML Doc Info if available
            if (!String.IsNullOrEmpty(docInfo.Documentation)) {
                ret.AppendLine();
                ret.AppendLine(StringUtils.SplitWords(docInfo.Documentation, true, lineWidth));
            }

            bool emittedParamDoc = false;
            foreach (var param in docInfo.Parameters) {
                if (!String.IsNullOrEmpty(param.Documentation)) {
                    if (!emittedParamDoc) {
                        ret.AppendLine();
                        emittedParamDoc = true;
                    }
                    ret.Append("    ");
                    ret.Append(param.Name);
                    ret.Append(": ");
                    ret.AppendLine(StringUtils.SplitWords(param.Documentation, false, lineWidth));
                }
            }

            if (!String.IsNullOrEmpty(docInfo.ReturnParameter.Documentation)) {
                ret.Append("    Returns: ");
                ret.AppendLine(StringUtils.SplitWords(docInfo.ReturnParameter.Documentation, false, lineWidth));
            }

            return ret.ToString();
        }

        private static string GetPythonTypeName(Type type) {
            if (type.IsByRef) {
                type = type.GetElementType();
            }

            return DynamicHelpers.GetPythonTypeFromType(type).Name;
        }

#if FEATURE_XMLDOC

        private static readonly object _CachedDocLockObject = new object();
        private static readonly List<Assembly> _AssembliesWithoutXmlDoc = new List<Assembly>();
        [MultiRuntimeAware]
        private static XPathDocument _CachedDoc;
        [MultiRuntimeAware]
        private static string _CachedDocName;

        private static string GetXmlName(Type type) {
            StringBuilder res = new StringBuilder();
            res.Append("T:");

            AppendTypeFormat(type, res);

            return res.ToString();
        }

        private static string GetXmlName(EventInfo field) {
            StringBuilder res = new StringBuilder();
            res.Append("E:");

            AppendTypeFormat(field.DeclaringType, res);
            res.Append('.');
            res.Append(field.Name);

            return res.ToString();
        }

        private static string GetXmlNameForProperty(Type declaringType, string propertyName) {
            StringBuilder res = new StringBuilder();
            res.Append("P:");
            if (!string.IsNullOrEmpty(declaringType.Namespace)) {
                res.Append(declaringType.Namespace);
                res.Append('.');
            }
            res.Append(declaringType.Name);
            res.Append('.');
            res.Append(propertyName);

            return res.ToString();
        }

        private static string GetXmlName(MethodBase info) {
            StringBuilder res = new StringBuilder();
            res.Append("M:");
            if (!string.IsNullOrEmpty(info.DeclaringType.Namespace)) {
                res.Append(info.DeclaringType.Namespace);
                res.Append('.');
            }
            res.Append(info.DeclaringType.Name);
            res.Append('.');
            res.Append(info.Name);
            ParameterInfo[] pi = info.GetParameters();
            if (pi.Length > 0) {
                res.Append('(');
                for (int i = 0; i < pi.Length; i++) {
                    Type curType = pi[i].ParameterType;

                    if (i != 0) res.Append(',');
                    AppendTypeFormat(curType, res, pi[i]);
                }
                res.Append(')');
            }
            return res.ToString();
        }

        /// <summary>
        /// Converts a Type object into a string suitable for lookup in the help file.  All generic types are
        /// converted down to their generic type definition.
        /// </summary>
        private static void AppendTypeFormat(Type curType, StringBuilder res, ParameterInfo pi=null) {
            if (curType.IsGenericType) {
                curType = curType.GetGenericTypeDefinition();
            }

            if (curType.IsGenericParameter) {
                res.Append(ReflectionUtils.GenericArityDelimiter);
                res.Append(curType.GenericParameterPosition);
            } else if (curType.ContainsGenericParameters) {
                if (!string.IsNullOrEmpty(curType.Namespace)) {
                    res.Append(curType.Namespace);
                    res.Append('.');
                }
                res.Append(curType.Name.Substring(0, curType.Name.Length - 2));
                res.Append('{');
                Type[] types = curType.GetGenericArguments();
                for (int j = 0; j < types.Length; j++) {
                    if (j != 0) res.Append(',');

                    if (types[j].IsGenericParameter) {
                        res.Append(ReflectionUtils.GenericArityDelimiter);
                        res.Append(types[j].GenericParameterPosition);
                    } else {
                        AppendTypeFormat(types[j], res);
                    }
                }
                res.Append('}');
            } else {
                if (pi != null) {
                    if((pi.IsOut || pi.ParameterType.IsByRef) && curType.FullName.EndsWith("&")) {
                        res.Append(curType.FullName.Replace("&", "@"));
                    } else {
                        res.Append(curType.FullName);
                    }
                } else {
                    res.Append(curType.FullName);
                }
            }
        }

        static string GetXmlDocLocation(Assembly assem) {
            if (_AssembliesWithoutXmlDoc.Contains(assem)) {
                return null;
            }

            string location = null;

            try {
                location = assem.Location;
            } catch (System.Security.SecurityException) {
                // FileIOPermission is required to access the file
            } catch (NotSupportedException) {
                // Dynamic assemblies do not support Assembly.Location
            }

            if (string.IsNullOrEmpty(location)) {
                _AssembliesWithoutXmlDoc.Add(assem);
                return null;
            }

            return location;
        }

        private const string _frameworkReferencePath = @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0";

        /// <summary>
        /// Gets the XPathDocument for the specified assembly, or null if one is not available.
        /// </summary>
        private static XPathDocument GetXPathDocument(Assembly asm) {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.CurrentCulture;

            string location = GetXmlDocLocation(asm);
            if (location == null) {
                return null;
            }

            string baseDir = Path.GetDirectoryName(location);
            string baseFile = Path.GetFileNameWithoutExtension(location) + ".xml";
            string xml = Path.Combine(Path.Combine(baseDir, ci.Name), baseFile);

            bool isRef = false;

            if (!File.Exists(xml)) {
                int hyphen = ci.Name.IndexOf('-');
                if (hyphen != -1) {
                    xml = Path.Combine(Path.Combine(baseDir, ci.Name.Substring(0, hyphen)), baseFile);
                }
                if (!File.Exists(xml)) {
                    xml = Path.Combine(baseDir, baseFile);
                    if (!File.Exists(xml)) {
#if !NETCOREAPP && !NETSTANDARD
                        // On .NET 4.0 documentation is in the reference assembly location
                        // for 64-bit processes, we need to look in Program Files (x86)
                        xml = Path.Combine(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.Is64BitProcess ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles), 
                                _frameworkReferencePath
                            ),
                            baseFile
                        );
                        isRef = true;

                        if (!File.Exists(xml))
#endif
                        {
                            _AssembliesWithoutXmlDoc.Add(asm);
                            return null;
                        }
                    }
                }
            }

            XPathDocument xpd;

            lock (_CachedDocLockObject) {
                if (_CachedDocName == xml) {
                    xpd = _CachedDoc;
                } else {
                    xpd = new XPathDocument(xml);
                    if (isRef) {
                        // attempt to redirect if required
                        var node = xpd.CreateNavigator().SelectSingleNode("/doc/@redirect");
                        if (node != null) {
                            var redirect = node.Value;
                            redirect = redirect.Replace("%PROGRAMFILESDIR%", Environment.GetFolderPath(Environment.Is64BitProcess ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles) + Path.DirectorySeparatorChar);
                            if (File.Exists(redirect)) {
                                xpd = new XPathDocument(redirect);
                            }
                        }
                    }
                }

                _CachedDoc = xpd;
                _CachedDocName = xml;
            }

            return xpd;
        }

        /// <summary>
        /// Gets the Xml documentation for the specified MethodBase.
        /// </summary>
        private static void GetXmlDoc(MethodBase info, out string summary, out string returns, out List<KeyValuePair<string, string>> parameters) {
            summary = null;
            returns = null;
            parameters = null;

            XPathDocument xpd = GetXPathDocument(info.DeclaringType.Assembly);
            if (xpd == null) return;

            XPathNavigator xpn = xpd.CreateNavigator();
            string path = "/doc/members/member[@name='" + GetXmlName(info) + "']/*";
            XPathNodeIterator iter = xpn.Select(path);

            while (iter.MoveNext()) {
                switch (iter.Current.Name) {
                    case "summary": summary = XmlToString(iter); break;
                    case "returns": returns = XmlToString(iter); break;
                    case "param":
                        string name = null;
                        string paramText = XmlToString(iter);
                        if (iter.Current.MoveToFirstAttribute()) {
                            name = iter.Current.Value;
                        }

                        if (name != null) {
                            if (parameters == null) {
                                parameters = new List<KeyValuePair<string, string>>();
                            }

                            parameters.Add(new KeyValuePair<string, string>(name, paramText.Trim()));
                        }
                        break;
                    case "exception":
                        break;
                }
            }
        }

        /// <summary>
        /// Gets the Xml documentation for the specified Type.
        /// </summary>
        private static void GetXmlDoc(Type type, out string summary) {
            summary = null;

            XPathDocument xpd = GetXPathDocument(type.Assembly);
            if (xpd == null) return;

            XPathNavigator xpn = xpd.CreateNavigator();
            string path = "/doc/members/member[@name='" + GetXmlName(type) + "']/*";
            XPathNodeIterator iter = xpn.Select(path);

            while (iter.MoveNext()) {
                switch (iter.Current.Name) {
                    case "summary": summary = XmlToString(iter); break;
                }
            }
        }        

        /// <summary>
        /// Gets the Xml documentation for the specified Field.
        /// </summary>
        private static void GetXmlDocForProperty(Type declaringType, string propertyName, out string summary, out string returns) {
            summary = null;
            returns = null;

            XPathDocument xpd = GetXPathDocument(declaringType.Assembly);
            if (xpd == null) return;

            XPathNavigator xpn = xpd.CreateNavigator();
            string path = "/doc/members/member[@name='" + GetXmlNameForProperty(declaringType, propertyName) + "']/*";
            XPathNodeIterator iter = xpn.Select(path);

            while (iter.MoveNext()) {
                switch (iter.Current.Name) {
                    case "summary": summary = XmlToString(iter); break;
                    case "returns": returns = XmlToString(iter); break;
                }
            }
        }

        /// <summary>
        /// Gets the Xml documentation for the specified Field.
        /// </summary>
        private static void GetXmlDoc(EventInfo info, out string summary, out string returns) {
            summary = null;
            returns = null;

            XPathDocument xpd = GetXPathDocument(info.DeclaringType.Assembly);
            if (xpd == null) return;

            XPathNavigator xpn = xpd.CreateNavigator();
            string path = "/doc/members/member[@name='" + GetXmlName(info) + "']/*";
            XPathNodeIterator iter = xpn.Select(path);

            while (iter.MoveNext()) {
                switch (iter.Current.Name) {
                    case "summary": summary = XmlToString(iter) + Environment.NewLine; break;
                }
            }
        }
        /// <summary>
        /// Converts the XML as stored in the config file into a human readable string.
        /// </summary>
        private static string XmlToString(XPathNodeIterator iter) {
            XmlReader xr = iter.Current.ReadSubtree();
            StringBuilder text = new StringBuilder();
            if (xr.Read()) {
                for (; ; ) {
                    switch (xr.NodeType) {
                        case XmlNodeType.Text:
                            text.Append(xr.ReadContentAsString());
                            continue;
                        case XmlNodeType.Element:
                            switch (xr.Name) {
                                case "see":
                                    if (xr.MoveToFirstAttribute() && xr.ReadAttributeValue()) {
                                        int arity = xr.Value.IndexOf(ReflectionUtils.GenericArityDelimiter);
                                        if (arity != -1)
                                            text.Append(xr.Value, 2, arity - 2);
                                        else
                                            text.Append(xr.Value, 2, xr.Value.Length - 2);
                                    }
                                    break;
                                case "paramref":
                                    if (xr.MoveToAttribute("name")) {
                                        text.Append(xr.Value);
                                    }
                                    break;
                            }
                            break;
                    }

                    if (!xr.Read()) break;
                }
            }
            return text.ToString().Trim();
        }
#endif
    }
}
