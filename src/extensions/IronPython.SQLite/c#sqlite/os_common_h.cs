using System.Diagnostics;
using va_list = System.Object;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2004 May 22
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    ******************************************************************************
    **
    ** This file contains macros and a little bit of code that is common to
    ** all of the platform-specific files (os_*.c) and is #included into those
    ** files.
    **
    ** This file should be #included by the os_*.c files only.  It is not a
    ** general purpose header file.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2010-08-23 18:52:01 42537b60566f288167f1b5864a5435986838e3a3
    **
    *************************************************************************
    */
    //#if !_OS_COMMON_H_
    //#define _OS_COMMON_H_
    /*
    ** At least two bugs have slipped in because we changed the MEMORY_DEBUG
    ** macro to SQLITE_DEBUG and some older makefiles have not yet made the
    ** switch.  The following code should catch this problem at compile-time.
    */
#if MEMORY_DEBUG
//# error "The MEMORY_DEBUG macro is obsolete.  Use SQLITE_DEBUG instead."
#endif

#if SQLITE_DEBUG || TRACE
    static bool sqlite3OsTrace = false;
    //#define OSTRACE(X)          if( sqlite3OSTrace ) sqlite3DebugPrintf X
    static void OSTRACE( string X, params va_list[] ap )
    {
      if ( sqlite3OsTrace )
        sqlite3DebugPrintf( X, ap );
    }
#else
    //#define OSTRACE(X)
    static void OSTRACE( string X, params object[] ap) { }
#endif

    /*
** Macros for performance tracing.  Normally turned off.  Only works
** on i486 hardware.
*/
#if SQLITE_PERFORMANCE_TRACE

/*
** hwtime.h contains inline assembler code for implementing
** high-performance timing routines.
*/
//#include "hwtime.h"

static sqlite_u3264 g_start;
static sqlite_u3264 g_elapsed;
//#define TIMER_START       g_start=sqlite3Hwtime()
//#define TIMER_END         g_elapsed=sqlite3Hwtime()-g_start
//#define TIMER_ELAPSED     g_elapsed
#else
    const int TIMER_START = 0;   //#define TIMER_START
    const int TIMER_END = 0;     //#define TIMER_END
    const int TIMER_ELAPSED = 0; //#define TIMER_ELAPSED     ((sqlite_u3264)0)
#endif

    /*
** If we compile with the SQLITE_TEST macro set, then the following block
** of code will give us the ability to simulate a disk I/O error.  This
** is used for testing the I/O recovery logic.
*/
#if SQLITE_TEST

#if !TCLSH
    static int sqlite3_io_error_hit = 0;            /* Total number of I/O Errors */
    static int sqlite3_io_error_hardhit = 0;        /* Number of non-benign errors */
    static int sqlite3_io_error_pending = 0;        /* Count down to first I/O error */
    static int sqlite3_io_error_persist = 0;        /* True if I/O errors persist */
    static int sqlite3_io_error_benign = 0;         /* True if errors are benign */
    static int sqlite3_diskfull_pending = 0;
    static int sqlite3_diskfull = 0;
#else
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_io_error_hit = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_io_error_hit" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_io_error_hardhit = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_io_error_hardhit" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_io_error_pending = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_io_error_pending" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_io_error_persist = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_io_error_persist" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_io_error_benign = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_io_error_benign" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_diskfull_pending = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_diskfull_pending" );
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_diskfull = new tcl.lang.Var.SQLITE3_GETSET( "sqlite_diskfull" );
#endif
    static void SimulateIOErrorBenign( int X )
    {
#if !TCLSH
      sqlite3_io_error_benign = ( X );
#else
      sqlite3_io_error_benign.iValue = ( X );
#endif
    }
    //#define SimulateIOError(CODE)  \
    //  if( (sqlite3_io_error_persist && sqlite3_io_error_hit) \
    //       || sqlite3_io_error_pending-- == 1 )  \
    //              { local_ioerr(); CODE; }
    static bool SimulateIOError()
    {
#if !TCLSH
      if ( ( sqlite3_io_error_persist != 0 && sqlite3_io_error_hit != 0 )
      || sqlite3_io_error_pending-- == 1 )
#else
      if ( ( sqlite3_io_error_persist.iValue != 0 && sqlite3_io_error_hit.iValue != 0 )
      || sqlite3_io_error_pending.iValue-- == 1 )
#endif
      {
        local_ioerr();
        return true;
      }
      return false;
    }

    static void local_ioerr()
    {
#if TRACE
      IOTRACE( "IOERR\n" );
#endif
#if !TCLSH
      sqlite3_io_error_hit++;
      if ( sqlite3_io_error_benign == 0 )
        sqlite3_io_error_hardhit++;
#else
      sqlite3_io_error_hit.iValue++;
      if ( sqlite3_io_error_benign.iValue == 0 )
        sqlite3_io_error_hardhit.iValue++;
#endif
    }
    //#define SimulateDiskfullError(CODE) \
    //   if( sqlite3_diskfull_pending ){ \
    //     if( sqlite3_diskfull_pending == 1 ){ \
    //       local_ioerr(); \
    //       sqlite3_diskfull = 1; \
    //       sqlite3_io_error_hit = 1; \
    //       CODE; \
    //     }else{ \
    //       sqlite3_diskfull_pending--; \
    //     } \
    //   }
    static bool SimulateDiskfullError()
    {
#if !TCLSH
      if ( sqlite3_diskfull_pending != 0 )
      {
        if ( sqlite3_diskfull_pending == 1 )
        {
#else
      if ( sqlite3_diskfull_pending.iValue != 0 )
      {
        if ( sqlite3_diskfull_pending.iValue == 1 )
        {
#endif
          local_ioerr();
#if !TCLSH
          sqlite3_diskfull = 1;
          sqlite3_io_error_hit = 1;
#else
          sqlite3_diskfull.iValue = 1;
          sqlite3_io_error_hit.iValue = 1;
#endif
          return true;
        }
        else
        {
#if !TCLSH
          sqlite3_diskfull_pending--;
#else
          sqlite3_diskfull_pending.iValue--;
#endif
        }
      }
      return false;
    }
#else
    static bool SimulateIOError() { return false; }
//#define SimulateIOErrorBenign(X)
    static void SimulateIOErrorBenign( int x ) { }

//#define SimulateIOError(A)
//#define SimulateDiskfullError(A)
#endif

    /*
** When testing, keep a count of the number of open files.
*/
#if SQLITE_TEST
#if !TCLSH
    static int sqlite3_open_file_count = 0;
#else
    static tcl.lang.Var.SQLITE3_GETSET sqlite3_open_file_count = new tcl.lang.Var.SQLITE3_GETSET( "sqlite3_open_file_count" );
#endif

    static void OpenCounter( int X )
    {
#if !TCLSH
      sqlite3_open_file_count += ( X );
#else
      sqlite3_open_file_count.iValue += ( X );
#endif
    }
#else
//#define OpenCounter(X)
#endif
    //#endif //* !_OS_COMMON_H_) */
  }
}
