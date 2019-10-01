// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

[assembly: PythonModule("pyexpat", typeof(IronPython.Modules.PythonExpat))]
[assembly: PythonModule("pyexpat.errors", typeof(IronPython.Modules.PythonExpat.PyExpatErrors))]
[assembly: PythonModule("pyexpat.model", typeof(IronPython.Modules.PythonExpat.PyExpatModel))]
namespace IronPython.Modules {
    public static class PythonExpat {
        public const int XML_PARAM_ENTITY_PARSING_NEVER = 0;
        public const int XML_PARAM_ENTITY_PARSING_UNLESS_STANDALONE = 1;
        public const int XML_PARAM_ENTITY_PARSING_ALWAYS = 2;

        private static readonly object _errorsKey = new object();

        internal enum XmlErrors : int {
            [Description("out of memory")]
            XML_ERROR_NO_MEMORY = 1,

            [Description("syntax error")]
            XML_ERROR_SYNTAX = 2,

            [Description("no element found")]
            XML_ERROR_NO_ELEMENTS = 3,

            [Description("not well-formed (invalid token)")]
            XML_ERROR_INVALID_TOKEN = 4,

            [Description("unclosed token")]
            XML_ERROR_UNCLOSED_TOKEN = 5,

            [Description("partial character")]
            XML_ERROR_PARTIAL_CHAR = 6,

            [Description("mismatched tag")]
            XML_ERROR_TAG_MISMATCH = 7,

            [Description("duplicate attribute")]
            XML_ERROR_DUPLICATE_ATTRIBUTE = 8,

            [Description("junk after document element")]
            XML_ERROR_JUNK_AFTER_DOC_ELEMENT = 9,

            [Description("illegal parameter entity reference")]
            XML_ERROR_PARAM_ENTITY_REF = 10,

            [Description("undefined entity")]
            XML_ERROR_UNDEFINED_ENTITY = 11,

            [Description("recursive entity reference")]
            XML_ERROR_RECURSIVE_ENTITY_REF = 12,

            [Description("asynchronous entity")]
            XML_ERROR_ASYNC_ENTITY = 13,

            [Description("reference to invalid character number")]
            XML_ERROR_BAD_CHAR_REF = 14,

            [Description("reference to binary entity")]
            XML_ERROR_BINARY_ENTITY_REF = 15,

            [Description("reference to external entity in attribute")]
            XML_ERROR_ATTRIBUTE_EXTERNAL_ENTITY_REF = 16,

            [Description("XML or text declaration not at start of entity")]
            XML_ERROR_MISPLACED_XML_PI = 17,

            [Description("unknown encoding")]
            XML_ERROR_UNKNOWN_ENCODING = 18,

            [Description("entity declared in parameter entity")]
            XML_ERROR_ENTITY_DECLARED_IN_PE = 19,

            [Description("requested feature requires XML_DTD support in Expat")]
            XML_ERROR_FEATURE_REQUIRES_XML_DTD = 20,

            [Description("cannot change setting once parsing has begun")]
            XML_ERROR_CANT_CHANGE_FEATURE_ONCE_PARSING = 21,

            [Description("unbound prefix")]
            XML_ERROR_UNBOUND_PREFIX = 22,

            [Description("must not undeclare prefix")]
            XML_ERROR_UNDECLARING_PREFIX = 23,

            [Description("incomplete markup in parameter entity")]
            XML_ERROR_INCOMPLETE_PE = 24,

            [Description("XML declaration not well-formed")]
            XML_ERROR_XML_DECL = 25,

            [Description("text declaration not well-formed")]
            XML_ERROR_TEXT_DECL = 26,

            [Description("illegal character(s) in public id")]
            XML_ERROR_PUBLICID = 27,

            [Description("parser suspended")]
            XML_ERROR_SUSPENDED = 28,

            [Description("parser not suspended")]
            XML_ERROR_NOT_SUSPENDED = 29,

            [Description("parsing aborted")]
            XML_ERROR_ABORTED = 30,

            [Description("parsing finished")]
            XML_ERROR_FINISHED = 31,

