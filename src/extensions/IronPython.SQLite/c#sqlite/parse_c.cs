using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using u8 = System.Byte;


using YYCODETYPE = System.Int32;
using YYACTIONTYPE = System.Int32;

namespace Community.CsharpSqlite
{
  using sqlite3ParserTOKENTYPE = Sqlite3.Token;

  public partial class Sqlite3
  {
    /*
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2010-08-23 18:52:01 42537b60566f288167f1b5864a5435986838e3a3
    **
    *************************************************************************
    */

    /* Driver template for the LEMON parser generator.
    ** The author disclaims copyright to this source code.
    **
    ** This version of "lempar.c" is modified, slightly, for use by SQLite.
    ** The only modifications are the addition of a couple of NEVER()
    ** macros to disable tests that are needed in the case of a general
    ** LALR(1) grammar but which are always false in the
    ** specific grammar used by SQLite.
    */
    /* First off, code is included that follows the "include" declaration
    ** in the input grammar file. */
    //#include <stdio.h>
    //#line 51 "parse.y"

    //#include "sqliteInt.h"
    /*
    ** Disable all error recovery processing in the parser push-down
    ** automaton.
    */
    //#define YYNOERRORRECOVERY 1
    const int YYNOERRORRECOVERY = 1;

    /*
    ** Make yytestcase() the same as testcase()
    */
    //#define yytestcase(X) testcase(X)
    static void yytestcase<T>( T X )
    {
      testcase( X );
    }

    /*
    ** An instance of this structure holds information about the
    ** LIMIT clause of a SELECT statement.
    */
    public struct LimitVal
    {
      public Expr pLimit;    /* The LIMIT expression.  NULL if there is no limit */
      public Expr pOffset;   /* The OFFSET expression.  NULL if there is none */
    };

    /*
    ** An instance of this structure is used to store the LIKE,
    ** GLOB, NOT LIKE, and NOT GLOB operators.
    */
    public struct LikeOp
    {
      public Token eOperator;  /* "like" or "glob" or "regexp" */
      public bool not;         /* True if the NOT keyword is present */
    };

    /*
    ** An instance of the following structure describes the event of a
    ** TRIGGER.  "a" is the event type, one of TK_UPDATE, TK_INSERT,
    ** TK_DELETE, or TK_INSTEAD.  If the event is of the form
    **
    **      UPDATE ON (a,b,c)
    **
    ** Then the "b" IdList records the list "a,b,c".
    */
    public struct TrigEvent
    {
      public int a;
      public IdList b;
    };
    /*
    ** An instance of this structure holds the ATTACH key and the key type.
    */
    public struct AttachKey
    {
      public int type;
      public Token key;
    };

    //#line 722 "parse.y"

    /* This is a utility routine used to set the ExprSpan.zStart and
    ** ExprSpan.zEnd values of pOut so that the span covers the complete
    ** range of text beginning with pStart and going to the end of pEnd.
    */
    static void spanSet( ExprSpan pOut, Token pStart, Token pEnd )
    {
      pOut.zStart = pStart.z;
      pOut.zEnd = pEnd.z.Substring( pEnd.n );
    }

    /* Construct a new Expr object from a single identifier.  Use the
    ** new Expr to populate pOut.  Set the span of pOut to be the identifier
    ** that created the expression.
    */
    static void spanExpr( ExprSpan pOut, Parse pParse, int op, Token pValue )
    {
      pOut.pExpr = sqlite3PExpr( pParse, op, 0, 0, pValue );
      pOut.zStart = pValue.z;
      pOut.zEnd = pValue.z.Substring( pValue.n );
    }
    //#line 817 "parse.y"

    /* This routine constructs a binary expression node out of two ExprSpan
    ** objects and uses the result to populate a new ExprSpan object.
    */
    static void spanBinaryExpr(
    ExprSpan pOut,     /* Write the result here */
    Parse pParse,      /* The parsing context.  Errors accumulate here */
    int op,            /* The binary operation */
    ExprSpan pLeft,    /* The left operand */
    ExprSpan pRight    /* The right operand */
    )
    {
      pOut.pExpr = sqlite3PExpr( pParse, op, pLeft.pExpr, pRight.pExpr, 0 );
      pOut.zStart = pLeft.zStart;
      pOut.zEnd = pRight.zEnd;
    }
    //#line 873 "parse.y"

    /* Construct an expression node for a unary postfix operator
    */
    static void spanUnaryPostfix(
    ExprSpan pOut,        /* Write the new expression node here */
    Parse pParse,         /* Parsing context to record errors */
    int op,               /* The operator */
    ExprSpan pOperand,    /* The operand */
    Token pPostOp         /* The operand token for setting the span */
    )
    {
      pOut.pExpr = sqlite3PExpr( pParse, op, pOperand.pExpr, 0, 0 );
      pOut.zStart = pOperand.zStart;
      pOut.zEnd = pPostOp.z.Substring( pPostOp.n );
    }
    //#line 892 "parse.y"

    /* A routine to convert a binary TK_IS or TK_ISNOT expression into a
    ** unary TK_ISNULL or TK_NOTNULL expression. */
    static void binaryToUnaryIfNull( Parse pParse, Expr pY, Expr pA, int op )
    {
      sqlite3 db = pParse.db;
      if ( /*db.mallocFailed == null && */pY.op == TK_NULL )
      {
        pA.op = (u8)op;
        sqlite3ExprDelete( db, ref pA.pRight );
        pA.pRight = null;
      }
    }
    //#line 920 "parse.y"

    /* Construct an expression node for a unary prefix operator
    */
    static void spanUnaryPrefix(
    ExprSpan pOut,        /* Write the new expression node here */
    Parse pParse,         /* Parsing context to record errors */
    int op,               /* The operator */
    ExprSpan pOperand,    /* The operand */
    Token pPreOp          /* The operand token for setting the span */
    )
    {
      pOut.pExpr = sqlite3PExpr( pParse, op, pOperand.pExpr, 0, 0 );
      pOut.zStart = pPreOp.z;
      pOut.zEnd = pOperand.zEnd;
    }
    //#line 141 "parse.c"
    /* Next is all token values, in a form suitable for use by makeheaders.
    ** This section will be null unless lemon is run with the -m switch.
    */
    /*
    ** These constants (all generated automatically by the parser generator)
    ** specify the various kinds of tokens (terminals) that the parser
    ** understands.
    **
    ** Each symbol here is a terminal symbol in the grammar.
    */
    /* Make sure the INTERFACE macro is defined.
    */
#if !INTERFACE
    //# define INTERFACE 1
#endif
    /* The next thing included is series of defines which control
** various aspects of the generated parser.
**    YYCODETYPE         is the data type used for storing terminal
**                       and nonterminal numbers.  "unsigned char" is
**                       used if there are fewer than 250 terminals
**                       and nonterminals.  "int" is used otherwise.
**    YYNOCODE           is a number of type YYCODETYPE which corresponds
**                       to no legal terminal or nonterminal number.  This
**                       number is used to fill in empty slots of the hash
**                       table.
**    YYFALLBACK         If defined, this indicates that one or more tokens
**                       have fall-back values which should be used if the
**                       original value of the token will not parse.
**    YYACTIONTYPE       is the data type used for storing terminal
**                       and nonterminal numbers.  "unsigned char" is
**                       used if there are fewer than 250 rules and
**                       states combined.  "int" is used otherwise.
**    sqlite3ParserTOKENTYPE     is the data type used for minor tokens given
**                       directly to the parser from the tokenizer.
**    YYMINORTYPE        is the data type used for all minor tokens.
**                       This is typically a union of many types, one of
**                       which is sqlite3ParserTOKENTYPE.  The entry in the union
**                       for base tokens is called "yy0".
**    YYSTACKDEPTH       is the maximum depth of the parser's stack.  If
**                       zero the stack is dynamically sized using realloc()
**    sqlite3ParserARG_SDECL     A static variable declaration for the %extra_argument
**    sqlite3ParserARG_PDECL     A parameter declaration for the %extra_argument
**    sqlite3ParserARG_STORE     Code to store %extra_argument into yypParser
**    sqlite3ParserARG_FETCH     Code to extract %extra_argument from yypParser
**    YYNSTATE           the combined number of states.
**    YYNRULE            the number of rules in the grammar
**    YYERRORSYMBOL      is the code number of the error symbol.  If not
**                       defined, then do no error processing.
*/
    //#define YYCODETYPE unsigned short char
    const int YYNOCODE = 253;
    //#define YYACTIONTYPE unsigned short int
    const int YYWILDCARD = 67;
    //#define sqlite3ParserTOKENTYPE Token
    public class YYMINORTYPE
    {
      public int yyinit;
      public sqlite3ParserTOKENTYPE yy0 = new sqlite3ParserTOKENTYPE();
      public int yy4;
      public TrigEvent yy90;
      public ExprSpan yy118 = new ExprSpan();
      public TriggerStep yy203;
      public u8 yy210;
      public struct _yy215
      {
        public int value;
        public int mask;
      }
      public _yy215 yy215;
      public SrcList yy259;
      public LimitVal yy292;
      public Expr yy314;
      public ExprList yy322;
      public LikeOp yy342;
      public IdList yy384;
      public Select yy387;
    }

#if !YYSTACKDEPTH
    const int YYSTACKDEPTH = 100;
#endif
    //#define sqlite3ParserARG_SDECL Parse pParse;
    //#define sqlite3ParserARG_PDECL ,Parse pParse
    //#define sqlite3ParserARG_FETCH Parse pParse = yypParser.pParse
    //#define sqlite3ParserARG_STORE yypParser.pParse = pParse
    const int YYNSTATE = 630;
    const int YYNRULE = 329;
    //#define YYFALLBACK 1
    const int YYFALLBACK = 1;
    const int YY_NO_ACTION = ( YYNSTATE + YYNRULE + 2 );
    const int YY_ACCEPT_ACTION = ( YYNSTATE + YYNRULE + 1 );
    const int YY_ERROR_ACTION = ( YYNSTATE + YYNRULE );

    /* The yyzerominor constant is used to initialize instances of
    ** YYMINORTYPE objects to zero. */
    YYMINORTYPE yyzerominor = new YYMINORTYPE();//static const YYMINORTYPE yyzerominor = { 0 };

    /* Define the yytestcase() macro to be a no-op if is not already defined
    ** otherwise.
    **
    ** Applications can choose to define yytestcase() in the %include section
    ** to a macro that can assist in verifying code coverage.  For production
    ** code the yytestcase() macro should be turned off.  But it is useful
    ** for testing.
    */
    //#if !yytestcase
    //# define yytestcase(X)
    //#endif

