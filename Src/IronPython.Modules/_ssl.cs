// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_FULL_NET

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_ssl", typeof(IronPython.Modules.PythonSsl))]
namespace IronPython.Modules {

    internal class Asn1Object {

        public Asn1Object(string shortName, string longName, int nid, byte[] oid) {
            ShortName = shortName;
            LongName = longName;
            NID = nid;
            OID = oid;
            OIDString = string.Join(".", OID);
        }

        public string ShortName {
            get; set;
        }
        public string LongName {
            get; set;
        }

        public int NID {
            get; set;
        }

        public byte[] OID {
            get; set;
        }

        public string OIDString {
            get;
        }

        public PythonTuple ToTuple() {
            return PythonTuple.MakeTuple(NID, ShortName, LongName, OIDString);
        }
    }

    public static class PythonSsl {
        public const string __doc__ = "Implementation module for SSL socket operations.  See the socket module\nfor documentation.";
        public const int OPENSSL_VERSION_NUMBER = 9437184;
        public static readonly PythonTuple OPENSSL_VERSION_INFO = PythonTuple.MakeTuple(0, 0, 0, 0, 0);
        public static readonly object _OPENSSL_API_VERSION = OPENSSL_VERSION_INFO;
        public const string OPENSSL_VERSION = "OpenSSL 0.0.0 (.NET SSL)";

        private static readonly List<Asn1Object> _asn1Objects = new List<Asn1Object>();

        static PythonSsl() {
            _asn1Objects.AddRange(new Asn1Object[] {
                new Asn1Object("serverAuth", "TLS Web Server Authentication", 129, new byte[] { 1, 3, 6, 1 ,5, 5, 7, 3, 1 }),
                new Asn1Object("clientAuth", "TLS Web Client Authentication", 130, new byte[] { 1, 3, 6, 1 ,5, 5, 7, 3, 2 }),
            });
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            var sslError = context.EnsureModuleException("SSLError", PythonSocket.error, dict, "SSLError", "ssl");
            context.EnsureModuleException("SSLZeroReturnError", sslError, dict, "SSLZeroReturnError", "ssl");
            context.EnsureModuleException("SSLWantWriteError", sslError, dict, "SSLWantWriteError", "ssl");
            context.EnsureModuleException("SSLSyscallError", sslError, dict, "SSLSyscallError", "ssl");
            context.EnsureModuleException("SSLEOFError", sslError, dict, "SSLEOFError", "ssl");
            context.EnsureModuleException("SSLWantReadError", sslError, dict, "SSLWantReadError", "ssl");
        }

        #region Stubs for RAND functions

        // The RAND_ functions are effectively no-ops, as the BCL draws on system sources
        // for cryptographically-strong randomness and doesn't need (or accept) user input

        public static void RAND_add(object buf, double entropy) {
            if (!(buf is string) && !(buf is IBufferProtocol)) {
                throw PythonOps.TypeError($"'{PythonOps.GetPythonTypeName(buf)}' does not support the buffer interface");
            }
        }

        public static int RAND_status() => 1; // always ready

        public static object RAND_bytes(int num) => PythonNT.urandom(num);

        public static object RAND_pseudo_bytes(int num) => PythonTuple.MakeTuple(PythonNT.urandom(num), true);

        #endregion

        [PythonType]
        public class _SSLContext {
            internal readonly X509Certificate2Collection _cert_store = new X509Certificate2Collection();
            internal string _cafile;
            private int _verify_mode = SSL_VERIFY_NONE;

            public _SSLContext(CodeContext context, int protocol) {
                if (protocol != PROTOCOL_SSLv2 && protocol != PROTOCOL_SSLv23 && protocol != PROTOCOL_SSLv3 &&
                    protocol != PROTOCOL_TLSv1 && protocol != PROTOCOL_TLSv1_1 && protocol != PROTOCOL_TLSv1_2) {
                    throw PythonOps.ValueError("invalid protocol version");
                }

                this.protocol = protocol;

                if (protocol != PROTOCOL_SSLv2)
                    options |= OP_NO_SSLv2;
                if (protocol != PROTOCOL_SSLv3)
                    options |= OP_NO_SSLv3;

                verify_mode = SSL_VERIFY_NONE;
                check_hostname = false;
            }

            public void set_ciphers(CodeContext context, string ciphers) {

            }

            public int options {
                get; set;
            }

            public int verify_mode {
                get {
                    return _verify_mode;
                }
                set {
                    if (_verify_mode != CERT_NONE && _verify_mode != CERT_OPTIONAL && _verify_mode != CERT_REQUIRED) {
                        throw PythonOps.ValueError("invalid value for verify_mode");
                    }
                    _verify_mode = value;
                }
            }

            public int protocol {
                get; set;
            }

            public bool check_hostname {
                get; set;
            }

            public void set_default_verify_paths(CodeContext context) {

            }

            public void set_ecdh_curve(CodeContext context, [NotNull] string curve) {
                if (curve != "prime256v1")
                    throw PythonOps.ValueError($"unknown elliptic curve name {PythonOps.Repr(context, curve)}");
            }

