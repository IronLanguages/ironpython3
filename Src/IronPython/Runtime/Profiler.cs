﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions.Calls;

using IronPython.Compiler;

namespace IronPython.Runtime {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    /// <summary>
    /// Manages the acquisition of profiling data for a single ScriptRuntime
    /// </summary>
    public sealed class Profiler {
        private readonly Dictionary<MethodBase/*!*/, int>/*!*/ _methods; // Unique lookup of methods -> profile indices
        private readonly Dictionary<string/*!*/, int>/*!*/ _names;       // Unique lookup of names -> profile indices; not all names are unique
        private readonly List<string/*!*/>/*!*/ _counters;
        private readonly List<long[,]>/*!*/ _profiles;
        private long[,] _profileData;

        const int _initialSize = 100; // we double each time we run out of space

        // Indexes into profile data
        const int TimeInBody = 0;
        const int TimeInChildMethods = 1;
        const int NumberOfCalls = 2;

        [MultiRuntimeAware]
        private static readonly object _profileKey = new object();

        /// <summary>
        /// Get the unique Profiler instance for this ScriptRuntime
        /// </summary>
        public static Profiler GetProfiler(PythonContext/*!*/ context) {
            return context.GetOrCreateModuleState(_profileKey, () => new Profiler());
        }

        private Profiler() {
            _methods = new Dictionary<MethodBase, int>();
            _names = new Dictionary<string, int>();
            _counters = new List<string>();
            _profiles = new List<long[,]>();
            _profileData = new long[_initialSize, 3];
        }