    /* Next are the tables used to determine what action to take based on the
    ** current state and lookahead token.  These tables are used to implement
    ** functions that take a state number and lookahead value and return an
    ** action integer.
    **
    ** Suppose the action integer is N.  Then the action is determined as
    ** follows
    **
    **   0 <= N < YYNSTATE                  Shift N.  That is, push the lookahead
    **                                      token onto the stack and goto state N.
    **
    **   YYNSTATE <= N < YYNSTATE+YYNRULE   Reduce by rule N-YYNSTATE.
    **
    **   N == YYNSTATE+YYNRULE              A syntax error has occurred.
    **
    **   N == YYNSTATE+YYNRULE+1            The parser accepts its input.
    **
    **   N == YYNSTATE+YYNRULE+2            No such action.  Denotes unused
    **                                      slots in the yy_action[] table.
    **
    ** The action table is constructed as a single large table named yy_action[].
    ** Given state S and lookahead X, the action is computed as
    **
    **      yy_action[ yy_shift_ofst[S] + X ]
    **
    ** If the index value yy_shift_ofst[S]+X is out of range or if the value
    ** yy_lookahead[yy_shift_ofst[S]+X] is not equal to X or if yy_shift_ofst[S]
    ** is equal to YY_SHIFT_USE_DFLT, it means that the action is not in the table
    ** and that yy_default[S] should be used instead.
    **
    ** The formula above is for computing the action when the lookahead is
    ** a terminal symbol.  If the lookahead is a non-terminal (as occurs after
    ** a reduce action) then the yy_reduce_ofst[] array is used in place of
    ** the yy_shift_ofst[] array and YY_REDUCE_USE_DFLT is used in place of
    ** YY_SHIFT_USE_DFLT.
    **
    ** The following are the tables generated in this section:
    **
    **  yy_action[]        A single table containing all actions.
    **  yy_lookahead[]     A table containing the lookahead for each entry in
    **                     yy_action.  Used to detect hash collisions.
    **  yy_shift_ofst[]    For each state, the offset into yy_action for
    **                     shifting terminals.
    **  yy_reduce_ofst[]   For each state, the offset into yy_action for
    **                     shifting non-terminals after a reduce.
    **  yy_default[]       Default action for each state.
    */
    //#define YY_ACTTAB_COUNT (1557)
    const int YY_ACTTAB_COUNT = 1557;
    static YYACTIONTYPE[] yy_action = new YYACTIONTYPE[]{
/*     0 */   313,  960,  186,  419,    2,  172,  627,  597,   55,   55,
/*    10 */    55,   55,   48,   53,   53,   53,   53,   52,   52,   51,
/*    20 */    51,   51,   50,  238,  302,  283,  623,  622,  516,  515,
/*    30 */   590,  584,   55,   55,   55,   55,  282,   53,   53,   53,
/*    40 */    53,   52,   52,   51,   51,   51,   50,  238,    6,   56,
/*    50 */    57,   47,  582,  581,  583,  583,   54,   54,   55,   55,
/*    60 */    55,   55,  608,   53,   53,   53,   53,   52,   52,   51,
/*    70 */    51,   51,   50,  238,  313,  597,  409,  330,  579,  579,
/*    80 */    32,   53,   53,   53,   53,   52,   52,   51,   51,   51,
/*    90 */    50,  238,  330,  217,  620,  619,  166,  411,  624,  382,
/*   100 */   379,  378,    7,  491,  590,  584,  200,  199,  198,   58,
/*   110 */   377,  300,  414,  621,  481,   66,  623,  622,  621,  580,
/*   120 */   254,  601,   94,   56,   57,   47,  582,  581,  583,  583,
/*   130 */    54,   54,   55,   55,   55,   55,  671,   53,   53,   53,
/*   140 */    53,   52,   52,   51,   51,   51,   50,  238,  313,  532,
/*   150 */   226,  506,  507,  133,  177,  139,  284,  385,  279,  384,
/*   160 */   169,  197,  342,  398,  251,  226,  253,  275,  388,  167,
/*   170 */   139,  284,  385,  279,  384,  169,  570,  236,  590,  584,
/*   180 */   672,  240,  275,  157,  620,  619,  554,  437,   51,   51,
/*   190 */    51,   50,  238,  343,  439,  553,  438,   56,   57,   47,
/*   200 */   582,  581,  583,  583,   54,   54,   55,   55,   55,   55,
/*   210 */   465,   53,   53,   53,   53,   52,   52,   51,   51,   51,
/*   220 */    50,  238,  313,  390,   52,   52,   51,   51,   51,   50,
/*   230 */   238,  391,  166,  491,  566,  382,  379,  378,  409,  440,
/*   240 */   579,  579,  252,  440,  607,   66,  377,  513,  621,   49,
/*   250 */    46,  147,  590,  584,  621,   16,  466,  189,  621,  441,
/*   260 */   442,  673,  526,  441,  340,  577,  595,   64,  194,  482,
/*   270 */   434,   56,   57,   47,  582,  581,  583,  583,   54,   54,
/*   280 */    55,   55,   55,   55,   30,   53,   53,   53,   53,   52,
/*   290 */    52,   51,   51,   51,   50,  238,  313,  593,  593,  593,
/*   300 */   387,  578,  606,  493,  259,  351,  258,  411,    1,  623,
/*   310 */   622,  496,  623,  622,   65,  240,  623,  622,  597,  443,
/*   320 */   237,  239,  414,  341,  237,  602,  590,  584,   18,  603,
/*   330 */   166,  601,   87,  382,  379,  378,   67,  623,  622,   38,
/*   340 */   623,  622,  176,  270,  377,   56,   57,   47,  582,  581,
/*   350 */   583,  583,   54,   54,   55,   55,   55,   55,  175,   53,
/*   360 */    53,   53,   53,   52,   52,   51,   51,   51,   50,  238,
/*   370 */   313,  396,  233,  411,  531,  565,  317,  620,  619,   44,
/*   380 */   620,  619,  240,  206,  620,  619,  597,  266,  414,  268,
/*   390 */   409,  597,  579,  579,  352,  184,  505,  601,   73,  533,
/*   400 */   590,  584,  466,  548,  190,  620,  619,  576,  620,  619,
/*   410 */   547,  383,  551,   35,  332,  575,  574,  600,  504,   56,
/*   420 */    57,   47,  582,  581,  583,  583,   54,   54,   55,   55,
/*   430 */    55,   55,  567,   53,   53,   53,   53,   52,   52,   51,
/*   440 */    51,   51,   50,  238,  313,  411,  561,  561,  528,  364,
/*   450 */   259,  351,  258,  183,  361,  549,  524,  374,  411,  597,
/*   460 */   414,  240,  560,  560,  409,  604,  579,  579,  328,  601,
/*   470 */    93,  623,  622,  414,  590,  584,  237,  564,  559,  559,
/*   480 */   520,  402,  601,   87,  409,  210,  579,  579,  168,  421,
/*   490 */   950,  519,  950,   56,   57,   47,  582,  581,  583,  583,
/*   500 */    54,   54,   55,   55,   55,   55,  192,   53,   53,   53,
/*   510 */    53,   52,   52,   51,   51,   51,   50,  238,  313,  600,
/*   520 */   293,  563,  511,  234,  357,  146,  475,  475,  367,  411,
/*   530 */   562,  411,  358,  542,  425,  171,  411,  215,  144,  620,
/*   540 */   619,  544,  318,  353,  414,  203,  414,  275,  590,  584,
/*   550 */   549,  414,  174,  601,   94,  601,   79,  558,  471,   61,
/*   560 */   601,   79,  421,  949,  350,  949,   34,   56,   57,   47,
/*   570 */   582,  581,  583,  583,   54,   54,   55,   55,   55,   55,
/*   580 */   535,   53,   53,   53,   53,   52,   52,   51,   51,   51,
/*   590 */    50,  238,  313,  307,  424,  394,  272,   49,   46,  147,
/*   600 */   349,  322,    4,  411,  491,  312,  321,  425,  568,  492,
/*   610 */   216,  264,  407,  575,  574,  429,   66,  549,  414,  621,
/*   620 */   540,  602,  590,  584,   13,  603,  621,  601,   72,   12,
/*   630 */   618,  617,  616,  202,  210,  621,  546,  469,  422,  319,
/*   640 */   148,   56,   57,   47,  582,  581,  583,  583,   54,   54,
/*   650 */    55,   55,   55,   55,  338,   53,   53,   53,   53,   52,
/*   660 */    52,   51,   51,   51,   50,  238,  313,  600,  600,  411,
/*   670 */    39,   21,   37,  170,  237,  875,  411,  572,  572,  201,
/*   680 */   144,  473,  538,  331,  414,  474,  143,  146,  630,  628,
/*   690 */   334,  414,  353,  601,   68,  168,  590,  584,  132,  365,
/*   700 */   601,   96,  307,  423,  530,  336,   49,   46,  147,  568,
/*   710 */   406,  216,  549,  360,  529,   56,   57,   47,  582,  581,
/*   720 */   583,  583,   54,   54,   55,   55,   55,   55,  411,   53,
/*   730 */    53,   53,   53,   52,   52,   51,   51,   51,   50,  238,
/*   740 */   313,  411,  605,  414,  484,  510,  172,  422,  597,  318,
/*   750 */   496,  485,  601,   99,  411,  142,  414,  411,  231,  411,
/*   760 */   540,  411,  359,  629,    2,  601,   97,  426,  308,  414,
/*   770 */   590,  584,  414,   20,  414,  621,  414,  621,  601,  106,
/*   780 */   503,  601,  105,  601,  108,  601,  109,  204,   28,   56,
/*   790 */    57,   47,  582,  581,  583,  583,   54,   54,   55,   55,
/*   800 */    55,   55,  411,   53,   53,   53,   53,   52,   52,   51,
/*   810 */    51,   51,   50,  238,  313,  411,  597,  414,  411,  276,
/*   820 */   214,  600,  411,  366,  213,  381,  601,  134,  274,  500,
/*   830 */   414,  167,  130,  414,  621,  411,  354,  414,  376,  601,
/*   840 */   135,  129,  601,  100,  590,  584,  601,  104,  522,  521,
/*   850 */   414,  621,  224,  273,  600,  167,  327,  282,  600,  601,
/*   860 */   103,  468,  521,   56,   57,   47,  582,  581,  583,  583,
/*   870 */    54,   54,   55,   55,   55,   55,  411,   53,   53,   53,
/*   880 */    53,   52,   52,   51,   51,   51,   50,  238,  313,  411,
/*   890 */    27,  414,  411,  375,  276,  167,  359,  544,   50,  238,
/*   900 */   601,   95,  128,  223,  414,  411,  165,  414,  411,  621,
/*   910 */   411,  621,  612,  601,  102,  372,  601,   76,  590,  584,
/*   920 */   414,  570,  236,  414,  470,  414,  167,  621,  188,  601,
/*   930 */    98,  225,  601,  138,  601,  137,  232,   56,   45,   47,
/*   940 */   582,  581,  583,  583,   54,   54,   55,   55,   55,   55,
/*   950 */   411,   53,   53,   53,   53,   52,   52,   51,   51,   51,
/*   960 */    50,  238,  313,  276,  276,  414,  411,  276,  544,  459,
/*   970 */   359,  171,  209,  479,  601,  136,  628,  334,  621,  621,
/*   980 */   125,  414,  621,  368,  411,  621,  257,  540,  589,  588,
/*   990 */   601,   75,  590,  584,  458,  446,   23,   23,  124,  414,
/*  1000 */   326,  325,  621,  427,  324,  309,  600,  288,  601,   92,
/*  1010 */   586,  585,   57,   47,  582,  581,  583,  583,   54,   54,
/*  1020 */    55,   55,   55,   55,  411,   53,   53,   53,   53,   52,
/*  1030 */    52,   51,   51,   51,   50,  238,  313,  587,  411,  414,
/*  1040 */   411,  207,  611,  476,  171,  472,  160,  123,  601,   91,
/*  1050 */   323,  261,   15,  414,  464,  414,  411,  621,  411,  354,
/*  1060 */   222,  411,  601,   74,  601,   90,  590,  584,  159,  264,
/*  1070 */   158,  414,  461,  414,  621,  600,  414,  121,  120,   25,
/*  1080 */   601,   89,  601,  101,  621,  601,   88,   47,  582,  581,
/*  1090 */   583,  583,   54,   54,   55,   55,   55,   55,  544,   53,
/*  1100 */    53,   53,   53,   52,   52,   51,   51,   51,   50,  238,
/*  1110 */    43,  405,  263,    3,  610,  264,  140,  415,  622,   24,
/*  1120 */   410,   11,  456,  594,  118,  155,  219,  452,  408,  621,
/*  1130 */   621,  621,  156,   43,  405,  621,    3,  286,  621,  113,
/*  1140 */   415,  622,  111,  445,  411,  400,  557,  403,  545,   10,
/*  1150 */   411,  408,  264,  110,  205,  436,  541,  566,  453,  414,
/*  1160 */   621,  621,   63,  621,  435,  414,  411,  621,  601,   94,
/*  1170 */   403,  621,  411,  337,  601,   86,  150,   40,   41,  534,
/*  1180 */   566,  414,  242,  264,   42,  413,  412,  414,  600,  595,
/*  1190 */   601,   85,  191,  333,  107,  451,  601,   84,  621,  539,
/*  1200 */    40,   41,  420,  230,  411,  149,  316,   42,  413,  412,
/*  1210 */   398,  127,  595,  315,  621,  399,  278,  625,  181,  414,
/*  1220 */   593,  593,  593,  592,  591,   14,  450,  411,  601,   71,
/*  1230 */   240,  621,   43,  405,  264,    3,  615,  180,  264,  415,
/*  1240 */   622,  614,  414,  593,  593,  593,  592,  591,   14,  621,
/*  1250 */   408,  601,   70,  621,  417,   33,  405,  613,    3,  411,
/*  1260 */   264,  411,  415,  622,  418,  626,  178,  509,    8,  403,
/*  1270 */   241,  416,  126,  408,  414,  621,  414,  449,  208,  566,
/*  1280 */   240,  221,  621,  601,   83,  601,   82,  599,  297,  277,
/*  1290 */   296,   30,  403,   31,  395,  264,  295,  397,  489,   40,
/*  1300 */    41,  411,  566,  220,  621,  294,   42,  413,  412,  271,
/*  1310 */   621,  595,  600,  621,   59,   60,  414,  269,  267,  623,
/*  1320 */   622,   36,   40,   41,  621,  601,   81,  598,  235,   42,
/*  1330 */   413,  412,  621,  621,  595,  265,  344,  411,  248,  556,
/*  1340 */   173,  185,  593,  593,  593,  592,  591,   14,  218,   29,
/*  1350 */   621,  543,  414,  305,  304,  303,  179,  301,  411,  566,
/*  1360 */   454,  601,   80,  289,  335,  593,  593,  593,  592,  591,
/*  1370 */    14,  411,  287,  414,  151,  392,  246,  260,  411,  196,
/*  1380 */   195,  523,  601,   69,  411,  245,  414,  526,  537,  285,
/*  1390 */   389,  595,  621,  414,  536,  601,   17,  362,  153,  414,
/*  1400 */   466,  463,  601,   78,  154,  414,  462,  152,  601,   77,
/*  1410 */   355,  255,  621,  455,  601,    9,  621,  386,  444,  517,
/*  1420 */   247,  621,  593,  593,  593,  621,  621,  244,  621,  243,
/*  1430 */   430,  518,  292,  621,  329,  621,  145,  393,  280,  513,
/*  1440 */   291,  131,  621,  514,  621,  621,  311,  621,  259,  346,
/*  1450 */   249,  621,  621,  229,  314,  621,  228,  512,  227,  240,
/*  1460 */   494,  488,  310,  164,  487,  486,  373,  480,  163,  262,
/*  1470 */   369,  371,  162,   26,  212,  478,  477,  161,  141,  363,
/*  1480 */   467,  122,  339,  187,  119,  348,  347,  117,  116,  115,
/*  1490 */   114,  112,  182,  457,  320,   22,  433,  432,  448,   19,
/*  1500 */   609,  431,  428,   62,  193,  596,  573,  298,  555,  552,
/*  1510 */   571,  404,  290,  380,  498,  510,  495,  306,  281,  499,
/*  1520 */   250,    5,  497,  460,  345,  447,  569,  550,  238,  299,
/*  1530 */   527,  525,  508,  961,  502,  501,  961,  401,  961,  211,
/*  1540 */   490,  356,  256,  961,  483,  961,  961,  961,  961,  961,
/*  1550 */   961,  961,  961,  961,  961,  961,  370,
};
    static YYCODETYPE[] yy_lookahead = new YYCODETYPE[]{
/*     0 */    19,  142,  143,  144,  145,   24,    1,   26,   77,   78,
/*    10 */    79,   80,   81,   82,   83,   84,   85,   86,   87,   88,
/*    20 */    89,   90,   91,   92,   15,   98,   26,   27,    7,    8,
/*    30 */    49,   50,   77,   78,   79,   80,  109,   82,   83,   84,
/*    40 */    85,   86,   87,   88,   89,   90,   91,   92,   22,   68,
/*    50 */    69,   70,   71,   72,   73,   74,   75,   76,   77,   78,
/*    60 */    79,   80,   23,   82,   83,   84,   85,   86,   87,   88,
/*    70 */    89,   90,   91,   92,   19,   94,  112,   19,  114,  115,
/*    80 */    25,   82,   83,   84,   85,   86,   87,   88,   89,   90,
/*    90 */    91,   92,   19,   22,   94,   95,   96,  150,  150,   99,
/*   100 */   100,  101,   76,  150,   49,   50,  105,  106,  107,   54,
/*   110 */   110,  158,  165,  165,  161,  162,   26,   27,  165,  113,
/*   120 */    16,  174,  175,   68,   69,   70,   71,   72,   73,   74,
/*   130 */    75,   76,   77,   78,   79,   80,  118,   82,   83,   84,
/*   140 */    85,   86,   87,   88,   89,   90,   91,   92,   19,   23,
/*   150 */    92,   97,   98,   24,   96,   97,   98,   99,  100,  101,
/*   160 */   102,   25,   97,  216,   60,   92,   62,  109,  221,   25,
/*   170 */    97,   98,   99,  100,  101,  102,   86,   87,   49,   50,
/*   180 */   118,  116,  109,   25,   94,   95,   32,   97,   88,   89,
/*   190 */    90,   91,   92,  128,  104,   41,  106,   68,   69,   70,
/*   200 */    71,   72,   73,   74,   75,   76,   77,   78,   79,   80,
/*   210 */    11,   82,   83,   84,   85,   86,   87,   88,   89,   90,
/*   220 */    91,   92,   19,   19,   86,   87,   88,   89,   90,   91,
/*   230 */    92,   27,   96,  150,   66,   99,  100,  101,  112,  150,
/*   240 */   114,  115,  138,  150,  161,  162,  110,  103,  165,  222,
/*   250 */   223,  224,   49,   50,  165,   22,   57,   24,  165,  170,
/*   260 */   171,  118,   94,  170,  171,   23,   98,   25,  185,  186,
/*   270 */   243,   68,   69,   70,   71,   72,   73,   74,   75,   76,
/*   280 */    77,   78,   79,   80,  126,   82,   83,   84,   85,   86,
/*   290 */    87,   88,   89,   90,   91,   92,   19,  129,  130,  131,
/*   300 */    88,   23,  172,  173,  105,  106,  107,  150,   22,   26,
/*   310 */    27,  181,   26,   27,   22,  116,   26,   27,   26,  230,
/*   320 */   231,  197,  165,  230,  231,  113,   49,   50,  204,  117,
/*   330 */    96,  174,  175,   99,  100,  101,   22,   26,   27,  136,
/*   340 */    26,   27,  118,   16,  110,   68,   69,   70,   71,   72,
/*   350 */    73,   74,   75,   76,   77,   78,   79,   80,  118,   82,
/*   360 */    83,   84,   85,   86,   87,   88,   89,   90,   91,   92,
/*   370 */    19,  214,  215,  150,   23,   23,  155,   94,   95,   22,
/*   380 */    94,   95,  116,  160,   94,   95,   94,   60,  165,   62,
/*   390 */   112,   26,  114,  115,  128,   23,   36,  174,  175,   88,
/*   400 */    49,   50,   57,  120,   22,   94,   95,   23,   94,   95,
/*   410 */   120,   51,   25,  136,  169,  170,  171,  194,   58,   68,
/*   420 */    69,   70,   71,   72,   73,   74,   75,   76,   77,   78,
/*   430 */    79,   80,   23,   82,   83,   84,   85,   86,   87,   88,
/*   440 */    89,   90,   91,   92,   19,  150,   12,   12,   23,  228,
/*   450 */   105,  106,  107,   23,  233,   25,  165,   19,  150,   94,
/*   460 */   165,  116,   28,   28,  112,  174,  114,  115,  108,  174,
/*   470 */   175,   26,   27,  165,   49,   50,  231,   11,   44,   44,
/*   480 */    46,   46,  174,  175,  112,  160,  114,  115,   50,   22,
/*   490 */    23,   57,   25,   68,   69,   70,   71,   72,   73,   74,
/*   500 */    75,   76,   77,   78,   79,   80,  119,   82,   83,   84,
/*   510 */    85,   86,   87,   88,   89,   90,   91,   92,   19,  194,
/*   520 */   225,   23,   23,  215,   19,   95,  105,  106,  107,  150,
/*   530 */    23,  150,   27,   23,   67,   25,  150,  206,  207,   94,
/*   540 */    95,  166,  104,  218,  165,   22,  165,  109,   49,   50,
/*   550 */   120,  165,   25,  174,  175,  174,  175,   23,   21,  234,
/*   560 */   174,  175,   22,   23,  239,   25,   25,   68,   69,   70,
/*   570 */    71,   72,   73,   74,   75,   76,   77,   78,   79,   80,
/*   580 */   205,   82,   83,   84,   85,   86,   87,   88,   89,   90,
/*   590 */    91,   92,   19,   22,   23,  216,   23,  222,  223,  224,
/*   600 */    63,  220,   35,  150,  150,  163,  220,   67,  166,  167,
/*   610 */   168,  150,  169,  170,  171,  161,  162,   25,  165,  165,
/*   620 */   150,  113,   49,   50,   25,  117,  165,  174,  175,   35,
/*   630 */     7,    8,    9,  160,  160,  165,  120,  100,   67,  247,
/*   640 */   248,   68,   69,   70,   71,   72,   73,   74,   75,   76,
/*   650 */    77,   78,   79,   80,  193,   82,   83,   84,   85,   86,
/*   660 */    87,   88,   89,   90,   91,   92,   19,  194,  194,  150,
/*   670 */   135,   24,  137,   35,  231,  138,  150,  129,  130,  206,
/*   680 */   207,   30,   27,  213,  165,   34,  118,   95,    0,    1,
/*   690 */     2,  165,  218,  174,  175,   50,   49,   50,   22,   48,
/*   700 */   174,  175,   22,   23,   23,  244,  222,  223,  224,  166,
/*   710 */   167,  168,  120,  239,   23,   68,   69,   70,   71,   72,
/*   720 */    73,   74,   75,   76,   77,   78,   79,   80,  150,   82,
/*   730 */    83,   84,   85,   86,   87,   88,   89,   90,   91,   92,
/*   740 */    19,  150,  173,  165,  181,  182,   24,   67,   26,  104,
/*   750 */   181,  188,  174,  175,  150,   39,  165,  150,   52,  150,
/*   760 */   150,  150,  150,  144,  145,  174,  175,  249,  250,  165,
/*   770 */    49,   50,  165,   52,  165,  165,  165,  165,  174,  175,
/*   780 */    29,  174,  175,  174,  175,  174,  175,  160,   22,   68,
/*   790 */    69,   70,   71,   72,   73,   74,   75,   76,   77,   78,
/*   800 */    79,   80,  150,   82,   83,   84,   85,   86,   87,   88,
/*   810 */    89,   90,   91,   92,   19,  150,   94,  165,  150,  150,
/*   820 */   160,  194,  150,  213,  160,   52,  174,  175,   23,   23,
/*   830 */   165,   25,   22,  165,  165,  150,  150,  165,   52,  174,
/*   840 */   175,   22,  174,  175,   49,   50,  174,  175,  190,  191,
/*   850 */   165,  165,  240,   23,  194,   25,  187,  109,  194,  174,
/*   860 */   175,  190,  191,   68,   69,   70,   71,   72,   73,   74,
/*   870 */    75,   76,   77,   78,   79,   80,  150,   82,   83,   84,
/*   880 */    85,   86,   87,   88,   89,   90,   91,   92,   19,  150,
/*   890 */    22,  165,  150,   23,  150,   25,  150,  166,   91,   92,
/*   900 */   174,  175,   22,  217,  165,  150,  102,  165,  150,  165,
/*   910 */   150,  165,  150,  174,  175,   19,  174,  175,   49,   50,
/*   920 */   165,   86,   87,  165,   23,  165,   25,  165,   24,  174,
/*   930 */   175,  187,  174,  175,  174,  175,  205,   68,   69,   70,
/*   940 */    71,   72,   73,   74,   75,   76,   77,   78,   79,   80,
/*   950 */   150,   82,   83,   84,   85,   86,   87,   88,   89,   90,
/*   960 */    91,   92,   19,  150,  150,  165,  150,  150,  166,   23,
/*   970 */   150,   25,  160,   20,  174,  175,    1,    2,  165,  165,
/*   980 */   104,  165,  165,   43,  150,  165,  240,  150,   49,   50,
/*   990 */   174,  175,   49,   50,   23,   23,   25,   25,   53,  165,
/*  1000 */   187,  187,  165,   23,  187,   25,  194,  205,  174,  175,
/*  1010 */    71,   72,   69,   70,   71,   72,   73,   74,   75,   76,
/*  1020 */    77,   78,   79,   80,  150,   82,   83,   84,   85,   86,
/*  1030 */    87,   88,   89,   90,   91,   92,   19,   98,  150,  165,
/*  1040 */   150,  160,  150,   59,   25,   53,  104,   22,  174,  175,
/*  1050 */   213,  138,    5,  165,    1,  165,  150,  165,  150,  150,
/*  1060 */   240,  150,  174,  175,  174,  175,   49,   50,  118,  150,
/*  1070 */    35,  165,   27,  165,  165,  194,  165,  108,  127,   76,
/*  1080 */   174,  175,  174,  175,  165,  174,  175,   70,   71,   72,
/*  1090 */    73,   74,   75,   76,   77,   78,   79,   80,  166,   82,
/*  1100 */    83,   84,   85,   86,   87,   88,   89,   90,   91,   92,
/*  1110 */    19,   20,  193,   22,  150,  150,  150,   26,   27,   76,
/*  1120 */   150,   22,    1,  150,  119,  121,  217,   20,   37,  165,
/*  1130 */   165,  165,   16,   19,   20,  165,   22,  205,  165,  119,
/*  1140 */    26,   27,  108,  128,  150,  150,  150,   56,  150,   22,
/*  1150 */   150,   37,  150,  127,  160,   23,  150,   66,  193,  165,
/*  1160 */   165,  165,   16,  165,   23,  165,  150,  165,  174,  175,
/*  1170 */    56,  165,  150,   65,  174,  175,   15,   86,   87,   88,
/*  1180 */    66,  165,  140,  150,   93,   94,   95,  165,  194,   98,
/*  1190 */   174,  175,   22,    3,  164,  193,  174,  175,  165,  150,
/*  1200 */    86,   87,    4,  180,  150,  248,  251,   93,   94,   95,
/*  1210 */   216,  180,   98,  251,  165,  221,  150,  149,    6,  165,
/*  1220 */   129,  130,  131,  132,  133,  134,  193,  150,  174,  175,
/*  1230 */   116,  165,   19,   20,  150,   22,  149,  151,  150,   26,
/*  1240 */    27,  149,  165,  129,  130,  131,  132,  133,  134,  165,
/*  1250 */    37,  174,  175,  165,  149,   19,   20,   13,   22,  150,
/*  1260 */   150,  150,   26,   27,  146,  147,  151,  150,   25,   56,
/*  1270 */   152,  159,  154,   37,  165,  165,  165,  193,  160,   66,
/*  1280 */   116,  193,  165,  174,  175,  174,  175,  194,  199,  150,
/*  1290 */   200,  126,   56,  124,  123,  150,  201,  122,  150,   86,
/*  1300 */    87,  150,   66,  193,  165,  202,   93,   94,   95,  150,
/*  1310 */   165,   98,  194,  165,  125,   22,  165,  150,  150,   26,
/*  1320 */    27,  135,   86,   87,  165,  174,  175,  203,  226,   93,
/*  1330 */    94,   95,  165,  165,   98,  150,  218,  150,  193,  157,
/*  1340 */   118,  157,  129,  130,  131,  132,  133,  134,    5,  104,
/*  1350 */   165,  211,  165,   10,   11,   12,   13,   14,  150,   66,
/*  1360 */    17,  174,  175,  210,  246,  129,  130,  131,  132,  133,
/*  1370 */   134,  150,  210,  165,   31,  121,   33,  150,  150,   86,
/*  1380 */    87,  176,  174,  175,  150,   42,  165,   94,  211,  210,
/*  1390 */   150,   98,  165,  165,  211,  174,  175,  150,   55,  165,
/*  1400 */    57,  150,  174,  175,   61,  165,  150,   64,  174,  175,
/*  1410 */   150,  150,  165,  150,  174,  175,  165,  104,  150,  184,
/*  1420 */   150,  165,  129,  130,  131,  165,  165,  150,  165,  150,
/*  1430 */   150,  176,  150,  165,   47,  165,  150,  150,  176,  103,
/*  1440 */   150,   22,  165,  178,  165,  165,  179,  165,  105,  106,
/*  1450 */   107,  165,  165,  229,  111,  165,   92,  176,  229,  116,
/*  1460 */   184,  176,  179,  156,  176,  176,   18,  157,  156,  237,
/*  1470 */    45,  157,  156,  135,  157,  157,  238,  156,   68,  157,
/*  1480 */   189,  189,  139,  219,   22,  157,   18,  192,  192,  192,
/*  1490 */   192,  189,  219,  199,  157,  242,   40,  157,  199,  242,
/*  1500 */   153,  157,   38,  245,  196,  166,  232,  198,  177,  177,
/*  1510 */   232,  227,  209,  178,  166,  182,  166,  148,  177,  177,
/*  1520 */   209,  196,  177,  199,  209,  199,  166,  208,   92,  195,
/*  1530 */   174,  174,  183,  252,  183,  183,  252,  191,  252,  235,
/*  1540 */   186,  241,  241,  252,  186,  252,  252,  252,  252,  252,
/*  1550 */   252,  252,  252,  252,  252,  252,  236,
};

    const int YY_SHIFT_USE_DFLT = -74;//#define YY_SHIFT_USE_DFLT (-74)
    const int YY_SHIFT_COUNT = 418;   //#define YY_SHIFT_COUNT (418)
    const int YY_SHIFT_MIN = -73;     //#define YY_SHIFT_MIN   (-73)
    const int YY_SHIFT_MAX = 1468;    //#define YY_SHIFT_MAX   (1468)

    static short[] yy_shift_ofst = new short[]{
/*     0 */   975, 1114, 1343, 1114, 1213, 1213,   90,   90,    0,  -19,
/*    10 */  1213, 1213, 1213, 1213, 1213,  345,  445,  721, 1091, 1213,
/*    20 */  1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213,
/*    30 */  1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213,
/*    40 */  1213, 1213, 1213, 1213, 1213, 1213, 1213, 1236, 1213, 1213,
/*    50 */  1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213, 1213,
/*    60 */  1213,  199,  445,  445,  835,  835,  365, 1164,   55,  647,
/*    70 */   573,  499,  425,  351,  277,  203,  129,  795,  795,  795,
/*    80 */   795,  795,  795,  795,  795,  795,  795,  795,  795,  795,
/*    90 */   795,  795,  795,  795,  795,  869,  795,  943, 1017, 1017,
/*   100 */   -69,  -45,  -45,  -45,  -45,  -45,   -1,   58,  138,  100,
/*   110 */   445,  445,  445,  445,  445,  445,  445,  445,  445,  445,
/*   120 */   445,  445,  445,  445,  445,  445,  537,  438,  445,  445,
/*   130 */   445,  445,  445,  365,  807, 1436,  -74,  -74,  -74, 1293,
/*   140 */    73,  434,  434,  311,  314,  290,  283,  286,  540,  467,
/*   150 */   445,  445,  445,  445,  445,  445,  445,  445,  445,  445,
/*   160 */   445,  445,  445,  445,  445,  445,  445,  445,  445,  445,
/*   170 */   445,  445,  445,  445,  445,  445,  445,  445,  445,  445,
/*   180 */   445,  445,   65,  722,  722,  722,  688,  266, 1164, 1164,
/*   190 */  1164,  -74,  -74,  -74,  136,  168,  168,  234,  360,  360,
/*   200 */   360,  430,  372,  435,  352,  278,  126,  -36,  -36,  -36,
/*   210 */   -36,  421,  651,  -36,  -36,  592,  292,  212,  623,  158,
/*   220 */   204,  204,  505,  158,  505,  144,  365,  154,  365,  154,
/*   230 */   645,  154,  204,  154,  154,  535,  548,  548,  365,  387,
/*   240 */   508,  233, 1464, 1222, 1222, 1456, 1456, 1222, 1462, 1410,
/*   250 */  1165, 1468, 1468, 1468, 1468, 1222, 1165, 1462, 1410, 1410,
/*   260 */  1222, 1448, 1338, 1425, 1222, 1222, 1448, 1222, 1448, 1222,
/*   270 */  1448, 1419, 1313, 1313, 1313, 1387, 1364, 1364, 1419, 1313,
/*   280 */  1336, 1313, 1387, 1313, 1313, 1254, 1245, 1254, 1245, 1254,
/*   290 */  1245, 1222, 1222, 1186, 1189, 1175, 1169, 1171, 1165, 1164,
/*   300 */  1243, 1244, 1244, 1212, 1212, 1212, 1212,  -74,  -74,  -74,
/*   310 */   -74,  -74,  -74,  939,  104,  680,  571,  327,    1,  980,
/*   320 */    26,  972,  971,  946,  901,  870,  830,  806,   54,   21,
/*   330 */   -73,  510,  242, 1198, 1190, 1170, 1042, 1161, 1108, 1146,
/*   340 */  1141, 1132, 1015, 1127, 1026, 1034, 1020, 1107, 1004, 1116,
/*   350 */  1121, 1005, 1099,  951, 1043, 1003,  969, 1045, 1035,  950,
/*   360 */  1053, 1047, 1025,  942,  913,  992, 1019,  945,  984,  940,
/*   370 */   876,  904,  953,  896,  748,  804,  880,  786,  868,  819,
/*   380 */   805,  810,  773,  751,  766,  706,  716,  691,  681,  568,
/*   390 */   655,  638,  676,  516,  541,  594,  599,  567,  541,  534,
/*   400 */   507,  527,  498,  523,  466,  382,  409,  384,  357,    6,
/*   410 */   240,  224,  143,   62,   18,   71,   39,    9,    5,
};

    const int YY_REDUCE_USE_DFLT = -142;  //#define YY_REDUCE_USE_DFLT (-142)
    const int YY_REDUCE_COUNT = 312;      //#define YY_REDUCE_COUNT (312)
    const int YY_REDUCE_MIN = -141;       //#define YY_REDUCE_MIN   (-141)
    const int YY_REDUCE_MAX = 1369;       //#define YY_REDUCE_MAX   (1369)

    static short[] yy_reduce_ofst = new short[]{
/*     0 */  -141,  994, 1118,  223,  157,  -53,   93,   89,   83,  375,
/*    10 */   386,  381,  379,  308,  295,  325,  -47,   27, 1240, 1234,
/*    20 */  1228, 1221, 1208, 1187, 1151, 1111, 1109, 1077, 1054, 1022,
/*    30 */  1016, 1000,  911,  908,  906,  890,  888,  874,  834,  816,
/*    40 */   800,  760,  758,  755,  742,  739,  726,  685,  672,  668,
/*    50 */   665,  652,  611,  609,  607,  604,  591,  578,  526,  519,
/*    60 */   453,  474,  454,  461,  443,  245,  442,  473,  484,  484,
/*    70 */   484,  484,  484,  484,  484,  484,  484,  484,  484,  484,
/*    80 */   484,  484,  484,  484,  484,  484,  484,  484,  484,  484,
/*    90 */   484,  484,  484,  484,  484,  484,  484,  484,  484,  484,
/*   100 */   484,  484,  484,  484,  484,  484,  484,  130,  484,  484,
/*   110 */  1145,  909, 1110, 1088, 1084, 1033, 1002,  965,  820,  837,
/*   120 */   746,  686,  612,  817,  610,  919,  221,  563,  814,  813,
/*   130 */   744,  669,  470,  543,  484,  484,  484,  484,  484,  291,
/*   140 */   569,  671,  658,  970, 1290, 1287, 1286, 1282,  518,  518,
/*   150 */  1280, 1279, 1277, 1270, 1268, 1263, 1261, 1260, 1256, 1251,
/*   160 */  1247, 1227, 1185, 1168, 1167, 1159, 1148, 1139, 1117, 1066,
/*   170 */  1049, 1006,  998,  996,  995,  973,  970,  966,  964,  892,
/*   180 */   762,  -52,  881,  932,  802,  731,  619,  812,  664,  660,
/*   190 */   627,  392,  331,  124, 1358, 1357, 1356, 1354, 1352, 1351,
/*   200 */  1349, 1319, 1334, 1346, 1334, 1334, 1334, 1334, 1334, 1334,
/*   210 */  1334, 1320, 1304, 1334, 1334, 1319, 1360, 1325, 1369, 1326,
/*   220 */  1315, 1311, 1301, 1324, 1300, 1335, 1350, 1345, 1348, 1342,
/*   230 */  1333, 1341, 1303, 1332, 1331, 1284, 1278, 1274, 1339, 1309,
/*   240 */  1308, 1347, 1258, 1344, 1340, 1257, 1253, 1337, 1273, 1302,
/*   250 */  1299, 1298, 1297, 1296, 1295, 1328, 1294, 1264, 1292, 1291,
/*   260 */  1322, 1321, 1238, 1232, 1318, 1317, 1316, 1314, 1312, 1310,
/*   270 */  1307, 1283, 1289, 1288, 1285, 1276, 1229, 1224, 1267, 1281,
/*   280 */  1265, 1262, 1235, 1255, 1205, 1183, 1179, 1177, 1162, 1140,
/*   290 */  1153, 1184, 1182, 1102, 1124, 1103, 1095, 1090, 1089, 1093,
/*   300 */  1112, 1115, 1086, 1105, 1092, 1087, 1068,  962,  955,  957,
/*   310 */  1031, 1023, 1030,
};
    static YYACTIONTYPE[] yy_default = new YYACTIONTYPE[] {
/*     0 */   635,  870,  959,  959,  959,  870,  899,  899,  959,  759,
/*    10 */   959,  959,  959,  959,  868,  959,  959,  933,  959,  959,
/*    20 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*    30 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*    40 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*    50 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*    60 */   959,  959,  959,  959,  899,  899,  674,  763,  794,  959,
/*    70 */   959,  959,  959,  959,  959,  959,  959,  932,  934,  809,
/*    80 */   808,  802,  801,  912,  774,  799,  792,  785,  796,  871,
/*    90 */   864,  865,  863,  867,  872,  959,  795,  831,  848,  830,
/*   100 */   842,  847,  854,  846,  843,  833,  832,  666,  834,  835,
/*   110 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   120 */   959,  959,  959,  959,  959,  959,  661,  728,  959,  959,
/*   130 */   959,  959,  959,  959,  836,  837,  851,  850,  849,  959,
/*   140 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   150 */   959,  939,  937,  959,  883,  959,  959,  959,  959,  959,
/*   160 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   170 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   180 */   959,  641,  959,  759,  759,  759,  635,  959,  959,  959,
/*   190 */   959,  951,  763,  753,  719,  959,  959,  959,  959,  959,
/*   200 */   959,  959,  959,  959,  959,  959,  959,  804,  742,  922,
/*   210 */   924,  959,  905,  740,  663,  761,  676,  751,  643,  798,
/*   220 */   776,  776,  917,  798,  917,  700,  959,  788,  959,  788,
/*   230 */   697,  788,  776,  788,  788,  866,  959,  959,  959,  760,
/*   240 */   751,  959,  944,  767,  767,  936,  936,  767,  810,  732,
/*   250 */   798,  739,  739,  739,  739,  767,  798,  810,  732,  732,
/*   260 */   767,  658,  911,  909,  767,  767,  658,  767,  658,  767,
/*   270 */   658,  876,  730,  730,  730,  715,  880,  880,  876,  730,
/*   280 */   700,  730,  715,  730,  730,  780,  775,  780,  775,  780,
/*   290 */   775,  767,  767,  959,  793,  781,  791,  789,  798,  959,
/*   300 */   718,  651,  651,  640,  640,  640,  640,  956,  956,  951,
/*   310 */   702,  702,  684,  959,  959,  959,  959,  959,  959,  959,
/*   320 */   885,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   330 */   959,  959,  959,  959,  636,  946,  959,  959,  943,  959,
/*   340 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   350 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  915,
/*   360 */   959,  959,  959,  959,  959,  959,  908,  907,  959,  959,
/*   370 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   380 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  959,
/*   390 */   959,  959,  959,  959,  790,  959,  782,  959,  869,  959,
/*   400 */   959,  959,  959,  959,  959,  959,  959,  959,  959,  745,
/*   410 */   819,  959,  818,  822,  817,  668,  959,  649,  959,  632,
/*   420 */   637,  955,  958,  957,  954,  953,  952,  947,  945,  942,
/*   430 */   941,  940,  938,  935,  931,  889,  887,  894,  893,  892,
/*   440 */   891,  890,  888,  886,  884,  805,  803,  800,  797,  930,
/*   450 */   882,  741,  738,  737,  657,  948,  914,  923,  921,  811,
/*   460 */   920,  919,  918,  916,  913,  900,  807,  806,  733,  874,
/*   470 */   873,  660,  904,  903,  902,  906,  910,  901,  769,  659,
/*   480 */   656,  665,  722,  721,  729,  727,  726,  725,  724,  723,
/*   490 */   720,  667,  675,  686,  714,  699,  698,  879,  881,  878,
/*   500 */   877,  707,  706,  712,  711,  710,  709,  708,  705,  704,
/*   510 */   703,  696,  695,  701,  694,  717,  716,  713,  693,  736,
/*   520 */   735,  734,  731,  692,  691,  690,  822,  689,  688,  828,
/*   530 */   827,  815,  858,  756,  755,  754,  766,  765,  778,  777,
/*   540 */   813,  812,  779,  764,  758,  757,  773,  772,  771,  770,
/*   550 */   762,  752,  784,  787,  786,  783,  860,  768,  857,  929,
/*   560 */   928,  927,  926,  925,  862,  861,  829,  826,  679,  680,
/*   570 */   898,  896,  897,  895,  682,  681,  678,  677,  859,  747,
/*   580 */   746,  855,  852,  844,  840,  856,  853,  845,  841,  839,
/*   590 */   838,  824,  823,  821,  820,  816,  825,  670,  748,  744,
/*   600 */   743,  814,  750,  749,  687,  685,  683,  664,  662,  655,
/*   610 */   653,  652,  654,  650,  648,  647,  646,  645,  644,  673,
/*   620 */   672,  671,  669,  668,  642,  639,  638,  634,  633,  631,
};

