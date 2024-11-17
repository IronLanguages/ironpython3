// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides a StreamContentProvider for a stream of content backed by a file on disk.
    /// </summary>
    [Serializable]
    internal sealed class MemoryStreamContentProvider : TextContentProvider {
        private readonly PythonContext _context;
        private readonly byte[] _data;
        private readonly int _index, _count;
        private readonly string _path;

        internal MemoryStreamContentProvider(PythonContext context, byte[] data, string path)
            : this(context, data, 0, data?.Length ?? 0, path) { }

        internal MemoryStreamContentProvider(PythonContext context, byte[] data, int index, int count, string path) {
            ContractUtils.RequiresNotNull(context, nameof(context));
            ContractUtils.RequiresNotNull(data, nameof(data));
            ContractUtils.RequiresArrayRange(data, index, count, nameof(index), nameof(count));

            _context = context;
            _data = data;
            _index = index;
            _count = count;
            _path = path;
        }

        public override SourceCodeReader GetReader() {
            return _context.GetSourceReader(new MemoryStream(_data, _index, _count, writable: false), _context.DefaultEncoding, _path);
        }
    }
}