            public void set_ecdh_curve(CodeContext context, [NotNull] Bytes curve) {
                if (curve.MakeString() != "prime256v1")
                    throw PythonOps.ValueError($"unknown elliptic curve name {PythonOps.Repr(context, curve)}");
            }

            public void load_cert_chain(string certfile, string keyfile = null, object password = null) {

            }

            public void load_verify_locations(CodeContext context, object cafile = null, string capath = null, object cadata = null) {
                if (cafile == null && capath == null && cadata == null) {
                    throw PythonOps.TypeError("cafile, capath and cadata cannot be all omitted");
                }

                if (cafile is not null) {
                    if (cafile is string s) {
                        _cafile = s;
                    } else if (cafile is Bytes b) {
                        _cafile = b.MakeString();
                    } else {
                        throw PythonOps.TypeError("cafile should be a valid filesystem path");
                    }
#if NET5_0_OR_GREATER
                    _cert_store.ImportFromPemFile(_cafile);
#else
                    _cert_store.Add(ReadCertificate(context, _cafile));
#endif
                }

                if (capath != null) {
                }

                if (cadata != null) {
                    var cabuf = cadata as IBufferProtocol;
                    if (cabuf != null) {
                        int pos = 0;
                        byte[] contents;
                        using (IPythonBuffer buf = cabuf.GetBuffer()) {
                            contents = buf.AsReadOnlySpan().ToArray();
                        }
                        while (pos < contents.Length) {
                            byte[] curr = new byte[contents.Length - pos];
                            Array.Copy(contents, pos, curr, 0, contents.Length - pos);
                            var cert = new X509Certificate2(curr);
                            _cert_store.Add(cert);
                            pos += cert.GetRawCertData().Length;
                        }
                    }
                }
            }

            public object _wrap_socket(CodeContext context, PythonSocket.socket sock, bool server_side, string server_hostname = null) {
                return new _SSLSocket(context, this, sock, server_side, server_hostname);
            }
        }

        [PythonType]
        public class _SSLSocket {
            private SslStream _sslStream;
            private readonly PythonSocket.socket _socket;
            private readonly X509Certificate2Collection _certCollection;
            private readonly X509Certificate _cert;
            private readonly int _protocol, _certsMode;
            private readonly bool _validate, _serverSide;
            private readonly CodeContext _context;
            private readonly RemoteCertificateValidationCallback _callback;
            private Exception _validationFailure;
            internal string _serverHostName;

            public _SSLContext context { get; }

            internal _SSLSocket(CodeContext context, _SSLContext sslcontext, PythonSocket.socket sock, bool server_side, string server_hostname) {
                if (sock == null) {
                    throw PythonOps.TypeError("expected socket object, got None");
                }

                this.context = sslcontext;
                _serverSide = server_side;
                _serverHostName = server_hostname;

                _certsMode = sslcontext.verify_mode;

                bool validate;
                RemoteCertificateValidationCallback callback;
                switch (_certsMode) {
                    case PythonSsl.CERT_NONE:
                        validate = false;
                        callback = CertValidationCallback;
                        break;
                    case PythonSsl.CERT_OPTIONAL:
                        validate = true;
                        callback = CertValidationCallbackOptional;
                        break;
                    case PythonSsl.CERT_REQUIRED:
                        validate = true;
                        callback = CertValidationCallbackRequired;
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("bad certs_mode: {0}", _certsMode));
                }

                _callback = callback;

                if (sslcontext._cert_store != null) {
                    _certCollection = sslcontext._cert_store;
                }

                if (sslcontext._cafile != null) {
                    _cert = PythonSsl.ReadCertificate(context, sslcontext._cafile);
                }

                _socket = sock;

                EnsureSslStream(false);

                _protocol = sslcontext.protocol | sslcontext.options;
                _validate = validate;
                _context = context;
            }

            private void EnsureSslStream(bool throwWhenNotConnected) {
                if (_sslStream == null && _socket._socket.Connected) {
                    if (_serverSide) {
                        _sslStream = new SslStream(
                            new NetworkStream(_socket._socket, false),
                            true,
                            _callback
                        );
                    } else {
                        _sslStream = new SslStream(
                            new NetworkStream(_socket._socket, false),
                            true,
                            _callback,
                            CertSelectLocal
                        );
                    }
                }

                if (throwWhenNotConnected && _sslStream == null) {
                    throw PythonExceptions.CreateThrowable(PythonSocket.error, 10057, "A request to send or receive data was disallowed because the socket is not connected.");
                }
            }

            internal bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                return true;
            }

            internal bool CertValidationCallbackOptional(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                if (!_serverSide) {
                    if (certificate != null && sslPolicyErrors != SslPolicyErrors.None) {
                        ValidateCertificate(certificate, chain, sslPolicyErrors);
                    }
                }

                return true;
            }

