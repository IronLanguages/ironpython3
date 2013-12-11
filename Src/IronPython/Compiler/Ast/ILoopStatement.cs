using System;
using System.Collections.Generic;
using System.Text;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif


namespace IronPython.Compiler.Ast {
    internal interface ILoopStatement {
        MSAst.LabelTarget BreakLabel {
            get;
            set;
        }

        MSAst.LabelTarget ContinueLabel {
            get;
            set;
        }

    }

}