    /* The next table maps tokens into fallback tokens.  If a construct
    ** like the following:
    **
    **      %fallback ID X Y Z.
    **
    ** appears in the grammar, then ID becomes a fallback token for X, Y,
    ** and Z.  Whenever one of the tokens X, Y, or Z is input to the parser
    ** but it does not parse, the type of the token is changed to ID and
    ** the parse is retried before an error is thrown.
    */
#if YYFALLBACK || TRUE
    static YYCODETYPE[] yyFallback = new YYCODETYPE[]{
0,  /*          $ => nothing */
0,  /*       SEMI => nothing */
26,  /*    EXPLAIN => ID */
26,  /*      QUERY => ID */
26,  /*       PLAN => ID */
26,  /*      BEGIN => ID */
0,  /* TRANSACTION => nothing */
26,  /*   DEFERRED => ID */
26,  /*  IMMEDIATE => ID */
26,  /*  EXCLUSIVE => ID */
0,  /*     COMMIT => nothing */
26,  /*        END => ID */
26,  /*   ROLLBACK => ID */
26,  /*  SAVEPOINT => ID */
26,  /*    RELEASE => ID */
0,  /*         TO => nothing */
0,  /*      TABLE => nothing */
0,  /*     CREATE => nothing */
26,  /*         IF => ID */
0,  /*        NOT => nothing */
0,  /*     EXISTS => nothing */
26,  /*       TEMP => ID */
0,  /*         LP => nothing */
0,  /*         RP => nothing */
0,  /*         AS => nothing */
0,  /*      COMMA => nothing */
0,  /*         ID => nothing */
0,  /*    INDEXED => nothing */
26,  /*      ABORT => ID */
26,  /*     ACTION => ID */
26,  /*      AFTER => ID */
26,  /*    ANALYZE => ID */
26,  /*        ASC => ID */
26,  /*     ATTACH => ID */
26,  /*     BEFORE => ID */
26,  /*         BY => ID */
26,  /*    CASCADE => ID */
26,  /*       CAST => ID */
26,  /*   COLUMNKW => ID */
26,  /*   CONFLICT => ID */
26,  /*   DATABASE => ID */
26,  /*       DESC => ID */
26,  /*     DETACH => ID */
26,  /*       EACH => ID */
26,  /*       FAIL => ID */
26,  /*        FOR => ID */
26,  /*     IGNORE => ID */
26,  /*  INITIALLY => ID */
26,  /*    INSTEAD => ID */
26,  /*    LIKE_KW => ID */
26,  /*      MATCH => ID */
26,  /*         NO => ID */
26,  /*        KEY => ID */
26,  /*         OF => ID */
26,  /*     OFFSET => ID */
26,  /*     PRAGMA => ID */
26,  /*      RAISE => ID */
26,  /*    REPLACE => ID */
26,  /*   RESTRICT => ID */
26,  /*        ROW => ID */
26,  /*    TRIGGER => ID */
26,  /*     VACUUM => ID */
26,  /*       VIEW => ID */
26,  /*    VIRTUAL => ID */
26,  /*    REINDEX => ID */
26,  /*     RENAME => ID */
26,  /*   CTIME_KW => ID */
};
#endif // * YYFALLBACK */

    /* The following structure represents a single element of the
** parser's stack.  Information stored includes:
**
**   +  The state number for the parser at this level of the stack.
**
**   +  The value of the token stored at this level of the stack.
**      (In other words, the "major" token.)
**
**   +  The semantic value stored at this level of the stack.  This is
**      the information used by the action routines in the grammar.
**      It is sometimes called the "minor" token.
*/
    public class yyStackEntry
    {
      public YYACTIONTYPE stateno;       /* The state-number */
      public YYCODETYPE major;         /* The major token value.  This is the code
** number for the token at this stack level */
      public YYMINORTYPE minor; /* The user-supplied minor token value.  This
** is the value of the token  */
    };
    //typedef struct yyStackEntry yyStackEntry;

    /* The state of the parser is completely contained in an instance of
    ** the following structure */
    public class yyParser
    {
      public int yyidx;                    /* Index of top element in stack */
#if YYTRACKMAXSTACKDEPTH
int yyidxMax;                 /* Maximum value of yyidx */
#endif
      public int yyerrcnt;                 /* Shifts left before out of the error */
      public Parse pParse;  // sqlite3ParserARG_SDECL                /* A place to hold %extra_argument */
#if YYSTACKDEPTH//<=0
public int yystksz;                  /* Current side of the stack */
public yyStackEntry *yystack;        /* The parser's stack */
#else
      public yyStackEntry[] yystack = new yyStackEntry[YYSTACKDEPTH];  /* The parser's stack */
#endif
    };
    //typedef struct yyParser yyParser;

#if !NDEBUG
    //#include <stdio.h>
    static TextWriter yyTraceFILE = null;
    static string yyTracePrompt = "";
#endif // * NDEBUG */

#if !NDEBUG
    /*
** Turn parser tracing on by giving a stream to which to write the trace
** and a prompt to preface each trace message.  Tracing is turned off
** by making either argument NULL
**
** Inputs:
** <ul>
** <li> A FILE* to which trace output should be written.
**      If NULL, then tracing is turned off.
** <li> A prefix string written at the beginning of every
**      line of trace output.  If NULL, then tracing is
**      turned off.
** </ul>
**
** Outputs:
** None.
*/
    static void sqlite3ParserTrace( TextWriter TraceFILE, string zTracePrompt )
    {
      yyTraceFILE = TraceFILE;
      yyTracePrompt = zTracePrompt;
      if ( yyTraceFILE == null )
        yyTracePrompt = "";
      else if ( yyTracePrompt == "" )
        yyTraceFILE = null;
    }
#endif // * NDEBUG */

#if !NDEBUG
    /* For tracing shifts, the names of all terminals and nonterminals
** are required.  The following table supplies these names */
    static string[] yyTokenName = {
"$",             "SEMI",          "EXPLAIN",       "QUERY",       
"PLAN",          "BEGIN",         "TRANSACTION",   "DEFERRED",    
"IMMEDIATE",     "EXCLUSIVE",     "COMMIT",        "END",         
"ROLLBACK",      "SAVEPOINT",     "RELEASE",       "TO",          
"TABLE",         "CREATE",        "IF",            "NOT",         
"EXISTS",        "TEMP",          "LP",            "RP",          
"AS",            "COMMA",         "ID",            "INDEXED",     
"ABORT",         "ACTION",        "AFTER",         "ANALYZE",     
"ASC",           "ATTACH",        "BEFORE",        "BY",          
"CASCADE",       "CAST",          "COLUMNKW",      "CONFLICT",    
"DATABASE",      "DESC",          "DETACH",        "EACH",        
"FAIL",          "FOR",           "IGNORE",        "INITIALLY",   
"INSTEAD",       "LIKE_KW",       "MATCH",         "NO",          
"KEY",           "OF",            "OFFSET",        "PRAGMA",      
"RAISE",         "REPLACE",       "RESTRICT",      "ROW",         
"TRIGGER",       "VACUUM",        "VIEW",          "VIRTUAL",     
"REINDEX",       "RENAME",        "CTIME_KW",      "ANY",         
"OR",            "AND",           "IS",            "BETWEEN",     
"IN",            "ISNULL",        "NOTNULL",       "NE",          
"EQ",            "GT",            "LE",            "LT",          
"GE",            "ESCAPE",        "BITAND",        "BITOR",       
"LSHIFT",        "RSHIFT",        "PLUS",          "MINUS",       
"STAR",          "SLASH",         "REM",           "CONCAT",      
"COLLATE",       "BITNOT",        "STRING",        "JOIN_KW",     
"CONSTRAINT",    "DEFAULT",       "NULL",          "PRIMARY",     
"UNIQUE",        "CHECK",         "REFERENCES",    "AUTOINCR",    
"ON",            "INSERT",        "DELETE",        "UPDATE",      
"SET",           "DEFERRABLE",    "FOREIGN",       "DROP",        
"UNION",         "ALL",           "EXCEPT",        "INTERSECT",   
"SELECT",        "DISTINCT",      "DOT",           "FROM",        
"JOIN",          "USING",         "ORDER",         "GROUP",       
"HAVING",        "LIMIT",         "WHERE",         "INTO",        
"VALUES",        "INTEGER",       "FLOAT",         "BLOB",        
"REGISTER",      "VARIABLE",      "CASE",          "WHEN",        
"THEN",          "ELSE",          "INDEX",         "ALTER",       
"ADD",           "error",         "input",         "cmdlist",     
"ecmd",          "explain",       "cmdx",          "cmd",         
"transtype",     "trans_opt",     "nm",            "savepoint_opt",
"create_table",  "create_table_args",  "createkw",      "temp",        
"ifnotexists",   "dbnm",          "columnlist",    "conslist_opt",
"select",        "column",        "columnid",      "type",        
"carglist",      "id",            "ids",           "typetoken",   
"typename",      "signed",        "plus_num",      "minus_num",   
"carg",          "ccons",         "term",          "expr",        
"onconf",        "sortorder",     "autoinc",       "idxlist_opt", 
"refargs",       "defer_subclause",  "refarg",        "refact",      
"init_deferred_pred_opt",  "conslist",      "tcons",         "idxlist",     
"defer_subclause_opt",  "orconf",        "resolvetype",   "raisetype",   
"ifexists",      "fullname",      "oneselect",     "multiselect_op",
"distinct",      "selcollist",    "from",          "where_opt",   
"groupby_opt",   "having_opt",    "orderby_opt",   "limit_opt",   
"sclp",          "as",            "seltablist",    "stl_prefix",  
"joinop",        "indexed_opt",   "on_opt",        "using_opt",   
"joinop2",       "inscollist",    "sortlist",      "sortitem",    
"nexprlist",     "setlist",       "insert_cmd",    "inscollist_opt",
"itemlist",      "exprlist",      "likeop",        "between_op",  
"in_op",         "case_operand",  "case_exprlist",  "case_else",   
"uniqueflag",    "collate",       "nmnum",         "plus_opt",    
"number",        "trigger_decl",  "trigger_cmd_list",  "trigger_time",
"trigger_event",  "foreach_clause",  "when_clause",   "trigger_cmd", 
"trnm",          "tridxby",       "database_kw_opt",  "key_opt",     
"add_column_fullname",  "kwcolumn_opt",  "create_vtab",   "vtabarglist", 
"vtabarg",       "vtabargtoken",  "lp",            "anylist",     
};
#endif // * NDEBUG */

#if !NDEBUG
    /* For tracing reduce actions, the names of all rules are required.
*/
    static string[] yyRuleName = {
/*   0 */ "input ::= cmdlist",
/*   1 */ "cmdlist ::= cmdlist ecmd",
/*   2 */ "cmdlist ::= ecmd",
/*   3 */ "ecmd ::= SEMI",
/*   4 */ "ecmd ::= explain cmdx SEMI",
/*   5 */ "explain ::=",
/*   6 */ "explain ::= EXPLAIN",
/*   7 */ "explain ::= EXPLAIN QUERY PLAN",
/*   8 */ "cmdx ::= cmd",
/*   9 */ "cmd ::= BEGIN transtype trans_opt",
/*  10 */ "trans_opt ::=",
/*  11 */ "trans_opt ::= TRANSACTION",
/*  12 */ "trans_opt ::= TRANSACTION nm",
/*  13 */ "transtype ::=",
/*  14 */ "transtype ::= DEFERRED",
/*  15 */ "transtype ::= IMMEDIATE",
/*  16 */ "transtype ::= EXCLUSIVE",
/*  17 */ "cmd ::= COMMIT trans_opt",
/*  18 */ "cmd ::= END trans_opt",
/*  19 */ "cmd ::= ROLLBACK trans_opt",
/*  20 */ "savepoint_opt ::= SAVEPOINT",
/*  21 */ "savepoint_opt ::=",
/*  22 */ "cmd ::= SAVEPOINT nm",
/*  23 */ "cmd ::= RELEASE savepoint_opt nm",
/*  24 */ "cmd ::= ROLLBACK trans_opt TO savepoint_opt nm",
/*  25 */ "cmd ::= create_table create_table_args",
/*  26 */ "create_table ::= createkw temp TABLE ifnotexists nm dbnm",
/*  27 */ "createkw ::= CREATE",
/*  28 */ "ifnotexists ::=",
/*  29 */ "ifnotexists ::= IF NOT EXISTS",
/*  30 */ "temp ::= TEMP",
/*  31 */ "temp ::=",
/*  32 */ "create_table_args ::= LP columnlist conslist_opt RP",
/*  33 */ "create_table_args ::= AS select",
/*  34 */ "columnlist ::= columnlist COMMA column",
/*  35 */ "columnlist ::= column",
/*  36 */ "column ::= columnid type carglist",
/*  37 */ "columnid ::= nm",
/*  38 */ "id ::= ID",
/*  39 */ "id ::= INDEXED",
/*  40 */ "ids ::= ID|STRING",
/*  41 */ "nm ::= id",
/*  42 */ "nm ::= STRING",
/*  43 */ "nm ::= JOIN_KW",
/*  44 */ "type ::=",
/*  45 */ "type ::= typetoken",
/*  46 */ "typetoken ::= typename",
/*  47 */ "typetoken ::= typename LP signed RP",
/*  48 */ "typetoken ::= typename LP signed COMMA signed RP",
/*  49 */ "typename ::= ids",
/*  50 */ "typename ::= typename ids",
/*  51 */ "signed ::= plus_num",
/*  52 */ "signed ::= minus_num",
/*  53 */ "carglist ::= carglist carg",
/*  54 */ "carglist ::=",
/*  55 */ "carg ::= CONSTRAINT nm ccons",
/*  56 */ "carg ::= ccons",
/*  57 */ "ccons ::= DEFAULT term",
/*  58 */ "ccons ::= DEFAULT LP expr RP",
/*  59 */ "ccons ::= DEFAULT PLUS term",
/*  60 */ "ccons ::= DEFAULT MINUS term",
/*  61 */ "ccons ::= DEFAULT id",
/*  62 */ "ccons ::= NULL onconf",
/*  63 */ "ccons ::= NOT NULL onconf",
/*  64 */ "ccons ::= PRIMARY KEY sortorder onconf autoinc",
/*  65 */ "ccons ::= UNIQUE onconf",
/*  66 */ "ccons ::= CHECK LP expr RP",
/*  67 */ "ccons ::= REFERENCES nm idxlist_opt refargs",
/*  68 */ "ccons ::= defer_subclause",
/*  69 */ "ccons ::= COLLATE ids",
/*  70 */ "autoinc ::=",
/*  71 */ "autoinc ::= AUTOINCR",
/*  72 */ "refargs ::=",
/*  73 */ "refargs ::= refargs refarg",
/*  74 */ "refarg ::= MATCH nm",
/*  75 */ "refarg ::= ON INSERT refact",
/*  76 */ "refarg ::= ON DELETE refact",
/*  77 */ "refarg ::= ON UPDATE refact",
/*  78 */ "refact ::= SET NULL",
/*  79 */ "refact ::= SET DEFAULT",
/*  80 */ "refact ::= CASCADE",
/*  81 */ "refact ::= RESTRICT",
/*  82 */ "refact ::= NO ACTION",
/*  83 */ "defer_subclause ::= NOT DEFERRABLE init_deferred_pred_opt",
/*  84 */ "defer_subclause ::= DEFERRABLE init_deferred_pred_opt",
/*  85 */ "init_deferred_pred_opt ::=",
/*  86 */ "init_deferred_pred_opt ::= INITIALLY DEFERRED",
/*  87 */ "init_deferred_pred_opt ::= INITIALLY IMMEDIATE",
/*  88 */ "conslist_opt ::=",
/*  89 */ "conslist_opt ::= COMMA conslist",
/*  90 */ "conslist ::= conslist COMMA tcons",
/*  91 */ "conslist ::= conslist tcons",
/*  92 */ "conslist ::= tcons",
/*  93 */ "tcons ::= CONSTRAINT nm",
/*  94 */ "tcons ::= PRIMARY KEY LP idxlist autoinc RP onconf",
/*  95 */ "tcons ::= UNIQUE LP idxlist RP onconf",
/*  96 */ "tcons ::= CHECK LP expr RP onconf",
/*  97 */ "tcons ::= FOREIGN KEY LP idxlist RP REFERENCES nm idxlist_opt refargs defer_subclause_opt",
/*  98 */ "defer_subclause_opt ::=",
/*  99 */ "defer_subclause_opt ::= defer_subclause",
/* 100 */ "onconf ::=",
/* 101 */ "onconf ::= ON CONFLICT resolvetype",
/* 102 */ "orconf ::=",
/* 103 */ "orconf ::= OR resolvetype",
/* 104 */ "resolvetype ::= raisetype",
/* 105 */ "resolvetype ::= IGNORE",
/* 106 */ "resolvetype ::= REPLACE",
/* 107 */ "cmd ::= DROP TABLE ifexists fullname",
/* 108 */ "ifexists ::= IF EXISTS",
/* 109 */ "ifexists ::=",
/* 110 */ "cmd ::= createkw temp VIEW ifnotexists nm dbnm AS select",
/* 111 */ "cmd ::= DROP VIEW ifexists fullname",
/* 112 */ "cmd ::= select",
/* 113 */ "select ::= oneselect",
/* 114 */ "select ::= select multiselect_op oneselect",
/* 115 */ "multiselect_op ::= UNION",
/* 116 */ "multiselect_op ::= UNION ALL",
/* 117 */ "multiselect_op ::= EXCEPT|INTERSECT",
/* 118 */ "oneselect ::= SELECT distinct selcollist from where_opt groupby_opt having_opt orderby_opt limit_opt",
/* 119 */ "distinct ::= DISTINCT",
/* 120 */ "distinct ::= ALL",
/* 121 */ "distinct ::=",
/* 122 */ "sclp ::= selcollist COMMA",
/* 123 */ "sclp ::=",
/* 124 */ "selcollist ::= sclp expr as",
/* 125 */ "selcollist ::= sclp STAR",
/* 126 */ "selcollist ::= sclp nm DOT STAR",
/* 127 */ "as ::= AS nm",
/* 128 */ "as ::= ids",
/* 129 */ "as ::=",
/* 130 */ "from ::=",
/* 131 */ "from ::= FROM seltablist",
/* 132 */ "stl_prefix ::= seltablist joinop",
/* 133 */ "stl_prefix ::=",
/* 134 */ "seltablist ::= stl_prefix nm dbnm as indexed_opt on_opt using_opt",
/* 135 */ "seltablist ::= stl_prefix LP select RP as on_opt using_opt",
/* 136 */ "seltablist ::= stl_prefix LP seltablist RP as on_opt using_opt",
/* 137 */ "dbnm ::=",
/* 138 */ "dbnm ::= DOT nm",
/* 139 */ "fullname ::= nm dbnm",
/* 140 */ "joinop ::= COMMA|JOIN",
/* 141 */ "joinop ::= JOIN_KW JOIN",
/* 142 */ "joinop ::= JOIN_KW nm JOIN",
/* 143 */ "joinop ::= JOIN_KW nm nm JOIN",
/* 144 */ "on_opt ::= ON expr",
/* 145 */ "on_opt ::=",
/* 146 */ "indexed_opt ::=",
/* 147 */ "indexed_opt ::= INDEXED BY nm",
/* 148 */ "indexed_opt ::= NOT INDEXED",
/* 149 */ "using_opt ::= USING LP inscollist RP",
/* 150 */ "using_opt ::=",
/* 151 */ "orderby_opt ::=",
/* 152 */ "orderby_opt ::= ORDER BY sortlist",
/* 153 */ "sortlist ::= sortlist COMMA sortitem sortorder",
/* 154 */ "sortlist ::= sortitem sortorder",
/* 155 */ "sortitem ::= expr",
/* 156 */ "sortorder ::= ASC",
/* 157 */ "sortorder ::= DESC",
/* 158 */ "sortorder ::=",
/* 159 */ "groupby_opt ::=",
/* 160 */ "groupby_opt ::= GROUP BY nexprlist",
/* 161 */ "having_opt ::=",
/* 162 */ "having_opt ::= HAVING expr",
/* 163 */ "limit_opt ::=",
/* 164 */ "limit_opt ::= LIMIT expr",
/* 165 */ "limit_opt ::= LIMIT expr OFFSET expr",
/* 166 */ "limit_opt ::= LIMIT expr COMMA expr",
/* 167 */ "cmd ::= DELETE FROM fullname indexed_opt where_opt",
/* 168 */ "where_opt ::=",
/* 169 */ "where_opt ::= WHERE expr",
/* 170 */ "cmd ::= UPDATE orconf fullname indexed_opt SET setlist where_opt",
/* 171 */ "setlist ::= setlist COMMA nm EQ expr",
/* 172 */ "setlist ::= nm EQ expr",
/* 173 */ "cmd ::= insert_cmd INTO fullname inscollist_opt VALUES LP itemlist RP",
/* 174 */ "cmd ::= insert_cmd INTO fullname inscollist_opt select",
/* 175 */ "cmd ::= insert_cmd INTO fullname inscollist_opt DEFAULT VALUES",
/* 176 */ "insert_cmd ::= INSERT orconf",
/* 177 */ "insert_cmd ::= REPLACE",
/* 178 */ "itemlist ::= itemlist COMMA expr",
/* 179 */ "itemlist ::= expr",
/* 180 */ "inscollist_opt ::=",
/* 181 */ "inscollist_opt ::= LP inscollist RP",
/* 182 */ "inscollist ::= inscollist COMMA nm",
/* 183 */ "inscollist ::= nm",
/* 184 */ "expr ::= term",
/* 185 */ "expr ::= LP expr RP",
/* 186 */ "term ::= NULL",
/* 187 */ "expr ::= id",
/* 188 */ "expr ::= JOIN_KW",
/* 189 */ "expr ::= nm DOT nm",
/* 190 */ "expr ::= nm DOT nm DOT nm",
/* 191 */ "term ::= INTEGER|FLOAT|BLOB",
/* 192 */ "term ::= STRING",
/* 193 */ "expr ::= REGISTER",
/* 194 */ "expr ::= VARIABLE",
/* 195 */ "expr ::= expr COLLATE ids",
/* 196 */ "expr ::= CAST LP expr AS typetoken RP",
/* 197 */ "expr ::= ID LP distinct exprlist RP",
/* 198 */ "expr ::= ID LP STAR RP",
/* 199 */ "term ::= CTIME_KW",
/* 200 */ "expr ::= expr AND expr",
/* 201 */ "expr ::= expr OR expr",
/* 202 */ "expr ::= expr LT|GT|GE|LE expr",
/* 203 */ "expr ::= expr EQ|NE expr",
/* 204 */ "expr ::= expr BITAND|BITOR|LSHIFT|RSHIFT expr",
/* 205 */ "expr ::= expr PLUS|MINUS expr",
/* 206 */ "expr ::= expr STAR|SLASH|REM expr",
/* 207 */ "expr ::= expr CONCAT expr",
/* 208 */ "likeop ::= LIKE_KW",
/* 209 */ "likeop ::= NOT LIKE_KW",
/* 210 */ "likeop ::= MATCH",
/* 211 */ "likeop ::= NOT MATCH",
/* 212 */ "expr ::= expr likeop expr",
/* 213 */ "expr ::= expr likeop expr ESCAPE expr",
/* 214 */ "expr ::= expr ISNULL|NOTNULL",
/* 215 */ "expr ::= expr NOT NULL",
/* 216 */ "expr ::= expr IS expr",
/* 217 */ "expr ::= expr IS NOT expr",
/* 218 */ "expr ::= NOT expr",
/* 219 */ "expr ::= BITNOT expr",
/* 220 */ "expr ::= MINUS expr",
/* 221 */ "expr ::= PLUS expr",
/* 222 */ "between_op ::= BETWEEN",
/* 223 */ "between_op ::= NOT BETWEEN",
/* 224 */ "expr ::= expr between_op expr AND expr",
/* 225 */ "in_op ::= IN",
/* 226 */ "in_op ::= NOT IN",
/* 227 */ "expr ::= expr in_op LP exprlist RP",
/* 228 */ "expr ::= LP select RP",
/* 229 */ "expr ::= expr in_op LP select RP",
/* 230 */ "expr ::= expr in_op nm dbnm",
/* 231 */ "expr ::= EXISTS LP select RP",
/* 232 */ "expr ::= CASE case_operand case_exprlist case_else END",
/* 233 */ "case_exprlist ::= case_exprlist WHEN expr THEN expr",
/* 234 */ "case_exprlist ::= WHEN expr THEN expr",
/* 235 */ "case_else ::= ELSE expr",
/* 236 */ "case_else ::=",
/* 237 */ "case_operand ::= expr",
/* 238 */ "case_operand ::=",
/* 239 */ "exprlist ::= nexprlist",
/* 240 */ "exprlist ::=",
/* 241 */ "nexprlist ::= nexprlist COMMA expr",
/* 242 */ "nexprlist ::= expr",
/* 243 */ "cmd ::= createkw uniqueflag INDEX ifnotexists nm dbnm ON nm LP idxlist RP",
/* 244 */ "uniqueflag ::= UNIQUE",
/* 245 */ "uniqueflag ::=",
/* 246 */ "idxlist_opt ::=",
/* 247 */ "idxlist_opt ::= LP idxlist RP",
/* 248 */ "idxlist ::= idxlist COMMA nm collate sortorder",
/* 249 */ "idxlist ::= nm collate sortorder",
/* 250 */ "collate ::=",
/* 251 */ "collate ::= COLLATE ids",
/* 252 */ "cmd ::= DROP INDEX ifexists fullname",
/* 253 */ "cmd ::= VACUUM",
/* 254 */ "cmd ::= VACUUM nm",
/* 255 */ "cmd ::= PRAGMA nm dbnm",
/* 256 */ "cmd ::= PRAGMA nm dbnm EQ nmnum",
/* 257 */ "cmd ::= PRAGMA nm dbnm LP nmnum RP",
/* 258 */ "cmd ::= PRAGMA nm dbnm EQ minus_num",
/* 259 */ "cmd ::= PRAGMA nm dbnm LP minus_num RP",
/* 260 */ "nmnum ::= plus_num",
/* 261 */ "nmnum ::= nm",
/* 262 */ "nmnum ::= ON",
/* 263 */ "nmnum ::= DELETE",
/* 264 */ "nmnum ::= DEFAULT",
/* 265 */ "plus_num ::= plus_opt number",
/* 266 */ "minus_num ::= MINUS number",
/* 267 */ "number ::= INTEGER|FLOAT",
/* 268 */ "plus_opt ::= PLUS",
/* 269 */ "plus_opt ::=",
/* 270 */ "cmd ::= createkw trigger_decl BEGIN trigger_cmd_list END",
/* 271 */ "trigger_decl ::= temp TRIGGER ifnotexists nm dbnm trigger_time trigger_event ON fullname foreach_clause when_clause",
/* 272 */ "trigger_time ::= BEFORE",
/* 273 */ "trigger_time ::= AFTER",
/* 274 */ "trigger_time ::= INSTEAD OF",
/* 275 */ "trigger_time ::=",
/* 276 */ "trigger_event ::= DELETE|INSERT",
/* 277 */ "trigger_event ::= UPDATE",
/* 278 */ "trigger_event ::= UPDATE OF inscollist",
/* 279 */ "foreach_clause ::=",
/* 280 */ "foreach_clause ::= FOR EACH ROW",
/* 281 */ "when_clause ::=",
/* 282 */ "when_clause ::= WHEN expr",
/* 283 */ "trigger_cmd_list ::= trigger_cmd_list trigger_cmd SEMI",
/* 284 */ "trigger_cmd_list ::= trigger_cmd SEMI",
/* 285 */ "trnm ::= nm",
/* 286 */ "trnm ::= nm DOT nm",
/* 287 */ "tridxby ::=",
/* 288 */ "tridxby ::= INDEXED BY nm",
/* 289 */ "tridxby ::= NOT INDEXED",
/* 290 */ "trigger_cmd ::= UPDATE orconf trnm tridxby SET setlist where_opt",
/* 291 */ "trigger_cmd ::= insert_cmd INTO trnm inscollist_opt VALUES LP itemlist RP",
/* 292 */ "trigger_cmd ::= insert_cmd INTO trnm inscollist_opt select",
/* 293 */ "trigger_cmd ::= DELETE FROM trnm tridxby where_opt",
/* 294 */ "trigger_cmd ::= select",
/* 295 */ "expr ::= RAISE LP IGNORE RP",
/* 296 */ "expr ::= RAISE LP raisetype COMMA nm RP",
/* 297 */ "raisetype ::= ROLLBACK",
/* 298 */ "raisetype ::= ABORT",
/* 299 */ "raisetype ::= FAIL",
/* 300 */ "cmd ::= DROP TRIGGER ifexists fullname",
/* 301 */ "cmd ::= ATTACH database_kw_opt expr AS expr key_opt",
/* 302 */ "cmd ::= DETACH database_kw_opt expr",
/* 303 */ "key_opt ::=",
/* 304 */ "key_opt ::= KEY expr",
/* 305 */ "database_kw_opt ::= DATABASE",
/* 306 */ "database_kw_opt ::=",
/* 307 */ "cmd ::= REINDEX",
/* 308 */ "cmd ::= REINDEX nm dbnm",
/* 309 */ "cmd ::= ANALYZE",
/* 310 */ "cmd ::= ANALYZE nm dbnm",
/* 311 */ "cmd ::= ALTER TABLE fullname RENAME TO nm",
/* 312 */ "cmd ::= ALTER TABLE add_column_fullname ADD kwcolumn_opt column",
/* 313 */ "add_column_fullname ::= fullname",
/* 314 */ "kwcolumn_opt ::=",
/* 315 */ "kwcolumn_opt ::= COLUMNKW",
/* 316 */ "cmd ::= create_vtab",
/* 317 */ "cmd ::= create_vtab LP vtabarglist RP",
/* 318 */ "create_vtab ::= createkw VIRTUAL TABLE nm dbnm USING nm",
/* 319 */ "vtabarglist ::= vtabarg",
/* 320 */ "vtabarglist ::= vtabarglist COMMA vtabarg",
/* 321 */ "vtabarg ::=",
/* 322 */ "vtabarg ::= vtabarg vtabargtoken",
/* 323 */ "vtabargtoken ::= ANY",
/* 324 */ "vtabargtoken ::= lp anylist RP",
/* 325 */ "lp ::= LP",
/* 326 */ "anylist ::=",
/* 327 */ "anylist ::= anylist LP anylist RP",
/* 328 */ "anylist ::= anylist ANY",
};
#endif // * NDEBUG */


#if YYSTACKDEPTH//<=0
/*
** Try to increase the size of the parser stack.
*/
static void yyGrowStack(yyParser p){
int newSize;
//yyStackEntry pNew;

newSize = p.yystksz*2 + 100;
//pNew = realloc(p.yystack, newSize*sizeof(pNew[0]));
//if( pNew !=null){
p.yystack = Array.Resize(p.yystack,newSize); //pNew;
p.yystksz = newSize;
#if !NDEBUG
if( yyTraceFILE ){
fprintf(yyTraceFILE,"%sStack grows to %d entries!\n",
yyTracePrompt, p.yystksz);
}
#endif
//}
}
#endif

