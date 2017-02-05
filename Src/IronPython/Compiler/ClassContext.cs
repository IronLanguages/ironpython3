using IronPython.Compiler.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronPython.Compiler {
    class ClassContext {
        public ClassContext(string name, Expression metaclass=null) {
            Name = name;
            Metaclass = metaclass;
            FirstArg = null;
        }

        public string Name {
            get;
            private set;
        }

        public Expression Metaclass {
            get;
            private set;
        }

        public bool InMember {
            get; set;
        }

        public string FirstArg {
            get; set;
        }
    }
}