            internal X509Certificate CertSelectLocal(object sender, string targetHost, X509CertificateCollection collection, X509Certificate remoteCertificate, string[] acceptableIssuers) {
                if (acceptableIssuers != null && acceptableIssuers.Length > 0 && collection != null && collection.Count > 0) {
                    // Use the first certificate that is from an acceptable issuer.
                    foreach (X509Certificate certificate in collection) {
                        string issuer = certificate.Issuer;
                        if (Array.IndexOf(acceptableIssuers, issuer) != -1)
                            return certificate;
                    }
                }

                if (collection != null && collection.Count > 0) {
                    return collection[0];
                }

                return null;
            }

            internal bool CertValidationCallbackRequired(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                if (!_serverSide) {
                    // client check
                    if (certificate == null) {
                        ValidationError(SslPolicyErrors.None);
                    } else if (sslPolicyErrors != SslPolicyErrors.None) {
                        ValidateCertificate(certificate, chain, sslPolicyErrors);
                    }
                }

                return true;
            }

            private void ValidateCertificate(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                chain = new X509Chain();
                X509Certificate2Collection certificates = new X509Certificate2Collection();
                foreach (object cert in _certCollection) {
                    if (cert is X509Certificate2) {
                        certificates.Add((X509Certificate2)cert);
                    } else if (cert is X509Certificate) {
                        certificates.Add(new X509Certificate2((X509Certificate)cert));
                    }
                }
                chain.ChainPolicy.ExtraStore.AddRange(certificates);
                chain.Build(new X509Certificate2(certificate));

                if (chain.ChainStatus.Length > 0) {
                    foreach (var elem in chain.ChainStatus) {
                        if (elem.Status == X509ChainStatusFlags.UntrustedRoot) {
                            bool isOk = false;
                            foreach (var cert in _certCollection) {
                                if (certificate.Issuer == cert.Subject) {
                                    isOk = true;
                                }
                            }

                            if (isOk) {
                                continue;
                            }
                        }

                        ValidationError(sslPolicyErrors);
                        break;
                    }
                }
            }

            private void ValidationError(object reason) {
                _validationFailure = PythonExceptions.CreateThrowable(PythonSsl.SSLError(_context), "errors while validating certificate chain: ", reason.ToString());
            }

            public void do_handshake() {
                try {
                    // make sure the remote side hasn't shutdown before authenticating so we don't
                    // hang if we're in blocking mode.
#pragma warning disable 219 // unused variable
                    int available = _socket._socket.Available;
#pragma warning restore 219
                } catch (SocketException) {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, "socket closed before handshake");
                }

                EnsureSslStream(true);

                var enabledSslProtocols = GetProtocolType(_protocol);

                try {
                    if (_serverSide) {
                        _sslStream.AuthenticateAsServer(_cert, _certsMode == PythonSsl.CERT_REQUIRED, enabledSslProtocols, false);
                    } else {

                        var collection = new X509CertificateCollection();

                        if (_cert != null) {
                            collection.Add(_cert);
                        }
                        _sslStream.AuthenticateAsClient(_serverHostName ?? _socket._hostName, collection, enabledSslProtocols, false);
                    }
                } catch (AuthenticationException e) {
                    ((IDisposable)_socket._socket).Dispose();
                    throw PythonExceptions.CreateThrowable(PythonSsl.SSLError(_context), "errors while performing handshake: ", e.ToString());
                }

