// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;

using IronPython.Runtime.Types;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    internal class PythonDocumentationProvider : DocumentationProvider {
        private readonly PythonContext _context;

        public PythonDocumentationProvider(PythonContext context) {
            _context = context;
        }

        public override ICollection<MemberDoc> GetMembers(object value) {
            List<MemberDoc> res = new List<MemberDoc>();

            if (value is PythonModule mod) {
                foreach (var kvp in mod.__dict__) {
                    AddMember(res, kvp, false);
                }
                return res;
            }

            if (value is NamespaceTracker ns) {
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
                if (value is PythonType pt) {
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

                if (value is IPythonObject ipo && ipo.Dict != null) {
                    foreach (var member in ipo.Dict) {
                        AddMember(res, member, false);
                    }
                }
            }

            return res.ToArray();
        }

        private static void AddMember(List<MemberDoc> res, KeyValuePair<object, object> member, bool fromClass) {
            if (member.Key is string name) {
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
                if (pt.IsSystemType && pt.UnderlyingSystemType.IsEnum) {
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
            } else if (value != null && value.GetType().IsEnum) {
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
            if (value is BuiltinFunction bf) {
                return GetBuiltinFunctionOverloads(bf);
            }

            if (value is BuiltinMethodDescriptor bmd) {
                return GetBuiltinFunctionOverloads(bmd.Template);
            }

            if (value is PythonFunction pf) {
                return new[] {
                    new OverloadDoc(
                        pf.__name__,
                        pf.__doc__ as string,
                        GetParameterDocs(pf)
                    )
                };
            }

            if (value is Method method) {
                return GetOverloads(method.__func__);
            }

            if (value is Delegate dlg) {
                return new[] { DocBuilder.GetOverloadDoc(dlg.GetType().GetMethod("Invoke"), dlg.GetType().Name, 0, false) };
            }

            return Array.Empty<OverloadDoc>();
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