            [Description("cannot suspend in external parameter entity")]
            XML_ERROR_SUSPEND_PE = 32,

            [Description("encoding specified in XML declaration is incorrect")]
            XML_ERROR_INCORRECT_ENCODING = 33,

            [Description("unclosed CDATA section")]
            XML_ERROR_UNCLOSED_CDATA_SECTION = 34,

            [Description("error in processing external entity reference")]
            XML_ERROR_EXTERNAL_ENTITY_HANDLING = 35,

            [Description("document is not standalone")]
            XML_ERROR_NOT_STANDALONE = 36,

            [Description("unexpected parser state - please send a bug report")]
            XML_ERROR_UNEXPECTED_STATE = 37
        }

        [PythonHidden]
        public class PyExpatErrors : IPythonMembersList {
            [SpecialName]
            public object GetCustomMember(CodeContext/*!*/ context, string name) {
                FieldInfo fi = typeof(XmlErrors).GetField(name);

                if (fi != null) {
                    DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                    if (attributes != null && attributes.Length > 0)
                        return attributes[0].Description;
                }
                return OperationFailed.Value;
            }

            IList<object> IPythonMembersList.GetMemberNames(CodeContext context) {
                throw new NotImplementedException();
            }

            IList<string> IMembersList.GetMemberNames() {
                throw new NotImplementedException();
            }

            internal static string GetFieldDescription(XmlErrors value) {
                FieldInfo fi = typeof(XmlErrors).GetField(value.ToString());

                DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attributes != null && attributes.Length > 0)
                    return attributes[0].Description;

                return value.ToString();
            }
        }

        [PythonHidden]
        public static class PyExpatModel {
            private enum XmlContentType {
                XML_CTYPE_EMPTY = 1,
                XML_CTYPE_ANY,
                XML_CTYPE_MIXED,
                XML_CTYPE_NAME,
                XML_CTYPE_CHOICE,
                XML_CTYPE_SEQ
            };

            public const int XML_CTYPE_EMPTY = (int)XmlContentType.XML_CTYPE_EMPTY;
            public const int XML_CTYPE_ANY = (int)XmlContentType.XML_CTYPE_ANY;
            public const int XML_CTYPE_MIXED = (int)XmlContentType.XML_CTYPE_MIXED;
            public const int XML_CTYPE_NAME = (int)XmlContentType.XML_CTYPE_NAME;
            public const int XML_CTYPE_CHOICE = (int)XmlContentType.XML_CTYPE_CHOICE;
            public const int XML_CTYPE_SEQ = (int)XmlContentType.XML_CTYPE_SEQ;

            private enum XmlContentQuant {
                XML_CQUANT_NONE,
                XML_CQUANT_OPT,
                XML_CQUANT_REP,
                XML_CQUANT_PLUS
            };

            public const int XML_CQUANT_NONE = (int)XmlContentQuant.XML_CQUANT_NONE;
            public const int XML_CQUANT_OPT = (int)XmlContentQuant.XML_CQUANT_OPT;
            public const int XML_CQUANT_REP = (int)XmlContentQuant.XML_CQUANT_REP;
            public const int XML_CQUANT_PLUS = (int)XmlContentQuant.XML_CQUANT_PLUS;
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            var exception = context.EnsureModuleException("pyexpaterror", dict, "ExpatError", "xml.parsers.expat");
            dict["error"] = exception;
            dict["ExpatError"] = exception;

            context.GetOrCreateModuleState(_errorsKey, () => {
                dict.Add("errors", context.GetBuiltinModule("pyexpat.errors"));
                dict.Add("model", context.GetBuiltinModule("pyexpat.model"));
                return dict;
            });
        }

        public static string ErrorString(int errno) {
            if (typeof(XmlErrors).IsEnumDefined(errno)) {
                return PyExpatErrors.GetFieldDescription((XmlErrors)errno);
            }
            return null;
        }

