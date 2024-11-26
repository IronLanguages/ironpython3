/*
*************************************************************************
**  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
**  C#-SQLite is an independent reimplementation of the SQLite software library
**
**  SQLITE_SOURCE_ID: 2010-08-23 18:52:01 42537b60566f288167f1b5864a5435986838e3a3
**
*************************************************************************
*/

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    //#define TK_SEMI                            1
    //#define TK_EXPLAIN                         2
    //#define TK_QUERY                           3
    //#define TK_PLAN                            4
    //#define TK_BEGIN                           5
    //#define TK_TRANSACTION                     6
    //#define TK_DEFERRED                        7
    //#define TK_IMMEDIATE                       8
    //#define TK_EXCLUSIVE                       9
    //#define TK_COMMIT                         10
    //#define TK_END                            11
    //#define TK_ROLLBACK                       12
    //#define TK_SAVEPOINT                      13
    //#define TK_RELEASE                        14
    //#define TK_TO                             15
    //#define TK_TABLE                          16
    //#define TK_CREATE                         17
    //#define TK_IF                             18
    //#define TK_NOT                            19
    //#define TK_EXISTS                         20
    //#define TK_TEMP                           21
    //#define TK_LP                             22
    //#define TK_RP                             23
    //#define TK_AS                             24
    //#define TK_COMMA                          25
    //#define TK_ID                             26
    //#define TK_INDEXED                        27
    //#define TK_ABORT                          28
    //#define TK_ACTION                         29
    //#define TK_AFTER                          30
    //#define TK_ANALYZE                        31
    //#define TK_ASC                            32
    //#define TK_ATTACH                         33
    //#define TK_BEFORE                         34
    //#define TK_BY                             35
    //#define TK_CASCADE                        36
    //#define TK_CAST                           37
    //#define TK_COLUMNKW                       38
    //#define TK_CONFLICT                       39
    //#define TK_DATABASE                       40
    //#define TK_DESC                           41
    //#define TK_DETACH                         42
    //#define TK_EACH                           43
    //#define TK_FAIL                           44
    //#define TK_FOR                            45
    //#define TK_IGNORE                         46
    //#define TK_INITIALLY                      47
    //#define TK_INSTEAD                        48
    //#define TK_LIKE_KW                        49
    //#define TK_MATCH                          50
    //#define TK_NO                             51
    //#define TK_KEY                            52
    //#define TK_OF                             53
    //#define TK_OFFSET                         54
    //#define TK_PRAGMA                         55
    //#define TK_RAISE                          56
    //#define TK_REPLACE                        57
    //#define TK_RESTRICT                       58
    //#define TK_ROW                            59
    //#define TK_TRIGGER                        60
    //#define TK_VACUUM                         61
    //#define TK_VIEW                           62
    //#define TK_VIRTUAL                        63
    //#define TK_REINDEX                        64
    //#define TK_RENAME                         65
    //#define TK_CTIME_KW                       66
    //#define TK_ANY                            67
    //#define TK_OR                             68
    //#define TK_AND                            69
    //#define TK_IS                             70
    //#define TK_BETWEEN                        71
    //#define TK_IN                             72
    //#define TK_ISNULL                         73
    //#define TK_NOTNULL                        74
    //#define TK_NE                             75
    //#define TK_EQ                             76
    //#define TK_GT                             77
    //#define TK_LE                             78
    //#define TK_LT                             79
    //#define TK_GE                             80
    //#define TK_ESCAPE                         81
    //#define TK_BITAND                         82
    //#define TK_BITOR                          83
    //#define TK_LSHIFT                         84
    //#define TK_RSHIFT                         85
    //#define TK_PLUS                           86
    //#define TK_MINUS                          87
    //#define TK_STAR                           88
    //#define TK_SLASH                          89
    //#define TK_REM                            90
    //#define TK_CONCAT                         91
    //#define TK_COLLATE                        92
    //#define TK_BITNOT                         93
    //#define TK_STRING                         94
    //#define TK_JOIN_KW                        95
    //#define TK_CONSTRAINT                     96
    //#define TK_DEFAULT                        97
    //#define TK_NULL                           98
    //#define TK_PRIMARY                        99
    //#define TK_UNIQUE                         100
    //#define TK_CHECK                          101
    //#define TK_REFERENCES                     102
    //#define TK_AUTOINCR                       103
    //#define TK_ON                             104
    //#define TK_INSERT                         105
    //#define TK_DELETE                         106
    //#define TK_UPDATE                         107
    //#define TK_SET                            108
    //#define TK_DEFERRABLE                     109
    //#define TK_FOREIGN                        110
    //#define TK_DROP                           111
    //#define TK_UNION                          112
    //#define TK_ALL                            113
    //#define TK_EXCEPT                         114
    //#define TK_INTERSECT                      115
    //#define TK_SELECT                         116
    //#define TK_DISTINCT                       117
    //#define TK_DOT                            118
    //#define TK_FROM                           119
    //#define TK_JOIN                           120
    //#define TK_USING                          121
    //#define TK_ORDER                          122
    //#define TK_GROUP                          123
    //#define TK_HAVING                         124
    //#define TK_LIMIT                          125
    //#define TK_WHERE                          126
    //#define TK_INTO                           127
    //#define TK_VALUES                         128
    //#define TK_INTEGER                        129
    //#define TK_FLOAT                          130
    //#define TK_BLOB                           131
    //#define TK_REGISTER                       132
    //#define TK_VARIABLE                       133
    //#define TK_CASE                           134
    //#define TK_WHEN                           135
    //#define TK_THEN                           136
    //#define TK_ELSE                           137
    //#define TK_INDEX                          138
    //#define TK_ALTER                          139
    //#define TK_ADD                            140
    //#define TK_TO_TEXT                        141
    //#define TK_TO_BLOB                        142
    //#define TK_TO_NUMERIC                     143
    //#define TK_TO_INT                         144
    //#define TK_TO_REAL                        145
    //#define TK_ISNOT                          146
    //#define TK_END_OF_FILE                    147
    //#define TK_ILLEGAL                        148
    //#define TK_SPACE                          149
    //#define TK_UNCLOSED_STRING                150
    //#define TK_FUNCTION                       151
    //#define TK_COLUMN                         152
    //#define TK_AGG_FUNCTION                   153
    //#define TK_AGG_COLUMN                     154
    //#define TK_CONST_FUNC                     155
    //#define TK_UMINUS                         156
    //#define TK_UPLUS                          157
    public const int TK_SEMI = 1;
    public const int TK_EXPLAIN = 2;
    public const int TK_QUERY = 3;
    public const int TK_PLAN = 4;
    public const int TK_BEGIN = 5;
    public const int TK_TRANSACTION = 6;
    public const int TK_DEFERRED = 7;
    public const int TK_IMMEDIATE = 8;
    public const int TK_EXCLUSIVE = 9;
    public const int TK_COMMIT = 10;
    public const int TK_END = 11;
    public const int TK_ROLLBACK = 12;
    public const int TK_SAVEPOINT = 13;
    public const int TK_RELEASE = 14;
    public const int TK_TO = 15;
    public const int TK_TABLE = 16;
    public const int TK_CREATE = 17;
    public const int TK_IF = 18;
    public const int TK_NOT = 19;
    public const int TK_EXISTS = 20;
    public const int TK_TEMP = 21;
    public const int TK_LP = 22;
    public const int TK_RP = 23;
    public const int TK_AS = 24;
    public const int TK_COMMA = 25;
    public const int TK_ID = 26;
    public const int TK_INDEXED = 27;
    public const int TK_ABORT = 28;
    public const int TK_ACTION = 29;
    public const int TK_AFTER = 30;
    public const int TK_ANALYZE = 31;
    public const int TK_ASC = 32;
    public const int TK_ATTACH = 33;
    public const int TK_BEFORE = 34;
    public const int TK_BY = 35;
    public const int TK_CASCADE = 36;
    public const int TK_CAST = 37;
    public const int TK_COLUMNKW = 38;
    public const int TK_CONFLICT = 39;
    public const int TK_DATABASE = 40;
    public const int TK_DESC = 41;
    public const int TK_DETACH = 42;
    public const int TK_EACH = 43;
    public const int TK_FAIL = 44;
    public const int TK_FOR = 45;
    public const int TK_IGNORE = 46;
    public const int TK_INITIALLY = 47;
    public const int TK_INSTEAD = 48;
    public const int TK_LIKE_KW = 49;
    public const int TK_MATCH = 50;
    public const int TK_NO = 51;
    public const int TK_KEY = 52;
    public const int TK_OF = 53;
    public const int TK_OFFSET = 54;
    public const int TK_PRAGMA = 55;
    public const int TK_RAISE = 56;
    public const int TK_REPLACE = 57;
    public const int TK_RESTRICT = 58;
    public const int TK_ROW = 59;
    public const int TK_TRIGGER = 60;
    public const int TK_VACUUM = 61;
    public const int TK_VIEW = 62;
    public const int TK_VIRTUAL = 63;
    public const int TK_REINDEX = 64;
    public const int TK_RENAME = 65;
    public const int TK_CTIME_KW = 66;
    public const int TK_ANY = 67;
    public const int TK_OR = 68;
    public const int TK_AND = 69;
    public const int TK_IS = 70;
    public const int TK_BETWEEN = 71;
    public const int TK_IN = 72;
    public const int TK_ISNULL = 73;
    public const int TK_NOTNULL = 74;
    public const int TK_NE = 75;
    public const int TK_EQ = 76;
    public const int TK_GT = 77;
    public const int TK_LE = 78;
    public const int TK_LT = 79;
    public const int TK_GE = 80;
    public const int TK_ESCAPE = 81;
    public const int TK_BITAND = 82;
    public const int TK_BITOR = 83;
    public const int TK_LSHIFT = 84;
    public const int TK_RSHIFT = 85;
    public const int TK_PLUS = 86;
    public const int TK_MINUS = 87;
    public const int TK_STAR = 88;
    public const int TK_SLASH = 89;
    public const int TK_REM = 90;
    public const int TK_CONCAT = 91;
    public const int TK_COLLATE = 92;
    public const int TK_BITNOT = 93;
    public const int TK_STRING = 94;
    public const int TK_JOIN_KW = 95;
    public const int TK_CONSTRAINT = 96;
    public const int TK_DEFAULT = 97;
    public const int TK_NULL = 98;
    public const int TK_PRIMARY = 99;
    public const int TK_UNIQUE = 100;
    public const int TK_CHECK = 101;
    public const int TK_REFERENCES = 102;
    public const int TK_AUTOINCR = 103;
    public const int TK_ON = 104;
    public const int TK_INSERT = 105;
    public const int TK_DELETE = 106;
    public const int TK_UPDATE = 107;
    public const int TK_SET = 108;
    public const int TK_DEFERRABLE = 109;
    public const int TK_FOREIGN = 110;
    public const int TK_DROP = 111;
    public const int TK_UNION = 112;
    public const int TK_ALL = 113;
    public const int TK_EXCEPT = 114;
    public const int TK_INTERSECT = 115;
    public const int TK_SELECT = 116;
    public const int TK_DISTINCT = 117;
    public const int TK_DOT = 118;
    public const int TK_FROM = 119;
    public const int TK_JOIN = 120;
    public const int TK_USING = 121;
    public const int TK_ORDER = 122;
    public const int TK_GROUP = 123;
    public const int TK_HAVING = 124;
    public const int TK_LIMIT = 125;
    public const int TK_WHERE = 126;
    public const int TK_INTO = 127;
    public const int TK_VALUES = 128;
    public const int TK_INTEGER = 129;
    public const int TK_FLOAT = 130;
    public const int TK_BLOB = 131;
    public const int TK_REGISTER = 132;
    public const int TK_VARIABLE = 133;
    public const int TK_CASE = 134;
    public const int TK_WHEN = 135;
    public const int TK_THEN = 136;
    public const int TK_ELSE = 137;
    public const int TK_INDEX = 138;
    public const int TK_ALTER = 139;
    public const int TK_ADD = 140;
    public const int TK_TO_TEXT = 141;
    public const int TK_TO_BLOB = 142;
    public const int TK_TO_NUMERIC = 143;
    public const int TK_TO_INT = 144;
    public const int TK_TO_REAL = 145;
    public const int TK_ISNOT = 146;
    public const int TK_END_OF_FILE = 147;
    public const int TK_ILLEGAL = 148;
    public const int TK_SPACE = 149;
    public const int TK_UNCLOSED_STRING = 150;
    public const int TK_FUNCTION = 151;
    public const int TK_COLUMN = 152;
    public const int TK_AGG_FUNCTION = 153;
    public const int TK_AGG_COLUMN = 154;
    public const int TK_CONST_FUNC = 155;
    public const int TK_UMINUS = 156;
    public const int TK_UPLUS = 157;
  }
}
