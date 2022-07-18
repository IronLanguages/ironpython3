// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Modules;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// New string formatter for 'str'.format(...) calls and support for the Formatter
    /// library via the _formatter_parser / _formatter_field_name_split
    /// methods.
    ///
    /// We parse this format:
    ///
    /// replacement_field =  "{" field_name ["!" conversion] [":" format_spec] "}"
    /// field_name        =  (identifier | integer) ("." attribute_name | "[" element_index "]")*
    /// attribute_name    =  identifier
    /// element_index     =  identifier
    /// conversion        =  "r" | "s"
    /// format_spec       = any char, { must be balanced (for computed values), passed to __format__ method on object
    /// </summary>
    internal sealed class NewStringFormatter {
        private static readonly char[]/*!*/ _brackets = new[] { '{', '}' };
        private static readonly char[]/*!*/ _fieldNameEnd = new[] { '{', '}', '!', ':' };

        #region Public APIs

        /// <summary>
        /// Runs the formatting operation on the given format and keyword arguments
        /// </summary>
        public static string/*!*/ FormatString(PythonContext/*!*/ context, string/*!*/ format, PythonTuple/*!*/ args, IDictionary<object, object>/*!*/ kwArgs) {
            ContractUtils.RequiresNotNull(context, nameof(context));
            ContractUtils.RequiresNotNull(format, nameof(format));
            ContractUtils.RequiresNotNull(args, nameof(args));
            ContractUtils.RequiresNotNull(kwArgs, nameof(kwArgs));

            return Formatter.FormatString(context, format, args, kwArgs);
        }

        /// <summary>
        /// Gets the formatting information for the given format.  This is a list of tuples.  The tuples
        /// include:
        ///
        /// text, field name, format spec, conversion
        /// </summary>
        public static IEnumerable<PythonTuple/*!*/>/*!*/ GetFormatInfo(string/*!*/ format) {
            ContractUtils.RequiresNotNull(format, nameof(format));

            return StringFormatParser.Parse(format);
        }

        /// <summary>
        /// Parses a field name returning the argument name and an iterable
        /// object which can be used to access the individual attribute
        /// or element accesses.  The iterator yields tuples of:
        ///
        /// bool (true if attribute, false if element index), attribute/index value
        /// </summary>
        public static PythonTuple/*!*/ GetFieldNameInfo(string/*!*/ name) {
            ContractUtils.RequiresNotNull(name, nameof(name));


            FieldName fieldName = ParseFieldName(name, false);

            if (String.IsNullOrEmpty(fieldName.ArgumentName)) {
                throw PythonOps.ValueError("empty field name");
            }

            int val;
            object argName = fieldName.ArgumentName;
            if (Int32.TryParse(fieldName.ArgumentName, out val)) {
                argName = ScriptingRuntimeHelpers.Int32ToObject(val);
            }

            return PythonTuple.MakeTuple(
                argName,
                AccessorsToPython(fieldName.Accessors)
            );
        }

        #endregion

        #region Parsing

        /// <summary>
        /// Base class used for parsing the format.  Subclasss override Text/ReplacementField methods.  Those
        /// methods get called when they call Parse and then they can do the appropriate actions for the
        /// format.
        /// </summary>
        private struct StringFormatParser {
            private readonly string/*!*/ _str;
            private int _index;

            private StringFormatParser(string/*!*/ text) {
                Assert.NotNull(text);

                _str = text;
                _index = 0;
            }

            /// <summary>
            /// Gets an enumerable object for walking the parsed format.
            ///
            /// TODO: object array?  struct?
            /// </summary>
            public static IEnumerable<PythonTuple/*!*/>/*!*/ Parse(string/*!*/ text) {
                return new StringFormatParser(text).Parse();
            }

            /// <summary>
            /// Provides an enumerable of the parsed format.  The elements of the tuple are:
            ///     the text preceding the format information
            ///     the field name
            ///     the format spec
            ///     the conversion
            /// </summary>
            private IEnumerable<PythonTuple/*!*/>/*!*/ Parse() {
                int lastTextStart = 0;
                while (_index != _str.Length) {
                    lastTextStart = _index;
                    _index = _str.IndexOfAny(_brackets, _index);

                    if (_index == -1) {
                        // no more formats, send the remaining text.
                        yield return PythonTuple.MakeTuple(
                            _str.Substring(lastTextStart, _str.Length - lastTextStart),
                            null,
                            null,
                            null);

                        break;
                    }

                    yield return ParseFormat(lastTextStart);
                }
            }

            private PythonTuple/*!*/ ParseFormat(int lastTextStart) {
                // check for {{ or }} and get the text string that we've skipped over
                string text;
                if (ParseDoubleBracket(lastTextStart, out text)) {
                    return PythonTuple.MakeTuple(text, null, null, null);
                }

                int bracketDepth = 1;
                char? conversion = null;
                string formatSpec = String.Empty;

                // all entries have a field name, read it first
                string fldName = ParseFieldName(ref bracketDepth);

                // check for conversion
                bool end = CheckEnd();
                if (!end && _str[_index] == '!') {
                    conversion = ParseConversion();
                }

                // check for format spec
                end = end || CheckEnd();
                if (!end && _str[_index] == ':') {
                    formatSpec = ParseFormatSpec(ref bracketDepth);
                }

                // verify we hit the end of the format
                end = end || CheckEnd();
                if (!end) {
                    throw PythonOps.ValueError("expected ':' after format specifier");
                }

                // yield the replacement field information
                return PythonTuple.MakeTuple(
                    text,
                    fldName,
                    formatSpec,
                    conversion.HasValue ?
                        conversion.ToString() :
                        null
                );
            }

            /// <summary>
            /// Handles {{ and }} within the string.  Returns true if a double bracket
            /// is found and yields the text
            /// </summary>
            private bool ParseDoubleBracket(int lastTextStart, out string/*!*/ text) {
                if (_str[_index] == '}') {
                    // report the text w/ a single } at the end
                    _index++;
                    if (_index == _str.Length || _str[_index] != '}') {
                        throw PythonOps.ValueError("Single '}}' encountered in format string");
                    }

                    text = _str.Substring(lastTextStart, _index - lastTextStart);
                    _index++;
                    return true;
                } else if (_index == _str.Length - 1) {
                    throw PythonOps.ValueError("Single '{{' encountered in format string");
                } else if (_str[_index + 1] == '{') {
                    // report the text w/ a single { at the end
                    text = _str.Substring(lastTextStart, ++_index - lastTextStart);
                    _index++;
                    return true;
                } else {
                    // yield the text
                    text = _str.Substring(lastTextStart, _index++ - lastTextStart);
                    return false;
                }
            }

            /// <summary>
            /// Parses the conversion character and returns it
            /// </summary>
            private char ParseConversion() {
                _index++; // eat the !
                if (CheckEnd()) {
                    throw PythonOps.ValueError("end of format while looking for conversion specifier");
                }

                return _str[_index++];
            }

            /// <summary>
            /// Checks to see if we're at the end of the format.  If there's no more characters left we report
            /// the error, otherwise if we hit a } we return true to indicate parsing should stop.
            /// </summary>
            private bool CheckEnd() {
                if (_index == _str.Length) {
                    throw PythonOps.ValueError("unmatched '{{' in format spec");
                } else if (_str[_index] == '}') {
                    _index++;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Parses the format spec string and returns it.
            /// </summary>
            private string/*!*/ ParseFormatSpec(ref int depth) {
                _index++; // eat the :
                return ParseFieldOrSpecWorker(_brackets, ref depth);
            }

            /// <summary>
            /// Parses the field name and returns it.
            /// </summary>
            private string/*!*/ ParseFieldName(ref int depth) {
                return ParseFieldOrSpecWorker(_fieldNameEnd, ref depth);
            }

            /// <summary>
            /// Handles parsing the field name and the format spec and returns it.  At the parse
            /// level these are basically the same - field names just have more terminating characters.
            ///
            /// The most complex part of parsing them is they both allow nested braces and require
            /// the braces are matched.  Strangely though the braces need to be matched across the
            /// combined field and format spec - not within each format.
            /// </summary>
            private string/*!*/ ParseFieldOrSpecWorker(char[]/*!*/ ends, ref int depth) {
                int end = _index - 1;
                bool done = false;
                do {
                    end = _str.IndexOfAny(ends, end + 1);

                    if (end == -1) {
                        throw PythonOps.ValueError("unmatched '{{' in format spec");
                    }

                    switch (_str[end]) {
                        case '{': depth++; break;
                        case '}': depth--; break;
                        default: done = true; break;
                    }
                } while (!done && depth != 0);

                string res = _str.Substring(_index, end - _index);
                _index = end;
                return res;
            }
        }

        #endregion

        #region String Formatter

        /// <summary>
        /// Provides the built-in string formatter which is exposed to Python via the str.format API.
        /// </summary>
        private class Formatter {
            private readonly PythonContext/*!*/ _context;
            private readonly PythonTuple/*!*/ _args;
            private readonly IDictionary<object, object>/*!*/ _kwArgs;
            private int _depth;
            private int _autoNumberedIndex;

            private Formatter(PythonContext/*!*/ context, PythonTuple/*!*/ args, IDictionary<object, object>/*!*/ kwArgs) {
                Assert.NotNull(context, args, kwArgs);

                _context = context;
                _args = args;
                _kwArgs = kwArgs;
            }

            public static string/*!*/ FormatString(PythonContext/*!*/ context, string/*!*/ format, PythonTuple/*!*/ args, IDictionary<object, object>/*!*/ kwArgs) {
                Assert.NotNull(context, args, kwArgs, format);

                return new Formatter(context, args, kwArgs).ReplaceText(format);
            }

            private string ReplaceText(string format) {
                if (_depth == 2) {
                    throw PythonOps.ValueError("Max string recursion exceeded");
                }

                StringBuilder builder = new StringBuilder();

                foreach (PythonTuple pt in StringFormatParser.Parse(format)) {
                    string text = (string)pt[0];
                    string fieldName = (string)pt[1];
                    string formatSpec = (string)pt[2];
                    string conversionStr = (string)pt[3];
                    char? conversion = conversionStr != null && conversionStr.Length > 0 ? conversionStr[0] : (char?)null;

                    builder.Append(text);

                    if (fieldName != null) {
                        // get the argument value
                        object argValue = GetArgumentValue(ParseFieldName(fieldName, true));

                        // apply the conversion
                        argValue = ApplyConversion(conversion, argValue);

                        // handle computed format specifiers
                        formatSpec = ReplaceComputedFormats(formatSpec);

                        // append the string
                        builder.Append(Builtin.format(_context.SharedContext, argValue, formatSpec));
                    }
                }

                return builder.ToString();
            }

            /// <summary>
            /// Inspects a format spec to see if it contains nested format specs which
            /// we need to compute.  If so runs another string formatter on the format
            /// spec to compute those values.
            /// </summary>
            private string/*!*/ ReplaceComputedFormats(string/*!*/ formatSpec) {
                int computeStart = formatSpec.IndexOf('{');
                if (computeStart != -1) {
                    _depth++;
                    formatSpec = ReplaceText(formatSpec);
                    _depth--;
                }
                return formatSpec;
            }

            /// <summary>
            /// Given the field name gets the object from our arguments running
            /// any of the member/index accessors.
            /// </summary>
            private object GetArgumentValue(FieldName fieldName) {
                return DoAccessors(fieldName, GetUnaccessedObject(fieldName));
            }

            /// <summary>
            /// Applies the known built-in conversions to the object if a conversion is
            /// specified.
            /// </summary>
            private object ApplyConversion(char? conversion, object argValue) {
                switch (conversion) {
                    case 'a':
                        argValue = PythonOps.Ascii(_context.SharedContext, argValue);
                        break;
                    case 'r':
                        argValue = PythonOps.Repr(_context.SharedContext, argValue);
                        break;
                    case 's':
                        argValue = PythonOps.ToString(_context.SharedContext, argValue);
                        break;
                    case null:
                        // no conversion specified
                        break;
                    default:
                        throw PythonOps.ValueError("Unknown conversion specifier {0}", conversion.Value);
                }

                return argValue;
            }

            /// <summary>
            /// Gets the initial object represented by the field name - e.g. the 0 or
            /// keyword name.
            /// </summary>
            private object GetUnaccessedObject(FieldName fieldName) {
                int argIndex;
                object argValue;

                // get the object
                if (fieldName.ArgumentName.Length == 0) {
                    // auto-numbering of format specifiers
                    if (_autoNumberedIndex == -1) {
                        throw PythonOps.ValueError("cannot switch from manual field specification to automatic field numbering");
                    }
                    argValue = _args[_autoNumberedIndex++];
                } else if (Int32.TryParse(fieldName.ArgumentName, out argIndex)) {
                    if (_autoNumberedIndex > 0) {
                        throw PythonOps.ValueError("cannot switch from automatic field numbering to manual field specification");
                    }
                    _autoNumberedIndex = -1;
                    argValue = _args[argIndex];
                } else {
                    argValue = _kwArgs[fieldName.ArgumentName];
                }

                return argValue;
            }

            /// <summary>
            /// Given the object value runs the accessors in the field name (if any) against the object.
            /// </summary>
            private object DoAccessors(FieldName fieldName, object argValue) {
                foreach (FieldAccessor accessor in fieldName.Accessors) {
                    // then do any accesses against the object
                    int intVal;
                    if (accessor.IsField) {
                        argValue = PythonOps.GetBoundAttr(
                            _context.SharedContext,
                            argValue,
                            accessor.AttributeName
                        );
                    } else if (Int32.TryParse(accessor.AttributeName, out intVal)) {
                        argValue = PythonOps.GetIndex(
                            _context.SharedContext,
                            argValue,
                            ScriptingRuntimeHelpers.Int32ToObject(intVal)
                        );
                    } else {
                        argValue = PythonOps.GetIndex(
                            _context.SharedContext,
                            argValue,
                            accessor.AttributeName
                        );
                    }
                }

                return argValue;
            }

        }

        #endregion

        #region Parser helper functions

        /// <summary>
        /// Parses the field name including attribute access or element indexing.
        /// </summary>
        private static FieldName ParseFieldName(string/*!*/ str, bool reportErrors) {
            // (identifier | integer) ("." attribute_name | "[" element_index "]")*
            int index = 0;
            string arg = ParseIdentifier(str, false, ref index);

            return new FieldName(arg, ParseFieldAccessors(str, index, reportErrors));
        }

        /// <summary>
        /// Parses the field name including attribute access or element indexing.
        /// </summary>
        private static IEnumerable<FieldAccessor> ParseFieldAccessors(string/*!*/ str, int index, bool reportErrors) {
            // (identifier | integer) ("." attribute_name | "[" element_index "]")*
            while (index != str.Length && str[index] != '}') {
                char accessType = str[index];

                if (accessType == '.' || accessType == '[') {
                    index++;
                    bool isIndex = accessType == '[';
                    string identifier = ParseIdentifier(str, isIndex, ref index);

                    if (isIndex) {
                        if (index == str.Length || str[index] != ']') {
                            throw PythonOps.ValueError("Missing ']' in format string");
                        }

                        index++;
                    }

                    if (identifier.Length == 0) {
                        throw PythonOps.ValueError("Empty attribute in format string");
                    }

                    yield return new FieldAccessor(identifier, !isIndex);
                } else {
                    if (reportErrors) {
                        throw PythonOps.ValueError("Only '.' and '[' are valid in format field specifier, got {0}", accessType);
                    } else {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts accessors from our internal structure into a PythonTuple matching how CPython
        /// exposes these
        /// </summary>
        private static IEnumerable<PythonTuple> AccessorsToPython(IEnumerable<FieldAccessor> accessors) {
            foreach (FieldAccessor accessor in accessors) {

                int val;
                object attrName = accessor.AttributeName;
                if (Int32.TryParse(accessor.AttributeName, out val)) {
                    attrName = ScriptingRuntimeHelpers.Int32ToObject(val);
                }

                yield return PythonTuple.MakeTuple(
                    ScriptingRuntimeHelpers.BooleanToObject(accessor.IsField),
                    attrName
                );
            }
        }

        /// <summary>
        /// Parses an identifier and returns it
        /// </summary>
        private static string/*!*/ ParseIdentifier(string/*!*/ str, bool isIndex, ref int index) {
            int start = index;
            while (index < str.Length && str[index] != '.' && (isIndex || str[index] != '[') && (!isIndex || str[index] != ']')) {
                index++;
            }

            return str.Substring(start, index - start);
        }

        /// <summary>
        /// Encodes all the information about the field name.
        /// </summary>
        private readonly struct FieldName {
            public readonly string/*!*/ ArgumentName;
            public readonly IEnumerable<FieldAccessor>/*!*/ Accessors;

            public FieldName(string/*!*/ argumentName, IEnumerable<FieldAccessor>/*!*/ accessors) {

                Assert.NotNull(argumentName, accessors);

                ArgumentName = argumentName;
                Accessors = accessors;
            }
        }

        /// <summary>
        /// Encodes a single field accessor (.b or [number] or [str])
        /// </summary>
        private readonly struct FieldAccessor {
            public readonly string AttributeName;
            public readonly bool IsField;

            public FieldAccessor(string attributeName, bool isField) {
                AttributeName = attributeName;
                IsField = isField;
            }
        }

        #endregion
    }
}
