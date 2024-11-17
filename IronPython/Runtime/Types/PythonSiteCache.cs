// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Actions;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Cached CallSites.  User types are cached on the PythonType and System types are cached on the
    /// PythonContext to avoid cross-runtime contamination due to the binder on the site.
    /// </summary>
    internal class PythonSiteCache {
        private Dictionary<string, CallSite<Func<CallSite, object, CodeContext, object>>> _tryGetMemSite;
        private Dictionary<string, CallSite<Func<CallSite, object, CodeContext, object>>> _tryGetMemSiteShowCls;
        private CallSite<Func<CallSite, CodeContext, object, object>> _dirSite;
        private CallSite<Func<CallSite, CodeContext, object, string, object>> _getAttributeSite;
        private CallSite<Func<CallSite, CodeContext, object, object, string, object, object>> _setAttrSite;
        private CallSite<Func<CallSite, CodeContext, object, object>> _lenSite;

        internal CallSite<Func<CallSite, object, CodeContext, object>> GetTryGetMemberSite(CodeContext context, string name) {
            CallSite<Func<CallSite, object, CodeContext, object>> site;
            if (PythonOps.IsClsVisible(context)) {
                if (_tryGetMemSiteShowCls == null) {
                    Interlocked.CompareExchange(
                        ref _tryGetMemSiteShowCls,
                        new Dictionary<string, CallSite<Func<CallSite, object, CodeContext, object>>>(StringComparer.Ordinal),
                        null
                    );
                }

                lock (_tryGetMemSiteShowCls) {
                    if (!_tryGetMemSiteShowCls.TryGetValue(name, out site)) {
                        _tryGetMemSiteShowCls[name] = site = CallSite<Func<CallSite, object, CodeContext, object>>.Create(
                            context.LanguageContext.GetMember(
                                name,
                                true
                            )
                        );
                    }
                }
            } else {
                if (_tryGetMemSite == null) {
                    Interlocked.CompareExchange(
                        ref _tryGetMemSite,
                        new Dictionary<string, CallSite<Func<CallSite, object, CodeContext, object>>>(StringComparer.Ordinal),
                        null
                    );
                }

                lock (_tryGetMemSite) {
                    if (!_tryGetMemSite.TryGetValue(name, out site)) {
                        _tryGetMemSite[name] = site = CallSite<Func<CallSite, object, CodeContext, object>>.Create(
                            context.LanguageContext.GetMember(
                                name,
                                true
                            )
                        );
                    }
                }
            }
            return site;
        }

        internal CallSite<Func<CallSite, CodeContext, object, object>> GetDirSite(CodeContext context) {
            if (_dirSite == null) {
                Interlocked.CompareExchange(
                    ref _dirSite,
                    CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                        context.LanguageContext.InvokeNone
                    ),
                    null);
            }
            return _dirSite;
        }

        internal CallSite<Func<CallSite, CodeContext, object, string, object>> GetGetAttributeSite(CodeContext context) {
            if (_getAttributeSite == null) {
                Interlocked.CompareExchange(
                    ref _getAttributeSite,
                    CallSite<Func<CallSite, CodeContext, object, string, object>>.Create(
                        context.LanguageContext.InvokeOne
                    ),
                    null
                );
            }
            return _getAttributeSite;
        }

        internal CallSite<Func<CallSite, CodeContext, object, object, string, object, object>> GetSetAttrSite(CodeContext context) {
            if (_setAttrSite == null) {
                Interlocked.CompareExchange(
                    ref _setAttrSite,
                    CallSite<Func<CallSite, CodeContext, object, object, string, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(4)
                        )
                    ),
                    null
                );
            }
            return _setAttrSite;
        }

        internal CallSite<Func<CallSite, CodeContext, object, object>> GetLenSite(CodeContext context) {
            if (_lenSite == null) {
                Interlocked.CompareExchange(
                    ref _lenSite,
                    CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                        context.LanguageContext.InvokeNone
                    ),
                    null
                );
            }
            return _lenSite;
        }
    }
}
