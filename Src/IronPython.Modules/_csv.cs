using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

[assembly: PythonModule("_csv", typeof(IronPython.Modules.PythonCsvModule))]
namespace IronPython.Modules
{
    using DialectRegistry = Dictionary<string, PythonCsvModule.Dialect>;
    public static class PythonCsvModule
    {
        public const string __doc__ = "";

        public const string __version__ = "1.0";

        public const int QUOTE_MINIMAL = 0;
        public const int QUOTE_ALL = 1;
        public const int QUOTE_NONNUMERIC = 2;
        public const int QUOTE_NONE = 3;

        private static readonly object _fieldSizeLimitKey = new object();
        private static readonly object _dialectRegistryKey = new object();

        private const int FieldSizeLimit = 128 * 1024;   /* max parsed field size */

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict)
        {
            if (!context.HasModuleState(_fieldSizeLimitKey))
            {
                context.SetModuleState(_fieldSizeLimitKey, FieldSizeLimit);
            }

            if (!context.HasModuleState(_dialectRegistryKey))
            {
                context.SetModuleState(_dialectRegistryKey,
                    new DialectRegistry());
            }
            InitModuleExceptions(context, dict);
        }

        public static int field_size_limit(CodeContext /*!*/ context, int new_limit)
        {
            PythonContext ctx = PythonContext.GetContext(context);
            int old_limit = (int)ctx.GetModuleState(_fieldSizeLimitKey);
            ctx.SetModuleState(_fieldSizeLimitKey, new_limit);
            return old_limit;
        }

        public static int field_size_limit(CodeContext/*!*/ context)
        {
            return (int)PythonContext.GetContext(context).
                GetModuleState(_fieldSizeLimitKey);
        }

