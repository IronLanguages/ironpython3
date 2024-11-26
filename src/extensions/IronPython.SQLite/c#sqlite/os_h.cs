#define SQLITE_OS_WIN
using u32 = System.UInt32;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2001 September 16
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
    ** This header file (together with is companion C source-code file
    ** "os.c") attempt to abstract the underlying operating system so that
    ** the SQLite library will work on both POSIX and windows systems.
    **
    ** This header file is #include-ed by sqliteInt.h and thus ends up
    ** being included by every source file.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2010-08-23 18:52:01 42537b60566f288167f1b5864a5435986838e3a3
    **
    *************************************************************************
    */
#if !_SQLITE_OS_H_
    //#define _SQLITE_OS_H_

    /*
    ** Figure out if we are dealing with Unix, Windows, or some other
    ** operating system.  After the following block of preprocess macros,
    ** all of SQLITE_OS_UNIX, SQLITE_OS_WIN, SQLITE_OS_OS2, and SQLITE_OS_OTHER
    ** will defined to either 1 or 0.  One of the four will be 1.  The other
    ** three will be 0.
    */
    //#if (SQLITE_OS_OTHER)
    //# if SQLITE_OS_OTHER==1
    //#   undef SQLITE_OS_UNIX
    //#   define SQLITE_OS_UNIX 0
    //#   undef SQLITE_OS_WIN
    //#   define SQLITE_OS_WIN 0
    //#   undef SQLITE_OS_OS2
    //#   define SQLITE_OS_OS2 0
    //# else
    //#   undef SQLITE_OS_OTHER
    //# endif
    //#endif
    //#if !(SQLITE_OS_UNIX) && !SQLITE_OS_OTHER)
    //# define SQLITE_OS_OTHER 0
    //# ifndef SQLITE_OS_WIN
    //#   if defined(_WIN32) || defined(WIN32) || defined(__CYGWIN__) || defined(__MINGW32__) || defined(__BORLANDC__)
    //#     define SQLITE_OS_WIN 1
    //#     define SQLITE_OS_UNIX 0
    //#     define SQLITE_OS_OS2 0
    //#   elif defined(__EMX__) || defined(_OS2) || defined(OS2) || defined(_OS2_) || defined(__OS2__)
    //#     define SQLITE_OS_WIN 0
    //#     define SQLITE_OS_UNIX 0
    //#     define SQLITE_OS_OS2 1
    //#   else
    //#     define SQLITE_OS_WIN 0
    //#     define SQLITE_OS_UNIX 1
    //#     define SQLITE_OS_OS2 0
    //#  endif
    //# else
    //#  define SQLITE_OS_UNIX 0
    //#  define SQLITE_OS_OS2 0
    //# endif
    //#else
    //# ifndef SQLITE_OS_WIN
    //#  define SQLITE_OS_WIN 0
    //# endif
    //#endif

    const bool SQLITE_OS_WIN = true;
    const bool SQLITE_OS_UNIX = false;
    const bool SQLITE_OS_OS2 = false;

    /*
    ** Determine if we are dealing with WindowsCE - which has a much
    ** reduced API.
    */
    //#if (_WIN32_WCE)
    //# define SQLITE_OS_WINCE 1
    //#else
    //# define SQLITE_OS_WINCE 0
    //#endif

    /*
    ** Define the maximum size of a temporary filename
    */
#if SQLITE_OS_WIN
    //# include <windows.h>
    const int MAX_PATH = 260;
    const int SQLITE_TEMPNAME_SIZE = ( MAX_PATH + 50 ); //# define SQLITE_TEMPNAME_SIZE (MAX_PATH+50)
#elif SQLITE_OS_OS2
# if FALSE //(__GNUC__ > 3 || __GNUC__ == 3 && __GNUC_MINOR__ >= 3) && OS2_HIGH_MEMORY)
//#  include <os2safe.h> /* has to be included before os2.h for linking to work */
# endif
//# define INCL_DOSDATETIME
//# define INCL_DOSFILEMGR
//# define INCL_DOSERRORS
//# define INCL_DOSMISC
//# define INCL_DOSPROCESS
//# define INCL_DOSMODULEMGR
//# define INCL_DOSSEMAPHORES
//# include <os2.h>
//# include <uconv.h>
//# define SQLITE_TEMPNAME_SIZE (CCHMAXPATHCOMP)
//#else
//# define SQLITE_TEMPNAME_SIZE 200
#endif

    /* If the SET_FULLSYNC macro is not defined above, then make it
** a no-op
*/
    //#if !SET_FULLSYNC
    //# define SET_FULLSYNC(x,y)
    //#endif

    /*
    ** The default size of a disk sector
    */
