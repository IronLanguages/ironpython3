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

#if FEATURE_FULL_NET

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;


[assembly: PythonModule("_ssl", typeof(IronPython.Modules.PythonSsl))]
namespace IronPython.Modules {

    internal class Asn1Object {

        public Asn1Object(string shortName, string longName, int nid, byte[] oid) {
            ShortName = shortName;
            LongName = longName;
            NID = nid;
            OID = oid;
#if !CLR2
            OIDString = string.Join(".", OID);
#else
            StringBuilder buf = new StringBuilder();
            foreach(byte b in oid) {
                buf.AppendFormat("{0}.");
            }
            OIDString = buf.ToString().Trim('.');
#endif
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

        private static List<Asn1Object> _asn1Objects = new List<Asn1Object>();

        static PythonSsl() {
            _asn1Objects.AddRange(new Asn1Object[] {
                new Asn1Object("serverAuth", "TLS Web Server Authentication", 129, new byte[] { 1, 3, 6, 1 ,5, 5, 7, 3, 1 }),
                new Asn1Object("clientAuth", "TLS Web Client Authentication", 130, new byte[] { 1, 3, 6, 1 ,5, 5, 7, 3, 2 }),
            });
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {            
            var socket = context.GetBuiltinModule("socket");
            var socketError = PythonSocket.GetSocketError(context, socket.__dict__);
            
            var sslError = context.EnsureModuleException("SSLError", socketError, dict, "SSLError", "ssl");
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
            if (buf == null) {
                throw PythonOps.TypeError("must be string or read-only buffer, not None");
            } else if (!(buf is string) && !(buf is PythonBuffer)) {
                throw PythonOps.TypeError("must be string or read-only buffer, not {0}", PythonOps.GetPythonTypeName(buf));
            }
        }

        public static int RAND_status() {
            return 1; // always ready
        }

		#endregion

		#region SSLContext
        [PythonType]
        public class _SSLContext {
            private X509Certificate2Collection _cert_store = new X509Certificate2Collection();
            private string _cafile;
            private int _verify_mode = SSL_VERIFY_NONE;

            public _SSLContext(CodeContext context, [DefaultParameterValue(PROTOCOL_SSLv23)] int protocol) {
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
                    if(_verify_mode != CERT_NONE && _verify_mode != CERT_OPTIONAL && _verify_mode != CERT_REQUIRED) {
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

            public void load_cert_chain(string certfile, [DefaultParameterValue(null)] string keyfile, [DefaultParameterValue(null)] object password) {

            }

            public void load_verify_locations(CodeContext context, [DefaultParameterValue(null)] string cafile, [DefaultParameterValue(null)] string capath, [DefaultParameterValue(null)] object cadata) {
                if(cafile == null && capath == null && cadata == null) {
                    throw PythonOps.TypeError("cafile, capath and cadata cannot be all omitted");
                }

                if(cafile != null) {
                    _cert_store.Add(ReadCertificate(context, cafile));
                    _cafile = cafile;
                }

                if(capath != null) {
                }

                if(cadata != null) {
                    var cabuf = cadata as IBufferProtocol;
                    if (cabuf != null) {
                        int pos = 0;
                        byte[] contents = cabuf.ToBytes(0, null).ToByteArray();
                        while(pos < contents.Length) {
                            byte[] curr = new byte[contents.Length - pos];
                            Array.Copy(contents, pos, curr, 0, contents.Length - pos);
                            var cert = new X509Certificate2(curr);
                            _cert_store.Add(cert);
                            pos += cert.GetRawCertData().Length;
                        }                        
                    }
                }
            }

            public object _wrap_socket(CodeContext context, [DefaultParameterValue(null)] PythonSocket.socket sock, [DefaultParameterValue(false)] bool server_side, [DefaultParameterValue(null)] string server_hostname, [DefaultParameterValue(null)] object ssl_sock) {
                return new PythonSocket.ssl(context, sock, server_side, null, _cafile, verify_mode, protocol | options, null, _cert_store);
            }
        }

#endregion

        public static PythonType SSLType = DynamicHelpers.GetPythonTypeFromType(typeof(PythonSocket.ssl));
        
        public static PythonSocket.ssl sslwrap(
            CodeContext context,
            PythonSocket.socket socket, 
            bool server_side, 
            [DefaultParameterValue(null)] string keyfile, 
            [DefaultParameterValue(null)] string certfile,
            [DefaultParameterValue(PythonSsl.CERT_NONE)]int certs_mode,
            [DefaultParameterValue(PythonSsl.PROTOCOL_SSLv23 | PythonSsl.OP_NO_SSLv2 | PythonSsl.OP_NO_SSLv3)]int protocol,
            [DefaultParameterValue(null)]string cacertsfile,
            [DefaultParameterValue(null)]object ciphers) {
            return new PythonSocket.ssl(
                context,
                socket,
                server_side,
                keyfile,
                certfile,
                certs_mode,
                protocol,
                cacertsfile,
                null
            );
        }

        public static object txt2obj(CodeContext context, string txt, [DefaultParameterValue(false)] object name) {
            bool nam = PythonOps.IsTrue(name); // if true, we also look at short name and long name
            Asn1Object obj = null;
            if(nam) {
                obj = _asn1Objects.Where(x => txt == x.OIDString || txt == x.ShortName || txt == x.LongName).FirstOrDefault();
            } else {
                obj = _asn1Objects.Where(x => txt == x.OIDString).FirstOrDefault();
            }         

            if(obj == null) {
                throw PythonOps.ValueError("unknown object '{0}'", txt);
            }

            return obj.ToTuple();
        }

        public static object nid2obj(CodeContext context, int nid) {
            if(nid < 0) {
                throw PythonOps.ValueError("NID must be positive");
            }

            var obj = _asn1Objects.Where(x => x.NID == nid).FirstOrDefault();
            if(obj == null) {
                throw PythonOps.ValueError("unknown NID {0}", nid);
            }

            return obj.ToTuple();
        }

        public static List enum_certificates(string store_name) {
            X509Store store = null;
            try {
                store = new X509Store(store_name, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var result = new List();

                foreach (var cert in store.Certificates) {
                    string format = cert.GetFormat();

                    switch(format) {
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
                            foreach(var oid in keyUsage.EnhancedKeyUsages) {
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
                if(store != null) {
#if NETSTANDARD
                    store.Dispose();
#else
                    store.Close();
#endif
                }
            }
            return new List();
        }

        public static List enum_crls(string store_name) {
            X509Store store = null;
            try {
                
                store = new X509Store(store_name, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var result = new List();

                foreach (var cert in store.Certificates) {
                    string format = cert.GetFormat();


                }
            } catch {

            } finally {
                if (store != null) {
#if NETSTANDARD
                    store.Dispose();
#else
                    store.Close();
#endif
                }
            }
            return new List();
        }

        internal static PythonType SSLError(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("SSLError");
        }

        public static PythonDictionary _test_decode_cert(CodeContext context, string filename, [DefaultParameterValue(false)]bool complete) {
            var cert = ReadCertificate(context, filename);
            return CertificateToPython(context, cert, complete);
        }

        internal static PythonDictionary CertificateToPython(CodeContext context, X509Certificate cert, bool complete) {
            return CertificateToPython(context, new X509Certificate2(cert.GetRawCertData()), complete);
        }

        internal static PythonDictionary CertificateToPython(CodeContext context, X509Certificate2 cert, bool complete) {
            var dict = new CommonDictionaryStorage();

            dict.AddNoLock("notAfter", ToPythonDateFormat(cert.NotAfter.ToString()));
            dict.AddNoLock("subject", IssuerToPython(context, cert.Subject));
            if (complete) {
                dict.AddNoLock("notBefore", ToPythonDateFormat(cert.NotBefore.ToString()));
                dict.AddNoLock("serialNumber", SerialNumberToPython(cert));
                dict.AddNoLock("version", cert.GetCertHashString());
                dict.AddNoLock("issuer", IssuerToPython(context, cert.Issuer));
                AddSubjectAltNames(dict, cert);
            }

            return new PythonDictionary(dict);
        }

        private static void AddSubjectAltNames(CommonDictionaryStorage dict, X509Certificate2 cert2) {
            foreach (var extension in cert2.Extensions) {
                if (extension.Oid.Value != "2.5.29.17") {  // Subject Alternative Name
                    continue;
                }
                var altNames = new List<object>();
                var sr = new StringReader(extension.Format(true));
                string line;
                while (null != (line = sr.ReadLine())) {
                    line = line.Trim();
                    var keyValue = line.Split('=');
                    if (keyValue[0] == "DNS Name" && keyValue.Length == 2) {
                        altNames.Add(PythonTuple.MakeTuple("DNS", keyValue[1]));
                    }
                }
                dict.AddNoLock("subjectAltName", PythonTuple.MakeTuple(altNames.ToArray()));
                break;
            }
        }

        private static string ToPythonDateFormat(string date) {
            return DateTime.Parse(date).ToUniversalTime().ToString("MMM d HH:mm:ss yyyy") + " GMT";
        }

#if NETSTANDARD
        private static string ByteArrayToString(IEnumerable<byte> bytes) {
            var builder = new StringBuilder();
            foreach (byte b in bytes)
                builder.Append(b.ToString("X2"));
            return builder.ToString();
        }

        private static string GetSerialNumberString(this X509Certificate cert) {
            return ByteArrayToString(cert.GetSerialNumber().Reverse()); // must be reversed
        }

        private static string GetCertHashString(this X509Certificate cert) {
            return ByteArrayToString(cert.GetCertHash());
        }

        internal static byte[] GetRawCertData(this X509Certificate cert) {
            return cert.Export(X509ContentType.Cert);
        }
#endif

        private static string SerialNumberToPython(X509Certificate cert) {
            var res = cert.GetSerialNumberString();
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
        }

        private static PythonTuple IssuerToPython(CodeContext context, string issuer) {
            var collector = new List<object>();
            foreach (var part in IssuerParts(issuer)) {
                var field = IssuerFieldToPython(context, part);
                if (field != null) {
                    collector.Add(field); 
                }
            }
            return PythonTuple.MakeTuple(collector.ToArray());
        }

        private static PythonTuple IssuerFieldToPython(CodeContext context, string p) {
            if (String.Compare(p, 0, "CN=", 0, 3) == 0) {
                return PythonTuple.MakeTuple("commonName", p.Substring(3));
            } else if (String.Compare(p, 0, "OU=", 0, 3) == 0) {
                return PythonTuple.MakeTuple("organizationalUnitName", p.Substring(3));
            } else if (String.Compare(p, 0, "O=", 0, 2) == 0) {
                return PythonTuple.MakeTuple("organizationName", p.Substring(2));
            } else if (String.Compare(p, 0, "L=", 0, 2) == 0) {
                return PythonTuple.MakeTuple("localityName", p.Substring(2));
            } else if (String.Compare(p, 0, "S=", 0, 2) == 0) {
                return PythonTuple.MakeTuple("stateOrProvinceName", p.Substring(2));
            } else if (String.Compare(p, 0, "C=", 0, 2) == 0) {
                return PythonTuple.MakeTuple("countryName", p.Substring(2));
            } else if (String.Compare(p, 0, "E=", 0, 2) == 0) {
                return PythonTuple.MakeTuple("email", p.Substring(2));
            }

            // Ignore unknown fields
            return null;
        }


        internal static X509Certificate2 ReadCertificate(CodeContext context, string filename) {

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
                    } else if (lines[i] == "-----BEGIN RSA PRIVATE KEY-----") {
                        var keyStr = ReadToEnd(lines, ref i, "-----END RSA PRIVATE KEY-----");

                        try {
                            var keyBytes = Convert.FromBase64String(keyStr.ToString());
                            key = ParsePkcs1DerEncodedPrivateKey(context, filename, keyBytes);
                        } catch (Exception e) {
                            throw ErrorDecoding(context, filename, e);
                        }
                    }
                }
            } catch (InvalidOperationException e) {
                throw ErrorDecoding(context, filename, e.Message);
            }

            if (cert != null) {
#if !NETSTANDARD
                if (key != null) {
                    try {
                        cert.PrivateKey = key;
                    } catch(CryptographicException e) {
                        throw ErrorDecoding(context, filename, "cert and private key are incompatible", e);
                    }
                }
#endif
                return cert;
            }
            throw ErrorDecoding(context, filename, "certificate not found");
        }

        #region Private Key Parsing

        const int ClassOffset = 6;
        const int ClassMask = 0xc0;
        const int ClassUniversal = 0x00 << ClassOffset;
        const int ClassApplication = 0x01 << ClassOffset;
        const int ClassContextSpecific = 0x02 << ClassOffset;
        const int ClassPrivate = 0x03 << ClassOffset;

        const int NumberMask = 0x1f;

        const int UnivesalSequence = 0x10;
        const int UniversalInteger = 0x02;
        const int UniversalOctetString = 0x04;

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
            } else if ((x[0] & NumberMask) != UnivesalSequence) {
                throw ErrorDecoding(context, filename, "failed to read sequence header");
            }

            // read length of sequence
            int offset = 1;
            ReadLength(x, ref offset);

            // read version
            int version = ReadUnivesalInt(x, ref offset);
            if (version != 0) {
                // unsupported version
                throw new InvalidOperationException(String.Format("bad vesion: {0}", version));
            }

            // read in parameters and initialize provider
            RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
            RSAParameters parameters = new RSAParameters();

            parameters.Modulus = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.Exponent = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.D = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.P = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.Q = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.DP = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.DQ = ReadUnivesalIntAsBytes(x, ref offset);
            parameters.InverseQ = ReadUnivesalIntAsBytes(x, ref offset);
            
            provider.ImportParameters(parameters);            
            return provider;
        }

        private static byte[] ReadUnivesalIntAsBytes(byte[] x, ref int offset) {
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
        private static int ReadUnivesalInt(byte[] x, ref int offset) {
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

        public const uint OP_ALL = 0x80000BFF;
        public const uint OP_DONT_INSERT_EMPTY_FRAGMENTS = 0x00000800;        
        public const int OP_NO_SSLv2 = 0x00000000;
        public const int OP_NO_SSLv3 = 0x02000000;
        public const int OP_NO_TLSv1 = 0x04000000;
        public const int OP_NO_TLSv1_1 = 0x10000000;
        public const int OP_NO_TLSv1_2 = 0x08000000;

        internal const int OP_NO_ALL = OP_NO_SSLv2 | OP_NO_SSLv3 | OP_NO_TLSv1 | OP_NO_TLSv1_1 | OP_NO_TLSv1_2;        

        public const int SSL_ERROR_SSL = 1;
        public const int SSL_ERROR_WANT_READ = 2;
        public const int SSL_ERROR_WANT_WRITE = 3;
        public const int SSL_ERROR_WANT_X509_LOOKUP = 4;
        public const int SSL_ERROR_SYSCALL = 5;
        public const int SSL_ERROR_ZERO_RETURN = 6;
        public const int SSL_ERROR_WANT_CONNECT = 7;
        public const int SSL_ERROR_EOF = 8;
        public const int SSL_ERROR_INVALID_ERROR_CODE = 9;

        public const int VERIFY_DEFAULT = 0;
        public const int VERIFY_CRL_CHECK_LEAF = 0x4; // from openssl/x509_vfy.h
        public const int VERIFY_CRL_CHECK_CHAIN = 0x4 | 0x8; // from openssl/x509_vfy.h
        public const int VERIFY_X509_STRICT = 0x20; // from openssl/x509_vfy.h
        public const int VERIFY_X509_TRUSTED_FIRST = 0x8000; // from openssl/x509_vfy.h

        public const bool HAS_SNI = false;
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