                if (_validationFailure != null) {
                    throw _validationFailure;
                }
            }

            public PythonSocket.socket shutdown() {
                _sslStream.Dispose();
                return _socket;
            }

            /* supported communication based upon what the client & server specify
             * as per the CPython docs:
             * client / server SSLv2 SSLv3 SSLv23 TLSv1 TLSv1.1 TLSv1.2
                         SSLv2   yes    no    yes    no      no      no
                         SSLv3    no   yes    yes    no      no      no
                        SSLv23    no   yes    yes   yes     yes     yes
                         TLSv1    no    no    yes   yes      no      no
                       TLSv1.1    no    no    yes    no     yes      no
                       TLSv1.2    no    no    yes    no      no     yes
             */

            private static SslProtocols GetProtocolType(int type) {
                SslProtocols result = SslProtocols.None;

                switch (type & ~PythonSsl.OP_NO_ALL) {
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CS0618 // Type or member is obsolete
                    case PythonSsl.PROTOCOL_SSLv2:
                        result = SslProtocols.Ssl2;
                        break;
                    case PythonSsl.PROTOCOL_SSLv3:
                        result = SslProtocols.Ssl3;
                        break;
                    case PythonSsl.PROTOCOL_SSLv23:
                        result = SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
                        break;
#pragma warning restore CS0618 // Type or member is obsolete
                    case PythonSsl.PROTOCOL_TLSv1:
                        result = SslProtocols.Tls;
                        break;
                    case PythonSsl.PROTOCOL_TLSv1_1:
                        result = SslProtocols.Tls11;
                        break;
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
                    case PythonSsl.PROTOCOL_TLSv1_2:
                        result = SslProtocols.Tls12;
                        break;
                    default:
                        throw new InvalidOperationException("bad ssl protocol type: " + type);
                }
                // Filter out requested protocol exclusions:
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CS0618 // Type or member is obsolete
                result &= (type & PythonSsl.OP_NO_SSLv3) != 0 ? ~SslProtocols.Ssl3 : ~SslProtocols.None;
                result &= (type & PythonSsl.OP_NO_SSLv2) != 0 ? ~SslProtocols.Ssl2 : ~SslProtocols.None;
#pragma warning restore CS0618 // Type or member is obsolete
                result &= (type & PythonSsl.OP_NO_TLSv1) != 0 ? ~SslProtocols.Tls : ~SslProtocols.None;
                result &= (type & PythonSsl.OP_NO_TLSv1_1) != 0 ? ~SslProtocols.Tls11 : ~SslProtocols.None;
                result &= (type & PythonSsl.OP_NO_TLSv1_2) != 0 ? ~SslProtocols.Tls12 : ~SslProtocols.None;
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
                return result;
            }

            public PythonTuple cipher() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    return PythonTuple.MakeTuple(
                        _sslStream.CipherAlgorithm.ToString(),
                        ProtocolToPython(),
                        _sslStream.CipherStrength
                    );
                }
                return null;
            }

            private string ProtocolToPython() {
                switch (_sslStream.SslProtocol) {
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CS0618 // Type or member is obsolete
                    case SslProtocols.Ssl2: return "SSLv2";
                    case SslProtocols.Ssl3: return "TLSv1/SSLv3";
#pragma warning restore CS0618 // Type or member is obsolete
                    case SslProtocols.Tls: return "TLSv1";
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
                    default: return _sslStream.SslProtocol.ToString();
                }
            }

            public object peer_certificate(bool binary_form) {
                var peerCert = _sslStream?.RemoteCertificate;

                if (peerCert != null) {
                    if (binary_form) {
                        return Bytes.Make(peerCert.GetRawCertData());
                    } else if (_validate) {
                        return CertificateToPython(_context, peerCert);
                    }
                }
                return null;
            }

            public int pending() {
                return _socket._socket.Available;
            }

            [Documentation("issuer() -> issuer_certificate\n\n"
                + "Returns a string that describes the issuer of the server's certificate. Only useful for debugging purposes."
                )]
            public string issuer() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    X509Certificate remoteCertificate = _sslStream.RemoteCertificate;
                    if (remoteCertificate != null) {
                        return remoteCertificate.Issuer;
                    } else {
                        return String.Empty;
                    }
                }
                return String.Empty;
            }

            [Documentation(@"read(size, [buffer])
Read up to size bytes from the SSL socket.")]
            public object read(CodeContext/*!*/ context, int size, ByteArray buffer = null) {
                EnsureSslStream(true);

                try {
                    byte[] buf = new byte[2048];
                    MemoryStream result = new MemoryStream(size);
                    while (true) {
                        int readLength = (size < buf.Length) ? size : buf.Length;
                        int bytes = _sslStream.Read(buf, 0, readLength);
                        if (bytes > 0) {
                            result.Write(buf, 0, bytes);
                            size -= bytes;
                        }

                        if (bytes == 0 || size == 0 || bytes < readLength) {
                            var res = result.ToArray();
                            if (buffer == null)
                                return Bytes.Make(res);

                            // TODO: get rid of the MemoryStream and write directly to the buffer
                            buffer[new Slice(0, res.Length)] = res;
                            return res.Length;
                        }
                    }
                } catch (Exception e) {
                    throw PythonSocket.MakeException(context, e);
                }
            }

            [Documentation("server() -> server_certificate\n\n"
                + "Returns a string that describes the server's certificate. Only useful for debugging purposes."
                )]
            public string server() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    X509Certificate remoteCertificate = _sslStream.RemoteCertificate;
                    if (remoteCertificate != null) {
                        return remoteCertificate.Subject;
                    }
                }
                return String.Empty;
            }

            [Documentation(@"Writes the bytes-like object b into the SSL object.

Returns the number of bytes written.")]
            public int write(CodeContext/*!*/ context, Bytes data) {
                EnsureSslStream(true);

                byte[] buffer = data.UnsafeByteArray;
                try {
                    _sslStream.Write(buffer);
                    return buffer.Length;
                } catch (Exception e) {
                    throw PythonSocket.MakeException(context, e);
                }
            }
        }

#nullable enable

        [PythonType]
        public class MemoryBIO {
            private bool _write_eof;

            public bool eof { get; private set; }
            public int pending { get; private set; }

            private Bytes? buf;
            private Queue<Bytes> queue = new Queue<Bytes>();

            public MemoryBIO() { }

            public Bytes read(int size = -1) {
                if (size == 0 || eof) {
                    return Bytes.Empty;
                }
                if (size == -1 || size > pending) {
                    size = pending;
                }

                byte[] res = new byte[size];
                var resSpan = res.AsSpan();

                if (buf is not null) {
                    var span = buf.AsSpan();
                    var length = resSpan.Length;
                    if (length < span.Length) {
                        buf = Bytes.Make(span.Slice(length).ToArray());
                        span = span.Slice(0, length);
                    } else {
                        buf = null;
                    }
                    span.CopyTo(resSpan);
                    resSpan = resSpan.Slice(span.Length);
                }

                while (resSpan.Length > 0) {
                    Debug.Assert(buf is null && queue.Count > 0);
                    var span = queue.Dequeue().AsSpan();
                    var length = resSpan.Length;
                    if (length < span.Length) {
                        buf = Bytes.Make(span.Slice(length).ToArray());
                        span = span.Slice(0, length);
                    }
                    span.CopyTo(resSpan);
                    resSpan = resSpan.Slice(span.Length);
                }

                pending -= size;
                if (_write_eof && pending == 0) eof = true;
                return Bytes.Make(res);
            }

            public int write(CodeContext context, [NotNull] IBufferProtocol b) {
                if (_write_eof) throw PythonExceptions.CreateThrowable(SSLError(context), "cannot write() after write_eof()");

                if (b is not Bytes bytes) {
                    using var buffer = b.GetBuffer();
                    bytes = Bytes.Make(buffer.ToArray());
                }
                if (bytes.Count == 0) return 0;
                queue.Enqueue(bytes);
                pending += bytes.Count;
                return bytes.Count;
            }

            public void write_eof() {
                _write_eof = true;
                if (pending == 0) eof = true;
            }
        }

#nullable restore

        public static object txt2obj(CodeContext context, string txt, bool name = false) {
            Asn1Object obj = null;
            if (name) {
                obj = _asn1Objects.Where(x => txt == x.OIDString || txt == x.ShortName || txt == x.LongName).FirstOrDefault();
            } else {
                obj = _asn1Objects.Where(x => txt == x.OIDString).FirstOrDefault();
            }

            if (obj == null) {
                throw PythonOps.ValueError("unknown object '{0}'", txt);
            }

            return obj.ToTuple();
        }

        public static object nid2obj(CodeContext context, int nid) {
            if (nid < 0) {
                throw PythonOps.ValueError("NID must be positive");
            }

            var obj = _asn1Objects.Where(x => x.NID == nid).FirstOrDefault();
            if (obj == null) {
                throw PythonOps.ValueError("unknown NID {0}", nid);
            }

            return obj.ToTuple();
        }

        public static PythonList enum_certificates(string store_name) {
            X509Store store = null;
            try {
                store = new X509Store(store_name, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var result = new PythonList();

                foreach (var cert in store.Certificates) {
                    string format = cert.GetFormat();

                    switch (format) {
                        case "X509":
                            format = "x509_asn";
                            break;
                        default:
                            format = "unknown";
                            break;
                    }

                    var set = new SetCollection();
                    bool found = false;
                    foreach (var ext in cert.Extensions) {
                        var keyUsage = ext as X509EnhancedKeyUsageExtension;
                        if (keyUsage != null) {
                            foreach (var oid in keyUsage.EnhancedKeyUsages) {
                                set.add(oid.Value);
                            }
                            found = true;
                            break;
                        }
                    }

                    result.Add(PythonTuple.MakeTuple(new Bytes(cert.RawData.ToList()), format, found ? set : ScriptingRuntimeHelpers.True));
                }

                return result;
            } catch {

            } finally {
                store?.Close();
            }
            return new PythonList();
        }

        public static PythonList enum_crls(string store_name) {
            X509Store store = null;
            try {
                store = new X509Store(store_name, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var result = new PythonList();

                foreach (var cert in store.Certificates) {
                    string format = cert.GetFormat();
                }
            } catch {

            } finally {
                store?.Close();
            }
            return new PythonList();
        }

        internal static PythonType SSLError(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("SSLError");
        }

        public static PythonDictionary _test_decode_cert(CodeContext context, string path) {
            var cert = ReadCertificate(context, path);
            return CertificateToPython(context, cert);
        }

        private static PythonDictionary CertificateToPython(CodeContext context, X509Certificate cert) {
            if (cert is X509Certificate2 cert2)
                return CertificateToPython(context, cert2);
            return CertificateToPython(context, new X509Certificate2(cert.GetRawCertData()));
        }

        private static PythonDictionary CertificateToPython(CodeContext context, X509Certificate2 cert) {
            var dict = new CommonDictionaryStorage();

            dict.AddNoLock("notAfter", ToPythonDateFormat(cert.NotAfter));
            dict.AddNoLock("subject", IssuerToPython(context, cert.Subject));
            dict.AddNoLock("notBefore", ToPythonDateFormat(cert.NotBefore));
            dict.AddNoLock("serialNumber", SerialNumberToPython(cert));
            dict.AddNoLock("version", cert.Version);
            dict.AddNoLock("issuer", IssuerToPython(context, cert.Issuer));
            AddSubjectAltNames(dict, cert);

            return new PythonDictionary(dict);

            string ToPythonDateFormat(DateTime date) {
                var dateStr = date.ToUniversalTime().ToString("MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture) + " GMT";
                if (dateStr[4] == '0')
                    dateStr = dateStr.Substring(0, 4) + " " + dateStr.Substring(5); // CPython uses leading space
                return dateStr;
            }
        }

        private static void AddSubjectAltNames(CommonDictionaryStorage dict, X509Certificate2 cert2) {
            foreach (var extension in cert2.Extensions) {
                if (extension.Oid.Value != "2.5.29.17") {  // Subject Alternative Name
                    continue;
                }
                var altNames = new List<object>();
                var sr = new StringReader(extension.Format(true));
                // The string generated by format varies depending on the platform, for example:
                // - On Windows, one entry per line:
                //     DNS Name=www.python.org
                //     DNS Name=pypi.python.org
                // - On Mac/Linux (.NET Core), multiple entries on a single line:
                //     DNS:www.python.org, DNS:pypi.python.org
                string line;
                while (null != (line = sr.ReadLine())) {
                    line = line.Trim();
                    // On Linux and Mac (.NET Core), Format produces a string matching the OpenSSL format which may contain multiple entries:
                    foreach (var val in line.Split(',')) {
                        var keyValue = val.Split(new char[] { ':', '=' });
                        // On Windows, Format produces different results based on the locale so we can't check for a specific key
                        if (keyValue[0].Contains("DNS") && keyValue.Length == 2) {
                            altNames.Add(PythonTuple.MakeTuple("DNS", keyValue[1]));
                        }
                    }
                }
                dict.AddNoLock("subjectAltName", PythonTuple.MakeTuple(altNames.ToArray()));
                break;
            }
        }

        private static string SerialNumberToPython(X509Certificate2 cert) {
            var res = cert.SerialNumber;
            for (int i = 0; i < res.Length; i++) {
                if (res[i] != '0') {
                    return res.Substring(i);
                }
            }
            return res;
        }

        // yields parts out of issuer or subject string
        // Respects quoted comma e.g: CN=*.c.ssl.fastly.net, O="Fastly, Inc.", L=San Francisco, S=California, C=US
        // Quote characters are removed
        private static IEnumerable<string> IssuerParts(string issuer) {
            var inQuote = false;
            var token = new StringBuilder();
            foreach (var c in issuer) {
                if (inQuote) {
                    if (c == '"') {
                        inQuote = false;
                    } else {
                        token.Append(c);
                    }
                } else {
                    if (c == '"') {
                        inQuote = true;
                    } else if (c == ',') {
                        yield return token.ToString().Trim();
                        token.Length = 0;
                    } else {
                        token.Append(c);
                    }
                }
            }
            if (token.Length > 0)
                yield return token.ToString().Trim();
        }

        private static PythonTuple IssuerToPython(CodeContext context, string issuer) {
            var collector = new List<object>();
            foreach (var part in IssuerParts(issuer)) {
                var field = IssuerFieldToPython(context, part);
                if (field != null) {
                    collector.Add(PythonTuple.MakeTuple(new object[] { field }));
                }
            }
            return PythonTuple.MakeTuple(collector.ToReverseArray()); // CPython reverses the fields
        }

        private static PythonTuple IssuerFieldToPython(CodeContext context, string p) {
            if (p.StartsWith("CN=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("commonName", p.Substring(3));
            } else if (p.StartsWith("OU=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("organizationalUnitName", p.Substring(3));
            } else if (p.StartsWith("O=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("organizationName", p.Substring(2));
            } else if (p.StartsWith("L=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("localityName", p.Substring(2));
            } else if (p.StartsWith("S=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("stateOrProvinceName", p.Substring(2));
            } else if (p.StartsWith("C=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("countryName", p.Substring(2));
            } else if (p.StartsWith("E=", StringComparison.Ordinal)) {
                return PythonTuple.MakeTuple("email", p.Substring(2));
            }

            // Ignore unknown fields
            return null;
        }

        private static X509Certificate2 ReadCertificate(CodeContext context, string filename, bool readKey = false) {
#if NET5_0_OR_GREATER
            if (readKey) {
                return X509Certificate2.CreateFromPemFile(filename);
            }
#endif

            string[] lines;
            try {
                lines = File.ReadAllLines(filename);
            } catch (IOException) {
                throw PythonExceptions.CreateThrowable(SSLError(context), "Can't open file ", filename);
            }

            X509Certificate2 cert = null;
            RSACryptoServiceProvider key = null;
            try {
                for (int i = 0; i < lines.Length; i++) {
                    if (lines[i] == "-----BEGIN CERTIFICATE-----") {
                        var certStr = ReadToEnd(lines, ref i, "-----END CERTIFICATE-----");

                        try {
                            cert = new X509Certificate2(Convert.FromBase64String(certStr.ToString()));
                        } catch (Exception e) {
                            throw ErrorDecoding(context, filename, e);
                        }
                        if (!readKey) return cert;
                    } else if (lines[i] == "-----BEGIN RSA PRIVATE KEY-----") {
                        var keyStr = ReadToEnd(lines, ref i, "-----END RSA PRIVATE KEY-----");

                        if (readKey) {
                            try {
                                var keyBytes = Convert.FromBase64String(keyStr.ToString());
                                key = ParsePkcs1DerEncodedPrivateKey(context, filename, keyBytes);
                            } catch (Exception e) {
                                throw ErrorDecoding(context, filename, e);
                            }
                        }
                    }
                }
            } catch (InvalidOperationException e) {
                throw ErrorDecoding(context, filename, e.Message);
            }

            if (cert != null) {
                if (key != null) {
                    try {
                        cert = cert.CopyWithPrivateKey(key);
                    } catch (CryptographicException e) {
                        throw ErrorDecoding(context, filename, "cert and private key are incompatible", e);
                    }
                }

                return cert;
            }
            throw ErrorDecoding(context, filename, "certificate not found");
        }

#if !NETCOREAPP && !NET472_OR_GREATER
#if NETSTANDARD
        private static MethodInfo CopyWithPrivateKeyMethodInfo = typeof(RSACertificateExtensions).GetMethod("CopyWithPrivateKey", new Type[] { typeof(X509Certificate2), typeof(System.Security.Cryptography.RSA) });
#endif

        private static X509Certificate2 CopyWithPrivateKey(this X509Certificate2 certificate, RSA privateKey) {
#if NETSTANDARD
            if (CopyWithPrivateKeyMethodInfo is not null) {
                return (X509Certificate2)CopyWithPrivateKeyMethodInfo.Invoke(null, new object[] { certificate, privateKey });
            }
#endif
            certificate.PrivateKey = privateKey;
            return certificate;
        }
#endif


        #region Private Key Parsing

        private const int ClassOffset = 6;
        private const int ClassMask = 0xc0;
        private const int ClassUniversal = 0x00 << ClassOffset;
        private const int ClassApplication = 0x01 << ClassOffset;
        private const int ClassContextSpecific = 0x02 << ClassOffset;
        private const int ClassPrivate = 0x03 << ClassOffset;

        private const int NumberMask = 0x1f;

        private const int UniversalSequence = 0x10;
        private const int UniversalInteger = 0x02;
        private const int UniversalOctetString = 0x04;

        private static RSACryptoServiceProvider ParsePkcs1DerEncodedPrivateKey(CodeContext context, string filename, byte[] x) {
            // http://tools.ietf.org/html/rfc3447#appendix-A.1.2
            // RSAPrivateKey ::= SEQUENCE {
            //   version           Version,
            //   modulus           INTEGER,  -- n
            //   publicExponent    INTEGER,  -- e
            //   privateExponent   INTEGER,  -- d
            //   prime1            INTEGER,  -- p
            //   prime2            INTEGER,  -- q
            //   exponent1         INTEGER,  -- d mod (p-1)
            //   exponent2         INTEGER,  -- d mod (q-1)
            //   coefficient       INTEGER,  -- (inverse of q) mod p
            //   otherPrimeInfos   OtherPrimeInfos OPTIONAL
            // }

            // read header for sequence
            if ((x[0] & ClassMask) != ClassUniversal) {
                throw ErrorDecoding(context, filename, "failed to find universal class");
            } else if ((x[0] & NumberMask) != UniversalSequence) {
                throw ErrorDecoding(context, filename, "failed to read sequence header");
            }

            // read length of sequence
            int offset = 1;
            ReadLength(x, ref offset);

            // read version
            int version = ReadUniversalInt(x, ref offset);
            if (version != 0) {
                // unsupported version
                throw new InvalidOperationException(String.Format("bad vesion: {0}", version));
            }

            // read in parameters and initialize provider
            RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
            RSAParameters parameters = new RSAParameters();

            parameters.Modulus = ReadUniversalIntAsBytes(x, ref offset);
            parameters.Exponent = ReadUniversalIntAsBytes(x, ref offset);
            parameters.D = ReadUniversalIntAsBytes(x, ref offset);
            parameters.P = ReadUniversalIntAsBytes(x, ref offset);
            parameters.Q = ReadUniversalIntAsBytes(x, ref offset);
            parameters.DP = ReadUniversalIntAsBytes(x, ref offset);
            parameters.DQ = ReadUniversalIntAsBytes(x, ref offset);
            parameters.InverseQ = ReadUniversalIntAsBytes(x, ref offset);

            provider.ImportParameters(parameters);
            return provider;
        }

        private static byte[] ReadUniversalIntAsBytes(byte[] x, ref int offset) {
            ReadIntType(x, ref offset);

            int bytes = ReadLength(x, ref offset);

            // we need to remove any leading 0 bytes which aren't part of a number.  Including
            // them causes our parsing to differ from certification parsing.
            while (x[offset] == 0) {
                bytes--;
                offset++;
            }

            byte[] res = new byte[bytes];
            for (int i = 0; i < res.Length; i++) {
                res[i] = x[offset++];
            }

            return res;

        }

        private static void ReadIntType(byte[] x, ref int offset) {
            int versionType = x[offset++];
            if (versionType != UniversalInteger) {
                throw new InvalidOperationException(String.Format("expected version, fonud {0}", versionType));
            }
        }
        private static int ReadUniversalInt(byte[] x, ref int offset) {
            ReadIntType(x, ref offset);

            return ReadInt(x, ref offset);
        }

        private static int ReadLength(byte[] x, ref int offset) {
            int bytes = x[offset++];
            if ((bytes & 0x80) == 0) {
                return bytes;
            }

            return ReadInt(x, ref offset, bytes & ~0x80);

        }

        private static int ReadInt(byte[] x, ref int offset, int bytes) {
            if (bytes + offset > x.Length) {
                throw new InvalidOperationException();
            }

            int res = 0;
            for (int i = 0; i < bytes; i++) {
                res = res << 8 | x[offset++];
            }
            return res;
        }
        /// <summary>
        /// BER encoding of an integer value is the number of bytes
        /// required to represent the integer followed by the bytes
        /// </summary>
        private static int ReadInt(byte[] x, ref int offset) {
            int bytes = x[offset++];

            return ReadInt(x, ref offset, bytes);
        }

        private static string ReadToEnd(string[] lines, ref int start, string end) {
            StringBuilder key = new StringBuilder();
            for (start++; start < lines.Length; start++) {
                if (lines[start] == end) {
                    return key.ToString();
                }
                key.Append(lines[start]);
            }
            return null;
        }

        #endregion

        private static Exception ErrorDecoding(CodeContext context, params object[] args) {
            return PythonExceptions.CreateThrowable(SSLError(context), ArrayUtils.Insert("Error decoding PEM-encoded file ", args));
        }

        #region Exported constants

        public const int CERT_NONE = 0;
        public const int CERT_OPTIONAL = 1;
        public const int CERT_REQUIRED = 2;

        public const int PROTOCOL_SSLv2 = 0;
        public const int PROTOCOL_SSLv3 = 1;
        public const int PROTOCOL_SSLv23 = 2;
        public const int PROTOCOL_TLSv1 = 3;
        public const int PROTOCOL_TLSv1_1 = 4;
        public const int PROTOCOL_TLSv1_2 = 5;

        public const int OP_ALL = unchecked((int)0x800003FF);
        public const int OP_CIPHER_SERVER_PREFERENCE = 0x400000;
        public const int OP_SINGLE_DH_USE = 0x100000;
        public const int OP_SINGLE_ECDH_USE = 0x80000;
        public const int OP_NO_SSLv2 = 0x01000000;
        public const int OP_NO_SSLv3 = 0x02000000;
        public const int OP_NO_TLSv1 = 0x04000000;
        public const int OP_NO_TLSv1_1 = 0x10000000;
        public const int OP_NO_TLSv1_2 = 0x08000000;

        internal const int OP_NO_COMPRESSION = 0x20000;
        internal const int OP_NO_ALL = OP_NO_SSLv2 | OP_NO_SSLv3 | OP_NO_TLSv1 | OP_NO_TLSv1_1 | OP_NO_TLSv1_2 | OP_NO_COMPRESSION;

        public const int SSL_ERROR_SSL = 1;
        public const int SSL_ERROR_WANT_READ = 2;
        public const int SSL_ERROR_WANT_WRITE = 3;
        public const int SSL_ERROR_WANT_X509_LOOKUP = 4;
        public const int SSL_ERROR_SYSCALL = 5;
        public const int SSL_ERROR_ZERO_RETURN = 6;
        public const int SSL_ERROR_WANT_CONNECT = 7;
        public const int SSL_ERROR_EOF = 8;
        public const int SSL_ERROR_INVALID_ERROR_CODE = 10;

        public const int VERIFY_DEFAULT = 0;
        public const int VERIFY_CRL_CHECK_LEAF = 0x4; // from openssl/x509_vfy.h
        public const int VERIFY_CRL_CHECK_CHAIN = 0x4 | 0x8; // from openssl/x509_vfy.h
        public const int VERIFY_X509_STRICT = 0x20; // from openssl/x509_vfy.h
        public const int VERIFY_X509_TRUSTED_FIRST = 0x8000; // from openssl/x509_vfy.h

        public const bool HAS_SNI = true;
        public const bool HAS_ECDH = true;
        public const bool HAS_NPN = false;
        public const bool HAS_ALPN = false;
        public const bool HAS_TLS_UNIQUE = false;

        private const int SSL_VERIFY_NONE = 0x00;
        private const int SSL_VERIFY_PEER = 0x01;
        private const int SSL_VERIFY_FAIL_IF_NO_PEER_CERT = 0x02;
        private const int SSL_VERIFY_CLIENT_ONCE = 0x04;

        #endregion
    }
}

#endif
