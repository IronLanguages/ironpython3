using System;
using IronPython.Runtime;

namespace IronPythonTest {
    public class MemberOverloadTest {
        private int _prop;
        public int Prop {
            get {
                return _prop;
            }
            set {
                _prop = value;
            }
        }

        public void SetMember(string name, object value) {
            throw new InvalidOperationException("Do not call SetMember0");
        }

        public void SetMember(CodeContext context, string name, object value) {
            throw new InvalidOperationException("Do not call SetMember1");
        }
        
        public object GetBoundMember(string name) {
            throw new InvalidOperationException("Do not call GetBoundMember0");
        }

        public object GetBoundMember(CodeContext context, string name) {
            throw new InvalidOperationException("Do not call GetBoundMember1");
        }
        
        public object GetCustomMember(string name) {
            throw new InvalidOperationException("Do not call GetCustomMember0");
        }

        public object GetCustomMember(CodeContext context, string name) {
            throw new InvalidOperationException("Do not call GetCustomMember1");
        }
        
        public void DeleteMember(string name) {
            throw new InvalidOperationException("Do not call DeleteMember0");
        }

        public void DeleteMember(CodeContext context, string name) {
            throw new InvalidOperationException("Do not call DeleteMember1");
        }
    }
}