#if !SQLITE_DEFAULT_SECTOR_SIZE
    const int SQLITE_DEFAULT_SECTOR_SIZE = 512;//# define SQLITE_DEFAULT_SECTOR_SIZE 512
#endif

    /*
** Temporary files are named starting with this prefix followed by 16 random
** alphanumeric characters, and no file extension. They are stored in the
** OS's standard temporary file directory, and are deleted prior to exit.
** If sqlite is being embedded in another program, you may wish to change the
** prefix to reflect your program's name, so that if your program exits
** prematurely, old temporary files can be easily identified. This can be done
** using -DSQLITE_TEMP_FILE_PREFIX=myprefix_ on the compiler command line.
**
** 2006-10-31:  The default prefix used to be "sqlite_".  But then
** Mcafee started using SQLite in their anti-virus product and it
** started putting files with the "sqlite" name in the c:/temp folder.
** This annoyed many windows users.  Those users would then do a
** Google search for "sqlite", find the telephone numbers of the
** developers and call to wake them up at night and complain.
** For this reason, the default name prefix is changed to be "sqlite"
** spelled backwards.  So the temp files are still identified, but
** anybody smart enough to figure out the code is also likely smart
** enough to know that calling the developer will not help get rid
** of the file.
*/
#if !SQLITE_TEMP_FILE_PREFIX
    const string SQLITE_TEMP_FILE_PREFIX = "etilqs_"; //# define SQLITE_TEMP_FILE_PREFIX "etilqs_"
#endif

    /*
** The following values may be passed as the second argument to
** sqlite3OsLock(). The various locks exhibit the following semantics:
**
** SHARED:    Any number of processes may hold a SHARED lock simultaneously.
** RESERVED:  A single process may hold a RESERVED lock on a file at
**            any time. Other processes may hold and obtain new SHARED locks.
** PENDING:   A single process may hold a PENDING lock on a file at
**            any one time. Existing SHARED locks may persist, but no new
**            SHARED locks may be obtained by other processes.
** EXCLUSIVE: An EXCLUSIVE lock precludes all other locks.
**
** PENDING_LOCK may not be passed directly to sqlite3OsLock(). Instead, a
** process that requests an EXCLUSIVE lock may actually obtain a PENDING
** lock. This can be upgraded to an EXCLUSIVE lock by a subsequent call to
** sqlite3OsLock().
*/
    const int NO_LOCK = 0;
    const int SHARED_LOCK = 1;
    const int RESERVED_LOCK = 2;
    const int PENDING_LOCK = 3;
    const int EXCLUSIVE_LOCK = 4;

    /*
    ** File Locking Notes:  (Mostly about windows but also some info for Unix)
    **
    ** We cannot use LockFileEx() or UnlockFileEx() on Win95/98/ME because
    ** those functions are not available.  So we use only LockFile() and
    ** UnlockFile().
    **
    ** LockFile() prevents not just writing but also reading by other processes.
    ** A SHARED_LOCK is obtained by locking a single randomly-chosen
    ** byte out of a specific range of bytes. The lock byte is obtained at
    ** random so two separate readers can probably access the file at the
    ** same time, unless they are unlucky and choose the same lock byte.
    ** An EXCLUSIVE_LOCK is obtained by locking all bytes in the range.
    ** There can only be one writer.  A RESERVED_LOCK is obtained by locking
    ** a single byte of the file that is designated as the reserved lock byte.
    ** A PENDING_LOCK is obtained by locking a designated byte different from
    ** the RESERVED_LOCK byte.
    **
    ** On WinNT/2K/XP systems, LockFileEx() and UnlockFileEx() are available,
    ** which means we can use reader/writer locks.  When reader/writer locks
    ** are used, the lock is placed on the same range of bytes that is used
    ** for probabilistic locking in Win95/98/ME.  Hence, the locking scheme
    ** will support two or more Win95 readers or two or more WinNT readers.
    ** But a single Win95 reader will lock out all WinNT readers and a single
    ** WinNT reader will lock out all other Win95 readers.
    **
    ** The following #defines specify the range of bytes used for locking.
    ** SHARED_SIZE is the number of bytes available in the pool from which
    ** a random byte is selected for a shared lock.  The pool of bytes for
    ** shared locks begins at SHARED_FIRST.
    **
    ** The same locking strategy and
    ** byte ranges are used for Unix.  This leaves open the possiblity of having
    ** clients on win95, winNT, and unix all talking to the same shared file
    ** and all locking correctly.  To do so would require that samba (or whatever
    ** tool is being used for file sharing) implements locks correctly between
    ** windows and unix.  I'm guessing that isn't likely to happen, but by
    ** using the same locking range we are at least open to the possibility.
    **
    ** Locking in windows is manditory.  For this reason, we cannot store
    ** actual data in the bytes used for locking.  The pager never allocates
    ** the pages involved in locking therefore.  SHARED_SIZE is selected so
    ** that all locks will fit on a single page even at the minimum page size.
    ** PENDING_BYTE defines the beginning of the locks.  By default PENDING_BYTE
    ** is set high so that we don't have to allocate an unused page except
    ** for very large databases.  But one should test the page skipping logic
    ** by setting PENDING_BYTE low and running the entire regression suite.
    **
    ** Changing the value of PENDING_BYTE results in a subtly incompatible
    ** file format.  Depending on how it is changed, you might not notice
    ** the incompatibility right away, even running a full regression test.
    ** The default location of PENDING_BYTE is the first byte past the
    ** 1GB boundary.
    **
    */