        [Documentation(@"Create a mapping from a string name to a dialect class.
dialect = csv.register_dialect(name, dialect)")]
        public static void register_dialect(CodeContext/*!*/ context,
            [ParamDictionary] IDictionary<object, object> kwArgs,
            params object[] args)
        {
            string name = null;
            object dialectObj = null;
            Dialect dialect = null;

            if (args.Length < 1)
            {
                throw PythonOps.TypeError("expected at least 1 arguments, got {0}",
                     args.Length);
            }

            if (args.Length > 2)
            {
                throw PythonOps.TypeError("expected at most 2 arguments, got {0}",
                    args.Length);
            }

            name = args[0] as string;
            if (name == null)
            {
                throw PythonOps.TypeError(
                    "dialect name must be a string or unicode");
            }

            if (args.Length > 1)
                dialectObj = args[1];

            dialect = (dialectObj != null) ?
                Dialect.Create(context, kwArgs, dialectObj) :
                Dialect.Create(context, kwArgs);

            if (dialect != null)
                GetDialects(context)[name] = dialect;
        }

        /// <summary>
        /// Returns the dialects from the code context.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static DialectRegistry GetDialects(CodeContext/*!*/ context)
        {
            PythonContext ctx = PythonContext.GetContext(context);
            if (!ctx.HasModuleState(_dialectRegistryKey))
            {
                ctx.SetModuleState(_dialectRegistryKey,
                    new DialectRegistry());
            }

            return (DialectRegistry)ctx.GetModuleState(_dialectRegistryKey);
        }

        private static int GetFieldSizeLimit(CodeContext/*!*/ context)
        {
            PythonContext ctx = PythonContext.GetContext(context);
            if (!ctx.HasModuleState(_fieldSizeLimitKey))
            {
                ctx.SetModuleState(_fieldSizeLimitKey, FieldSizeLimit);
            }

            return (int)ctx.GetModuleState(_fieldSizeLimitKey);
        }

        [Documentation(@"Delete the name/dialect mapping associated with a string name.\n
    csv.unregister_dialect(name)")]
        public static void unregister_dialect(CodeContext/*!*/ context,
            string name)
        {
            DialectRegistry dialects = GetDialects(context);
            if (name == null || !dialects.ContainsKey(name))
                throw MakeError("unknown dialect");

            if (dialects.ContainsKey(name))
                dialects.Remove(name);
        }

        [Documentation(@"Return the dialect instance associated with name.
    dialect = csv.get_dialect(name)")]
        public static object get_dialect(CodeContext/*!*/ context, string name)
        {
            DialectRegistry dialects = GetDialects(context);
            if (name == null || !dialects.ContainsKey(name))
                throw MakeError("unknown dialect");
            return dialects[name];
        }

        [Documentation(@"Return a list of all know dialect names
    names = csv.list_dialects()")]
        public static List list_dialects(CodeContext/*!*/ context)
        {
            return new List(GetDialects(context).Keys);
        }

        [Documentation(@"csv_reader = reader(iterable [, dialect='excel']
                       [optional keyword args])
    for row in csv_reader:
        process(row)

    The ""iterable"" argument can be any object that returns a line
    of input for each iteration, such as a file object or a list.  The
    optional ""dialect"" parameter is discussed below.  The function
    also accepts optional keyword arguments which override settings
    provided by the dialect.

    The returned object is an iterator.  Each iteration returns a row
    of the CSV file (which can span multiple input lines)")]
        public static object reader(CodeContext/*!*/ context,
            [ParamDictionary] IDictionary<object, object> kwArgs,
            params object[] args)
        {
            object dialectObj = null;
            Dialect dialect = null;
            IEnumerator e = null;
            DialectRegistry dialects = GetDialects(context);

            if (args.Length < 1)
            {
                throw PythonOps.TypeError(
                    "expected at least 1 arguments, got {0}",
                     args.Length);
            }

            if (args.Length > 2)
            {
                throw PythonOps.TypeError(
                    "expected at most 2 arguments, got {0}",
                    args.Length);
            }

            if (!PythonOps.TryGetEnumerator(context, args[0], out e))
            {
                throw PythonOps.TypeError("argument 1 must be an iterator");
            }

            if (args.Length > 1)
                dialectObj = args[1];

            if (dialectObj is string && !dialects.ContainsKey((string)dialectObj))
                throw MakeError("unknown dialect");
            else if (dialectObj is string)
            {
                dialect = dialects[(string)dialectObj];
                dialectObj = dialect;
            }

            dialect = dialectObj != null ?
                Dialect.Create(context, kwArgs, dialectObj) :
                Dialect.Create(context, kwArgs);

            return new Reader(context, e, dialect);
        }

        public static object writer(CodeContext/*!*/ context,
            [ParamDictionary] IDictionary<object, object> kwArgs,
            params object[] args)
        {
            object output_file = null;
            object dialectObj = null;
            Dialect dialect = null;
            DialectRegistry dialects = GetDialects(context);

            if (args.Length < 1)
            {
                throw PythonOps.TypeError("expected at least 1 arguments, got {0}",
                     args.Length);
            }

            if (args.Length > 2)
            {
                throw PythonOps.TypeError("expected at most 2 arguments, got {0}",
                    args.Length);
            }

            output_file = args[0];
            if (args.Length > 1)
                dialectObj = args[1];

            if (dialectObj is string && !dialects.ContainsKey((string)dialectObj))
                throw MakeError("unknown dialect");
            else if (dialectObj is string)
            {
                dialect = dialects[(string)dialectObj];
                dialectObj = dialect;
            }

            dialect = dialectObj != null ?
                Dialect.Create(context, kwArgs, dialectObj) :
                Dialect.Create(context, kwArgs);

            return new Writer(context, output_file, dialect);
        }

        [Documentation(@"CSV dialect
The Dialect type records CSV parsing and generation options.")]
        [PythonType]
        public class Dialect
        {
            private string _delimiter = ",";
            private string _escapechar = null;
            private bool _skipinitialspace;
            private bool _doublequote = true;
            private bool _strict;
            private int _quoting = QUOTE_MINIMAL;
            private string _quotechar = "\"";
            private string _lineterminator = "\r\n";

            private static readonly string[] VALID_KWARGS = {
                                                            "dialect",
                                                            "delimiter",
                                                            "doublequote",
                                                            "escapechar",
                                                            "lineterminator",
                                                            "quotechar",
                                                            "quoting",
                                                            "skipinitialspace",
                                                            "strict"};

            private Dialect()
            {
            }

            public static Dialect Create(CodeContext/*!*/ context,
                [ParamDictionary] IDictionary<object, object> kwArgs,
                params object[] args)
            {
                object dialect = null;
                object delimiter = null;
                object doublequote = null;
                object escapechar = null;
                object lineterminator = null;
                object quotechar = null;
                object quoting = null;
                object skipinitialspace = null;
                object strict = null;
                DialectRegistry dialects = GetDialects(context);

                if (args.Length > 0 && args[0] != null)
                    dialect = args[0];

                if (dialect == null)
                    kwArgs.TryGetValue("dialect", out dialect);
                kwArgs.TryGetValue("delimiter", out delimiter);
                kwArgs.TryGetValue("doublequote", out doublequote);
                kwArgs.TryGetValue("escapechar", out escapechar);
                kwArgs.TryGetValue("lineterminator", out lineterminator);
                kwArgs.TryGetValue("quotechar", out quotechar);
                kwArgs.TryGetValue("quoting", out quoting);
                kwArgs.TryGetValue("skipinitialspace", out skipinitialspace);
                kwArgs.TryGetValue("strict", out strict);

                if (dialect != null)
                {
                    if (dialect is string)
                    {
                        string dialectName = (string)dialect;
                        if (dialects.ContainsKey(dialectName))
                            dialect = dialects[dialectName];
                        else
                            throw MakeError("unknown dialect");
                    }
                    
                    if (dialect is Dialect &&
                        delimiter == null &&
                        doublequote == null &&
                        escapechar == null &&
                        lineterminator == null &&
                        quotechar == null &&
                        quoting == null &&
                        skipinitialspace == null &&
                        strict == null)
                    {
                        return dialect as Dialect;
                    }
                }

                Dialect result = dialect != null ?
                    new Dialect(context, kwArgs, dialect) :
                    new Dialect(context, kwArgs);

                return result;
            }

            [SpecialName]
            public void DeleteMember(CodeContext/*!*/ context, string name)
            {
                if (string.Compare(name, "delimiter") == 0 ||
                    string.Compare(name, "skipinitialspace") == 0 ||
                    string.Compare(name, "doublequote") == 0 ||
                    string.Compare(name, "strict") == 0)
                {
                    throw PythonOps.TypeError("readonly attribute");
                }
                else if (string.Compare(name, "escapechar") == 0 ||
                    string.Compare(name, "lineterminator") == 0 ||
                    string.Compare(name, "quotechar") == 0 ||
                    string.Compare(name, "quoting") == 0)
                {
                    throw PythonOps.AttributeError("attribute '{0}' of " +
                        "'_csv.Dialect' objects is not writable", name);
                }
                else
                {
                    throw PythonOps.AttributeError("'_csv.Dialect' object " +
                        "has no attribute '{0}'", name);
                }
            }

            [SpecialName]
            public void SetMember(CodeContext/*!*/ context, string name, object value)
            {
                if (string.Compare(name, "delimiter") == 0 ||
                    string.Compare(name, "skipinitialspace") == 0 ||
                    string.Compare(name, "doublequote") == 0 ||
                    string.Compare(name, "strict") == 0)
                {
                    throw PythonOps.TypeError("readonly attribute");
                }
                else if (string.Compare(name, "escapechar") == 0 ||
                    string.Compare(name, "lineterminator") == 0 ||
                    string.Compare(name, "quotechar") == 0 ||
                    string.Compare(name, "quoting") == 0)
                {
                    throw PythonOps.AttributeError("attribute '{0}' of " +
                        "'_csv.Dialect' objects is not writable", name);
                }
                else
                {
                    throw PythonOps.AttributeError("'_csv.Dialect' object " +
                        "has no attribute '{0}'", name);
                }
            }

            #region Parameter Setting

            static int SetInt(string name, object src, bool found, int @default)
            {
                int result = @default;
                if (found)
                {
                    if (!(src is int))
                    {
                        throw PythonOps.TypeError("\"{0}\" must be an integer",
                            name);
                    }
                    result = (int)src;
                }
                return result;
            }

            static bool SetBool(string name, object src, bool found, bool @default)
            {
                bool result = @default;
                if (found)
                    result = PythonOps.IsTrue(src);

                return result;
            }

            static string SetChar(string name, object src, bool found, string @default)
            {
                string result = @default;
                if (found)
                {
                    if (src == null)
                        result = null;
                    else if (src is string)
                    {
                        string source = src as string;
                        if (source.Length == 0)
                            result = null;
                        else if (source.Length != 1)
                        {
                            throw PythonOps.TypeError(
                                "\"{0}\" must be an 1-character string",
                                name);
                        }
                        else
                            result = source.Substring(0, 1);
                    }
                    else
                    {
                        throw PythonOps.TypeError(
                            "\"{0}\" must be string, not {1}", name, PythonOps.GetPythonTypeName(src));
                    }
                }
                return result;
            }

            static string SetString(string name, object src, bool found, string @default)
            {
                string result = @default;
                if (found)
                {
                    if (src == null)
                        result = null;
                    else if (!(src is string))
                    {
                        throw PythonOps.TypeError(
                            "\"{0}\" must be a string", name);
                    }
                    else
                    {
                        result = src as string;
                    }
                }
                return result;
            }
            #endregion

            public Dialect(CodeContext/*!*/ context,
                [ParamDictionary] IDictionary<object, object> kwArgs,
                params object[] args)
            {
                object dialect = null;
                object delimiter = null;
                object doublequote = null;
                object escapechar = null;
                object lineterminator = null;
                object quotechar = null;
                object quoting = null;
                object skipinitialspace = null;
                object strict = null;

                Dictionary<string, bool> foundParams =
                    new Dictionary<string, bool>();

                foreach (object key in kwArgs.Keys)
                {
                    if (Array.IndexOf(VALID_KWARGS, key) < 0)
                    {
                        throw PythonOps.TypeError("'{0}' is an invalid " +
                            "keyword argument for this function", key);
                    }
                }

                if (args.Length > 0 && args[0] != null)
                {
                    dialect = args[0];
                    foundParams["dialect"] = true;
                }

                if (dialect == null)
                {
                    foundParams["dialect"] =
                        kwArgs.TryGetValue("dialect", out dialect);
                }
                foundParams["delimiter"] = kwArgs.TryGetValue("delimiter", out delimiter);
                foundParams["doublequote"] = kwArgs.TryGetValue("doublequote", out doublequote);
                foundParams["escapechar"] = kwArgs.TryGetValue("escapechar", out escapechar);
                foundParams["lineterminator"] = kwArgs.TryGetValue("lineterminator", out lineterminator);
                foundParams["quotechar"] = kwArgs.TryGetValue("quotechar", out quotechar);
                foundParams["quoting"] = kwArgs.TryGetValue("quoting", out quoting);
                foundParams["skipinitialspace"] = kwArgs.TryGetValue("skipinitialspace", out skipinitialspace);
                foundParams["strict"] = kwArgs.TryGetValue("strict", out strict);

                if (dialect != null)
                {
                    if (!foundParams["delimiter"] && delimiter == null)
                        foundParams["delimiter"] = PythonOps.TryGetBoundAttr(dialect, "delimiter", out delimiter);
                    if (!foundParams["doublequote"] && doublequote == null)
                        foundParams["doublequote"] = PythonOps.TryGetBoundAttr(dialect, "doublequote", out doublequote);
                    if (!foundParams["escapechar"] && escapechar == null)
                        foundParams["escapechar"] = PythonOps.TryGetBoundAttr(dialect, "escapechar", out escapechar);
                    if (!foundParams["lineterminator"] && lineterminator == null)
                        foundParams["lineterminator"] = PythonOps.TryGetBoundAttr(dialect, "lineterminator", out lineterminator);
                    if (!foundParams["quotechar"] && quotechar == null)
                        foundParams["quotechar"] = PythonOps.TryGetBoundAttr(dialect, "quotechar", out quotechar);
                    if (!foundParams["quoting"] && quoting == null)
                        foundParams["quoting"] = PythonOps.TryGetBoundAttr(dialect, "quoting", out quoting);
                    if (!foundParams["skipinitialspace"] && skipinitialspace == null)
                        foundParams["skipinitialspace"] = PythonOps.TryGetBoundAttr(dialect, "skipinitialspace", out skipinitialspace);
                    if (!foundParams["strict"] && strict == null)
                        foundParams["strict"] = PythonOps.TryGetBoundAttr(dialect, "strict", out strict);
                }

                _delimiter = SetChar("delimiter", delimiter,
                    foundParams["delimiter"], ",");
                _doublequote = SetBool("doublequote", doublequote,
                    foundParams["doublequote"], true);
                _escapechar = SetString("escapechar", escapechar,
                    foundParams["escapechar"], null);
                _lineterminator = SetString("lineterminator",
                    lineterminator, foundParams["lineterminator"], "\r\n");
                _quotechar = SetChar("quotechar", quotechar,
                    foundParams["quotechar"], "\"");
                _quoting = SetInt("quoting", quoting,
                    foundParams["quoting"], QUOTE_MINIMAL);
                _skipinitialspace = SetBool("skipinitialspace",
                    skipinitialspace, foundParams["skipinitialspace"], false);
                _strict = SetBool("strict", strict, foundParams["strict"], false);

                // validate options
                if (_quoting < QUOTE_MINIMAL || _quoting > QUOTE_NONE)
                    throw PythonOps.TypeError("bad \"quoting\" value");
                if (string.IsNullOrEmpty(_delimiter))
                    throw PythonOps.TypeError("\"delimiter\" must be an 1-character string");

                if ((foundParams["quotechar"] && quotechar == null) && quoting == null)
                    _quoting = QUOTE_NONE;
                if (_quoting != QUOTE_NONE && string.IsNullOrEmpty(_quotechar))
                    throw PythonOps.TypeError("quotechar must be set if quoting enabled");

                if (_lineterminator == null)
                    throw PythonOps.TypeError("lineterminator must be set");
            }

            public string escapechar
            {
                get { return _escapechar; }
            }

            public string delimiter
            {
                get { return _delimiter; }
            }

            public bool skipinitialspace
            {
                get { return _skipinitialspace; }
            }

            public bool doublequote
            {
                get { return _doublequote; }
            }

            public string lineterminator
            {
                get { return _lineterminator; }
            }

            public bool strict
            {
                get { return _strict; }
            }

            public int quoting
            {
                get { return _quoting; }
            }

            public string quotechar
            {
                get { return _quotechar; }
            }
        }

        [Documentation(@"CSV reader

Reader objects are responsible for reading and parsing tabular data
in CSV format.")]
        [PythonType]
        public class Reader : IEnumerable
        {
            private IEnumerator _input_iter;
            private Dialect _dialect;
            private int _line_num;
            private ReaderIterator _iterator;

            public Reader(CodeContext/*!*/ context, IEnumerator input_iter,
                Dialect dialect)
            {
                _input_iter = input_iter;
                _dialect = dialect;
                _iterator = new ReaderIterator(context, this);
            }

            public object __next__()
            {
                if (!_iterator.MoveNext())
                    throw PythonOps.StopIteration();

                return _iterator.Current;
            }

            #region IEnumerable Members

            public IEnumerator GetEnumerator()
            {
                return _iterator;
            }

            private sealed class ReaderIterator : IEnumerator, IEnumerable
            {
                private CodeContext _context;
                private Reader _reader;
                private List _fields = new List();
                private bool _is_numeric_field;
                private State _state = State.StartRecord;
                private StringBuilder _field = new StringBuilder();
                private IEnumerator _iterator;

                enum State
                {
                    StartRecord,
                    StartField,
                    EscapedChar,
                    InField,
                    InQuotedField,
                    EscapeInQuotedField,
                    QuoteInQuotedField,
                    EatCrNl
                }

                public ReaderIterator(CodeContext/*!*/ context, Reader reader)
                {
                    _context = context;
                    _reader = reader;
                    _iterator = _reader._input_iter;
                }

                #region IEnumerator Members

                public object Current
                {
                    get { return new List(_fields); }
                }

                public bool MoveNext()
                {
                    bool result = false;
                    Reset();

                    do
                    {
                        object lineobj = null;
                        if (!_iterator.MoveNext())
                        {
                            // End of input OR exception
                            if(_field.Length > 0 || _state == State.InQuotedField) {
                                if(_reader._dialect.strict) {
                                    throw MakeError("unexpected end of data");
                                } else {
                                    ParseSaveField();
                                    return true;
                                }
                            }
                            return false;
                        }
                        else
                        {
                            lineobj = _iterator.Current;
                        }

                        _reader._line_num++;

                        if (lineobj is char)
                            lineobj = lineobj.ToString();

                        if (!(lineobj is string))
                        {
                            throw PythonOps.TypeError("expected string or " +
                                "Unicode object, {0} found",
                                DynamicHelpers.GetPythonType(lineobj.GetType()));
                        }

                        string line = lineobj as string;
                        if (!string.IsNullOrEmpty(line))
                        {
                            for (int i = 0; i < line.Length; i++)
                            {
                                char c = line[i];
                                if (c == '\0')
                                    throw MakeError("line contains NULL byte");

                                ProcessChar(c);
                            }
                        }

                        ProcessChar('\0');
                        result = true;

                    } while (_state != State.StartRecord);

                    return result;
                }

                public void Reset()
                {
                    _state = State.StartRecord;
                    _fields.Clear();
                    _is_numeric_field = false;
                    _field.Clear();
                }

                #endregion

                #region IEnumerable Members

                public IEnumerator GetEnumerator()
                {
                    return this;
                }

                #endregion

                private void ProcessChar(char c)
                {
                    Dialect dialect = _reader._dialect;
                    switch (_state)
                    {
                        case State.StartRecord:
                            // start of record
                            if (c == '\0')
                            {
                                // empty line, will return empty list
                                break;
                            }
                            else if (c == '\n' || c == '\r')
                            {
                                _state = State.EatCrNl;
                                break;
                            }

                            // normal character, handle as start of field
                            _state = State.StartField;
                            goto case State.StartField;

                        case State.StartField:
                            // expecting field
                            if (c == '\n' || c == '\r' || c == '\0')
                            {
                                // save empty field - return [fields]
                                ParseSaveField();
                                _state = (c == '\0' ?
                                    State.StartRecord : State.EatCrNl);
                            }
                            else if (!string.IsNullOrEmpty(dialect.quotechar) &&
                                c == dialect.quotechar[0] &&
                                dialect.quoting != QUOTE_NONE)
                            {
                                // start quoted field
                                _state = State.InQuotedField;
                            }
                            else if (!string.IsNullOrEmpty(dialect.escapechar) &&
                                c == dialect.escapechar[0])
                            {
                                // possible escaped char
                                _state = State.EscapedChar;
                            }
                            else if (c == ' ' && dialect.skipinitialspace)
                            {
                                // ignore space at start of field
                            }
                            else if (c == dialect.delimiter[0])
                            {
                                // save empty field
                                ParseSaveField();
                            }
                            else
                            {
                                // begin new unquoted field
                                if (dialect.quoting == QUOTE_NONNUMERIC)
                                    _is_numeric_field = true;

                                ParseAddChar(c);
                                _state = State.InField;
                            }
                            break;

                        case State.EscapedChar:
                            if (c == '\0')
                                c = '\n';

                            ParseAddChar(c);
                            _state = State.InField;
                            break;

                        case State.InField:
                            // in unquoted field
                            if (c == '\n' || c == '\r' || c == '\0')
                            {
                                // end of line, return [fields]
                                ParseSaveField();
                                _state = (c == '\0' ? State.StartRecord : State.EatCrNl);
                            }
                            else if (!string.IsNullOrEmpty(dialect.escapechar) &&
                                c == dialect.escapechar[0])
                            {
                                // possible escaped character
                                _state = State.EscapedChar;
                            }
                            else if (c == dialect.delimiter[0])
                            {
                                // save field - wait for new field
                                ParseSaveField();
                                _state = State.StartField;
                            }
                            else
                            {
                                // normal character - save in field
                                ParseAddChar(c);
                            }
                            break;

                        case State.InQuotedField:
                            // in quoted field
                            if (c == '\0')
                            {
                                // ignore null character
                            }
                            else if (!string.IsNullOrEmpty(dialect.escapechar) &&
                                c == dialect.escapechar[0])
                            {
                                // possible escape character
                                _state = State.EscapeInQuotedField;
                            }
                            else if (!string.IsNullOrEmpty(dialect.quotechar) &&
                                c == dialect.quotechar[0] &&
                                dialect.quoting != QUOTE_NONE)
                            {
                                if (dialect.doublequote)
                                {
                                    // doublequote; " represented by ""
                                    _state = State.QuoteInQuotedField;
                                }
                                else
                                {
                                    // end of quote part of field
                                    _state = State.InField;
                                }
                            }
                            else
                            {
                                // normal character - save in field
                                ParseAddChar(c);
                            }
                            break;

                        case State.EscapeInQuotedField:
                            if (c == '\0')
                                c = '\n';

                            ParseAddChar(c);
                            _state = State.InQuotedField;
                            break;

                        case State.QuoteInQuotedField:
                            // doublequote - seen a quote in a quoted field
                            if (dialect.quoting != QUOTE_NONE &&
                                c == dialect.quotechar[0])
                            {
                                // save "" as "
                                ParseAddChar(c);
                                _state = State.InQuotedField;
                            }
                            else if (c == dialect.delimiter[0])
                            {
                                // save field - wait for new field
                                ParseSaveField();
                                _state = State.StartField;
                            }
                            else if (c == '\n' || c == '\r' || c == '\0')
                            {
                                // end of line - return [fields]
                                ParseSaveField();
                                _state = (c == '\0' ? State.StartRecord : State.EatCrNl);
                            }
                            else if (!dialect.strict)
                            {
                                ParseAddChar(c);
                                _state = State.InField;
                            }
                            else
                            {
                                // illegal!
                                throw MakeError("'{0}' expected after '{1}'",
                                    dialect.delimiter, dialect.quotechar);
                            }
                            break;

                        case State.EatCrNl:
                            if (c == '\n' || c == '\r')
                            {
                                // eat the CR NL
                            }
                            else if (c == '\0')
                                _state = State.StartRecord;
                            else
                            {
                                throw MakeError("new-line character seen " +
                                    "in unquoted field - do you need to open" +
                                    " the file in universal-newline mode?");
                            }
                            break;
                    }
                }

                private void ParseAddChar(char c)
                {
                    int field_size_limit = GetFieldSizeLimit(_context);
                    if (_field.Length >= field_size_limit)
                    {
                        throw MakeError(
                            string.Format("field larger than field " +
                                "limit ({0})", field_size_limit));
                    }

                    _field.Append(c);
                }

                private void ParseSaveField()
                {
                    string field = _field.ToString();
                    if (_is_numeric_field)
                    {
                        _is_numeric_field = false;

                        double tmp;
                        if (double.TryParse(field, out tmp))
                        {
                            if (field.Contains("."))
                                _fields.Add(tmp);
                            else
                                _fields.Add((int)tmp);
                        }
                        else
                        {
                            throw PythonOps.ValueError(
                                "invalid literal for float(): {0}", field);
                        }
                    }
                    else
                        _fields.Add(field);

                    _field.Clear();
                }
            }

            #endregion

            public object dialect
            {
                get { return _dialect; }
            }

            public int line_num
            {
                get { return _line_num; }
            }
        }

        [Documentation(@"CSV writer

Writer objects are responsible for generating tabular data
in CSV format from sequence input.")]
        [PythonType]
        public class Writer
        {
            private Dialect _dialect;
            private object _writeline;

            private List<string> _rec = new List<string>();
            private int _num_fields;

            public Writer(CodeContext/*!*/ context, object output_file,
                Dialect dialect)
            {
                _dialect = dialect;
                if (!PythonOps.TryGetBoundAttr(
                    output_file, "write", out _writeline) ||
                    _writeline == null ||
                    !PythonOps.IsCallable(context, _writeline))
                {
                    throw PythonOps.TypeError(
                        "argument 1 must have a \"write\" method");
                }
            }

            public object dialect
            {
                get { return _dialect; }
            }

            [Documentation(@"writerow(sequence)

Construct and write a CSV record from a sequence of fields.  Non-string
elements will be converted to string.")]
            public void writerow(CodeContext/*!*/ context, object sequence)
            {
                IEnumerator e = null;
                if (!PythonOps.TryGetEnumerator(context, sequence, out e))
                    throw MakeError("sequence expected");

                int rowlen = PythonOps.Length(sequence);

                // join all fields in internal buffer
                JoinReset();
                while (e.MoveNext())
                {
                    object field = e.Current;
                    bool quoted = false;

                    switch (_dialect.quoting)
                    {
                        case QUOTE_NONNUMERIC:
                            quoted = !(PythonOps.CheckingConvertToFloat(field) ||
                                PythonOps.CheckingConvertToInt(field) ||
                                PythonOps.CheckingConvertToLong(field));
                            break;
                        case QUOTE_ALL:
                            quoted = true;
                            break;
                    }

                    if (field is string)
                        JoinAppend((string)field, quoted, rowlen == 1);
                    else if (field is double)
                    {
                        JoinAppend(DoubleOps.__repr__(context, (double)field),
                            quoted, rowlen == 1);
                    }
                    else if (field is float)
                    {
                        JoinAppend(SingleOps.__repr__(context, (float)field),
                            quoted, rowlen == 1);
                    }
                    else if (field == null)
                        JoinAppend(string.Empty, quoted, rowlen == 1);
                    else
                        JoinAppend(field.ToString(), quoted, rowlen == 1);
                }

                _rec.Add(_dialect.lineterminator);

                PythonOps.CallWithContext(
                    context, _writeline, string.Join("", _rec.ToArray()));
            }

            [Documentation(@"writerows(sequence of sequences)

Construct and write a series of sequences to a csv file.  Non-string 
elements will be converted to string.")]
            public void writerows(CodeContext/*!*/ context, object sequence)
            {
                IEnumerator e = null;
                if (!PythonOps.TryGetEnumerator(context, sequence, out e))
                {
                    throw PythonOps.TypeError(
                        "writerows() argument must be iterable");
                }

                while (e.MoveNext())
                {
                    writerow(context, e.Current);
                }
            }

            private void JoinReset()
            {
                _num_fields = 0;
                _rec.Clear();
            }

            private void JoinAppend(string field, bool quoted, bool quote_empty)
            {
                // if this is not the first field, we need a field separator
                if (_num_fields > 0)
                    _rec.Add(_dialect.delimiter);

                List<char> need_escape = new List<char>();
                if (_dialect.quoting == QUOTE_NONE)
                {
                    need_escape.AddRange(_dialect.lineterminator.ToCharArray());
                    if (!string.IsNullOrEmpty(_dialect.escapechar))
                        need_escape.Add(_dialect.escapechar[0]);
                    if (!string.IsNullOrEmpty(_dialect.delimiter))
                        need_escape.Add(_dialect.delimiter[0]);
                    if (!string.IsNullOrEmpty(_dialect.quotechar))
                        need_escape.Add(_dialect.quotechar[0]);
                }
                else
                {
                    List<char> temp = new List<char>();
                    temp.AddRange(_dialect.lineterminator.ToCharArray());
                    if (!string.IsNullOrEmpty(_dialect.delimiter))
                        temp.Add(_dialect.delimiter[0]);
                    if (!string.IsNullOrEmpty(_dialect.escapechar))
                        temp.Add(_dialect.escapechar[0]);

                    if (field.IndexOfAny(temp.ToArray()) >= 0)
                        quoted = true;

                    need_escape.Clear();

                    if (!string.IsNullOrEmpty(_dialect.quotechar) && field.Contains(_dialect.quotechar))
                    {
                        if (_dialect.doublequote)
                        {
                            field = field.Replace(_dialect.quotechar,
                                _dialect.quotechar + _dialect.quotechar);
                            quoted = true;
                        }
                        else
                        {
                            need_escape.Add(_dialect.quotechar[0]);
                        }
                    }
                }

                foreach (char c in need_escape)
                {
                    if (field.IndexOf(c) >= 0)
                    {
                        if (string.IsNullOrEmpty(_dialect.escapechar))
                            throw MakeError("need to escape, but no escapechar set");
                        field = field.Replace(c.ToString(), _dialect.escapechar + c);
                    }
                }

                // If field is empty check if it needs to be quoted
                if (string.IsNullOrEmpty(field) && quote_empty)
                {
                    if (_dialect.quoting == QUOTE_NONE)
                        throw MakeError("single empty field record must be quoted");
                    quoted = true;
                }

                if (quoted)
                    field = _dialect.quotechar + field + _dialect.quotechar;

                _rec.Add(field);
                _num_fields++;
            }
        }

        public static PythonType Error;
        internal static Exception MakeError(params object[] args)
        {
            return PythonOps.CreateThrowable(Error, args);
        }

        private static void InitModuleExceptions(PythonContext context,
            PythonDictionary dict)
        {
            Error = context.EnsureModuleException("csv.Error",
                PythonExceptions.Exception, dict, "Error", "_csv");
        }
    }

#if CLR2
    static class StringBuilderExtensions
    {
        internal static StringBuilder Clear(this StringBuilder sb) 
        {
            sb.Length = 0;
            return sb;
        }
    }
#endif
}