        public static object ParserCreate(CodeContext context, [ParamDictionary]IDictionary<object, object> kwArgs\u00F8, params object[] args\u00F8) {
            var numArgs = argsø.Length + kwArgsø.Count;
            if (numArgs > 3) throw PythonOps.TypeError($"ParserCreate() takes at most 3 arguments ({numArgs} given)");

            object encoding = argsø.Length > 0 ? argsø[0] : null;
            object namespace_separator = argsø.Length > 1 ? argsø[1] : null;
            object intern = argsø.Length > 2 ? argsø[0] : new PythonDictionary();

            foreach (var pair in kwArgsø) {
                switch (pair.Key) {
                    case nameof(encoding):
                        if (argsø.Length > 0) throw PythonOps.TypeError($"Argument given by name('{nameof(encoding)}') and position(1)");
                        encoding = pair.Value;
                        break;
                    case nameof(namespace_separator):
                        if (argsø.Length > 1) throw PythonOps.TypeError($"Argument given by name('{nameof(namespace_separator)}') and position(2)");
                        namespace_separator = pair.Value;
                        break;
                    case nameof(intern):
                        intern = pair.Value;
                        break;
                    default:
                        throw PythonOps.TypeError($"'{pair.Key}' is an invalid keyword argument for this function");
                }
            }

            return ParserCreate(encoding, namespace_separator, intern);
        }

        public static object ParserCreate(object encoding, object namespace_separator, object intern) {
            var encoding_str = encoding == null ? null : (encoding as string ?? throw PythonOps.TypeError($"ParserCreate() argument 1 must be string or None, not {PythonTypeOps.GetName(encoding)}"));
            var namespace_separator_str = namespace_separator == null ? null : (namespace_separator as string ?? throw PythonOps.TypeError($"ParserCreate() argument 2 must be string or None, not {PythonTypeOps.GetName(namespace_separator)}"));

            return ParserCreateImpl(encoding_str, namespace_separator_str, intern);
        }

        private static object ParserCreateImpl(string encoding, string namespace_separator, object intern) {
            if (namespace_separator?.Length > 1)
                throw PythonOps.ValueError("namespace_separator must be at most one character, omitted, or None");

            return new xmlparser(encoding, namespace_separator, intern);
        }

        public static object XMLParserType { get; } = DynamicHelpers.GetPythonTypeFromType(typeof(xmlparser));

        [PythonType, PythonHidden]
        public class xmlparser {
            private readonly StringBuilder text_buffer = new StringBuilder();
            private int _buffer_size = 8192;
            private bool _use_foreign_dtd = true;
            private bool _parsing_done = false;

            public object buffer_size {
                get => _buffer_size;
                set {
                    if (value is int i) {
                        if (i <= 0)
                            throw PythonOps.ValueError($"{nameof(buffer_size)} must be greater than zero");

                        FlushBuffer();
                        _buffer_size = i;
                        return;
                    }
                    throw PythonOps.TypeError($"{nameof(buffer_size)} must be an integer");
                }
            }
            public bool buffer_text { get; set; }
            public int buffer_used { get; }

            public bool namespace_prefixes { get; set; }
            public bool ordered_attributes { get; set; }
            public bool returns_unicode { get; set; } = true;
            public bool specified_attributes { get; set; }

            public Action<string, string, string, string, bool> AttlistDeclHandler { get; set; } // NotImplemented
            public Action<string> CharacterDataHandler { get; set; }
            public Action<string> CommentHandler { get; set; }
            public Action<string> DefaultHandler { get; set; } // NotImplemented
            public Action<string> DefaultHandlerExpand { get; set; } // NotImplemented
            public Action<string, object> ElementDeclHandler { get; set; } // NotImplemented
            public Action EndCdataSectionHandler { get; set; }
            public Action EndDoctypeDeclHandler { get; set; }
            public Action<string> EndElementHandler { get; set; }
            public Action<string> EndNamespaceDeclHandler { get; set; }
            public Action<string, bool, string, string, string, string, string> EntityDeclHandler { get; set; } // NotImplemented
            public Action<string, string, string, string> ExternalEntityRefHandler { get; set; } // NotImplemented
            public Action NotStandaloneHandler { get; set; } // NotImplemented
            public Action<string, string, string, string> NotationDeclHandler { get; set; } // NotImplemented
            public Action<string, object> ProcessingInstructionHandler { get; set; }
            public Action<string, object> SkippedEntityHandler { get; set; } // NotImplemented
            public Action StartCdataSectionHandler { get; set; }
            public StartDoctypeDeclHandlerDelegate StartDoctypeDeclHandler { get; set; }
            public Action<string, object> StartElementHandler { get; set; }
            public Action<string, string> StartNamespaceDeclHandler { get; set; }
            public Action<string, string, string, string, string> UnparsedEntityDeclHandler { get; set; } // NotImplemented
            public XmlDeclHandlerDelegate XmlDeclHandler { get; set; }

