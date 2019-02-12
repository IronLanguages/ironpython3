using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;


//[assembly: PythonModule("_hashlib", typeof(IronPython.Modules.PythonHashlib))]
namespace IronPython.Modules {
    
    [PythonHidden]
    public abstract class HashBase<T>
#if FEATURE_ICLONEABLE
            : ICloneable
#endif
            where T : HashAlgorithm {

        protected T _hasher;
        private static MethodInfo _memberwiseClone;

        private static readonly byte[] _empty = new byte[0];

        public readonly string name;
        public readonly int block_size;
        public readonly int digest_size;
        public readonly int digestsize;

        internal HashBase(string name, int blocksize, int digestsize) {
            this.name = name;
            this.block_size = blocksize;
            this.digest_size = this.digestsize = digestsize;
            CreateHasher();
        }

        protected abstract void CreateHasher();

        [Documentation("update(string) -> None (update digest with string data)")]
        public void update([BytesConversion]IList<byte> newBytes) {
            byte[] bytes = newBytes.ToArray();
            lock (_hasher) {
                _hasher.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }
        }

        [Documentation("digest() -> int (current digest value)")]
        public string digest() {
            T copy = CloneHasher();
            copy.TransformFinalBlock(_empty, 0, 0);
            return copy.Hash.MakeString();
        }

        [Documentation("hexdigest() -> string (current digest as hex digits)")]
        public string hexdigest() {
            T copy = CloneHasher();
            copy.TransformFinalBlock(_empty, 0, 0);

            StringBuilder result = new StringBuilder(2 * copy.Hash.Length);
            for (int i = 0; i < copy.Hash.Length; i++) {
                result.Append(copy.Hash[i].ToString("x2"));
            }
            return result.ToString();
        }

        public abstract HashBase<T> copy();

#if FEATURE_ICLONEABLE
        object ICloneable.Clone() {
            return copy();
        }
#endif

        protected T CloneHasher() {
            T clone = default(T);
            if (_memberwiseClone == null) {
                _memberwiseClone = _hasher.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            if (_memberwiseClone != null) {
                lock (_hasher) {
                    clone = (T)_memberwiseClone.Invoke(_hasher, new object[0]);
                }
            }

            FieldInfo[] fields = _hasher.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if(fields != null) {
                foreach(FieldInfo field in fields) {
                    if(field.FieldType.IsArray) {
                        lock (_hasher) {
                            Array orig = field.GetValue(_hasher) as Array;
                            if (orig != null) {
                                field.SetValue(clone, orig.Clone());
                            }
                        }
                    }
                }
            }
            return clone;
        }
    }
}
