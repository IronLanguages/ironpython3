﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    internal class PythonDocumentationProvider : DocumentationProvider {
        private readonly PythonContext _context;

        public PythonDocumentationProvider(PythonContext context) {
            _context = context;
        }

        public override ICollection<MemberDoc> GetMembers(object value) {
            List<MemberDoc> res = new List<MemberDoc>();

            PythonModule mod = value as PythonModule;
            if (mod != null) {
                foreach (var kvp in mod.__dict__) {
                    AddMember(res, kvp, false);
                }
                return res;
            }

            NamespaceTracker ns = value as NamespaceTracker;
            if (ns != null) {
                foreach (var v in ns) {
                    AddMember(
                        res,
                        new KeyValuePair<object, object>(
                            v.Key,
                            Importer.MemberTrackerToPython(_context.SharedClsContext, v.Value)
                        ),
                        false
                    );
                }
            } else {
                PythonType pt = value as PythonType;
                if (pt != null) {
                    foreach (PythonType type in pt.ResolutionOrder) {
                        foreach (var member in type.GetMemberDictionary(_context.SharedContext)) {
                            AddMember(res, member, true);
                        }
                    }
                } else {

                    pt = DynamicHelpers.GetPythonType(value);
                    foreach (var member in pt.GetMemberDictionary(_context.SharedContext)) {
                        AddMember(res, member, true);
                    }
                }

                IPythonObject ipo = value as IPythonObject;
                if (ipo != null && ipo.Dict != null) {
                    foreach (var member in ipo.Dict) {
                        AddMember(res, member, false);
                    }
                }
            }

            return res.ToArray();
        }

        private static void AddMember(List<MemberDoc> res, KeyValuePair<object, object> member, bool fromClass) {
            string name = member.Key as string;
            if (name != null) {
                res.Add(MakeMemberDoc(name, member.Value, fromClass));
            }
        }

        private static MemberDoc MakeMemberDoc(string name, object value, bool fromClass) {
            MemberKind kind = MemberKind.None;
            if (value is BuiltinFunction) {
                kind = MemberKind.Function;
            } else if (value is NamespaceTracker) {
                kind = MemberKind.Namespace;            
            } else if (value is PythonFunction) {
                kind = fromClass ? MemberKind.Method : MemberKind.Function;
            } else if (value is BuiltinMethodDescriptor || value is Method) {
                kind = MemberKind.Method;
            } else if (value is PythonType) {
                var pt = value as PythonType;
                if (pt.IsSystemType && pt.UnderlyingSystemType.IsEnum()) {
                    kind = MemberKind.Enum;
                } else {
                    kind = MemberKind.Class;
                }
            } else if (value is Delegate) {
                kind = MemberKind.Delegate;
            } else if (value is ReflectedProperty || value is ReflectedExtensionProperty) {
                kind = MemberKind.Property;
            } else if (value is ReflectedEvent) {
                kind = MemberKind.Event;
            } else if (value is ReflectedField) {
                kind = MemberKind.Field;
            } else if (value != null && value.GetType().IsEnum()) {
                kind = MemberKind.EnumMember;
            } else if (value is PythonType) {
                kind = MemberKind.Class;
            } else if (value is IPythonObject) {
                kind = MemberKind.Instance;
            }

            return new MemberDoc(
                name,
                kind
            );
        }

        public override ICollection<OverloadDoc> GetOverloads(object value) {
            BuiltinFunction bf = value as BuiltinFunction;
            if (bf != null) {
                return GetBuiltinFunctionOverloads(bf);
            }

            BuiltinMethodDescriptor bmd = value as BuiltinMethodDescriptor;
            if (bmd != null) {
                return GetBuiltinFunctionOverloads(bmd.Template);
            }

            PythonFunction pf = value as PythonFunction;
            if (pf != null) {
                return new[] { 
                    new OverloadDoc(
                        pf.__name__,
                        pf.__doc__ as string,
                        GetParameterDocs(pf)
                    )
                };
            }

            Method method = value as Method;
            if (method != null) {
                return GetOverloads(method.__func__);
            }

            Delegate dlg = value as Delegate;
            if (dlg != null) {
                return new[] { DocBuilder.GetOverloadDoc(dlg.GetType().GetMethod("Invoke"), dlg.GetType().Name, 0, false) };
            }

            return new OverloadDoc[0];
        }

        private static ICollection<ParameterDoc> GetParameterDocs(PythonFunction pf) {
            ParameterDoc[] res = new ParameterDoc[pf.ArgNames.Length];

            for (int i = 0; i < res.Length; i++) {
                ParameterFlags flags = ParameterFlags.None;
                if (i == pf.ExpandDictPosition) {
                    flags |= ParameterFlags.ParamsDict;
                } else if (i == pf.ExpandListPosition) {
                    flags |= ParameterFlags.ParamsArray;
                }

                res[i] = new ParameterDoc(
                    pf.ArgNames[i],
                    flags
                );
            }
            return res;
        }

        private static ICollection<OverloadDoc> GetBuiltinFunctionOverloads(BuiltinFunction bf) {
            OverloadDoc[] res = new OverloadDoc[bf.Targets.Count];
            for (int i = 0; i < bf.Targets.Count; i++) {
                res[i] = GetOverloadDoc(bf.__name__, bf.Targets[i]);
            }
            return res;
        }

        private static OverloadDoc GetOverloadDoc(string name, MethodBase method) {
            return DocBuilder.GetOverloadDoc(method, name, 0);
        }
    }
}