            public delegate void StartDoctypeDeclHandlerDelegate(string doctypeName, string systemId, string publicId, bool has_internal_subset);
            public delegate void XmlDeclHandlerDelegate(string version, string encoding, int standalone);

            public object intern { get; set; }

            private readonly string namespace_separator;

            public xmlparser(string encoding, string namespace_separator, object intern) {
                this.intern = intern;
                this.namespace_separator = namespace_separator;
            }

            public bool SetParamEntityParsing(int flag) {
                throw new NotImplementedException();
            }

            public void UseForeignDTD(bool use_foreign_dtd = true) {
                _use_foreign_dtd = use_foreign_dtd;
            }

            private XmlReader xmlReader;

            public int CurrentColumnNumber => (xmlReader as IXmlLineInfo)?.LinePosition ?? 0;
            public int CurrentLineNumber => (xmlReader as IXmlLineInfo)?.LineNumber ?? 0;

            public long CurrentByteIndex { get; private set; } = 0;

            private void parse(CodeContext context) {
                try {
                    while (xmlReader.Read()) {
                        switch (xmlReader.NodeType) {
                            case XmlNodeType.Element:
                                handleElement();
                                break;
                            case XmlNodeType.EndElement:
                                handleEndElement();
                                break;
                            case XmlNodeType.CDATA:
                                handleCDATA();
                                break;
                            case XmlNodeType.Text:
                                BufferText(xmlReader.Value);
                                break;
                            case XmlNodeType.SignificantWhitespace:
                            case XmlNodeType.Whitespace:
                                if (xmlReader.Depth > 0)
                                    BufferText(xmlReader.Value);
                                break;
                            case XmlNodeType.ProcessingInstruction:
                                handleProcessingInstruction();
                                break;
                            case XmlNodeType.Comment:
                                handleComment();
                                break;
                            case XmlNodeType.DocumentType:
                                handleDocumentType();
                                break;
                            case XmlNodeType.XmlDeclaration:
                                handleXmlDeclaration();
                                break;
                            default:
                                break;
                        }
                    }
                } catch (XmlException e) {
                    throw Error(context, e);
                }
                FlushBuffer();
            }

            private string qname() {
                if (namespace_separator == null) return xmlReader.Name;
                if (!string.IsNullOrEmpty(xmlReader.NamespaceURI)) {
                    var temp = xmlReader.NamespaceURI + namespace_separator + xmlReader.LocalName;
                    if (namespace_prefixes && !string.IsNullOrEmpty(xmlReader.Prefix))
                        return temp + namespace_separator + xmlReader.Prefix;
                    return temp;
                }
                return xmlReader.LocalName;
            }

            private readonly Stack<string> ns_stack = new Stack<string>();

