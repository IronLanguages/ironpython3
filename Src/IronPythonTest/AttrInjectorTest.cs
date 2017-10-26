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