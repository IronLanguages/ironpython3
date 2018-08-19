// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using IronPython.Runtime;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

[assembly: ExtensionType(typeof(XmlElement), typeof(IronPythonTest.AttrInjectorTest.SimpleXmlAttrInjector))]
namespace IronPythonTest {
    public class AttrInjectorTest {
        public static object LoadXml(string text) {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(text);
            return doc.DocumentElement;
        }

        public static class SimpleXmlAttrInjector {
            [SpecialName]
            public static IList<string> GetMemberNames(object obj) {
                List<string> list = new List<string>();
                XmlElement xml = obj as XmlElement;

                if (xml != null) {
                    for (XmlNode n = xml.FirstChild; n != null; n = n.NextSibling) {
                        if (n is XmlElement) {
                            list.Add(n.Name);
                        }
                    }
                }

                return list;
            }

            [SpecialName]
            public static object GetBoundMember(object obj, string name) {
                XmlElement xml = obj as XmlElement;

                if (xml != null) {
                    for (XmlNode n = xml.FirstChild; n != null; n = n.NextSibling) {
                        if (n is XmlElement && string.CompareOrdinal(n.Name, name) == 0) {
                            if (n.HasChildNodes && n.FirstChild == n.LastChild && n.FirstChild is XmlText) {
                                return n.InnerText;
                            } else {
                                return n;
                            }

                        }
                    }
                }

                return NotImplementedType.Value;
            }

        }
    }
}