            private void handleElement() {
                var name = qname();
                ns_stack.Push(null);

                PythonList attributeList = null;
                PythonDictionary attributeDict = null;
                if (ordered_attributes) {
                    attributeList = new PythonList();
                } else {
                    attributeDict = new PythonDictionary();
                }

                while (xmlReader.MoveToNextAttribute()) {
                    if (namespace_separator != null
                        && (xmlReader.Prefix == "xmlns" || xmlReader.Prefix == string.Empty && xmlReader.LocalName == "xmlns")) {
                        var prefix = xmlReader.Prefix == string.Empty ? string.Empty : xmlReader.LocalName;
                        var uri = xmlReader.Value;
                        ns_stack.Push(prefix);
                        var startNamespaceDeclHandler = StartNamespaceDeclHandler;
                        if (startNamespaceDeclHandler != null) {
                            FlushBuffer();
                            startNamespaceDeclHandler(prefix == string.Empty ? null : prefix, uri);
                        }
                        continue;
                    }
                    var key = qname();
                    var value = xmlReader.Value;
                    if (ordered_attributes) {
                        attributeList.append(key);
                        attributeList.append(value);
                    } else {
                        attributeDict[key] = value;
                    }
                }
                xmlReader.MoveToElement();

                var startElementHandler = StartElementHandler;
                if (startElementHandler != null) {
                    FlushBuffer();
                    startElementHandler(name, ordered_attributes ? (object)attributeList : attributeDict);
                }

                // EndElement node is not generated for empty elements.
                // Call its handler here.
                if (xmlReader.IsEmptyElement)
                    handleEndElement();
            }

            private void handleEndElement() {
                var name = qname();
                var endElementHandler = EndElementHandler;
                if (endElementHandler != null) {
                    FlushBuffer();
                    endElementHandler(name);
                }
                while (true) {
                    var prefix = ns_stack.Pop();
                    if (prefix == null)
                        break;
                    var endNamespaceDeclHandler = EndNamespaceDeclHandler;
                    if (endNamespaceDeclHandler != null) {
                        FlushBuffer();
                        endNamespaceDeclHandler(prefix == string.Empty ? null : prefix);
                    }
                }
            }

            private void handleComment() {
                var commentHandler = CommentHandler;
                if (commentHandler != null) {
                    FlushBuffer();
                    commentHandler(xmlReader.Value);
                }
            }

            private void handleCDATA() {
                var startCdataSectionHandler = StartCdataSectionHandler;
                if (startCdataSectionHandler != null) {
                    FlushBuffer();
                    startCdataSectionHandler();
                }

                BufferText(xmlReader.Value);

                var endCdataSectionHandler = EndCdataSectionHandler;
                if (endCdataSectionHandler != null) {
                    FlushBuffer();
                    endCdataSectionHandler();
                }
            }

            private void BufferText(string text) {
                if (buffer_text) {
                    text_buffer.Append(text);
                    while (text_buffer.Length >= _buffer_size) {
                        text = text_buffer.ToString(0, _buffer_size);
                        text_buffer.Remove(0, _buffer_size);
                        CharacterDataHandler?.Invoke(text);
                    }
                } else {
                    if (text.Contains("\n")) {
                        var split = text.Split('\n');
                        int i;
                        for (i = 0; i < split.Length - 1; i++) {
                            var s = split[i];
                            if (s.Length != 0)
                                CharacterDataHandler?.Invoke(s);
                            CharacterDataHandler?.Invoke("\n");
                        }
                        CharacterDataHandler?.Invoke(split[i]);
                    } else {
                        CharacterDataHandler?.Invoke(text);
                    }
                }
            }

            private void FlushBuffer() {
                if (buffer_text && text_buffer.Length > 0) {
                    var text = text_buffer.ToString();
                    CharacterDataHandler?.Invoke(text);
                    text_buffer.Clear();
                }
            }

            private void handleProcessingInstruction() {
                var processingInstructionHandler = ProcessingInstructionHandler;
                if (processingInstructionHandler != null) {
                    FlushBuffer();
                    processingInstructionHandler.Invoke(qname(), xmlReader.Value);
                }
            }

            private void handleDocumentType() {
                var doctypeName = xmlReader.Name;
                string systemId = null;
                string publicId = null;
                var internal_subset = xmlReader.Value;
                bool has_internal_subset = !string.IsNullOrEmpty(internal_subset);
                while (xmlReader.MoveToNextAttribute()) {
                    if (xmlReader.Name == "SYSTEM") {
                        systemId = xmlReader.Value;
                    } else if (xmlReader.Name == "PUBLIC") {
                        publicId = xmlReader.Value;
                    }
                }
                StartDoctypeDeclHandler?.Invoke(doctypeName, systemId, publicId, has_internal_subset);

                // TODO: process the internal_subset
                // if (has_internal_subset) throw new NotImplementedException();

                EndDoctypeDeclHandler?.Invoke();
            }