    /*
** This function allocates a new parser.
** The only argument is a pointer to a function which works like
** malloc.
**
** Inputs:
** A pointer to the function used to allocate memory.
**
** Outputs:
** A pointer to a parser.  This pointer is used in subsequent calls
** to sqlite3Parser and sqlite3ParserFree.
*/
    static yyParser sqlite3ParserAlloc()
    {//void *(*mallocProc)(size_t)){
      yyParser pParser = new yyParser();
      //pParser = (yyParser*)(*mallocProc)( (size_t)yyParser.Length );
      if ( pParser != null )
      {
        pParser.yyidx = -1;
#if YYTRACKMAXSTACKDEPTH
pParser.yyidxMax=0;
#endif

#if YYSTACKDEPTH//<=0
pParser.yystack = NULL;
pParser.yystksz = null;
yyGrowStack(pParser);
#endif
      }
      return pParser;
    }

    /* The following function deletes the value associated with a
    ** symbol.  The symbol can be either a terminal or nonterminal.
    ** "yymajor" is the symbol code, and "yypminor" is a pointer to
    ** the value.
    */
    static void yy_destructor(
    yyParser yypParser,    /* The parser */
    YYCODETYPE yymajor,    /* Type code for object to destroy */
    YYMINORTYPE yypminor   /* The object to be destroyed */
    )
    {
      Parse pParse = yypParser.pParse; // sqlite3ParserARG_FETCH;
      switch ( yymajor )
      {
        /* Here is inserted the actions which take place when a
        ** terminal or non-terminal is destroyed.  This can happen
        ** when the symbol is popped from the stack during a
        ** reduce or during error processing or when a parser is
        ** being destroyed before it is finished parsing.
        **
        ** Note: during a reduce, the only symbols destroyed are those
        ** which appear on the RHS of the rule, but which are not used
        ** inside the C code.
        */
        case 160: /* select */
        case 194: /* oneselect */
          {
            //#line 403 "parse.y"
            sqlite3SelectDelete( pParse.db, ref ( yypminor.yy387 ) );
            //#line 1399 "parse.c"
          }
          break;
        case 174: /* term */
        case 175: /* Expr */
          {
            //#line 720 "parse.y"
            sqlite3ExprDelete( pParse.db, ref ( yypminor.yy118 ).pExpr );
            //#line 1407 "parse.c"
          }
          break;
        case 179: /* idxlist_opt */
        case 187: /* idxlist */
        case 197: /* selcollist */
        case 200: /* groupby_opt */
        case 202: /* orderby_opt */
        case 204: /* sclp */
        case 214: /* sortlist */
        case 216: /* nexprlist */
        case 217: /* setlist */
        case 220: /* itemlist */
        case 221: /* exprlist */
        case 226: /* case_exprlist */
          {
            //#line 1103 "parse.y"
            sqlite3ExprListDelete( pParse.db, ref ( yypminor.yy322 ) );
            //#line 1425 "parse.c"
          }
          break;
        case 193: /* fullname */
        case 198: /* from */
        case 206: /* seltablist */
        case 207: /* stl_prefix */
          {
            //#line 534 "parse.y"
            sqlite3SrcListDelete( pParse.db, ref ( yypminor.yy259 ) );
            //#line 1435 "parse.c"
          }
          break;
        case 199: /* where_opt */
        case 201: /* having_opt */
        case 210: /* on_opt */
        case 215: /* sortitem */
        case 225: /* case_operand */
        case 227: /* case_else */
        case 238: /* when_clause */
        case 243: /* key_opt */
          {
            //#line 644 "parse.y"
            sqlite3ExprDelete( pParse.db, ref ( yypminor.yy314 ) );
            //#line 1449 "parse.c"
          }
          break;
        case 211: /* using_opt */
        case 213: /* inscollist */
        case 219: /* inscollist_opt */
          {
            //#line 566 "parse.y"
            sqlite3IdListDelete( pParse.db, ref ( yypminor.yy384 ) );
            //#line 1458 "parse.c"
          }
          break;
        case 234: /* trigger_cmd_list */
        case 239: /* trigger_cmd */
          {
            //#line 1210"parse.y"
            sqlite3DeleteTriggerStep( pParse.db, ref ( yypminor.yy203 ) );
            //#line 1466 "parse.c"
          }
          break;
        case 236: /* trigger_event */
          {
            //#line 1196 "parse.y"
            sqlite3IdListDelete( pParse.db, ref ( yypminor.yy90 ).b );
            //#line 1473 "parse.c"
          }
          break;
        default:
          break;   /* If no destructor action specified: do nothing */
      }
    }

    /*
    ** Pop the parser's stack once.
    **
    ** If there is a destructor routine associated with the token which
    ** is popped from the stack, then call it.
    **
    ** Return the major token number for the symbol popped.
    */
    static int yy_pop_parser_stack( yyParser pParser )
    {
      YYCODETYPE yymajor;
      yyStackEntry yytos = pParser.yystack[pParser.yyidx];

      /* There is no mechanism by which the parser stack can be popped below
      ** empty in SQLite.  */
      if ( NEVER( pParser.yyidx < 0 ) )
        return 0;
#if !NDEBUG
      if ( yyTraceFILE != null && pParser.yyidx >= 0 )
      {
        fprintf( yyTraceFILE, "%sPopping %s\n",
        yyTracePrompt,
        yyTokenName[yytos.major] );
      }
#endif
      yymajor = yytos.major;
      yy_destructor( pParser, yymajor, yytos.minor );
      pParser.yyidx--;
      return yymajor;
    }

    /*
    ** Deallocate and destroy a parser.  Destructors are all called for
    ** all stack elements before shutting the parser down.
    **
    ** Inputs:
    ** <ul>
    ** <li>  A pointer to the parser.  This should be a pointer
    **       obtained from sqlite3ParserAlloc.
    ** <li>  A pointer to a function used to reclaim memory obtained
    **       from malloc.
    ** </ul>
    */
    static void sqlite3ParserFree(
    yyParser p,                    /* The parser to be deleted */
    dxDel freeProc//)(void*)     /* Function used to reclaim memory */
    )
    {
      yyParser pParser = p;
      /* In SQLite, we never try to destroy a parser that was not successfully
      ** created in the first place. */
      if ( NEVER( pParser == null ) )
        return;
      while ( pParser.yyidx >= 0 )
        yy_pop_parser_stack( pParser );
#if YYSTACKDEPTH//<=0
pParser.yystack = null;//free(pParser.yystack);
#endif
      pParser = null;// freeProc(ref pParser);
    }

    /*
    ** Return the peak depth of the stack for a parser.
    */
#if YYTRACKMAXSTACKDEPTH
int sqlite3ParserStackPeak(void p){
yyParser pParser = (yyParser*)p;
return pParser.yyidxMax;
}
#endif

    /*
** Find the appropriate action for a parser given the terminal
** look-ahead token iLookAhead.
**
** If the look-ahead token is YYNOCODE, then check to see if the action is
** independent of the look-ahead.  If it is, return the action, otherwise
** return YY_NO_ACTION.
*/
    static int yy_find_shift_action(
    yyParser pParser,         /* The parser */
    YYCODETYPE iLookAhead     /* The look-ahead token */
    )
    {
      int i;
      int stateno = pParser.yystack[pParser.yyidx].stateno;

      if ( stateno > YY_SHIFT_COUNT
      || ( i = yy_shift_ofst[stateno] ) == YY_SHIFT_USE_DFLT )
      {
        return yy_default[stateno];
      }
      Debug.Assert( iLookAhead != YYNOCODE );
      i += iLookAhead;
      if ( i < 0 || i >= YY_ACTTAB_COUNT || yy_lookahead[i] != iLookAhead )
      {
        if ( iLookAhead > 0 )
        {
          //#if YYFALLBACK
          YYCODETYPE iFallback;            /* Fallback token */
          if ( iLookAhead < yyFallback.Length //yyFallback.Length/sizeof(yyFallback[0])
          && ( iFallback = yyFallback[iLookAhead] ) != 0 )
          {
#if !NDEBUG
            if ( yyTraceFILE != null )
            {
              fprintf( yyTraceFILE, "%sFALLBACK %s => %s\n",
              yyTracePrompt, yyTokenName[iLookAhead], yyTokenName[iFallback] );
            }
#endif
            return yy_find_shift_action( pParser, iFallback );
          }
          //#endif
          //#if YYWILDCARD
          {
            int j = i - iLookAhead + YYWILDCARD;
            if (
              //#if YY_SHIFT_MIN+YYWILDCARD<0
            j >= 0 &&
              //#endif
              //#if YY_SHIFT_MAX+YYWILDCARD>=YY_ACTTAB_COUNT
            j < YY_ACTTAB_COUNT &&
              //#endif
            yy_lookahead[j] == YYWILDCARD
            )
            {
#if !NDEBUG
              if ( yyTraceFILE != null )
              {
                Debugger.Break(); // TODO --
                //fprintf(yyTraceFILE, "%sWILDCARD %s => %s\n",
                //   yyTracePrompt, yyTokenName[iLookAhead], yyTokenName[YYWILDCARD]);
              }
#endif // * NDEBUG */
              return yy_action[j];
            }
          }
          //#endif // * YYWILDCARD */
        }
        return yy_default[stateno];
      }
      else
      {
        return yy_action[i];
      }
    }

    /*
    ** Find the appropriate action for a parser given the non-terminal
    ** look-ahead token iLookAhead.
    **
    ** If the look-ahead token is YYNOCODE, then check to see if the action is
    ** independent of the look-ahead.  If it is, return the action, otherwise
    ** return YY_NO_ACTION.
    */
    static int yy_find_reduce_action(
    int stateno,              /* Current state number */
    YYCODETYPE iLookAhead     /* The look-ahead token */
    )
    {
      int i;
#if YYERRORSYMBOL
if( stateno>YY_REDUCE_COUNT ){
return yy_default[stateno];
}
#else
      Debug.Assert( stateno <= YY_REDUCE_COUNT );
#endif
      i = yy_reduce_ofst[stateno];
      Debug.Assert( i != YY_REDUCE_USE_DFLT );
      Debug.Assert( iLookAhead != YYNOCODE );
      i += iLookAhead;
#if YYERRORSYMBOL
if( i<0 || i>=YY_ACTTAB_COUNT || yy_lookahead[i]!=iLookAhead ){
return yy_default[stateno];
}
#else
      Debug.Assert( i >= 0 && i < YY_ACTTAB_COUNT );
      Debug.Assert( yy_lookahead[i] == iLookAhead );
#endif
      return yy_action[i];
    }

    /*
    ** The following routine is called if the stack overflows.
    */
    static void yyStackOverflow( yyParser yypParser, YYMINORTYPE yypMinor )
    {
      Parse pParse = yypParser.pParse; // sqlite3ParserARG_FETCH;
      yypParser.yyidx--;
#if !NDEBUG
      if ( yyTraceFILE != null )
      {
        Debugger.Break(); // TODO --
        //fprintf(yyTraceFILE, "%sStack Overflow!\n", yyTracePrompt);
      }
#endif
      while ( yypParser.yyidx >= 0 )
        yy_pop_parser_stack( yypParser );
      /* Here code is inserted which will execute if the parser
      ** stack every overflows */
      //#line 38 "parse.y"

      UNUSED_PARAMETER( yypMinor ); /* Silence some compiler warnings */
      sqlite3ErrorMsg( pParse, "parser stack overflow" );
      pParse.parseError = 1;
      //#line 1664  "parse.c"
      yypParser.pParse = pParse;//      sqlite3ParserARG_STORE; /* Suppress warning about unused %extra_argument var */
    }