        private static string FormatMethodName(MethodBase method) {
            var sb = new StringBuilder();
            if (method.DeclaringType != null) {
                sb.Append("type ");
                sb.Append(method.DeclaringType.Name);
                sb.Append(": ");
            }
            sb.Append("method: ");
            sb.Append(method.Name);
            sb.Append('(');
            bool comma = false;
            foreach (var p in method.GetParameters()) {
                if (comma) {
                    sb.Append(", ");
                } else {
                    comma = true;
                }
                sb.Append(p.ParameterType.Name);
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Given a MethodBase, return an index into the array of perf data.  Treat each
        /// CLR method as unique.
        /// </summary>
        private int GetProfilerIndex(MethodBase/*!*/ method) {
            lock (_methods) {
                int index;
                if (!_methods.TryGetValue(method, out index)) {
                    index = GetNewProfilerIndex(FormatMethodName(method));
                    _methods[method] = index;
                }
                return index;
            }
        }

        /// <summary>
        /// Given the unique name of something we're profiling, return an index into the array of perf data.
        /// </summary>
        private int GetProfilerIndex(string/*!*/ name) {
            lock (_methods) {
                int index;
                if (!_names.TryGetValue(name, out index)) {
                    index = GetNewProfilerIndex(name);
                    _names[name] = index;
                }
                return index;
            }
        }

        /// <summary>
        /// Add a new profiler entry. Not all names are unique.
        /// </summary>
        private int GetNewProfilerIndex(string/*!*/ name) {
            int index;
            lock (_counters) {
                index = _counters.Count;
                _counters.Add(name);
                if (index >= (_profileData.Length / 3)) {
                    long[,] newProfileData = new long[index * 2, 3];
                    _profiles.Add(Interlocked.Exchange(ref _profileData, newProfileData));
                }
            }
            return index;
        }

        /// <summary>
        /// Gets the current summary of profile data
        /// </summary>
        public List<Data>/*!*/ GetProfile(bool includeUnused) {
            var result = new List<Data>(_counters.Count);
            lock (_counters) {
                // capture the current profile
                int length = (_profileData.Length / 3);
                long[,] newProfileData = new long[length, 3];
                long[,] totals = Interlocked.Exchange(ref _profileData, newProfileData);

                // TODO: There's a slim possibility of data being lost here if the runtime helper on
                // another thread acquires the memory reference and then loses the quantum before
                // performing the interlocked add, and then this thread replaces the _profileData
                // reference and uses the pre-add profile value.

                for (int i = 0; i < _profiles.Count; i++) {
                    for (int j = 0; j < length; j++) {
                        if (j < (_profiles[i].Length / 3)) {
                            totals[j, TimeInBody] += _profiles[i][j, TimeInBody];
                            totals[j, TimeInChildMethods] += _profiles[i][j, TimeInChildMethods];
                            totals[j, NumberOfCalls] += _profiles[i][j, NumberOfCalls];
                        }
                    }
                }

                _profiles.Clear();
                _profiles.Add(totals);

                for (int i = 0; i < _counters.Count; i++) {
                    if (includeUnused || totals[i, NumberOfCalls] > 0) {
                        result.Add(new Data(
                            _counters[i],
                            DateTimeTicksFromTimeData(totals[i, TimeInBody] + totals[i, TimeInChildMethods]),
                            DateTimeTicksFromTimeData(totals[i, TimeInBody]),
                            (int)totals[i, NumberOfCalls]
                        ));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Resets the current summary of profile data back to zero
        /// </summary>
        public void Reset() {
            lock (_counters) {
                // capture the current profile
                int length = (_profileData.Length / 3);
                long[,] newProfileData = new long[length, 3];
                Interlocked.Exchange(ref _profileData, newProfileData);
                _profiles.Clear();
            }
        }

        private static long DateTimeTicksFromTimeData(long elapsedStopwatchTicks) {
#if FEATURE_STOPWATCH
            if (Stopwatch.IsHighResolution) {
                return (long)(((double)elapsedStopwatchTicks) * 10000000.0 / (double)Stopwatch.Frequency);
            }
#endif
            return elapsedStopwatchTicks;
        }

        #region Runtime helpers

        public long StartCall(int index) {
            Interlocked.Increment(ref _profileData[index, NumberOfCalls]);
#if FEATURE_STOPWATCH
            return Stopwatch.GetTimestamp();
#else
            return DateTime.Now.Ticks;
#endif
        }

        public long StartNestedCall(int index, long timestamp) {
#if FEATURE_STOPWATCH
            long now = Stopwatch.GetTimestamp();
#else
            long now = DateTime.Now.Ticks;
#endif
            Interlocked.Add(ref _profileData[index, TimeInBody], now - timestamp);
            return now;
        }

        public long FinishNestedCall(int index, long timestamp) {
#if FEATURE_STOPWATCH
            long now = Stopwatch.GetTimestamp();
#else
            long now = DateTime.Now.Ticks;
#endif
            Interlocked.Add(ref _profileData[index, TimeInChildMethods], now - timestamp);
            return now;
        }

        public void FinishCall(int index, long timestamp) {
#if FEATURE_STOPWATCH
            long now = Stopwatch.GetTimestamp();
#else
            long now = DateTime.Now.Ticks;
#endif
            Interlocked.Add(ref _profileData[index, TimeInBody], now - timestamp);
        }

        #endregion

        #region Compilation support

        internal MSAst.Expression AddOuterProfiling(MSAst.Expression/*!*/ body, MSAst.ParameterExpression/*!*/ tick, int profileIndex) {
            return Ast.Block(
                Ast.Assign(
                    tick,
                    Ast.Call(
                        Ast.Constant(this, typeof(Profiler)),
                        typeof(Profiler).GetMethod("StartCall"),
                        AstUtils.Constant(profileIndex)
                    )
                ),
                AstUtils.Try(                
                    body
                ).Finally(
                    Ast.Call(
                        Ast.Constant(this, typeof(Profiler)),
                        typeof(Profiler).GetMethod("FinishCall"),
                        AstUtils.Constant(profileIndex),
                        tick
                    )
                )
            );
        }

        internal MSAst.Expression AddInnerProfiling(MSAst.Expression/*!*/ body, MSAst.ParameterExpression/*!*/ tick, int profileIndex) {
            return Ast.Block(
                Ast.Assign(
                    tick,
                    Ast.Call(
                        Ast.Constant(this, typeof(Profiler)),
                        typeof(Profiler).GetMethod("StartNestedCall"),
                        AstUtils.Constant(profileIndex),
                        tick
                    )
                ),
                AstUtils.Try(
                    body
                ).Finally(
                    Ast.Assign(
                        tick,
                        Ast.Call(
                            Ast.Constant(this, typeof(Profiler)),
                            typeof(Profiler).GetMethod("FinishNestedCall"),
                            AstUtils.Constant(profileIndex),
                            tick
                        )
                    )
                )
            );
        }

        private sealed class InnerMethodProfiler : MSAst.DynamicExpressionVisitor {
            private readonly Profiler/*!*/ _profiler;
            private readonly MSAst.ParameterExpression/*!*/ _tick;
            private readonly int _profileIndex;

            public InnerMethodProfiler(Profiler/*!*/ profiler, MSAst.ParameterExpression/*!*/ tick, int profileIndex) {
                _profiler = profiler;
                _tick = tick;
                _profileIndex = profileIndex;
            }

            protected override MSAst.Expression/*!*/ VisitDynamic(MSAst.DynamicExpression/*!*/ node) {
                return _profiler.AddInnerProfiling(node, _tick, _profileIndex);
            }

            protected override MSAst.Expression/*!*/ VisitExtension(MSAst.Expression node) {
                if (node is ReducableDynamicExpression) {
                    return _profiler.AddInnerProfiling(node, _tick, _profileIndex);
                }
                return base.VisitExtension(node);
            }

            protected override MSAst.Expression VisitMethodCall(MSAst.MethodCallExpression node) {
                var result = base.VisitMethodCall(node);
                if (IgnoreMethod(node.Method)) {
                    // To ignore the called method, we need to prevent its time from being added to the current method's total
                    return _profiler.AddInnerProfiling(node, _tick, _profileIndex);
                }
                return result;
            }

            protected override MSAst.Expression VisitLambda<T>(MSAst.Expression<T> node) {
                // Don't trace into embedded function
                return node;
            }
        }

        private static bool IgnoreMethod(MethodBase method) {
            return method.GetCustomAttributes(typeof(ProfilerTreatsAsExternalAttribute), false).Any();
        }

        /// <summary>
        /// Adds profiling calls to a Python method.
        /// Calculates both the time spent only in this method
        /// </summary>
        internal MSAst.Expression AddProfiling(MSAst.Expression/*!*/ body, MSAst.ParameterExpression/*!*/ tick, string/*!*/ name, bool unique) {
            int profileIndex = GetProfilerIndex(name);
            return AddOuterProfiling(new InnerMethodProfiler(this, tick, profileIndex).Visit(body), tick, profileIndex);
        }

        /// <summary>
        /// Wraps a call to a MethodInfo with profiling capture for that MethodInfo
        /// </summary>
        internal MSAst.Expression AddProfiling(MSAst.Expression/*!*/ body, MethodBase/*!*/ method) {
            if ((method is DynamicMethod) || IgnoreMethod(method)) {
                return body;
            }
            int profileIndex = GetProfilerIndex(method);
            MSAst.ParameterExpression tick = Ast.Variable(typeof(long), "$tick");
            return Ast.Block(
                new MSAst.ParameterExpression[] { tick },
                AddOuterProfiling(body, tick, profileIndex)
            );
        }

        #endregion

        /// <summary>
        /// Encapsulates profiler data to return to clients
        /// </summary>
        public struct Data {
            public string Name;
            public long InclusiveTime;
            public long ExclusiveTime;
            public int Calls;

            public Data(string _name, long _inclusive, long _exclusive, int _calls) {
                Name = _name;
                InclusiveTime = _inclusive;
                ExclusiveTime = _exclusive;
                Calls = _calls;
            }
        }
    }

    /// <summary>
    /// Marks that this built-in method should be treated as external by the profiler.
    /// When placed on a call emitted into a Python method, all the time spent in this
    /// call will still show up in its parent's inclusive time, but will not be
    /// part of its exclusive time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProfilerTreatsAsExternalAttribute : Attribute {
    }
}