            private void handleXmlDeclaration() {
                string version = null;
                string encoding = null;
                int standalone = -1; // omitted

                while (xmlReader.MoveToNextAttribute()) {
                    if (xmlReader.Name == "version") {
                        version = xmlReader.Value;
                    } else if (xmlReader.Name == "encoding") {
                        encoding = xmlReader.Value;
                    } else if (xmlReader.Name == "standalone") {
                        standalone = xmlReader.Value == "yes" ? 1 : 0;
                    }
                }

                XmlDeclHandler?.Invoke(version, encoding, standalone);
            }

            private MemoryStream buffer = new MemoryStream();

            public void Parse(CodeContext context, string data, bool isfinal = false)
                => Parse(context, PythonOps.MakeBytes(data), isfinal);

            public void Parse(CodeContext context, [BytesConversion]IList<byte> data, bool isfinal = false) {
                CheckParsingDone(context);

                if (data.Any()) {
                    byte[] bytes;
                    switch (data) {
                        case Bytes b:
                            bytes = b.GetUnsafeByteArray();
                            break;
                        case byte[] b:
                            bytes = b;
                            break;
                        default:
                            bytes = data.ToArray();
                            break;
                    }

                    buffer.Write(bytes, 0, bytes.Length);
                }

                if (isfinal) {
                    buffer.Seek(0, SeekOrigin.Begin);
                    var settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
                    xmlReader = XmlReader.Create(buffer, settings);
                    parse(context);
                    _parsing_done = true;
                    xmlReader.Dispose();
                    buffer.Dispose();
                }
            }

            public void ParseFile(CodeContext context, object file) {
                CheckParsingDone(context);

                if (!PythonOps.TryGetBoundAttr(context, file, "read", out object _readMethod))
                    throw PythonOps.TypeError("argument must have 'read' attribute");

                object readResult = PythonOps.CallWithContext(context, _readMethod);
                if (readResult is Bytes b) {
                    using (var stream = new MemoryStream(b.GetUnsafeByteArray())) {
                        var settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
                        using (xmlReader = XmlReader.Create(stream, settings)) {
                            parse(context);
                            _parsing_done = true;
                        }
                    }
                } else {
                    throw PythonOps.TypeError("read() did not return a bytes object (type={0})", DynamicHelpers.GetPythonType(readResult).Name);
                }


            }

            private void CheckParsingDone(CodeContext context) {
                if (_parsing_done) {
                    throw Error(context, XmlErrors.XML_ERROR_FINISHED);
                }
            }

            private static Exception Error(CodeContext/*!*/ context, XmlException e) {
                var msg = FormatExpatMsg(e, out int code);
                var throwable = PythonExceptions.CreatePythonThrowable((PythonType)context.LanguageContext.GetModuleState("pyexpaterror"), msg);

                throwable.SetMemberAfter("lineno", e.LineNumber);
                throwable.SetMemberAfter("offset", e.LinePosition);
                throwable.SetMemberAfter("code", code);

                return throwable.GetClrException();
            }

            private static Exception Error(CodeContext/*!*/ context, XmlErrors code) {
                var msg = PyExpatErrors.GetFieldDescription(code);
                var throwable = PythonExceptions.CreatePythonThrowable((PythonType)context.LanguageContext.GetModuleState("pyexpaterror"), msg);

                throwable.SetMemberAfter("code", (int)code);

                return throwable.GetClrException();
            }

            private static string FormatExpatMsg(XmlException e, out int code) {
                var res = e.Message;
                code = 0;
                if (e.Message.StartsWith("Syntax for an XML declaration")) {
                    res = $"XML declaration not well-formed: line {e.LineNumber}, column {e.LinePosition}";
                    code = (int)XmlErrors.XML_ERROR_XML_DECL;
                }

                return res;
            }
        }
    }
}