    /*
    ** Perform a shift action.
    */
    static void yy_shift(
    yyParser yypParser,          /* The parser to be shifted */
    int yyNewState,               /* The new state to shift in */
    int yyMajor,                  /* The major token to shift in */
    YYMINORTYPE yypMinor         /* Pointer to the minor token to shift in */
    )
    {
      yyStackEntry yytos = new yyStackEntry();
      yypParser.yyidx++;
#if YYTRACKMAXSTACKDEPTH
if( yypParser.yyidx>yypParser.yyidxMax ){
yypParser.yyidxMax = yypParser.yyidx;
}
#endif
#if !YYSTACKDEPTH//was YYSTACKDEPTH>0
      if ( yypParser.yyidx >= YYSTACKDEPTH )
      {
        yyStackOverflow( yypParser, yypMinor );
        return;
      }
#else
if( yypParser.yyidx>=yypParser.yystksz ){
yyGrowStack(yypParser);
if( yypParser.yyidx>=yypParser.yystksz ){
yyStackOverflow(yypParser, yypMinor);
return;
}
}
#endif
      yypParser.yystack[yypParser.yyidx] = yytos;//yytos = yypParser.yystack[yypParser.yyidx];
      yytos.stateno = (YYACTIONTYPE)yyNewState;
      yytos.major = (YYCODETYPE)yyMajor;
      yytos.minor = yypMinor;
#if !NDEBUG
      if ( yyTraceFILE != null && yypParser.yyidx > 0 )
      {
        int i;
        fprintf( yyTraceFILE, "%sShift %d\n", yyTracePrompt, yyNewState );
        fprintf( yyTraceFILE, "%sStack:", yyTracePrompt );
        for ( i = 1; i <= yypParser.yyidx; i++ )
          fprintf( yyTraceFILE, " %s", yyTokenName[yypParser.yystack[i].major] );
        fprintf( yyTraceFILE, "\n" );
      }
#endif
    }
    /* The following table contains information about every rule that
    ** is used during the reduce.
    */
    public struct _yyRuleInfo
    {
      public YYCODETYPE lhs;         /* Symbol on the left-hand side of the rule */
      public byte nrhs;     /* Number of right-hand side symbols in the rule */
      public _yyRuleInfo( YYCODETYPE lhs, byte nrhs )
      {
        this.lhs = lhs;
        this.nrhs = nrhs;
      }

    }
    static _yyRuleInfo[] yyRuleInfo = new _yyRuleInfo[]{
new _yyRuleInfo( 142, 1 ),
new _yyRuleInfo( 143, 2 ),
new _yyRuleInfo( 143, 1 ),
new _yyRuleInfo( 144, 1 ),
new _yyRuleInfo( 144, 3 ),
new _yyRuleInfo( 145, 0 ),
new _yyRuleInfo( 145, 1 ),
new _yyRuleInfo( 145, 3 ),
new _yyRuleInfo( 146, 1 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 149, 0 ),
new _yyRuleInfo( 149, 1 ),
new _yyRuleInfo( 149, 2 ),
new _yyRuleInfo( 148, 0 ),
new _yyRuleInfo( 148, 1 ),
new _yyRuleInfo( 148, 1 ),
new _yyRuleInfo( 148, 1 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 151, 1 ),
new _yyRuleInfo( 151, 0 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 152, 6 ),
new _yyRuleInfo( 154, 1 ),
new _yyRuleInfo( 156, 0 ),
new _yyRuleInfo( 156, 3 ),
new _yyRuleInfo( 155, 1 ),
new _yyRuleInfo( 155, 0 ),
new _yyRuleInfo( 153, 4 ),
new _yyRuleInfo( 153, 2 ),
new _yyRuleInfo( 158, 3 ),
new _yyRuleInfo( 158, 1 ),
new _yyRuleInfo( 161, 3 ),
new _yyRuleInfo( 162, 1 ),
new _yyRuleInfo( 165, 1 ),
new _yyRuleInfo( 165, 1 ),
new _yyRuleInfo( 166, 1 ),
new _yyRuleInfo( 150, 1 ),
new _yyRuleInfo( 150, 1 ),
new _yyRuleInfo( 150, 1 ),
new _yyRuleInfo( 163, 0 ),
new _yyRuleInfo( 163, 1 ),
new _yyRuleInfo( 167, 1 ),
new _yyRuleInfo( 167, 4 ),
new _yyRuleInfo( 167, 6 ),
new _yyRuleInfo( 168, 1 ),
new _yyRuleInfo( 168, 2 ),
new _yyRuleInfo( 169, 1 ),
new _yyRuleInfo( 169, 1 ),
new _yyRuleInfo( 164, 2 ),
new _yyRuleInfo( 164, 0 ),
new _yyRuleInfo( 172, 3 ),
new _yyRuleInfo( 172, 1 ),
new _yyRuleInfo( 173, 2 ),
new _yyRuleInfo( 173, 4 ),
new _yyRuleInfo( 173, 3 ),
new _yyRuleInfo( 173, 3 ),
new _yyRuleInfo( 173, 2 ),
new _yyRuleInfo( 173, 2 ),
new _yyRuleInfo( 173, 3 ),
new _yyRuleInfo( 173, 5 ),
new _yyRuleInfo( 173, 2 ),
new _yyRuleInfo( 173, 4 ),
new _yyRuleInfo( 173, 4 ),
new _yyRuleInfo( 173, 1 ),
new _yyRuleInfo( 173, 2 ),
new _yyRuleInfo( 178, 0 ),
new _yyRuleInfo( 178, 1 ),
new _yyRuleInfo( 180, 0 ),
new _yyRuleInfo( 180, 2 ),
new _yyRuleInfo( 182, 2 ),
new _yyRuleInfo( 182, 3 ),
new _yyRuleInfo( 182, 3 ),
new _yyRuleInfo( 182, 3 ),
new _yyRuleInfo( 183, 2 ),
new _yyRuleInfo( 183, 2 ),
new _yyRuleInfo( 183, 1 ),
new _yyRuleInfo( 183, 1 ),
new _yyRuleInfo( 183, 2 ),
new _yyRuleInfo( 181, 3 ),
new _yyRuleInfo( 181, 2 ),
new _yyRuleInfo( 184, 0 ),
new _yyRuleInfo( 184, 2 ),
new _yyRuleInfo( 184, 2 ),
new _yyRuleInfo( 159, 0 ),
new _yyRuleInfo( 159, 2 ),
new _yyRuleInfo( 185, 3 ),
new _yyRuleInfo( 185, 2 ),
new _yyRuleInfo( 185, 1 ),
new _yyRuleInfo( 186, 2 ),
new _yyRuleInfo( 186, 7 ),
new _yyRuleInfo( 186, 5 ),
new _yyRuleInfo( 186, 5 ),
new _yyRuleInfo( 186, 10 ),
new _yyRuleInfo( 188, 0 ),
new _yyRuleInfo( 188, 1 ),
new _yyRuleInfo( 176, 0 ),
new _yyRuleInfo( 176, 3 ),
new _yyRuleInfo( 189, 0 ),
new _yyRuleInfo( 189, 2 ),
new _yyRuleInfo( 190, 1 ),
new _yyRuleInfo( 190, 1 ),
new _yyRuleInfo( 190, 1 ),
new _yyRuleInfo( 147, 4 ),
new _yyRuleInfo( 192, 2 ),
new _yyRuleInfo( 192, 0 ),
new _yyRuleInfo( 147, 8 ),
new _yyRuleInfo( 147, 4 ),
new _yyRuleInfo( 147, 1 ),
new _yyRuleInfo( 160, 1 ),
new _yyRuleInfo( 160, 3 ),
new _yyRuleInfo( 195, 1 ),
new _yyRuleInfo( 195, 2 ),
new _yyRuleInfo( 195, 1 ),
new _yyRuleInfo( 194, 9 ),
new _yyRuleInfo( 196, 1 ),
new _yyRuleInfo( 196, 1 ),
new _yyRuleInfo( 196, 0 ),
new _yyRuleInfo( 204, 2 ),
new _yyRuleInfo( 204, 0 ),
new _yyRuleInfo( 197, 3 ),
new _yyRuleInfo( 197, 2 ),
new _yyRuleInfo( 197, 4 ),
new _yyRuleInfo( 205, 2 ),
new _yyRuleInfo( 205, 1 ),
new _yyRuleInfo( 205, 0 ),
new _yyRuleInfo( 198, 0 ),
new _yyRuleInfo( 198, 2 ),
new _yyRuleInfo( 207, 2 ),
new _yyRuleInfo( 207, 0 ),
new _yyRuleInfo( 206, 7 ),
new _yyRuleInfo( 206, 7 ),
new _yyRuleInfo( 206, 7 ),
new _yyRuleInfo( 157, 0 ),
new _yyRuleInfo( 157, 2 ),
new _yyRuleInfo( 193, 2 ),
new _yyRuleInfo( 208, 1 ),
new _yyRuleInfo( 208, 2 ),
new _yyRuleInfo( 208, 3 ),
new _yyRuleInfo( 208, 4 ),
new _yyRuleInfo( 210, 2 ),
new _yyRuleInfo( 210, 0 ),
new _yyRuleInfo( 209, 0 ),
new _yyRuleInfo( 209, 3 ),
new _yyRuleInfo( 209, 2 ),
new _yyRuleInfo( 211, 4 ),
new _yyRuleInfo( 211, 0 ),
new _yyRuleInfo( 202, 0 ),
new _yyRuleInfo( 202, 3 ),
new _yyRuleInfo( 214, 4 ),
new _yyRuleInfo( 214, 2 ),
new _yyRuleInfo( 215, 1 ),
new _yyRuleInfo( 177, 1 ),
new _yyRuleInfo( 177, 1 ),
new _yyRuleInfo( 177, 0 ),
new _yyRuleInfo( 200, 0 ),
new _yyRuleInfo( 200, 3 ),
new _yyRuleInfo( 201, 0 ),
new _yyRuleInfo( 201, 2 ),
new _yyRuleInfo( 203, 0 ),
new _yyRuleInfo( 203, 2 ),
new _yyRuleInfo( 203, 4 ),
new _yyRuleInfo( 203, 4 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 199, 0 ),
new _yyRuleInfo( 199, 2 ),
new _yyRuleInfo( 147, 7 ),
new _yyRuleInfo( 217, 5 ),
new _yyRuleInfo( 217, 3 ),
new _yyRuleInfo( 147, 8 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 218, 2 ),
new _yyRuleInfo( 218, 1 ),
new _yyRuleInfo( 220, 3 ),
new _yyRuleInfo( 220, 1 ),
new _yyRuleInfo( 219, 0 ),
new _yyRuleInfo( 219, 3 ),
new _yyRuleInfo( 213, 3 ),
new _yyRuleInfo( 213, 1 ),
new _yyRuleInfo( 175, 1 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 174, 1 ),
new _yyRuleInfo( 175, 1 ),
new _yyRuleInfo( 175, 1 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 174, 1 ),
new _yyRuleInfo( 174, 1 ),
new _yyRuleInfo( 175, 1 ),
new _yyRuleInfo( 175, 1 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 6 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 175, 4 ),
new _yyRuleInfo( 174, 1 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 222, 1 ),
new _yyRuleInfo( 222, 2 ),
new _yyRuleInfo( 222, 1 ),
new _yyRuleInfo( 222, 2 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 175, 2 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 4 ),
new _yyRuleInfo( 175, 2 ),
new _yyRuleInfo( 175, 2 ),
new _yyRuleInfo( 175, 2 ),
new _yyRuleInfo( 175, 2 ),
new _yyRuleInfo( 223, 1 ),
new _yyRuleInfo( 223, 2 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 224, 1 ),
new _yyRuleInfo( 224, 2 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 175, 3 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 175, 4 ),
new _yyRuleInfo( 175, 4 ),
new _yyRuleInfo( 175, 5 ),
new _yyRuleInfo( 226, 5 ),
new _yyRuleInfo( 226, 4 ),
new _yyRuleInfo( 227, 2 ),
new _yyRuleInfo( 227, 0 ),
new _yyRuleInfo( 225, 1 ),
new _yyRuleInfo( 225, 0 ),
new _yyRuleInfo( 221, 1 ),
new _yyRuleInfo( 221, 0 ),
new _yyRuleInfo( 216, 3 ),
new _yyRuleInfo( 216, 1 ),
new _yyRuleInfo( 147, 11 ),
new _yyRuleInfo( 228, 1 ),
new _yyRuleInfo( 228, 0 ),
new _yyRuleInfo( 179, 0 ),
new _yyRuleInfo( 179, 3 ),
new _yyRuleInfo( 187, 5 ),
new _yyRuleInfo( 187, 3 ),
new _yyRuleInfo( 229, 0 ),
new _yyRuleInfo( 229, 2 ),
new _yyRuleInfo( 147, 4 ),
new _yyRuleInfo( 147, 1 ),
new _yyRuleInfo( 147, 2 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 230, 1 ),
new _yyRuleInfo( 230, 1 ),
new _yyRuleInfo( 230, 1 ),
new _yyRuleInfo( 230, 1 ),
new _yyRuleInfo( 230, 1 ),
new _yyRuleInfo( 170, 2 ),
new _yyRuleInfo( 171, 2 ),
new _yyRuleInfo( 232, 1 ),
new _yyRuleInfo( 231, 1 ),
new _yyRuleInfo( 231, 0 ),
new _yyRuleInfo( 147, 5 ),
new _yyRuleInfo( 233, 11 ),
new _yyRuleInfo( 235, 1 ),
new _yyRuleInfo( 235, 1 ),
new _yyRuleInfo( 235, 2 ),
new _yyRuleInfo( 235, 0 ),
new _yyRuleInfo( 236, 1 ),
new _yyRuleInfo( 236, 1 ),
new _yyRuleInfo( 236, 3 ),
new _yyRuleInfo( 237, 0 ),
new _yyRuleInfo( 237, 3 ),
new _yyRuleInfo( 238, 0 ),
new _yyRuleInfo( 238, 2 ),
new _yyRuleInfo( 234, 3 ),
new _yyRuleInfo( 234, 2 ),
new _yyRuleInfo( 240, 1 ),
new _yyRuleInfo( 240, 3 ),
new _yyRuleInfo( 241, 0 ),
new _yyRuleInfo( 241, 3 ),
new _yyRuleInfo( 241, 2 ),
new _yyRuleInfo( 239, 7 ),
new _yyRuleInfo( 239, 8 ),
new _yyRuleInfo( 239, 5 ),
new _yyRuleInfo( 239, 5 ),
new _yyRuleInfo( 239, 1 ),
new _yyRuleInfo( 175, 4 ),
new _yyRuleInfo( 175, 6 ),
new _yyRuleInfo( 191, 1 ),
new _yyRuleInfo( 191, 1 ),
new _yyRuleInfo( 191, 1 ),
new _yyRuleInfo( 147, 4 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 243, 0 ),
new _yyRuleInfo( 243, 2 ),
new _yyRuleInfo( 242, 1 ),
new _yyRuleInfo( 242, 0 ),
new _yyRuleInfo( 147, 1 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 147, 1 ),
new _yyRuleInfo( 147, 3 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 147, 6 ),
new _yyRuleInfo( 244, 1 ),
new _yyRuleInfo( 245, 0 ),
new _yyRuleInfo( 245, 1 ),
new _yyRuleInfo( 147, 1 ),
new _yyRuleInfo( 147, 4 ),
new _yyRuleInfo( 246, 7 ),
new _yyRuleInfo( 247, 1 ),
new _yyRuleInfo( 247, 3 ),
new _yyRuleInfo( 248, 0 ),
new _yyRuleInfo( 248, 2 ),
new _yyRuleInfo( 249, 1 ),
new _yyRuleInfo( 249, 3 ),
new _yyRuleInfo( 250, 1 ),
new _yyRuleInfo( 251, 0 ),
new _yyRuleInfo( 251, 4 ),
new _yyRuleInfo( 251, 2 ),
};

    //static void yy_accept(yyParser*);  /* Forward Declaration */