#if SQLITE_OMIT_WSD
//# define PENDING_BYTE     (0x40000000)
    static int PENDING_BYTE = 0x40000000; 
#else
    //# define PENDING_BYTE      sqlite3PendingByte
    static int PENDING_BYTE = 0x40000000;
#endif

    static int RESERVED_BYTE = ( PENDING_BYTE + 1 );
    static int SHARED_FIRST = ( PENDING_BYTE + 2 );
    static int SHARED_SIZE = 510;

    /*
    ** Wrapper around OS specific sqlite3_os_init() function.
    */
    //int sqlite3OsInit(void);

    /*
    ** Functions for accessing sqlite3_file methods
    */
    //int sqlite3OsClose(sqlite3_file);
    //int sqlite3OsRead(sqlite3_file*, void*, int amt, i64 offset);
    //int sqlite3OsWrite(sqlite3_file*, const void*, int amt, i64 offset);
    //int sqlite3OsTruncate(sqlite3_file*, i64 size);
    //int sqlite3OsSync(sqlite3_file*, int);
    //int sqlite3OsFileSize(sqlite3_file*, i64 pSize);
    //int sqlite3OsLock(sqlite3_file*, int);
    //int sqlite3OsUnlock(sqlite3_file*, int);
    //int sqlite3OsCheckReservedLock(sqlite3_file *id, int pResOut);
    //int sqlite3OsFileControl(sqlite3_file*,int,void);
    //#define SQLITE_FCNTL_DB_UNCHANGED 0xca093fa0
    const u32 SQLITE_FCNTL_DB_UNCHANGED = 0xca093fa0;

    //int sqlite3OsSectorSize(sqlite3_file *id);
    //int sqlite3OsDeviceCharacteristics(sqlite3_file *id);
    //int sqlite3OsShmMap(sqlite3_file *,int,int,int,object volatile *);
    //int sqlite3OsShmLock(sqlite3_file *id, int, int, int);
    //void sqlite3OsShmBarrier(sqlite3_file *id);
    //int sqlite3OsShmUnmap(sqlite3_file *id, int);

    /*
    ** Functions for accessing sqlite3_vfs methods
    */
    //int sqlite3OsOpen(sqlite3_vfs *, string , sqlite3_file*, int, int );
    //int sqlite3OsDelete(sqlite3_vfs *, string , int);
    //int sqlite3OsAccess(sqlite3_vfs *, string , int, int pResOut);
    //int sqlite3OsFullPathname(sqlite3_vfs *, string , int, char );
#if !SQLITE_OMIT_LOAD_EXTENSION
    //void *sqlite3OsDlOpen(sqlite3_vfs *, string );
    //void sqlite3OsDlError(sqlite3_vfs *, int, char );
    //void (*sqlite3OsDlSym(sqlite3_vfs *, object  *, string ))(void);
    //void sqlite3OsDlClose(sqlite3_vfs *, object  );
#endif
    //int sqlite3OsRandomness(sqlite3_vfs *, int, char );
    //int sqlite3OsSleep(sqlite3_vfs *, int);
    //int sqlite3OsCurrentTimeInt64(sqlite3_vfs *, sqlite3_int64);

    /*
    ** Convenience functions for opening and closing files using
    ** sqlite3Malloc() to obtain space for the file-handle structure.
    */
    //int sqlite3OsOpenMalloc(sqlite3_vfs *, string , sqlite3_file **, int,int);
    //int sqlite3OsCloseFree(sqlite3_file );
#endif // * _SQLITE_OS_H_ */

  }
}
