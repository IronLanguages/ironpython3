namespace Community.CsharpSqlite
{
  using sqlite3_value = Sqlite3.Mem;
  using System;

  public partial class Sqlite3
  {
    /*
    ** 2010 February 23
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    **
    ** This file implements routines used to report what compile-time options
    ** SQLite was built with.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-01-28 17:03:50 ed759d5a9edb3bba5f48f243df47be29e3fe8cd7
    **
    *************************************************************************
    */

#if !SQLITE_OMIT_COMPILEOPTION_DIAGS

    //#include "sqliteInt.h"

    /*
    ** An array of names of all compile-time options.  This array should 
    ** be sorted A-Z.
    **
    ** This array looks large, but in a typical installation actually uses
    ** only a handful of compile-time options, so most times this array is usually
    ** rather short and uses little memory space.
    */
    static string[] azCompileOpt = {

/* These macros are provided to "stringify" the value of the define
** for those options in which the value is meaningful. */
//#define CTIMEOPT_VAL_(opt) #opt
//#define CTIMEOPT_VAL(opt) CTIMEOPT_VAL_(opt)

#if SQLITE_32BIT_ROWID
"32BIT_ROWID",
#endif
#if SQLITE_4_BYTE_ALIGNED_MALLOC
"4_BYTE_ALIGNED_MALLOC",
#endif
#if SQLITE_CASE_SENSITIVE_LIKE
"CASE_SENSITIVE_LIKE",
#endif
#if SQLITE_CHECK_PAGES
"CHECK_PAGES",
#endif
#if SQLITE_COVERAGE_TEST
"COVERAGE_TEST",
#endif
#if SQLITE_DEBUG
"DEBUG",
#endif
#if SQLITE_DEFAULT_LOCKING_MODE
"DEFAULT_LOCKING_MODE=" CTIMEOPT_VAL(SQLITE_DEFAULT_LOCKING_MODE),
#endif
#if SQLITE_DISABLE_DIRSYNC
"DISABLE_DIRSYNC",
#endif
#if SQLITE_DISABLE_LFS
"DISABLE_LFS",
#endif
#if SQLITE_ENABLE_ATOMIC_WRITE
"ENABLE_ATOMIC_WRITE",
#endif
#if SQLITE_ENABLE_CEROD
"ENABLE_CEROD",
#endif
#if SQLITE_ENABLE_COLUMN_METADATA
"ENABLE_COLUMN_METADATA",
#endif
#if SQLITE_ENABLE_EXPENSIVE_ASSERT
"ENABLE_EXPENSIVE_ASSERT",
#endif
#if SQLITE_ENABLE_FTS1
"ENABLE_FTS1",
#endif
#if SQLITE_ENABLE_FTS2
"ENABLE_FTS2",
#endif
#if SQLITE_ENABLE_FTS3
"ENABLE_FTS3",
#endif
#if SQLITE_ENABLE_FTS3_PARENTHESIS
"ENABLE_FTS3_PARENTHESIS",
#endif
#if SQLITE_ENABLE_FTS4
"ENABLE_FTS4",
#endif
#if SQLITE_ENABLE_ICU
"ENABLE_ICU",
#endif
#if SQLITE_ENABLE_IOTRACE
"ENABLE_IOTRACE",
#endif
#if SQLITE_ENABLE_LOAD_EXTENSION
"ENABLE_LOAD_EXTENSION",
#endif
#if SQLITE_ENABLE_LOCKING_STYLE
"ENABLE_LOCKING_STYLE=" CTIMEOPT_VAL(SQLITE_ENABLE_LOCKING_STYLE),
#endif
#if SQLITE_ENABLE_MEMORY_MANAGEMENT
"ENABLE_MEMORY_MANAGEMENT",
#endif
#if SQLITE_ENABLE_MEMSYS3
"ENABLE_MEMSYS3",
#endif
#if SQLITE_ENABLE_MEMSYS5
"ENABLE_MEMSYS5",
#endif
#if SQLITE_ENABLE_OVERSIZE_CELL_CHECK
"ENABLE_OVERSIZE_CELL_CHECK",
#endif
#if SQLITE_ENABLE_RTREE
"ENABLE_RTREE",
#endif
#if SQLITE_ENABLE_STAT2
"ENABLE_STAT2",
#endif
#if SQLITE_ENABLE_UNLOCK_NOTIFY
"ENABLE_UNLOCK_NOTIFY",
#endif
#if SQLITE_ENABLE_UPDATE_DELETE_LIMIT
"ENABLE_UPDATE_DELETE_LIMIT",
#endif
#if SQLITE_HAS_CODEC
"HAS_CODEC",
#endif
#if SQLITE_HAVE_ISNAN
"HAVE_ISNAN",
#endif
#if SQLITE_HOMEGROWN_RECURSIVE_MUTEX
"HOMEGROWN_RECURSIVE_MUTEX",
#endif
#if SQLITE_IGNORE_AFP_LOCK_ERRORS
"IGNORE_AFP_LOCK_ERRORS",
#endif
#if SQLITE_IGNORE_FLOCK_LOCK_ERRORS
"IGNORE_FLOCK_LOCK_ERRORS",
#endif
#if SQLITE_INT64_TYPE
"INT64_TYPE",
#endif
#if SQLITE_LOCK_TRACE
"LOCK_TRACE",
#endif
#if SQLITE_MEMDEBUG
"MEMDEBUG",
#endif
#if SQLITE_MIXED_ENDIAN_64BIT_FLOAT
"MIXED_ENDIAN_64BIT_FLOAT",
#endif
#if SQLITE_NO_SYNC
"NO_SYNC",
#endif
#if SQLITE_OMIT_ALTERTABLE
"OMIT_ALTERTABLE",
#endif
#if SQLITE_OMIT_ANALYZE
"OMIT_ANALYZE",
#endif
#if SQLITE_OMIT_ATTACH
"OMIT_ATTACH",
#endif
#if SQLITE_OMIT_AUTHORIZATION
"OMIT_AUTHORIZATION",
#endif
#if SQLITE_OMIT_AUTOINCREMENT
"OMIT_AUTOINCREMENT",
#endif
#if SQLITE_OMIT_AUTOINIT
"OMIT_AUTOINIT",
#endif
#if SQLITE_OMIT_AUTOMATIC_INDEX
"OMIT_AUTOMATIC_INDEX",
#endif
#if SQLITE_OMIT_AUTORESET
"OMIT_AUTORESET",
#endif
#if SQLITE_OMIT_AUTOVACUUM
"OMIT_AUTOVACUUM",
#endif
#if SQLITE_OMIT_BETWEEN_OPTIMIZATION
"OMIT_BETWEEN_OPTIMIZATION",
#endif
#if SQLITE_OMIT_BLOB_LITERAL
"OMIT_BLOB_LITERAL",
#endif
#if SQLITE_OMIT_BTREECOUNT
"OMIT_BTREECOUNT",
#endif
#if SQLITE_OMIT_BUILTIN_TEST
"OMIT_BUILTIN_TEST",
#endif
#if SQLITE_OMIT_CAST
"OMIT_CAST",
#endif
#if SQLITE_OMIT_CHECK
"OMIT_CHECK",
#endif
/* // redundant
** #if SQLITE_OMIT_COMPILEOPTION_DIAGS
**   "OMIT_COMPILEOPTION_DIAGS",
** #endif
*/
#if SQLITE_OMIT_COMPLETE
"OMIT_COMPLETE",
#endif
#if SQLITE_OMIT_COMPOUND_SELECT
"OMIT_COMPOUND_SELECT",
#endif
#if SQLITE_OMIT_DATETIME_FUNCS
"OMIT_DATETIME_FUNCS",
#endif
#if SQLITE_OMIT_DECLTYPE
"OMIT_DECLTYPE",
#endif
#if SQLITE_OMIT_DEPRECATED
"OMIT_DEPRECATED",
#endif
#if SQLITE_OMIT_DISKIO
"OMIT_DISKIO",
#endif
#if SQLITE_OMIT_EXPLAIN
"OMIT_EXPLAIN",
#endif
#if SQLITE_OMIT_FLAG_PRAGMAS
"OMIT_FLAG_PRAGMAS",
#endif
#if SQLITE_OMIT_FLOATING_POINT
"OMIT_FLOATING_POINT",
#endif
#if SQLITE_OMIT_FOREIGN_KEY
"OMIT_FOREIGN_KEY",
#endif
#if SQLITE_OMIT_GET_TABLE
"OMIT_GET_TABLE",
#endif
#if SQLITE_OMIT_INCRBLOB
"OMIT_INCRBLOB",
#endif
#if SQLITE_OMIT_INTEGRITY_CHECK
"OMIT_INTEGRITY_CHECK",
#endif
#if SQLITE_OMIT_LIKE_OPTIMIZATION
"OMIT_LIKE_OPTIMIZATION",
#endif
#if SQLITE_OMIT_LOAD_EXTENSION
"OMIT_LOAD_EXTENSION",
#endif
#if SQLITE_OMIT_LOCALTIME
"OMIT_LOCALTIME",
#endif
#if SQLITE_OMIT_LOOKASIDE
"OMIT_LOOKASIDE",
#endif
#if SQLITE_OMIT_MEMORYDB
"OMIT_MEMORYDB",
#endif
#if SQLITE_OMIT_OR_OPTIMIZATION
"OMIT_OR_OPTIMIZATION",
#endif
#if SQLITE_OMIT_PAGER_PRAGMAS
"OMIT_PAGER_PRAGMAS",
#endif
#if SQLITE_OMIT_PRAGMA
"OMIT_PRAGMA",
#endif
#if SQLITE_OMIT_PROGRESS_CALLBACK
"OMIT_PROGRESS_CALLBACK",
#endif
#if SQLITE_OMIT_QUICKBALANCE
"OMIT_QUICKBALANCE",
#endif
#if SQLITE_OMIT_REINDEX
"OMIT_REINDEX",
#endif
#if SQLITE_OMIT_SCHEMA_PRAGMAS
"OMIT_SCHEMA_PRAGMAS",
#endif
#if SQLITE_OMIT_SCHEMA_VERSION_PRAGMAS
"OMIT_SCHEMA_VERSION_PRAGMAS",
#endif
#if SQLITE_OMIT_SHARED_CACHE
"OMIT_SHARED_CACHE",
#endif
#if SQLITE_OMIT_SUBQUERY
"OMIT_SUBQUERY",
#endif
#if SQLITE_OMIT_TCL_VARIABLE
"OMIT_TCL_VARIABLE",
#endif
#if SQLITE_OMIT_TEMPDB
"OMIT_TEMPDB",
#endif
#if SQLITE_OMIT_TRACE
"OMIT_TRACE",
#endif
#if SQLITE_OMIT_TRIGGER
"OMIT_TRIGGER",
#endif
#if SQLITE_OMIT_TRUNCATE_OPTIMIZATION
"OMIT_TRUNCATE_OPTIMIZATION",
#endif
#if SQLITE_OMIT_UTF16
"OMIT_UTF16",
#endif
#if SQLITE_OMIT_VACUUM
"OMIT_VACUUM",
#endif
#if SQLITE_OMIT_VIEW
"OMIT_VIEW",
#endif
#if SQLITE_OMIT_VIRTUALTABLE
"OMIT_VIRTUALTABLE",
#endif
#if SQLITE_OMIT_WAL
"OMIT_WAL",
#endif
#if SQLITE_OMIT_WSD
"OMIT_WSD",
#endif
#if SQLITE_OMIT_XFER_OPT
"OMIT_XFER_OPT",
#endif
#if SQLITE_PERFORMANCE_TRACE
"PERFORMANCE_TRACE",
#endif
#if SQLITE_PROXY_DEBUG
"PROXY_DEBUG",
#endif
#if SQLITE_SECURE_DELETE
"SECURE_DELETE",
#endif
#if SQLITE_SMALL_STACK
"SMALL_STACK",
#endif
#if SQLITE_SOUNDEX
"SOUNDEX",
#endif
#if SQLITE_TCL
"TCL",
#endif
//#if SQLITE_TEMP_STORE
"TEMP_STORE=1",//CTIMEOPT_VAL(SQLITE_TEMP_STORE),
//#endif
#if SQLITE_TEST
"TEST",
#endif
#if SQLITE_THREADSAFE
"THREADSAFE=2", // For C#, hardcode to = 2 CTIMEOPT_VAL(SQLITE_THREADSAFE),
#else
"THREADSAFE=0", // For C#, hardcode to = 0
#endif
#if SQLITE_USE_ALLOCA
"USE_ALLOCA",
#endif
#if SQLITE_ZERO_MALLOC
"ZERO_MALLOC"
#endif
};

    /*
    ** Given the name of a compile-time option, return true if that option
    ** was used and false if not.
    **
    ** The name can optionally begin with "SQLITE_" but the "SQLITE_" prefix
    ** is not required for a match.
    */
    static int sqlite3_compileoption_used( string zOptName )
    {
      if ( zOptName.EndsWith( "=" ) )
        return 0;
      int i, n = 0;
      if ( zOptName.StartsWith( "SQLITE_", System.StringComparison.OrdinalIgnoreCase ) )
        n = 7;
      //n = sqlite3Strlen30(zOptName);

      /* Since ArraySize(azCompileOpt) is normally in single digits, a
      ** linear search is adequate.  No need for a binary search. */
      if ( !String.IsNullOrEmpty( zOptName ) )
        for ( i = 0; i < ArraySize( azCompileOpt ); i++ )
        {
          int n1 = ( zOptName.Length-n < azCompileOpt[i].Length ) ? zOptName.Length-n : azCompileOpt[i].Length;
          if ( String.Compare( zOptName, n, azCompileOpt[i], 0, n1, StringComparison.OrdinalIgnoreCase ) == 0 )
            return 1;
        }
      return 0;
    }

    /*
    ** Return the N-th compile-time option string.  If N is out of range,
    ** return a NULL pointer.
    */
    static string sqlite3_compileoption_get( int N )
    {
      if ( N >= 0 && N < ArraySize( azCompileOpt ) )
      {
        return azCompileOpt[N];
      }
      return null;
    }

#endif //* SQLITE_OMIT_COMPILEOPTION_DIAGS */
  }
}