    /*
    ** Perform a reduce action and the shift that must immediately
    ** follow the reduce.
    */
    static void yy_reduce(
    yyParser yypParser,         /* The parser */
    int yyruleno                 /* Number of the rule by which to reduce */
    )
    {
      int yygoto;                     /* The next state */
      int yyact;                      /* The next action */
      YYMINORTYPE yygotominor;        /* The LHS of the rule reduced */
      yymsp yymsp; // yyStackEntry[] yymsp = new yyStackEntry[0];            /* The top of the parser's stack */
      int yysize;                     /* Amount to pop the stack */
      Parse pParse = yypParser.pParse; //sqlite3ParserARG_FETCH;

      yymsp = new yymsp( ref yypParser, yypParser.yyidx ); //      yymsp[0] = yypParser.yystack[yypParser.yyidx];
#if !NDEBUG
      if ( yyTraceFILE != null && yyruleno >= 0
      && yyruleno < yyRuleName.Length )
      { //(int)(yyRuleName.Length/sizeof(yyRuleName[0])) ){
        fprintf( yyTraceFILE, "%sReduce [%s].\n", yyTracePrompt,
        yyRuleName[yyruleno] );
      }
#endif // * NDEBUG */

      /* Silence complaints from purify about yygotominor being uninitialized
** in some cases when it is copied into the stack after the following
** switch.  yygotominor is uninitialized when a rule reduces that does
** not set the value of its left-hand side nonterminal.  Leaving the
** value of the nonterminal uninitialized is utterly harmless as long
** as the value is never used.  So really the only thing this code
** accomplishes is to quieten purify.
**
** 2007-01-16:  The wireshark project (www.wireshark.org) reports that
** without this code, their parser segfaults.  I'm not sure what there
** parser is doing to make this happen.  This is the second bug report
** from wireshark this week.  Clearly they are stressing Lemon in ways
** that it has not been previously stressed...  (SQLite ticket #2172)
*/
      yygotominor = new YYMINORTYPE(); //memset(yygotominor, 0, yygotominor).Length;
      switch ( yyruleno )
      {
        /* Beginning here are the reduction cases.  A typical example
        ** follows:
        **   case 0:
        **  //#line <lineno> <grammarfile>
        **     { ... }           // User supplied code
        **  //#line <lineno> <thisfile>
        **     break;
        */
        case 5: /* explain ::= */
          //#line 107 "parse.y"
          {
            sqlite3BeginParse( pParse, 0 );
          }
          //#line 2107 "parse.c"
          break;
        case 6: /* explain ::= EXPLAIN */
          //#line 109 "parse.y"
          {
            sqlite3BeginParse( pParse, 1 );
          }
          //#line 2112 "parse.c"
          break;
        case 7: /* explain ::= EXPLAIN QUERY PLAN */
          //#line 110 "parse.y"
          {
            sqlite3BeginParse( pParse, 2 );
          }
          //#line 2117 "parse.c"
          break;
        case 8: /* cmdx ::= cmd */
          //#line 112 "parse.y"
          {
            sqlite3FinishCoding( pParse );
          }
          //#line 2122 "parse.c"
          break;
        case 9: /* cmd ::= BEGIN transtype trans_opt */
          //#line 117 "parse.y"
          {
            sqlite3BeginTransaction( pParse, yymsp[-1].minor.yy4 );
          }
          //#line 2127 "parse.c"
          break;
        case 13: /* transtype ::= */
          //#line 122 "parse.y"
          {
            yygotominor.yy4 = TK_DEFERRED;
          }
          //#line 2132 "parse.c"
          break;
        case 14: /* transtype ::= DEFERRED */
        case 15: /* transtype ::= IMMEDIATE */ //yytestcase(yyruleno==15);
        case 16: /* transtype ::= EXCLUSIVE */ //yytestcase(yyruleno==16);
        case 115: /* multiselect_op ::= UNION */ //yytestcase(yyruleno==114);
        case 117: /* multiselect_op ::= EXCEPT|INTERSECT */ //yytestcase(yyruleno==116);
          //#line 123 "parse.y"
          {
            yygotominor.yy4 = yymsp[0].major;
          }
          //#line 2141 "parse.c"
          break;
        case 17: /* cmd ::= COMMIT trans_opt */
        case 18: /* cmd ::= END trans_opt */ //yytestcase(yyruleno==18);
          //#line 126 "parse.y"
          {
            sqlite3CommitTransaction( pParse );
          }
          //#line 2147 "parse.c"
          break;
        case 19: /* cmd ::= ROLLBACK trans_opt */
          //#line 128 "parse.y"
          {
            sqlite3RollbackTransaction( pParse );
          }
          //#line 2152 "parse.c"
          break;
        case 22: /* cmd ::= SAVEPOINT nm */
          //#line 132 "parse.y"
          {
            sqlite3Savepoint( pParse, SAVEPOINT_BEGIN, yymsp[0].minor.yy0 );
          }
          //#line 2159 "parse.c"
          break;
        case 23: /* cmd ::= RELEASE savepoint_opt nm */
          //#line 135 "parse.y"
          {
            sqlite3Savepoint( pParse, SAVEPOINT_RELEASE, yymsp[0].minor.yy0 );
          }
          //#line 2166 "parse.c"
          break;
        case 24: /* cmd ::= ROLLBACK trans_opt TO savepoint_opt nm */
          //#line 138 "parse.y"
          {
            sqlite3Savepoint( pParse, SAVEPOINT_ROLLBACK, yymsp[0].minor.yy0 );
          }
          //#line 2173 "parse.c"
          break;
        case 26: /* create_table ::= createkw temp TABLE ifnotexists nm dbnm */
          //#line 145 "parse.y"
          {
            sqlite3StartTable( pParse, yymsp[-1].minor.yy0, yymsp[0].minor.yy0, yymsp[-4].minor.yy4, 0, 0, yymsp[-2].minor.yy4 );
          }
          //#line 2180 "parse.c"
          break;
        case 27: /* createkw ::= CREATE */
          //#line 148 "parse.y"
          {
            pParse.db.lookaside.bEnabled = 0;
            yygotominor.yy0 = yymsp[0].minor.yy0;
          }
          //#line 2188 "parse.c"
          break;
        case 28: /* ifnotexists ::= */
        case 31: /* temp ::= */ //yytestcase(yyruleno == 31);
        case 70: /* autoinc ::= */ //yytestcase(yyruleno == 70);
        case 83: /* defer_subclause ::= NOT DEFERRABLE init_deferred_pred_opt */ //yytestcase(yyruleno == 83);
        case 85: /* init_deferred_pred_opt ::= */ //yytestcase(yyruleno == 85);
        case 87: /* init_deferred_pred_opt ::= INITIALLY IMMEDIATE */ //yytestcase(yyruleno == 87);
        case 98: /* defer_subclause_opt ::= */ //yytestcase(yyruleno == 98);
        case 109: /* ifexists ::= */ //yytestcase(yyruleno == 109);
        case 120: /* distinct ::= ALL */ //yytestcase(yyruleno == 120);
        case 121: /* distinct ::= */ //yytestcase(yyruleno == 121);
        case 222: /* between_op ::= BETWEEN */ //yytestcase(yyruleno == 223);
        case 225: /* in_op ::= IN */ //yytestcase(yyruleno == 226);
          //#line 153 "parse.y"
          {
            yygotominor.yy4 = 0;
          }
          //#line 2204 "parse.c"
          break;
        case 29: /* ifnotexists ::= IF NOT EXISTS */
        case 30: /* temp ::= TEMP */ //yytestcase(yyruleno == 30);
        case 71: /* autoinc ::= AUTOINCR */ //yytestcase(yyruleno == 71);
        case 86: /* init_deferred_pred_opt ::= INITIALLY DEFERRED */ //yytestcase(yyruleno == 86);
        case 108: /* ifexists ::= IF EXISTS */ //yytestcase(yyruleno == 108);
        case 119: /* distinct ::= DISTINCT */ //yytestcase(yyruleno == 119);
        case 223: /* between_op ::= NOT BETWEEN */ //yytestcase(yyruleno == 224);
        case 226: /* in_op ::= NOT IN */ //yytestcase(yyruleno == 227);
          //#line 154 "parse.y"
          {
            yygotominor.yy4 = 1;
          }
          //#line 2216 "parse.c"
          break;
        case 32: /* create_table_args ::= LP columnlist conslist_opt RP */
          //#line 160 "parse.y"
          {
            sqlite3EndTable( pParse, yymsp[-1].minor.yy0, yymsp[0].minor.yy0, 0 );
          }
          //#line 2223 "parse.c"
          break;
        case 33: /* create_table_args ::= AS select */
          //#line 163 "parse.y"
          {
            sqlite3EndTable( pParse, 0, 0, yymsp[0].minor.yy387 );
            sqlite3SelectDelete( pParse.db, ref yymsp[0].minor.yy387 );
          }
          //#line 2231 "parse.c"
          break;
        case 36: /* column ::= columnid type carglist */
          //#line 175 "parse.y"
          {
            //yygotominor.yy0.z = yymsp[-2].minor.yy0.z;
            //yygotominor.yy0.n = (int)( pParse.sLastToken.z - yymsp[-2].minor.yy0.z ) + pParse.sLastToken.n; 
            yygotominor.yy0.n = (int)( yymsp[-2].minor.yy0.z.Length - pParse.sLastToken.z.Length ) + pParse.sLastToken.n;
            yygotominor.yy0.z = yymsp[-2].minor.yy0.z.Substring( 0, yygotominor.yy0.n );
          }
          //#line 2239 "parse.c"
          break;
        case 37: /* columnid ::= nm */
          //#line 179 "parse.y"
          {
            sqlite3AddColumn( pParse, yymsp[0].minor.yy0 );
            yygotominor.yy0 = yymsp[0].minor.yy0;
          }
          //#line 2247 "parse.c"
          break;
        case 38: /* id ::= ID */
        case 39: /* id ::= INDEXED */ //yytestcase(yyruleno==39);
        case 40: /* ids ::= ID|STRING */ //yytestcase(yyruleno==40);
        case 41: /* nm ::= id */ //yytestcase(yyruleno==41);
        case 42: /* nm ::= STRING */ //yytestcase(yyruleno==42);
        case 43: /* nm ::= JOIN_KW */ //yytestcase(yyruleno==43);
        case 46: /* typetoken ::= typename */ //yytestcase(yyruleno==46);
        case 49: /* typename ::= ids */ //yytestcase(yyruleno==49);
        case 127: /* as ::= AS nm */ ////yytestcase(yyruleno == 127);
        case 128: /* as ::= ids */ ////yytestcase(yyruleno == 128);
        case 138: /* dbnm ::= DOT nm */ ////yytestcase(yyruleno == 138);
        case 147: /* indexed_opt ::= INDEXED BY nm */ ////yytestcase(yyruleno == 147);
        case 251: /* collate ::= COLLATE ids */ //testcase(yyruleno == 251);
        case 260: /* nmnum ::= plus_num */ //testcase(yyruleno == 260);
        case 261: /* nmnum ::= nm */ //testcase(yyruleno == 261);
        case 262: /* nmnum ::= ON */ //testcase(yyruleno == 262);
        case 263: /* nmnum ::= DELETE */ //testcase(yyruleno == 263);
        case 264: /* nmnum ::= DEFAULT */ //testcase(yyruleno == 264);
        case 265: /* plus_num ::= plus_opt number */ //testcase(yyruleno == 265);
        case 266: /* minus_num ::= MINUS number */ //testcase(yyruleno == 266);
        case 267: /* number ::= INTEGER|FLOAT */ //testcase(yyruleno == 267);
        case 285: /* trnm ::= nm */ //testcase(yyruleno == 285);
          //#line 189 "parse.y"
          {
            yygotominor.yy0 = yymsp[0].minor.yy0;
          }
          //#line 2273 "parse.c"
          break;
        case 45: /* type ::= typetoken */
          //#line 251 "parse.y"
          {
            sqlite3AddColumnType( pParse, yymsp[0].minor.yy0 );
          }
          //#line 2278 "parse.c"
          break;
        case 47: /* typetoken ::= typename LP signed RP */
          //#line 253 "parse.y"
          {
            //yygotominor.yy0.z = yymsp[-3].minor.yy0.z;
            //yygotominor.yy0.n = (int)( &yymsp[0].minor.yy0.z[yymsp[0].minor.yy0.n] - yymsp[-3].minor.yy0.z );
            yygotominor.yy0.n = yymsp[-3].minor.yy0.z.Length - yymsp[0].minor.yy0.z.Length + yymsp[0].minor.yy0.n;
            yygotominor.yy0.z = yymsp[-3].minor.yy0.z.Substring( 0, yygotominor.yy0.n );
          }
          //#line 2286 "parse.c"
          break;
        case 48: /* typetoken ::= typename LP signed COMMA signed RP */
          //#line 257 "parse.y"
          {
            //yygotominor.yy0.z = yymsp[-5].minor.yy0.z;
            //yygotominor.yy0.n = (int)( &yymsp[0].minor.yy0.z[yymsp[0].minor.yy0.n] - yymsp[-5].minor.yy0.z );
            yygotominor.yy0.n = yymsp[-5].minor.yy0.z.Length - yymsp[0].minor.yy0.z.Length + 1;
            yygotominor.yy0.z = yymsp[-5].minor.yy0.z.Substring( 0, yygotominor.yy0.n );
          }
          //#line 2294 "parse.c"
          break;
        case 50: /* typename ::= typename ids */
          //#line 263 "parse.y"
          {
            //yygotominor.yy0.z=yymsp[-1].minor.yy0.z; yygotominor.yy0.n=yymsp[0].minor.yy0.n+(int)(yymsp[0].minor.yy0.z-yymsp[-1].minor.yy0.z);
            yygotominor.yy0.z = yymsp[-1].minor.yy0.z;
            yygotominor.yy0.n = yymsp[0].minor.yy0.n + (int)( yymsp[-1].minor.yy0.z.Length - yymsp[0].minor.yy0.z.Length );
          }
          //#line 2299 "parse.c"
          break;
        case 57: /* ccons ::= DEFAULT term */
        case 59: /* ccons ::= DEFAULT PLUS term */ //yytestcase(yyruleno==59);
          //#line 274 "parse.y"
          {
            sqlite3AddDefaultValue( pParse, yymsp[0].minor.yy118 );
          }
          //#line 2308 "parse.c"
          break;
        case 58: /* ccons ::= DEFAULT LP expr RP */
          //#line 275 "parse.y"
          {
            sqlite3AddDefaultValue( pParse, yymsp[-1].minor.yy118 );
          }
          //#line 2310 "parse.c"
          break;
        case 60: /* ccons ::= DEFAULT MINUS term */
          //#line 277 "parse.y"
          {
            ExprSpan v = new ExprSpan();
            v.pExpr = sqlite3PExpr( pParse, TK_UMINUS, yymsp[0].minor.yy118.pExpr, 0, 0 );
            v.zStart = yymsp[-1].minor.yy0.z;
            v.zEnd = yymsp[0].minor.yy118.zEnd;
            sqlite3AddDefaultValue( pParse, v );
          }
          //#line 2321 "parse.c"
          break;
        case 61: /* ccons ::= DEFAULT id */
          //#line 284 "parse.y"
          {
            ExprSpan v = new ExprSpan();
            spanExpr( v, pParse, TK_STRING, yymsp[0].minor.yy0 );
            sqlite3AddDefaultValue( pParse, v );
          }
          //#line 2330 "parse.c"
          break;
        case 63: /* ccons ::= NOT NULL onconf */
          //#line 294 "parse.y"
          {
            sqlite3AddNotNull( pParse, yymsp[0].minor.yy4 );
          }
          //#line 2335 "parse.c"
          break;
        case 64: /* ccons ::= PRIMARY KEY sortorder onconf autoinc */
          //#line 296 "parse.y"
          {
            sqlite3AddPrimaryKey( pParse, 0, yymsp[-1].minor.yy4, yymsp[0].minor.yy4, yymsp[-2].minor.yy4 );
          }
          //#line 2340 "parse.c"
          break;
        case 65: /* ccons ::= UNIQUE onconf */
          //#line 297 "parse.y"
          {
            sqlite3CreateIndex( pParse, 0, 0, 0, 0, yymsp[0].minor.yy4, 0, 0, 0, 0 );
          }
          //#line 2345 "parse.c"
          break;
        case 66: /* ccons ::= CHECK LP expr RP */
          //#line 298 "parse.y"
          {
            sqlite3AddCheckConstraint( pParse, yymsp[-1].minor.yy118.pExpr );
          }
          //#line 2350 "parse.c"
          break;
        case 67: /* ccons ::= REFERENCES nm idxlist_opt refargs */
          //#line 300 "parse.y"
          {
            sqlite3CreateForeignKey( pParse, 0, yymsp[-2].minor.yy0, yymsp[-1].minor.yy322, yymsp[0].minor.yy4 );
          }
          //#line 2355 "parse.c"
          break;
        case 68: /* ccons ::= defer_subclause */
          //#line 301 "parse.y"
          {
            sqlite3DeferForeignKey( pParse, yymsp[0].minor.yy4 );
          }
          //#line 2360 "parse.c"
          break;
        case 69: /* ccons ::= COLLATE ids */
          //#line 302 "parse.y"
          {
            sqlite3AddCollateType( pParse, yymsp[0].minor.yy0 );
          }
          //#line 2365 "parse.c"
          break;
        case 72: /* refargs ::= */
          //#line 315 "parse.y"
          {
            yygotominor.yy4 = OE_None * 0x0101; /* EV: R-19803-45884 */
          }
          //#line 2370 "parse.c"
          break;
        case 73: /* refargs ::= refargs refarg */
          //#line 316 "parse.y"
          {
            yygotominor.yy4 = ( yymsp[-1].minor.yy4 & ~yymsp[0].minor.yy215.mask ) | yymsp[0].minor.yy215.value;
          }
          //#line 2375 "parse.c"
          break;
        case 74: /* refarg ::= MATCH nm */
        case 75: /* refarg ::= ON INSERT refact */ //yytestcase(yyruleno==75);
          //#line 318 "parse.y"
          {
            yygotominor.yy215.value = 0;
            yygotominor.yy215.mask = 0x000000;
          }
          //#line 2381 "parse.c"
          break;
        case 76: /* refarg ::= ON DELETE refact */
          //#line 320 "parse.y"
          {
            yygotominor.yy215.value = yymsp[0].minor.yy4;
            yygotominor.yy215.mask = 0x0000ff;
          }
          //#line 2386 "parse.c"
          break;
        case 77: /* refarg ::= ON UPDATE refact */
          //#line 321 "parse.y"
          {
            yygotominor.yy215.value = yymsp[0].minor.yy4 << 8;
            yygotominor.yy215.mask = 0x00ff00;
          }
          //#line 2391 "parse.c"
          break;
        case 78: /* refact ::= SET NULL */
          //#line 323 "parse.y"
          {
            yygotominor.yy4 = OE_SetNull;  /* EV: R-33326-45252 */
          }
          //#line 2396 "parse.c"
          break;
        case 79: /* refact ::= SET DEFAULT */
          //#line 324 "parse.y"
          {
            yygotominor.yy4 = OE_SetDflt;  /* EV: R-33326-45252 */
          }
          //#line 2401 "parse.c"
          break;
        case 80: /* refact ::= CASCADE */
          //#line 325 "parse.y"
          {
            yygotominor.yy4 = OE_Cascade;  /* EV: R-33326-45252 */
          }
          //#line 2406 "parse.c"
          break;
        case 81: /* refact ::= RESTRICT */
          //#line 326 "parse.y"
          {
            yygotominor.yy4 = OE_Restrict; /* EV: R-33326-45252 */
          }
          //#line 2411 "parse.c"
          break;
        case 82: /* refact ::= NO ACTION */
          //#line 327 "parse.y"
          {
            yygotominor.yy4 = OE_None;     /* EV: R-33326-45252 */
          }
          //#line 2416 "parse.c"
          break;
        case 84: /* defer_subclause ::= DEFERRABLE init_deferred_pred_opt */
        case 99: /* defer_subclause_opt ::= defer_subclause */ //yytestcase(yyruleno==99);
        case 101: /* onconf ::= ON CONFLICT resolvetype */ //yytestcase(yyruleno==101);
        case 104: /* resolvetype ::= raisetype */ //yytestcase(yyruleno==104);
          //#line 330 "parse.y"
          {
            yygotominor.yy4 = yymsp[0].minor.yy4;
          }
          //#line 2424 "parse.c"
          break;
        case 88: /* conslist_opt ::= */
          //#line 339 "parse.y"
          {
            yygotominor.yy0.n = 0;
            yygotominor.yy0.z = null;
          }
          //#line 2429 "parse.c"
          break;
        case 89: /* conslist_opt ::= COMMA conslist */
          //#line 340 "parse.y"
          {
            yygotominor.yy0 = yymsp[-1].minor.yy0;
          }
          //#line 2434 "parse.c"
          break;
        case 94: /* tcons ::= PRIMARY KEY LP idxlist autoinc RP onconf */
          //#line 346 "parse.y"
          {
            sqlite3AddPrimaryKey( pParse, yymsp[-3].minor.yy322, yymsp[0].minor.yy4, yymsp[-2].minor.yy4, 0 );
          }
          //#line 2439 "parse.c"
          break;
        case 95: /* tcons ::= UNIQUE LP idxlist RP onconf */
          //#line 348 "parse.y"
          {
            sqlite3CreateIndex( pParse, 0, 0, 0, yymsp[-2].minor.yy322, yymsp[0].minor.yy4, 0, 0, 0, 0 );
          }
          //#line 2444 "parse.c"
          break;
        case 96: /* tcons ::= CHECK LP expr RP onconf */
          //#line 350 "parse.y"
          {
            sqlite3AddCheckConstraint( pParse, yymsp[-2].minor.yy118.pExpr );
          }
          //#line 2449 "parse.c"
          break;
        case 97: /* tcons ::= FOREIGN KEY LP idxlist RP REFERENCES nm idxlist_opt refargs defer_subclause_opt */
          //#line 352 "parse.y"
          {
            sqlite3CreateForeignKey( pParse, yymsp[-6].minor.yy322, yymsp[-3].minor.yy0, yymsp[-2].minor.yy322, yymsp[-1].minor.yy4 );
            sqlite3DeferForeignKey( pParse, yymsp[0].minor.yy4 );
          }
          //#line 2457 "parse.c"
          break;
        case 100: /* onconf ::= */
          //#line 366 "parse.y"
          {
            yygotominor.yy4 = OE_Default;
          }
          //#line 2462 "parse.c"
          break;
        case 102: /* orconf ::= */
          //#line 368 "parse.y"
          {
            yygotominor.yy210 = OE_Default;
          }
          //#line 2467 "parse.c"
          break;
        case 103: /* orconf ::= OR resolvetype */
          //#line 369 "parse.y"
          {
            yygotominor.yy210 = (u8)yymsp[0].minor.yy4;
          }
          //#line 2472 "parse.c"
          break;
        case 105: /* resolvetype ::= IGNORE */
          //#line 371 "parse.y"
          {
            yygotominor.yy4 = OE_Ignore;
          }
          //#line 2477 "parse.c"
          break;
        case 106: /* resolvetype ::= REPLACE */
          //#line 372 "parse.y"
          {
            yygotominor.yy4 = OE_Replace;
          }
          //#line 2482 "parse.c"
          break;
        case 107: /* cmd ::= DROP TABLE ifexists fullname */
          //#line 376 "parse.y"
          {
            sqlite3DropTable( pParse, yymsp[0].minor.yy259, 0, yymsp[-1].minor.yy4 );
          }
          //#line 2489 "parse.c"
          break;
        case 110: /* cmd ::= createkw temp VIEW ifnotexists nm dbnm AS select */
          //#line 386 "parse.y"
          {
            sqlite3CreateView( pParse, yymsp[-7].minor.yy0, yymsp[-3].minor.yy0, yymsp[-2].minor.yy0, yymsp[0].minor.yy387, yymsp[-6].minor.yy4, yymsp[-4].minor.yy4 );
          }
          //#line 2496 "parse.c"
          break;
        case 111: /* cmd ::= DROP VIEW ifexists fullname */
          //#line 389 "parse.y"
          {
            sqlite3DropTable( pParse, yymsp[0].minor.yy259, 1, yymsp[-1].minor.yy4 );
          }
          //#line 2503 "parse.c"
          break;
        case 112: /* cmd ::= select */
          //#line 396 "parse.y"
          {
            SelectDest dest = new SelectDest( SRT_Output, '\0', 0, 0, 0 );
            sqlite3Select( pParse, yymsp[0].minor.yy387, ref dest );
            sqlite3SelectDelete( pParse.db, ref yymsp[0].minor.yy387 );
          }
          //#line 2512 "parse.c"
          break;
        case 113: /* select ::= oneselect */
          //#line 407 "parse.y"
          {
            yygotominor.yy387 = yymsp[0].minor.yy387;
          }
          //#line 2517 "parse.c"
          break;
        case 114: /* select ::= select multiselect_op oneselect */
          //#line 409 "parse.y"
          {
            if ( yymsp[0].minor.yy387 != null )
            {
              yymsp[0].minor.yy387.op = (u8)yymsp[-1].minor.yy4;
              yymsp[0].minor.yy387.pPrior = yymsp[-2].minor.yy387;
            }
            else
            {
              sqlite3SelectDelete( pParse.db, ref yymsp[-2].minor.yy387 );
            }
            yygotominor.yy387 = yymsp[0].minor.yy387;
          }
          //#line 2530 "parse.c"
          break;
        case 116: /* multiselect_op ::= UNION ALL */
          //#line 420 "parse.y"
          {
            yygotominor.yy4 = TK_ALL;
          }
          //#line 2535 "parse.c"
          break;
        case 118: /* oneselect ::= SELECT distinct selcollist from where_opt groupby_opt having_opt orderby_opt limit_opt */
          //#line 424 "parse.y"
          {
            yygotominor.yy387 = sqlite3SelectNew( pParse, yymsp[-6].minor.yy322, yymsp[-5].minor.yy259, yymsp[-4].minor.yy314, yymsp[-3].minor.yy322, yymsp[-2].minor.yy314, yymsp[-1].minor.yy322, yymsp[-7].minor.yy4, yymsp[0].minor.yy292.pLimit, yymsp[0].minor.yy292.pOffset );
          }
          //#line 2542 "parse.c"
          break;
        case 122: /* sclp ::= selcollist COMMA */
        case 247: /* idxlist_opt ::= LP idxlist RP */ //yytestcase(yyruleno==247);
          //#line 445 "parse.y"
          {
            yygotominor.yy322 = yymsp[-1].minor.yy322;
          }
          //#line 2548 "parse.c"
          break;
        case 123: /* sclp ::= */
        case 151: /* orderby_opt ::= */ //yytestcase(yyruleno==151);
        case 159: /* groupby_opt ::= */ //yytestcase(yyruleno==159);
        case 240: /* exprlist ::= */ //yytestcase(yyruleno==240);
        case 246: /* idxlist_opt ::= */ //yytestcase(yyruleno==246);
          //#line 446 "parse.y"
          {
            yygotominor.yy322 = null;
          }
          //#line 2557 "parse.c"
          break;
        case 124: /* selcollist ::= sclp expr as */
          //#line 447 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-2].minor.yy322, yymsp[-1].minor.yy118.pExpr );
            if ( yymsp[0].minor.yy0.n > 0 )
              sqlite3ExprListSetName( pParse, yygotominor.yy322, yymsp[0].minor.yy0, 1 );
            sqlite3ExprListSetSpan( pParse, yygotominor.yy322, yymsp[-1].minor.yy118 );
          }
          //#line 2566 "parse.c"
          break;
        case 125: /* selcollist ::= sclp STAR */
          //#line 452 "parse.y"
          {
            Expr p = sqlite3Expr( pParse.db, TK_ALL, null );
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-1].minor.yy322, p );
          }
          //#line 2574 "parse.c"
          break;
        case 126: /* selcollist ::= sclp nm DOT STAR */
          //#line 456 "parse.y"
          {
            Expr pRight = sqlite3PExpr( pParse, TK_ALL, 0, 0, yymsp[0].minor.yy0 );
            Expr pLeft = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[-2].minor.yy0 );
            Expr pDot = sqlite3PExpr( pParse, TK_DOT, pLeft, pRight, 0 );
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-3].minor.yy322, pDot );
          }
          //#line 2584 "parse.c"
          break;
        case 129: /* as ::= */
          //#line 469 "parse.y"
          {
            yygotominor.yy0.n = 0;
          }
          //#line 2589 "parse.c"
          break;
        case 130: /* from ::= */
          //#line 481 "parse.y"
          {
            yygotominor.yy259 = new SrcList();
          }//sqlite3DbMallocZero(pParse.db, sizeof(*yygotominor.yy259));}
          //#line 2594 "parse.c"
          break;
        case 131: /* from ::= FROM seltablist */
          //#line 482 "parse.y"
          {
            yygotominor.yy259 = yymsp[0].minor.yy259;
            sqlite3SrcListShiftJoinType( yygotominor.yy259 );
          }
          //#line 2602 "parse.c"
          break;
        case 132: /* stl_prefix ::= seltablist joinop */
          //#line 490 "parse.y"
          {
            yygotominor.yy259 = yymsp[-1].minor.yy259;
            if ( ALWAYS( yygotominor.yy259 != null && yygotominor.yy259.nSrc > 0 ) )
              yygotominor.yy259.a[yygotominor.yy259.nSrc - 1].jointype = (u8)yymsp[0].minor.yy4;
          }
          //#line 2610 "parse.c"
          break;
        case 133: /* stl_prefix ::= */
          //#line 494 "parse.y"
          {
            yygotominor.yy259 = null;
          }
          //#line 2615 "parse.c"
          break;
        case 134: /* seltablist ::= stl_prefix nm dbnm as indexed_opt on_opt using_opt */
          //#line 495 "parse.y"
          {
            yygotominor.yy259 = sqlite3SrcListAppendFromTerm( pParse, yymsp[-6].minor.yy259, yymsp[-5].minor.yy0, yymsp[-4].minor.yy0, yymsp[-3].minor.yy0, 0, yymsp[-1].minor.yy314, yymsp[0].minor.yy384 );
            sqlite3SrcListIndexedBy( pParse, yygotominor.yy259, yymsp[-2].minor.yy0 );
          }
          //#line 2623 "parse.c"
          break;
        case 135: /* seltablist ::= stl_prefix LP select RP as on_opt using_opt */
          //#line 501 "parse.y"
          {
            yygotominor.yy259 = sqlite3SrcListAppendFromTerm( pParse, yymsp[-6].minor.yy259, 0, 0, yymsp[-2].minor.yy0, yymsp[-4].minor.yy387, yymsp[-1].minor.yy314, yymsp[0].minor.yy384 );
          }
          //#line 2630 "parse.c"
          break;
        case 136: /* seltablist ::= stl_prefix LP seltablist RP as on_opt using_opt */
          //#line 505 "parse.y"
          {
            if ( yymsp[-6].minor.yy259 == null && yymsp[-2].minor.yy0.n == 0 && yymsp[-1].minor.yy314 == null && yymsp[0].minor.yy384 == null )
            {
              yygotominor.yy259 = yymsp[-4].minor.yy259;
            }
            else
            {
              Select pSubquery;
              sqlite3SrcListShiftJoinType( yymsp[-4].minor.yy259 );
              pSubquery = sqlite3SelectNew( pParse, 0, yymsp[-4].minor.yy259, 0, 0, 0, 0, 0, 0, 0 );
              yygotominor.yy259 = sqlite3SrcListAppendFromTerm( pParse, yymsp[-6].minor.yy259, 0, 0, yymsp[-2].minor.yy0, pSubquery, yymsp[-1].minor.yy314, yymsp[0].minor.yy384 );
            }
          }
          //#line 2644 "parse.c"
          break;
        case 137: /* dbnm ::= */
        case 146: /* indexed_opt ::= */ //yytestcase(yyruleno==146);
          //#line 530 "parse.y"
          {
            yygotominor.yy0.z = null;
            yygotominor.yy0.n = 0;
          }
          //#line 2650 "parse.c"
          break;
        case 139: /* fullname ::= nm dbnm */
          //#line 535 "parse.y"
          {
            yygotominor.yy259 = sqlite3SrcListAppend( pParse.db, 0, yymsp[-1].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2655 "parse.c"
          break;
        case 140: /* joinop ::= COMMA|JOIN */
          //#line 539 "parse.y"
          {
            yygotominor.yy4 = JT_INNER;
          }
          //#line 2660 "parse.c"
          break;
        case 141: /* joinop ::= JOIN_KW JOIN */
          //#line 540 "parse.y"
          {
            yygotominor.yy4 = sqlite3JoinType( pParse, yymsp[-1].minor.yy0, 0, 0 );
          }
          //#line 2665 "parse.c"
          break;
        case 142: /* joinop ::= JOIN_KW nm JOIN */
          //#line 541 "parse.y"
          {
            yygotominor.yy4 = sqlite3JoinType( pParse, yymsp[-2].minor.yy0, yymsp[-1].minor.yy0, 0 );
          }
          //#line 2670 "parse.c"
          break;
        case 143: /* joinop ::= JOIN_KW nm nm JOIN */
          //#line 543 "parse.y"
          {
            yygotominor.yy4 = sqlite3JoinType( pParse, yymsp[-3].minor.yy0, yymsp[-2].minor.yy0, yymsp[-1].minor.yy0 );
          }
          //#line 2675 "parse.c"
          break;
        case 144: /* on_opt ::= ON expr */
        case 155: /* sortitem ::= expr */ //yytestcase(yyruleno==155);
        case 162: /* having_opt ::= HAVING expr */ //yytestcase(yyruleno==162);
        case 169: /* where_opt ::= WHERE expr */ //yytestcase(yyruleno==169);
        case 235: /* case_else ::= ELSE expr */ //yytestcase(yyruleno==235);
        case 237: /* case_operand ::= expr */ //yytestcase(yyruleno==237);
          //#line 547 "parse.y"
          {
            yygotominor.yy314 = yymsp[0].minor.yy118.pExpr;
          }
          //#line 2685 "parse.c"
          break;
        case 145: /* on_opt ::= */
        case 161: /* having_opt ::= */ //yytestcase(yyruleno==161);
        case 168: /* where_opt ::= */ //yytestcase(yyruleno==168);
        case 236: /* case_else ::= */ //yytestcase(yyruleno==236);
        case 238: /* case_operand ::= */ //yytestcase(yyruleno==238);
          //#line 548 "parse.y"
          {
            yygotominor.yy314 = null;
          }
          //#line 2694 "parse.c"
          break;
        case 148: /* indexed_opt ::= NOT INDEXED */
          //#line 563 "parse.y"
          {
            yygotominor.yy0.z = null;
            yygotominor.yy0.n = 1;
          }
          //#line 2699 "parse.c"
          break;
        case 149: /* using_opt ::= USING LP inscollist RP */
        case 181: /* inscollist_opt ::= LP inscollist RP */ //yytestcase(yyruleno==181);
          //#line 567 "parse.y"
          {
            yygotominor.yy384 = yymsp[-1].minor.yy384;
          }
          //#line 2705 "parse.c"
          break;
        case 150: /* using_opt ::= */
        case 180: /* inscollist_opt ::= */ //yytestcase(yyruleno==180);
          //#line 568 "parse.y"
          {
            yygotominor.yy384 = null;
          }
          //#line 2711 "parse.c"
          break;
        case 152: /* orderby_opt ::= ORDER BY sortlist */
        case 160: /* groupby_opt ::= GROUP BY nexprlist */ //yytestcase(yyruleno==160);
        case 239: /* exprlist ::= nexprlist */ //yytestcase(yyruleno==239);
          //#line 579 "parse.y"
          {
            yygotominor.yy322 = yymsp[0].minor.yy322;
          }
          //#line 2718 "parse.c"
          break;
        case 153: /* sortlist ::= sortlist COMMA sortitem sortorder */
          //#line 580 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-3].minor.yy322, yymsp[-1].minor.yy314 );
            if ( yygotominor.yy322 != null )
              yygotominor.yy322.a[yygotominor.yy322.nExpr - 1].sortOrder = (u8)yymsp[0].minor.yy4;
          }
          //#line 2726 "parse.c"
          break;
        case 154: /* sortlist ::= sortitem sortorder */
          //#line 584 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, 0, yymsp[-1].minor.yy314 );
            if ( yygotominor.yy322 != null && ALWAYS( yygotominor.yy322.a != null ) )
              yygotominor.yy322.a[0].sortOrder = (u8)yymsp[0].minor.yy4;
          }
          //#line 2734 "parse.c"
          break;
        case 156: /* sortorder ::= ASC */
        case 158: /* sortorder ::= */ //yytestcase(yyruleno==158);
          //#line 592 "parse.y"
          {
            yygotominor.yy4 = SQLITE_SO_ASC;
          }
          //#line 2740 "parse.c"
          break;
        case 157: /* sortorder ::= DESC */
          //#line 593 "parse.y"
          {
            yygotominor.yy4 = SQLITE_SO_DESC;
          }
          //#line 2745 "parse.c"
          break;
        case 163: /* limit_opt ::= */
          //#line 619 "parse.y"
          {
            yygotominor.yy292.pLimit = null;
            yygotominor.yy292.pOffset = null;
          }
          //#line 2750 "parse.c"
          break;
        case 164: /* limit_opt ::= LIMIT expr */
          //#line 620 "parse.y"
          {
            yygotominor.yy292.pLimit = yymsp[0].minor.yy118.pExpr;
            yygotominor.yy292.pOffset = null;
          }
          //#line 2755 "parse.c"
          break;
        case 165: /* limit_opt ::= LIMIT expr OFFSET expr */
          //#line 622 "parse.y"
          {
            yygotominor.yy292.pLimit = yymsp[-2].minor.yy118.pExpr;
            yygotominor.yy292.pOffset = yymsp[0].minor.yy118.pExpr;
          }
          //#line 2760 "parse.c"
          break;
        case 166: /* limit_opt ::= LIMIT expr COMMA expr */
          //#line 624 "parse.y"
          {
            yygotominor.yy292.pOffset = yymsp[-2].minor.yy118.pExpr;
            yygotominor.yy292.pLimit = yymsp[0].minor.yy118.pExpr;
          }
          //#line 2765 "parse.c"
          break;
        case 167: /* cmd ::= DELETE FROM fullname indexed_opt where_opt */
          //#line 637 "parse.y"
          {
            sqlite3SrcListIndexedBy( pParse, yymsp[-2].minor.yy259, yymsp[-1].minor.yy0 );
            sqlite3DeleteFrom( pParse, yymsp[-2].minor.yy259, yymsp[0].minor.yy314 );
          }
          //#line 2773 "parse.c"
          break;
        case 170: /* cmd ::= UPDATE orconf fullname indexed_opt SET setlist where_opt */
          //#line 660 "parse.y"
          {
            sqlite3SrcListIndexedBy( pParse, yymsp[-4].minor.yy259, yymsp[-3].minor.yy0 );
            sqlite3ExprListCheckLength( pParse, yymsp[-1].minor.yy322, "set list" );
            sqlite3Update( pParse, yymsp[-4].minor.yy259, yymsp[-1].minor.yy322, yymsp[0].minor.yy314, yymsp[-5].minor.yy210 );
          }
          //#line 2782 "parse.c"
          break;
        case 171: /* setlist ::= setlist COMMA nm EQ expr */
          //#line 670 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-4].minor.yy322, yymsp[0].minor.yy118.pExpr );
            sqlite3ExprListSetName( pParse, yygotominor.yy322, yymsp[-2].minor.yy0, 1 );
          }
          //#line 2790 "parse.c"
          break;
        case 172: /* setlist ::= nm EQ expr */
          //#line 674 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, 0, yymsp[0].minor.yy118.pExpr );
            sqlite3ExprListSetName( pParse, yygotominor.yy322, yymsp[-2].minor.yy0, 1 );
          }
          //#line 2798 "parse.c"
          break;
        case 173: /* cmd ::= insert_cmd INTO fullname inscollist_opt VALUES LP itemlist RP */
          //#line 683 "parse.y"
          {
            sqlite3Insert( pParse, yymsp[-5].minor.yy259, yymsp[-1].minor.yy322, 0, yymsp[-4].minor.yy384, yymsp[-7].minor.yy210 );
          }
          //#line 2803 "parse.c"
          break;
        case 174: /* cmd ::= insert_cmd INTO fullname inscollist_opt select */
          //#line 685 "parse.y"
          {
            sqlite3Insert( pParse, yymsp[-2].minor.yy259, 0, yymsp[0].minor.yy387, yymsp[-1].minor.yy384, yymsp[-4].minor.yy210 );
          }
          //#line 2808 "parse.c"
          break;
        case 175: /* cmd ::= insert_cmd INTO fullname inscollist_opt DEFAULT VALUES */
          //#line 687 "parse.y"
          {
            sqlite3Insert( pParse, yymsp[-3].minor.yy259, 0, 0, yymsp[-2].minor.yy384, yymsp[-5].minor.yy210 );
          }
          //#line 2813 "parse.c"
          break;
        case 176: /* insert_cmd ::= INSERT orconf */
          //#line 690 "parse.y"
          {
            yygotominor.yy210 = yymsp[0].minor.yy210;
          }
          //#line 2818 "parse.c"
          break;
        case 177: /* insert_cmd ::= REPLACE */
          //#line 691 "parse.y"
          {
            yygotominor.yy210 = OE_Replace;
          }
          //#line 2823 "parse.c"
          break;
        case 178: /* itemlist ::= itemlist COMMA expr */
        case 241: /* nexprlist ::= nexprlist COMMA expr */ //yytestcase(yyruleno==241);
          //#line 698 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-2].minor.yy322, yymsp[0].minor.yy118.pExpr );
          }
          //#line 2829 "parse.c"
          break;
        case 179: /* itemlist ::= expr */
        case 242: /* nexprlist ::= expr */ //yytestcase(yyruleno==242);
          //#line 700 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, 0, yymsp[0].minor.yy118.pExpr );
          }
          //#line 2835 "parse.c"
          break;
        case 182: /* inscollist ::= inscollist COMMA nm */
          //#line 710 "parse.y"
          {
            yygotominor.yy384 = sqlite3IdListAppend( pParse.db, yymsp[-2].minor.yy384, yymsp[0].minor.yy0 );
          }
          //#line 2840 "parse.c"
          break;
        case 183: /* inscollist ::= nm */
          //#line 712 "parse.y"
          {
            yygotominor.yy384 = sqlite3IdListAppend( pParse.db, 0, yymsp[0].minor.yy0 );
          }
          //#line 2845 "parse.c"
          break;
        case 184: /* expr ::= term */
          //#line 743 "parse.y"
          {
            yygotominor.yy118 = yymsp[0].minor.yy118;
          }
          //#line 2850 "parse.c"
          break;
        case 185: /* expr ::= LP expr RP */
          //#line 744 "parse.y"
          {
            yygotominor.yy118.pExpr = yymsp[-1].minor.yy118.pExpr;
            spanSet( yygotominor.yy118, yymsp[-2].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2855 "parse.c"
          break;
        case 186: /* term ::= NULL */
        case 191: /* term ::= INTEGER|FLOAT|BLOB */ //yytestcase(yyruleno==191);
        case 192: /* term ::= STRING */ //yytestcase(yyruleno==192);
          //#line 745 "parse.y"
          {
            spanExpr( yygotominor.yy118, pParse, yymsp[0].major, yymsp[0].minor.yy0 );
          }
          //#line 2862 "parse.c"
          break;
        case 187: /* expr ::= id */
        case 188: /* expr ::= JOIN_KW */ //yytestcase(yyruleno==188);
          //#line 746 "parse.y"
          {
            spanExpr( yygotominor.yy118, pParse, TK_ID, yymsp[0].minor.yy0 );
          }
          //#line 2868 "parse.c"
          break;
        case 189: /* expr ::= nm DOT nm */
          //#line 748 "parse.y"
          {
            Expr temp1 = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[-2].minor.yy0 );
            Expr temp2 = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[0].minor.yy0 );
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_DOT, temp1, temp2, 0 );
            spanSet( yygotominor.yy118, yymsp[-2].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2878 "parse.c"
          break;
        case 190: /* expr ::= nm DOT nm DOT nm */
          //#line 754 "parse.y"
          {
            Expr temp1 = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[-4].minor.yy0 );
            Expr temp2 = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[-2].minor.yy0 );
            Expr temp3 = sqlite3PExpr( pParse, TK_ID, 0, 0, yymsp[0].minor.yy0 );
            Expr temp4 = sqlite3PExpr( pParse, TK_DOT, temp2, temp3, 0 );
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_DOT, temp1, temp4, 0 );
            spanSet( yygotominor.yy118, yymsp[-4].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2890 "parse.c"
          break;
        case 193: /* expr ::= REGISTER */
          //#line 764 "parse.y"
          {
            /* When doing a nested parse, one can include terms in an expression
            ** that look like this:   #1 #2 ...  These terms refer to registers
            ** in the virtual machine.  #N is the N-th register. */
            if ( pParse.nested == 0 )
            {
              sqlite3ErrorMsg( pParse, "near \"%T\": syntax error", yymsp[0].minor.yy0 );
              yygotominor.yy118.pExpr = null;
            }
            else
            {
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_REGISTER, 0, 0, yymsp[0].minor.yy0 );
              if ( yygotominor.yy118.pExpr != null )
                sqlite3GetInt32( yymsp[0].minor.yy0.z, 1, ref yygotominor.yy118.pExpr.iTable );
            }
            spanSet( yygotominor.yy118, yymsp[0].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2907 "parse.c"
          break;
        case 194: /* expr ::= VARIABLE */
          //#line 777 "parse.y"
          {
            spanExpr( yygotominor.yy118, pParse, TK_VARIABLE, yymsp[0].minor.yy0 );
            sqlite3ExprAssignVarNumber( pParse, yygotominor.yy118.pExpr );
            spanSet( yygotominor.yy118, yymsp[0].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2916 "parse.c"
          break;
        case 195: /* expr ::= expr COLLATE ids */
          //#line 782 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3ExprSetCollByToken( pParse, yymsp[-2].minor.yy118.pExpr, yymsp[0].minor.yy0 );
            yygotominor.yy118.zStart = yymsp[-2].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 2925 "parse.c"
          break;
        case 196: /* expr ::= CAST LP expr AS typetoken RP */
          //#line 788 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_CAST, yymsp[-3].minor.yy118.pExpr, 0, yymsp[-1].minor.yy0 );
            spanSet( yygotominor.yy118, yymsp[-5].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2933 "parse.c"
          break;
        case 197: /* expr ::= ID LP distinct exprlist RP */
          //#line 793 "parse.y"
          {
            if ( yymsp[-1].minor.yy322 != null && yymsp[-1].minor.yy322.nExpr > pParse.db.aLimit[SQLITE_LIMIT_FUNCTION_ARG] )
            {
              sqlite3ErrorMsg( pParse, "too many arguments on function %T", yymsp[-4].minor.yy0 );
            }
            yygotominor.yy118.pExpr = sqlite3ExprFunction( pParse, yymsp[-1].minor.yy322, yymsp[-4].minor.yy0 );
            spanSet( yygotominor.yy118, yymsp[-4].minor.yy0, yymsp[0].minor.yy0 );
            if ( yymsp[-2].minor.yy4 != 0 && yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.flags |= EP_Distinct;
            }
          }
          //#line 2947 "parse.c"
          break;
        case 198: /* expr ::= ID LP STAR RP */
          //#line 803 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3ExprFunction( pParse, 0, yymsp[-3].minor.yy0 );
            spanSet( yygotominor.yy118, yymsp[-3].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2955 "parse.c"
          break;
        case 199: /* term ::= CTIME_KW */
          //#line 807 "parse.y"
          {
            /* The CURRENT_TIME, CURRENT_DATE, and CURRENT_TIMESTAMP values are
            ** treated as functions that return constants */
            yygotominor.yy118.pExpr = sqlite3ExprFunction( pParse, 0, yymsp[0].minor.yy0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.op = TK_CONST_FUNC;
            }
            spanSet( yygotominor.yy118, yymsp[0].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 2968 "parse.c"
          break;
        case 200: /* expr ::= expr AND expr */
        case 201: /* expr ::= expr OR expr */ //yytestcase(yyruleno==201);
        case 202: /* expr ::= expr LT|GT|GE|LE expr */ //yytestcase(yyruleno==202);
        case 203: /* expr ::= expr EQ|NE expr */ //yytestcase(yyruleno==203);
        case 204: /* expr ::= expr BITAND|BITOR|LSHIFT|RSHIFT expr */ //yytestcase(yyruleno==204);
        case 205: /* expr ::= expr PLUS|MINUS expr */ //yytestcase(yyruleno==205);
        case 206: /* expr ::= expr STAR|SLASH|REM expr */ //yytestcase(yyruleno==206);
        case 207: /* expr ::= expr CONCAT expr */ //yytestcase(yyruleno==207);
          //#line 834 "parse.y"
          {
            spanBinaryExpr( yygotominor.yy118, pParse, yymsp[-1].major, yymsp[-2].minor.yy118, yymsp[0].minor.yy118 );
          }
          //#line 2980 "parse.c"
          break;
        case 208: /* likeop ::= LIKE_KW */
        case 210: /* likeop ::= MATCH */ //yytestcase(yyruleno==210);
          //#line 847 "parse.y"
          {
            yygotominor.yy342.eOperator = yymsp[0].minor.yy0;
            yygotominor.yy342.not = false;
          }
          //#line 2986 "parse.c"
          break;
        case 209: /* likeop ::= NOT LIKE_KW */
        case 211: /* likeop ::= NOT MATCH */ //yytestcase(yyruleno==211);
          //#line 848 "parse.y"
          {
            yygotominor.yy342.eOperator = yymsp[0].minor.yy0;
            yygotominor.yy342.not = true;
          }
          //#line 2992 "parse.c"
          break;
        case 212: /* expr ::= expr likeop expr */
          //#line 851 "parse.y"
          {
            ExprList pList;
            pList = sqlite3ExprListAppend( pParse, 0, yymsp[0].minor.yy118.pExpr );
            pList = sqlite3ExprListAppend( pParse, pList, yymsp[-2].minor.yy118.pExpr );
            yygotominor.yy118.pExpr = sqlite3ExprFunction( pParse, pList, yymsp[-1].minor.yy342.eOperator );
            if ( yymsp[-1].minor.yy342.not )
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            yygotominor.yy118.zStart = yymsp[-2].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy118.zEnd;
            if ( yygotominor.yy118.pExpr != null )
              yygotominor.yy118.pExpr.flags |= EP_InfixFunc;
          }
          //#line 3006 "parse.c"
          break;
        case 213: /* expr ::= expr likeop expr ESCAPE expr */
          //#line 861 "parse.y"
          {
            ExprList pList;
            pList = sqlite3ExprListAppend( pParse, 0, yymsp[-2].minor.yy118.pExpr );
            pList = sqlite3ExprListAppend( pParse, pList, yymsp[-4].minor.yy118.pExpr );
            pList = sqlite3ExprListAppend( pParse, pList, yymsp[0].minor.yy118.pExpr );
            yygotominor.yy118.pExpr = sqlite3ExprFunction( pParse, pList, yymsp[-3].minor.yy342.eOperator );
            if ( yymsp[-3].minor.yy342.not )
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            yygotominor.yy118.zStart = yymsp[-4].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy118.zEnd;
            if ( yygotominor.yy118.pExpr != null )
              yygotominor.yy118.pExpr.flags |= EP_InfixFunc;
          }
          //#line 3021 "parse.c"
          break;
        case 214: /* expr ::= expr ISNULL|NOTNULL */
          //#line 889 "parse.y"
          {
            spanUnaryPostfix( yygotominor.yy118, pParse, yymsp[0].major, yymsp[-1].minor.yy118, yymsp[0].minor.yy0 );
          }
          //#line 3026 "parse.c"
          break;
        case 215: /* expr ::= expr NOT NULL */
          //#line 890 "parse.y"
          {
            spanUnaryPostfix( yygotominor.yy118, pParse, TK_NOTNULL, yymsp[-2].minor.yy118, yymsp[0].minor.yy0 );
          }
          //#line 3031 "parse.c"
          break;
        case 216: /* expr ::= expr IS expr */
          //#line 911 "parse.y"
          {
            spanBinaryExpr( yygotominor.yy118, pParse, TK_IS, yymsp[-2].minor.yy118, yymsp[0].minor.yy118 );
            binaryToUnaryIfNull( pParse, yymsp[0].minor.yy118.pExpr, yygotominor.yy118.pExpr, TK_ISNULL );
          }
          //#line 3039 "parse.c"
          break;
        case 217: /* expr ::= expr IS NOT expr */
          //#line 915 "parse.y"
          {
            spanBinaryExpr( yygotominor.yy118, pParse, TK_ISNOT, yymsp[-3].minor.yy118, yymsp[0].minor.yy118 );
            binaryToUnaryIfNull( pParse, yymsp[0].minor.yy118.pExpr, yygotominor.yy118.pExpr, TK_NOTNULL );
          }
          //#line 3047 "parse.c"
          break;
        case 218: /* expr ::= NOT expr */
        case 219: /* expr ::= BITNOT expr */ //yytestcase(yyruleno==219);
          //#line 938 "parse.y"
          {
            spanUnaryPrefix( yygotominor.yy118, pParse, yymsp[-1].major, yymsp[0].minor.yy118, yymsp[-1].minor.yy0 );
          }
          //#line 3053 "parse.c"
          break;
        case 220: /* expr ::= MINUS expr */
          //#line 941 "parse.y"
          {
            spanUnaryPrefix( yygotominor.yy118, pParse, TK_UMINUS, yymsp[0].minor.yy118, yymsp[-1].minor.yy0 );
          }
          //#line 3058 "parse.c"
          break;
        case 221: /* expr ::= PLUS expr */
          //#line 943 "parse.y"
          {
            spanUnaryPrefix( yygotominor.yy118, pParse, TK_UPLUS, yymsp[0].minor.yy118, yymsp[-1].minor.yy0 );
          }
          //#line 3063 "parse.c"
          break;
        case 224: /* expr ::= expr between_op expr AND expr */
          //#line 948 "parse.y"
          {
            ExprList pList = sqlite3ExprListAppend( pParse, 0, yymsp[-2].minor.yy118.pExpr );
            pList = sqlite3ExprListAppend( pParse, pList, yymsp[0].minor.yy118.pExpr );
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_BETWEEN, yymsp[-4].minor.yy118.pExpr, 0, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.x.pList = pList;
            }
            else
            {
              sqlite3ExprListDelete( pParse.db, ref pList );
            }
            if ( yymsp[-3].minor.yy4 != 0 )
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            yygotominor.yy118.zStart = yymsp[-4].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy118.zEnd;
          }
          //#line 3080 "parse.c"
          break;
        case 227: /* expr ::= expr in_op LP exprlist RP */
          //#line 965 "parse.y"
          {
            if ( yymsp[-1].minor.yy322 == null )
            {
              /* Expressions of the form
              **
              **      expr1 IN ()
              **      expr1 NOT IN ()
              **
              ** simplify to constants 0 (false) and 1 (true), respectively,
              ** regardless of the value of expr1.
              */
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_INTEGER, 0, 0, sqlite3IntTokens[yymsp[-3].minor.yy4] );
              sqlite3ExprDelete( pParse.db, ref yymsp[-4].minor.yy118.pExpr );
            }
            else
            {
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_IN, yymsp[-4].minor.yy118.pExpr, 0, 0 );
              if ( yygotominor.yy118.pExpr != null )
              {
                yygotominor.yy118.pExpr.x.pList = yymsp[-1].minor.yy322;
                sqlite3ExprSetHeight( pParse, yygotominor.yy118.pExpr );
              }
              else
              {
                sqlite3ExprListDelete( pParse.db, ref yymsp[-1].minor.yy322 );
              }
              if ( yymsp[-3].minor.yy4 != 0 )
                yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            }
            yygotominor.yy118.zStart = yymsp[-4].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//[yymsp[0].minor.yy0.n];
          }
          //#line 3109 "parse.c"
          break;
        case 228: /* expr ::= LP select RP */
          //#line 990 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_SELECT, 0, 0, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.x.pSelect = yymsp[-1].minor.yy387;
              ExprSetProperty( yygotominor.yy118.pExpr, EP_xIsSelect );
              sqlite3ExprSetHeight( pParse, yygotominor.yy118.pExpr );
            }
            else
            {
              sqlite3SelectDelete( pParse.db, ref yymsp[-1].minor.yy387 );
            }
            yygotominor.yy118.zStart = yymsp[-2].minor.yy0.z;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 3125 "parse.c"
          break;
        case 229: /* expr ::= expr in_op LP select RP */
          //#line 1002 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_IN, yymsp[-4].minor.yy118.pExpr, 0, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.x.pSelect = yymsp[-1].minor.yy387;
              ExprSetProperty( yygotominor.yy118.pExpr, EP_xIsSelect );
              sqlite3ExprSetHeight( pParse, yygotominor.yy118.pExpr );
            }
            else
            {
              sqlite3SelectDelete( pParse.db, ref yymsp[-1].minor.yy387 );
            }
            if ( yymsp[-3].minor.yy4 != 0 )
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            yygotominor.yy118.zStart = yymsp[-4].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 3142 "parse.c"
          break;
        case 230: /* expr ::= expr in_op nm dbnm */
          //#line 1015 "parse.y"
          {
            SrcList pSrc = sqlite3SrcListAppend( pParse.db, 0, yymsp[-1].minor.yy0, yymsp[0].minor.yy0 );
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_IN, yymsp[-3].minor.yy118.pExpr, 0, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.x.pSelect = sqlite3SelectNew( pParse, 0, pSrc, 0, 0, 0, 0, 0, 0, 0 );
              ExprSetProperty( yygotominor.yy118.pExpr, EP_xIsSelect );
              sqlite3ExprSetHeight( pParse, yygotominor.yy118.pExpr );
            }
            else
            {
              sqlite3SrcListDelete( pParse.db, ref pSrc );
            }
            if ( yymsp[-2].minor.yy4 != 0 )
              yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_NOT, yygotominor.yy118.pExpr, 0, 0 );
            yygotominor.yy118.zStart = yymsp[-3].minor.yy118.zStart;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z != null ? yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n ) : yymsp[-1].minor.yy0.z.Substring( yymsp[-1].minor.yy0.n );
          }
          //#line 3160 "parse.c"
          break;
        case 231: /* expr ::= EXISTS LP select RP */
          //#line 1029 "parse.y"
          {
            Expr p = yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_EXISTS, 0, 0, 0 );
            if ( p != null )
            {
              p.x.pSelect = yymsp[-1].minor.yy387;
              ExprSetProperty( p, EP_xIsSelect );
              sqlite3ExprSetHeight( pParse, p );
            }
            else
            {
              sqlite3SelectDelete( pParse.db, ref yymsp[-1].minor.yy387 );
            }
            yygotominor.yy118.zStart = yymsp[-3].minor.yy0.z;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n ); //z[yymsp[0].minor.yy0.n];
          }
          //#line 3176 "parse.c"
          break;
        case 232: /* expr ::= CASE case_operand case_exprlist case_else END */
          //#line 1044 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_CASE, yymsp[-3].minor.yy314, yymsp[-1].minor.yy314, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.x.pList = yymsp[-2].minor.yy322;
              sqlite3ExprSetHeight( pParse, yygotominor.yy118.pExpr );
            }
            else
            {
              sqlite3ExprListDelete( pParse.db, ref yymsp[-2].minor.yy322 );
            }
            yygotominor.yy118.zStart = yymsp[-4].minor.yy0.z;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 3191 "parse.c"
          break;
        case 233: /* case_exprlist ::= case_exprlist WHEN expr THEN expr */
          //#line 1057 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-4].minor.yy322, yymsp[-2].minor.yy118.pExpr );
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yygotominor.yy322, yymsp[0].minor.yy118.pExpr );
          }
          //#line 3199 "parse.c"
          break;
        case 234: /* case_exprlist ::= WHEN expr THEN expr */
          //#line 1061 "parse.y"
          {
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, 0, yymsp[-2].minor.yy118.pExpr );
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yygotominor.yy322, yymsp[0].minor.yy118.pExpr );
          }
          //#line 3207 "parse.c"
          break;
        case 243: /* cmd ::= createkw uniqueflag INDEX ifnotexists nm dbnm ON nm LP idxlist RP */
          //#line 1090 "parse.y"
          {
            sqlite3CreateIndex( pParse, yymsp[-6].minor.yy0, yymsp[-5].minor.yy0,
            sqlite3SrcListAppend( pParse.db, 0, yymsp[-3].minor.yy0, 0 ), yymsp[-1].minor.yy322, yymsp[-9].minor.yy4,
            yymsp[-10].minor.yy0, yymsp[0].minor.yy0, SQLITE_SO_ASC, yymsp[-7].minor.yy4 );
          }
          //#line 3216 "parse.c"
          break;
        case 244: /* uniqueflag ::= UNIQUE */
        case 298: /* raisetype ::= ABORT */ //yytestcase(yyruleno==298);
          //#line 1097 "parse.y"
          {
            yygotominor.yy4 = OE_Abort;
          }
          //#line 3222 "parse.c"
          break;
        case 245: /* uniqueflag ::= */
          //#line 1098 "parse.y"
          {
            yygotominor.yy4 = OE_None;
          }
          //#line 3227 "parse.c"
          break;
        case 248: /* idxlist ::= idxlist COMMA nm collate sortorder */
          //#line 1107 "parse.y"
          {
            Expr p = null;
            if ( yymsp[-1].minor.yy0.n > 0 )
            {
              p = sqlite3Expr( pParse.db, TK_COLUMN, null );
              sqlite3ExprSetCollByToken( pParse, p, yymsp[-1].minor.yy0 );
            }
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, yymsp[-4].minor.yy322, p );
            sqlite3ExprListSetName( pParse, yygotominor.yy322, yymsp[-2].minor.yy0, 1 );
            sqlite3ExprListCheckLength( pParse, yygotominor.yy322, "index" );
            if ( yygotominor.yy322 != null )
              yygotominor.yy322.a[yygotominor.yy322.nExpr - 1].sortOrder = (u8)yymsp[0].minor.yy4;
          }
          //#line 3242 "parse.c"
          break;
        case 249: /* idxlist ::= nm collate sortorder */
          //#line 1118 "parse.y"
          {
            Expr p = null;
            if ( yymsp[-1].minor.yy0.n > 0 )
            {
              p = sqlite3PExpr( pParse, TK_COLUMN, 0, 0, 0 );
              sqlite3ExprSetCollByToken( pParse, p, yymsp[-1].minor.yy0 );
            }
            yygotominor.yy322 = sqlite3ExprListAppend( pParse, 0, p );
            sqlite3ExprListSetName( pParse, yygotominor.yy322, yymsp[-2].minor.yy0, 1 );
            sqlite3ExprListCheckLength( pParse, yygotominor.yy322, "index" );
            if ( yygotominor.yy322 != null )
              yygotominor.yy322.a[yygotominor.yy322.nExpr - 1].sortOrder = (u8)yymsp[0].minor.yy4;
          }
          //#line 3257 "parse.c"
          break;
        case 250: /* collate ::= */
          //#line 1131 "parse.y"
          {
            yygotominor.yy0.z = null;
            yygotominor.yy0.n = 0;
          }
          //#line 3262 "parse.c"
          break;
        case 252: /* cmd ::= DROP INDEX ifexists fullname */
          //#line 1137 "parse.y"
          {
            sqlite3DropIndex( pParse, yymsp[0].minor.yy259, yymsp[-1].minor.yy4 );
          }
          //#line 3267 "parse.c"
          break;
        case 253: /* cmd ::= VACUUM */
        case 254: /* cmd ::= VACUUM nm */ //yytestcase(yyruleno==254);
          //#line 1143 "parse.y"
          {
            sqlite3Vacuum( pParse );
          }
          //#line 3273 "parse.c"
          break;
        case 255: /* cmd ::= PRAGMA nm dbnm */
          //#line 1151 "parse.y"
          {
            sqlite3Pragma( pParse, yymsp[-1].minor.yy0, yymsp[0].minor.yy0, 0, 0 );
          }
          //#line 3278 "parse.c"
          break;
        case 256: /* cmd ::= PRAGMA nm dbnm EQ nmnum */
          //#line 1152 "parse.y"
          {
            sqlite3Pragma( pParse, yymsp[-3].minor.yy0, yymsp[-2].minor.yy0, yymsp[0].minor.yy0, 0 );
          }
          //#line 3283 "parse.c"
          break;
        case 257: /* cmd ::= PRAGMA nm dbnm LP nmnum RP */
          //#line 1153 "parse.y"
          {
            sqlite3Pragma( pParse, yymsp[-4].minor.yy0, yymsp[-3].minor.yy0, yymsp[-1].minor.yy0, 0 );
          }
          //#line 3288 "parse.c"
          break;
        case 258: /* cmd ::= PRAGMA nm dbnm EQ minus_num */
          //#line 1155 "parse.y"
          {
            sqlite3Pragma( pParse, yymsp[-3].minor.yy0, yymsp[-2].minor.yy0, yymsp[0].minor.yy0, 1 );
          }
          //#line 3293 "parse.c"
          break;
        case 259: /* cmd ::= PRAGMA nm dbnm LP minus_num RP */
          //#line 1157 "parse.y"
          {
            sqlite3Pragma( pParse, yymsp[-4].minor.yy0, yymsp[-3].minor.yy0, yymsp[-1].minor.yy0, 1 );
          }
          //#line 3298 "parse.c"
          break;
        case 270: /* cmd ::= createkw trigger_decl BEGIN trigger_cmd_list END */
          //#line 1175 "parse.y"
          {
            Token all = new Token();
            //all.z = yymsp[-3].minor.yy0.z;
            //all.n = (int)(yymsp[0].minor.yy0.z - yymsp[-3].minor.yy0.z) + yymsp[0].minor.yy0.n;
            all.n = (int)( yymsp[-3].minor.yy0.z.Length - yymsp[0].minor.yy0.z.Length ) + yymsp[0].minor.yy0.n;
            all.z = yymsp[-3].minor.yy0.z.Substring( 0, all.n );
            sqlite3FinishTrigger( pParse, yymsp[-1].minor.yy203, all );
          }
          //#line 3308 "parse.c"
          break;
        case 271: /* trigger_decl ::= temp TRIGGER ifnotexists nm dbnm trigger_time trigger_event ON fullname foreach_clause when_clause */
          //#line 1184 "parse.y"
          {
            sqlite3BeginTrigger( pParse, yymsp[-7].minor.yy0, yymsp[-6].minor.yy0, yymsp[-5].minor.yy4, yymsp[-4].minor.yy90.a, yymsp[-4].minor.yy90.b, yymsp[-2].minor.yy259, yymsp[0].minor.yy314, yymsp[-10].minor.yy4, yymsp[-8].minor.yy4 );
            yygotominor.yy0 = ( yymsp[-6].minor.yy0.n == 0 ? yymsp[-7].minor.yy0 : yymsp[-6].minor.yy0 );
          }
          //#line 3316 "parse.c"
          break;
        case 272: /* trigger_time ::= BEFORE */
        case 275: /* trigger_time ::= */ //yytestcase(yyruleno==275);
          //#line 1190 "parse.y"
          {
            yygotominor.yy4 = TK_BEFORE;
          }
          //#line 3322 "parse.c"
          break;
        case 273: /* trigger_time ::= AFTER */
          //#line 1191 "parse.y"
          {
            yygotominor.yy4 = TK_AFTER;
          }
          //#line 3327 "parse.c"
          break;
        case 274: /* trigger_time ::= INSTEAD OF */
          //#line 1192 "parse.y"
          {
            yygotominor.yy4 = TK_INSTEAD;
          }
          //#line 3332 "parse.c"
          break;
        case 276: /* trigger_event ::= DELETE|INSERT */
        case 277: /* trigger_event ::= UPDATE */ //yytestcase(yyruleno==277);
          //#line 1197 "parse.y"
          {
            yygotominor.yy90.a = yymsp[0].major;
            yygotominor.yy90.b = null;
          }
          //#line 3338 "parse.c"
          break;
        case 278: /* trigger_event ::= UPDATE OF inscollist */
          //#line 1199 "parse.y"
          {
            yygotominor.yy90.a = TK_UPDATE;
            yygotominor.yy90.b = yymsp[0].minor.yy384;
          }
          //#line 3343 "parse.c"
          break;
        case 281: /* when_clause ::= */
        case 303: /* key_opt ::= */ //yytestcase(yyruleno==303);
          //#line 1206 "parse.y"
          {
            yygotominor.yy314 = null;
          }
          //#line 3349 "parse.c"
          break;
        case 282: /* when_clause ::= WHEN expr */
        case 304: /* key_opt ::= KEY expr */ //yytestcase(yyruleno==304);
          //#line 1207 "parse.y"
          {
            yygotominor.yy314 = yymsp[0].minor.yy118.pExpr;
          }
          //#line 3355 "parse.c"
          break;
        case 283: /* trigger_cmd_list ::= trigger_cmd_list trigger_cmd SEMI */
          //#line 1211 "parse.y"
          {
            Debug.Assert( yymsp[-2].minor.yy203 != null );
            yymsp[-2].minor.yy203.pLast.pNext = yymsp[-1].minor.yy203;
            yymsp[-2].minor.yy203.pLast = yymsp[-1].minor.yy203;
            yygotominor.yy203 = yymsp[-2].minor.yy203;
          }
          //#line 3365 "parse.c"
          break;
        case 284: /* trigger_cmd_list ::= trigger_cmd SEMI */
          //#line 1217 "parse.y"
          {
            Debug.Assert( yymsp[-1].minor.yy203 != null );
            yymsp[-1].minor.yy203.pLast = yymsp[-1].minor.yy203;
            yygotominor.yy203 = yymsp[-1].minor.yy203;
          }
          //#line 3374 "parse.c"
          break;
        case 286: /* trnm ::= nm DOT nm */
          //#line 1229 "parse.y"
          {
            yygotominor.yy0 = yymsp[0].minor.yy0;
            sqlite3ErrorMsg( pParse,
            "qualified table names are not allowed on INSERT, UPDATE, and DELETE " +
            "statements within triggers" );
          }
          //#line 3384 "parse.c"
          break;
        case 288: /* tridxby ::= INDEXED BY nm */
          //#line 1241 "parse.y"
          {
            sqlite3ErrorMsg( pParse,
            "the INDEXED BY clause is not allowed on UPDATE or DELETE statements " +
            "within triggers" );
          }
          //#line 3393 "parse.c"
          break;
        case 289: /* tridxby ::= NOT INDEXED */
          //#line 1246 "parse.y"
          {
            sqlite3ErrorMsg( pParse,
            "the NOT INDEXED clause is not allowed on UPDATE or DELETE statements " +
            "within triggers" );
          }
          //#line 3402 "parse.c"
          break;
        case 290: /* trigger_cmd ::= UPDATE orconf trnm tridxby SET setlist where_opt */
          //#line 1259 "parse.y"
          {
            yygotominor.yy203 = sqlite3TriggerUpdateStep( pParse.db, yymsp[-4].minor.yy0, yymsp[-1].minor.yy322, yymsp[0].minor.yy314, yymsp[-5].minor.yy210 );
          }
          //#line 3407 "parse.c"
          break;
        case 291: /* trigger_cmd ::= insert_cmd INTO trnm inscollist_opt VALUES LP itemlist RP */
          //#line 1264 "parse.y"
          {
            yygotominor.yy203 = sqlite3TriggerInsertStep( pParse.db, yymsp[-5].minor.yy0, yymsp[-4].minor.yy384, yymsp[-1].minor.yy322, 0, yymsp[-7].minor.yy210 );
          }
          //#line 3412 "parse.c"
          break;
        case 292: /* trigger_cmd ::= insert_cmd INTO trnm inscollist_opt select */
          //#line 1267 "parse.y"
          {
            yygotominor.yy203 = sqlite3TriggerInsertStep( pParse.db, yymsp[-2].minor.yy0, yymsp[-1].minor.yy384, 0, yymsp[0].minor.yy387, yymsp[-4].minor.yy210 );
          }
          //#line 3417 "parse.c"
          break;
        case 293: /* trigger_cmd ::= DELETE FROM trnm tridxby where_opt */
          //#line 1271 "parse.y"
          {
            yygotominor.yy203 = sqlite3TriggerDeleteStep( pParse.db, yymsp[-2].minor.yy0, yymsp[0].minor.yy314 );
          }
          //#line 3422 "parse.c"
          break;
        case 294: /* trigger_cmd ::= select */
          //#line 1274 "parse.y"
          {
            yygotominor.yy203 = sqlite3TriggerSelectStep( pParse.db, yymsp[0].minor.yy387 );
          }
          //#line 3427 "parse.c"
          break;
        case 295: /* expr ::= RAISE LP IGNORE RP */
          //#line 1277 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_RAISE, 0, 0, 0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.affinity = (char)OE_Ignore;
            }
            yygotominor.yy118.zStart = yymsp[-3].minor.yy0.z;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 3439 "parse.c"
          break;
        case 296: /* expr ::= RAISE LP raisetype COMMA nm RP */
          //#line 1285 "parse.y"
          {
            yygotominor.yy118.pExpr = sqlite3PExpr( pParse, TK_RAISE, 0, 0, yymsp[-1].minor.yy0 );
            if ( yygotominor.yy118.pExpr != null )
            {
              yygotominor.yy118.pExpr.affinity = (char)yymsp[-3].minor.yy4;
            }
            yygotominor.yy118.zStart = yymsp[-5].minor.yy0.z;
            yygotominor.yy118.zEnd = yymsp[0].minor.yy0.z.Substring( yymsp[0].minor.yy0.n );//z[yymsp[0].minor.yy0.n];
          }
          //#line 3451 "parse.c"
          break;
        case 297: /* raisetype ::= ROLLBACK */
          //#line 1296 "parse.y"
          {
            yygotominor.yy4 = OE_Rollback;
          }
          //#line 3456 "parse.c"
          break;
        case 299: /* raisetype ::= FAIL */
          //#line 1298 "parse.y"
          {
            yygotominor.yy4 = OE_Fail;
          }
          //#line 3461 "parse.c"
          break;
        case 300: /* cmd ::= DROP TRIGGER ifexists fullname */
          //#line 1303 "parse.y"
          {
            sqlite3DropTrigger( pParse, yymsp[0].minor.yy259, yymsp[-1].minor.yy4 );
          }
          //#line 3468 "parse.c"
          break;
        case 301: /* cmd ::= ATTACH database_kw_opt expr AS expr key_opt */
          //#line 1310 "parse.y"
          {
            sqlite3Attach( pParse, yymsp[-3].minor.yy118.pExpr, yymsp[-1].minor.yy118.pExpr, yymsp[0].minor.yy314 );
          }
          //#line 3475 "parse.c"
          break;
        case 302: /* cmd ::= DETACH database_kw_opt expr */
          //#line 1313 "parse.y"
          {
            sqlite3Detach( pParse, yymsp[0].minor.yy118.pExpr );
          }
          //#line 3482 "parse.c"
          break;
        case 307: /* cmd ::= REINDEX */
          //#line 1328 "parse.y"
          {
            sqlite3Reindex( pParse, 0, 0 );
          }
          //#line 3487 "parse.c"
          break;
        case 308: /* cmd ::= REINDEX nm dbnm */
          //#line 1329 "parse.y"
          {
            sqlite3Reindex( pParse, yymsp[-1].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 3492 "parse.c"
          break;
        case 309: /* cmd ::= ANALYZE */
          //#line 1334 "parse.y"
          {
            sqlite3Analyze( pParse, 0, 0 );
          }
          //#line 3497 "parse.c"
          break;
        case 310: /* cmd ::= ANALYZE nm dbnm */
          //#line 1335 "parse.y"
          {
            sqlite3Analyze( pParse, yymsp[-1].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 3502 "parse.c"
          break;
        case 311: /* cmd ::= ALTER TABLE fullname RENAME TO nm */
          //#line 1340 "parse.y"
          {
            sqlite3AlterRenameTable( pParse, yymsp[-3].minor.yy259, yymsp[0].minor.yy0 );
          }
          //#line 3509 "parse.c"
          break;
        case 312: /* cmd ::= ALTER TABLE add_column_fullname ADD kwcolumn_opt column */
          //#line 1343 "parse.y"
          {
            sqlite3AlterFinishAddColumn( pParse, yymsp[0].minor.yy0 );
          }
          //#line 3516 "parse.c"
          break;
        case 313: /* add_column_fullname ::= fullname */
          //#line 1346 "parse.y"
          {
            pParse.db.lookaside.bEnabled = 0;
            sqlite3AlterBeginAddColumn( pParse, yymsp[0].minor.yy259 );
          }
          //#line 3524 "parse.c"
          break;
        case 316: /* cmd ::= create_vtab */
          //#line 1356 "parse.y"
          {
            sqlite3VtabFinishParse( pParse, (Token)null );
          }
          //#line 3529 "parse.c"
          break;
        case 317: /* cmd ::= create_vtab LP vtabarglist RP */
          //#line 1357 "parse.y"
          {
            sqlite3VtabFinishParse( pParse, yymsp[0].minor.yy0 );
          }
          //#line 3534 "parse.c"
          break;
        case 318: /* create_vtab ::= createkw VIRTUAL TABLE nm dbnm USING nm */
          //#line 1358 "parse.y"
          {
            sqlite3VtabBeginParse( pParse, yymsp[-3].minor.yy0, yymsp[-2].minor.yy0, yymsp[0].minor.yy0 );
          }
          //#line 3541 "parse.c"
          break;
        case 321: /* vtabarg ::= */
          //#line 1363 "parse.y"
          {
            sqlite3VtabArgInit( pParse );
          }
          //#line 3546 "parse.c"
          break;
        case 323: /* vtabargtoken ::= ANY */
        case 324: /* vtabargtoken ::= lp anylist RP */ //yytestcase(yyruleno==324);
        case 325: /* lp ::= LP */ //yytestcase(yyruleno==325);
          //#line 1365 "parse.y"
          {
            sqlite3VtabArgExtend( pParse, yymsp[0].minor.yy0 );
          }
          //#line 3553 "parse.c"
          break;
        default:
          /* (0) input ::= cmdlist */
          //yytestcase(yyruleno==0);
          /* (1) cmdlist ::= cmdlist ecmd */
          //yytestcase(yyruleno==1);
          /* (2) cmdlist ::= ecmd */
          //yytestcase(yyruleno==2);
          /* (3) ecmd ::= SEMI */
          //yytestcase(yyruleno==3);
          /* (4) ecmd ::= explain cmdx SEMI */
          //yytestcase(yyruleno==4);
          /* (10) trans_opt ::= */
          //yytestcase(yyruleno==10);
          /* (11) trans_opt ::= TRANSACTION */
          //yytestcase(yyruleno==11);
          /* (12) trans_opt ::= TRANSACTION nm */
          //yytestcase(yyruleno==12);
          /* (20) savepoint_opt ::= SAVEPOINT */
          //yytestcase(yyruleno==20);
          /* (21) savepoint_opt ::= */
          //yytestcase(yyruleno==21);
          /* (25) cmd ::= create_table create_table_args */
          //yytestcase(yyruleno==25);
          /* (34) columnlist ::= columnlist COMMA column */
          //yytestcase(yyruleno==34);
          /* (35) columnlist ::= column */
          //yytestcase(yyruleno==35);
          /* (44) type ::= */
          //yytestcase(yyruleno==44);
          /* (51) signed ::= plus_num */
          //yytestcase(yyruleno==51);
          /* (52) signed ::= minus_num */
          //yytestcase(yyruleno==52);
          /* (53) carglist ::= carglist carg */
          //yytestcase(yyruleno==53);
          /* (54) carglist ::= */
          //yytestcase(yyruleno==54);
          /* (55) carg ::= CONSTRAINT nm ccons */
          //yytestcase(yyruleno==55);
          /* (56) carg ::= ccons */
          //yytestcase(yyruleno==56);
          /* (62) ccons ::= NULL onconf */
          //yytestcase(yyruleno==62);
          /* (90) conslist ::= conslist COMMA tcons */
          //yytestcase(yyruleno==90);
          /* (91) conslist ::= conslist tcons */
          //yytestcase(yyruleno==91);
          /* (92) conslist ::= tcons */
          //yytestcase(yyruleno==92);
          /* (93) tcons ::= CONSTRAINT nm */
          //yytestcase(yyruleno==93);
          /* (268) plus_opt ::= PLUS */
          //yytestcase(yyruleno==268);
          /* (269) plus_opt ::= */
          //yytestcase(yyruleno==269);
          /* (279) foreach_clause ::= */
          //yytestcase(yyruleno==279);
          /* (280) foreach_clause ::= FOR EACH ROW */
          //yytestcase(yyruleno==280);
          /* (287) tridxby ::= */
          //yytestcase(yyruleno==287);
          /* (305) database_kw_opt ::= DATABASE */
          //yytestcase(yyruleno==305);
          /* (306) database_kw_opt ::= */
          //yytestcase(yyruleno==306);
          /* (314) kwcolumn_opt ::= */
          //yytestcase(yyruleno==314);
          /* (315) kwcolumn_opt ::= COLUMNKW */
          //yytestcase(yyruleno==315);
          /* (319) vtabarglist ::= vtabarg */
          //yytestcase(yyruleno==319);
          /* (320) vtabarglist ::= vtabarglist COMMA vtabarg */
          //yytestcase(yyruleno==320);
          /* (322) vtabarg ::= vtabarg vtabargtoken */
          //yytestcase(yyruleno==322);
          /* (326) anylist ::= */
          //yytestcase(yyruleno==326);
          /* (327) anylist ::= anylist LP anylist RP */
          //yytestcase(yyruleno==327);
          /* (328) anylist ::= anylist ANY */
          //yytestcase(yyruleno==328);
          break;
      };
      yygoto = yyRuleInfo[yyruleno].lhs;
      yysize = yyRuleInfo[yyruleno].nrhs;
      yypParser.yyidx -= yysize;
      yyact = yy_find_reduce_action( yymsp[-yysize].stateno, (YYCODETYPE)yygoto );
      if ( yyact < YYNSTATE )
      {
#if NDEBUG
/* If we are not debugging and the reduce action popped at least
** one element off the stack, then we can push the new element back
** onto the stack here, and skip the stack overflow test in yy_shift().
** That gives a significant speed improvement. */
if( yysize!=0 ){
yypParser.yyidx++;
yymsp._yyidx -= yysize - 1;
yymsp[0].stateno = (YYACTIONTYPE)yyact;
yymsp[0].major = (YYCODETYPE)yygoto;
yymsp[0].minor = yygotominor;
}else
#endif
        {
          yy_shift( yypParser, yyact, yygoto, yygotominor );
        }
      }
      else
      {
        Debug.Assert( yyact == YYNSTATE + YYNRULE + 1 );
        yy_accept( yypParser );
      }
    }

    /*
    ** The following code executes when the parse fails
    */
#if !YYNOERRORRECOVERY
    static void yy_parse_failed(
    yyParser yypParser           /* The parser */
    )
    {
      Parse pParse = yypParser.pParse; //       sqlite3ParserARG_FETCH;
#if !NDEBUG
      if ( yyTraceFILE != null )
      {
        Debugger.Break(); // TODO --        fprintf(yyTraceFILE, "%sFail!\n", yyTracePrompt);
      }
#endif
      while ( yypParser.yyidx >= 0 )
        yy_pop_parser_stack( yypParser );
      /* Here code is inserted which will be executed whenever the
      ** parser fails */
      yypParser.pParse = pParse;//      sqlite3ParserARG_STORE; /* Suppress warning about unused %extra_argument variable */
    }
#endif //* YYNOERRORRECOVERY */

    /*
** The following code executes when a syntax error first occurs.
*/
    static void yy_syntax_error(
    yyParser yypParser,           /* The parser */
    int yymajor,                   /* The major type of the error token */
    YYMINORTYPE yyminor            /* The minor type of the error token */
    )
    {
      Parse pParse = yypParser.pParse; //       sqlite3ParserARG_FETCH;
      //#define TOKEN (yyminor.yy0)
      //#line 32 "parse.y"

      UNUSED_PARAMETER( yymajor );  /* Silence some compiler warnings */
      Debug.Assert( yyminor.yy0.z.Length > 0 ); //TOKEN.z[0]);  /* The tokenizer always gives us a token */
      sqlite3ErrorMsg( pParse, "near \"%T\": syntax error", yyminor.yy0 );//&TOKEN);
      pParse.parseError = 1;
      //#line 3661 "parse.c"
      yypParser.pParse = pParse; // sqlite3ParserARG_STORE; /* Suppress warning about unused %extra_argument variable */
    }

    /*
    ** The following is executed when the parser accepts
    */
    static void yy_accept(
    yyParser yypParser           /* The parser */
    )
    {
      Parse pParse = yypParser.pParse; //       sqlite3ParserARG_FETCH;
#if !NDEBUG
      if ( yyTraceFILE != null )
      {
        fprintf( yyTraceFILE, "%sAccept!\n", yyTracePrompt );
      }
#endif
      while ( yypParser.yyidx >= 0 )
        yy_pop_parser_stack( yypParser );
      /* Here code is inserted which will be executed whenever the
      ** parser accepts */
      yypParser.pParse = pParse;//      sqlite3ParserARG_STORE; /* Suppress warning about unused %extra_argument variable */
    }

    /* The main parser program.
    ** The first argument is a pointer to a structure obtained from
    ** "sqlite3ParserAlloc" which describes the current state of the parser.
    ** The second argument is the major token number.  The third is
    ** the minor token.  The fourth optional argument is whatever the
    ** user wants (and specified in the grammar) and is available for
    ** use by the action routines.
    **
    ** Inputs:
    ** <ul>
    ** <li> A pointer to the parser (an opaque structure.)
    ** <li> The major token number.
    ** <li> The minor token number.
    ** <li> An option argument of a grammar-specified type.
    ** </ul>
    **
    ** Outputs:
    ** None.
    */
    static void sqlite3Parser(
    yyParser yyp,                   /* The parser */
    int yymajor,                     /* The major token code number */
    sqlite3ParserTOKENTYPE yyminor  /* The value for the token */
    , Parse pParse //sqlite3ParserARG_PDECL           /* Optional %extra_argument parameter */
    )
    {
      YYMINORTYPE yyminorunion = new YYMINORTYPE();
      int yyact;            /* The parser action. */
      bool yyendofinput;     /* True if we are at the end of input */
#if YYERRORSYMBOL
int yyerrorhit = null;   /* True if yymajor has invoked an error */
#endif
      yyParser yypParser;  /* The parser */

      /* (re)initialize the parser, if necessary */
      yypParser = yyp;
      if ( yypParser.yyidx < 0 )
      {
#if YYSTACKDEPTH//<=0
if( yypParser.yystksz <=0 ){
memset(yyminorunion, 0, yyminorunion).Length;
yyStackOverflow(yypParser, yyminorunion);
return;
}
#endif
        yypParser.yyidx = 0;
        yypParser.yyerrcnt = -1;
        yypParser.yystack[0] = new yyStackEntry();
        yypParser.yystack[0].stateno = 0;
        yypParser.yystack[0].major = 0;
      }
      yyminorunion.yy0 = yyminor.Copy();
      yyendofinput = ( yymajor == 0 );
      yypParser.pParse = pParse;//      sqlite3ParserARG_STORE;

#if !NDEBUG
      if ( yyTraceFILE != null )
      {
        fprintf( yyTraceFILE, "%sInput %s\n", yyTracePrompt, yyTokenName[yymajor] );
      }
#endif

      do
      {
        yyact = yy_find_shift_action( yypParser, (YYCODETYPE)yymajor );
        if ( yyact < YYNSTATE )
        {
          Debug.Assert( !yyendofinput );  /* Impossible to shift the $ token */
          yy_shift( yypParser, yyact, yymajor, yyminorunion );
          yypParser.yyerrcnt--;
          yymajor = YYNOCODE;
        }
        else if ( yyact < YYNSTATE + YYNRULE )
        {
          yy_reduce( yypParser, yyact - YYNSTATE );
        }
        else
        {
          Debug.Assert( yyact == YY_ERROR_ACTION );
#if YYERRORSYMBOL
int yymx;
#endif
#if !NDEBUG
          if ( yyTraceFILE != null )
          {
            Debugger.Break(); // TODO --            fprintf(yyTraceFILE, "%sSyntax Error!\n", yyTracePrompt);
          }
#endif
#if YYERRORSYMBOL
/* A syntax error has occurred.
** The response to an error depends upon whether or not the
** grammar defines an error token "ERROR".
**
** This is what we do if the grammar does define ERROR:
**
**  * Call the %syntax_error function.
**
**  * Begin popping the stack until we enter a state where
**    it is legal to shift the error symbol, then shift
**    the error symbol.
**
**  * Set the error count to three.
**
**  * Begin accepting and shifting new tokens.  No new error
**    processing will occur until three tokens have been
**    shifted successfully.
**
*/
if( yypParser.yyerrcnt<0 ){
yy_syntax_error(yypParser,yymajor,yyminorunion);
}
yymx = yypParser.yystack[yypParser.yyidx].major;
if( yymx==YYERRORSYMBOL || yyerrorhit ){
#if !NDEBUG
if( yyTraceFILE ){
Debug.Assert(false); // TODO --                      fprintf(yyTraceFILE,"%sDiscard input token %s\n",
yyTracePrompt,yyTokenName[yymajor]);
}
#endif
yy_destructor(yypParser,(YYCODETYPE)yymajor,yyminorunion);
yymajor = YYNOCODE;
}else{
while(
yypParser.yyidx >= 0 &&
yymx != YYERRORSYMBOL &&
(yyact = yy_find_reduce_action(
yypParser.yystack[yypParser.yyidx].stateno,
YYERRORSYMBOL)) >= YYNSTATE
){
yy_pop_parser_stack(yypParser);
}
if( yypParser.yyidx < 0 || yymajor==0 ){
yy_destructor(yypParser, (YYCODETYPE)yymajor,yyminorunion);
yy_parse_failed(yypParser);
yymajor = YYNOCODE;
}else if( yymx!=YYERRORSYMBOL ){
YYMINORTYPE u2;
u2.YYERRSYMDT = null;
yy_shift(yypParser,yyact,YYERRORSYMBOL,u2);
}
}
yypParser.yyerrcnt = 3;
yyerrorhit = 1;
#elif (YYNOERRORRECOVERY)
/* If the YYNOERRORRECOVERY macro is defined, then do not attempt to
** do any kind of error recovery.  Instead, simply invoke the syntax
** error routine and continue going as if nothing had happened.
**
** Applications can set this macro (for example inside %include) if
** they intend to abandon the parse upon the first syntax error seen.
*/
yy_syntax_error(yypParser,yymajor,yyminorunion);
yy_destructor(yypParser,(YYCODETYPE)yymajor,yyminorunion);
yymajor = YYNOCODE;
#else  // * YYERRORSYMBOL is not defined */
          /* This is what we do if the grammar does not define ERROR:
**
**  * Report an error message, and throw away the input token.
**
**  * If the input token is $, then fail the parse.
**
** As before, subsequent error messages are suppressed until
** three input tokens have been successfully shifted.
*/
          if ( yypParser.yyerrcnt <= 0 )
          {
            yy_syntax_error( yypParser, yymajor, yyminorunion );
          }
          yypParser.yyerrcnt = 3;
          yy_destructor( yypParser, (YYCODETYPE)yymajor, yyminorunion );
          if ( yyendofinput )
          {
            yy_parse_failed( yypParser );
          }
          yymajor = YYNOCODE;
#endif
        }
      } while ( yymajor != YYNOCODE && yypParser.yyidx >= 0 );
      return;
    }
    public class yymsp
    {
      public yyParser _yyParser;
      public int _yyidx;
      // CONSTRUCTOR
      public yymsp( ref yyParser pointer_to_yyParser, int yyidx ) //' Parser and Stack Index
      {
        this._yyParser = pointer_to_yyParser;
        this._yyidx = yyidx;
      }
      // Default Value
      public yyStackEntry this[int offset]
      {
        get
        {
          return _yyParser.yystack[_yyidx + offset];
        }
      }
    }
  }
}
