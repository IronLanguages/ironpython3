using System.Diagnostics;
using System.Text;

using HANDLE = System.IntPtr;
using i64 = System.Int64;
using u32 = System.UInt32;
using sqlite3_int64 = System.Int64;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {

    /*
    ** 2005 November 29
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
    ** This file contains OS interface code that is common to all
    ** architectures.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2010-12-07 20:14:09 a586a4deeb25330037a49df295b36aaf624d0f45
    **
    *************************************************************************
    */
    //#define _SQLITE_OS_C_ 1
    //#include "sqliteInt.h"
    //#undef _SQLITE_OS_C_

    /*
    ** The default SQLite sqlite3_vfs implementations do not allocate
    ** memory (actually, os_unix.c allocates a small amount of memory
    ** from within OsOpen()), but some third-party implementations may.
    ** So we test the effects of a malloc() failing and the sqlite3OsXXX()
    ** function returning SQLITE_IOERR_NOMEM using the DO_OS_MALLOC_TEST macro.
    **
    ** The following functions are instrumented for malloc() failure
    ** testing:
    **
    **     sqlite3OsOpen()
    **     sqlite3OsRead()
    **     sqlite3OsWrite()
    **     sqlite3OsSync()
    **     sqlite3OsLock()
    **
    */
#if (SQLITE_TEST)
    static int sqlite3_memdebug_vfs_oom_test = 1;

    //#define DO_OS_MALLOC_TEST(x)                                       \
    //if (sqlite3_memdebug_vfs_oom_test && (!x || !sqlite3IsMemJournal(x))) {  \
    //  void *pTstAlloc = sqlite3Malloc(10);                             \
    //  if (!pTstAlloc) return SQLITE_IOERR_NOMEM;                       \
    //  sqlite3_free(pTstAlloc);                                         \
    //}
    static void DO_OS_MALLOC_TEST( sqlite3_file x )
    {
    }
#else
    //#define DO_OS_MALLOC_TEST(x)
    static void DO_OS_MALLOC_TEST( sqlite3_file x ) { }
#endif


    /*
** The following routines are convenience wrappers around methods
** of the sqlite3_file object.  This is mostly just syntactic sugar. All
** of this would be completely automatic if SQLite were coded using
** C++ instead of plain old C.
*/
    static int sqlite3OsClose( sqlite3_file pId )
    {
      int rc = SQLITE_OK;
      if ( pId.pMethods != null )
      {
        rc = pId.pMethods.xClose( pId );
        pId.pMethods = null;
      }
      return rc;
    }
    static int sqlite3OsRead( sqlite3_file id, byte[] pBuf, int amt, i64 offset )
    {
      DO_OS_MALLOC_TEST( id );
      if ( pBuf == null )
        pBuf = sqlite3Malloc( amt );
      return id.pMethods.xRead( id, pBuf, amt, offset );
    }
    static int sqlite3OsWrite( sqlite3_file id, byte[] pBuf, int amt, i64 offset )
    {
      DO_OS_MALLOC_TEST( id );
      return id.pMethods.xWrite( id, pBuf, amt, offset );
    }
    static int sqlite3OsTruncate( sqlite3_file id, i64 size )
    {
      return id.pMethods.xTruncate( id, size );
    }
    static int sqlite3OsSync( sqlite3_file id, int flags )
    {
      DO_OS_MALLOC_TEST( id );
      return id.pMethods.xSync( id, flags );
    }
    static int sqlite3OsFileSize( sqlite3_file id, ref long pSize )
    {
      return id.pMethods.xFileSize( id, ref pSize );
    }
    static int sqlite3OsLock( sqlite3_file id, int lockType )
    {
      DO_OS_MALLOC_TEST( id );
      return id.pMethods.xLock( id, lockType );
    }
    static int sqlite3OsUnlock( sqlite3_file id, int lockType )
    {
      return id.pMethods.xUnlock( id, lockType );
    }
    static int sqlite3OsCheckReservedLock( sqlite3_file id, ref int pResOut )
    {
      DO_OS_MALLOC_TEST( id );
      return id.pMethods.xCheckReservedLock( id, ref pResOut );
    }
    static int sqlite3OsFileControl( sqlite3_file id, u32 op, ref sqlite3_int64 pArg )
    {
      return id.pMethods.xFileControl( id, (int)op, ref pArg );
    }

    static int sqlite3OsSectorSize( sqlite3_file id )
    {
      dxSectorSize xSectorSize = id.pMethods.xSectorSize;
      return ( xSectorSize != null ? xSectorSize( id ) : SQLITE_DEFAULT_SECTOR_SIZE );
    }
    static int sqlite3OsDeviceCharacteristics( sqlite3_file id )
    {
      return id.pMethods.xDeviceCharacteristics( id );
    }

    static int sqlite3OsShmLock( sqlite3_file id, int offset, int n, int flags )
    {
      return id.pMethods.xShmLock( id, offset, n, flags );
    }
    static void sqlite3OsShmBarrier( sqlite3_file id )
    {
      id.pMethods.xShmBarrier( id );
    }

    static int sqlite3OsShmUnmap( sqlite3_file id, int deleteFlag )
    {
      return id.pMethods.xShmUnmap( id, deleteFlag );
    }
    static int sqlite3OsShmMap(
sqlite3_file id,              /* Database file handle */
int iPage,
int pgsz,
int bExtend,                  /* True to extend file if necessary */
out object pp                 /* OUT: Pointer to mapping */
)
    {
      return id.pMethods.xShmMap( id, iPage, pgsz, bExtend, out pp );
    }

    /*
    ** The next group of routines are convenience wrappers around the
    ** VFS methods.
    */
    static int sqlite3OsOpen(
    sqlite3_vfs pVfs,
    string zPath,
    sqlite3_file pFile,
    int flags,
    ref int pFlagsOut
    )
    {
      int rc;
      DO_OS_MALLOC_TEST( null );
      /* 0x87f3f is a mask of SQLITE_OPEN_ flags that are valid to be passed
      ** down into the VFS layer.  Some SQLITE_OPEN_ flags (for example,
      ** SQLITE_OPEN_FULLMUTEX or SQLITE_OPEN_SHAREDCACHE) are blocked before
      ** reaching the VFS. */
      rc = pVfs.xOpen( pVfs, zPath, pFile, flags & 0x87f3f, out pFlagsOut );
      Debug.Assert( rc == SQLITE_OK || pFile.pMethods == null );
      return rc;
    }
    static int sqlite3OsDelete( sqlite3_vfs pVfs, string zPath, int dirSync )
    {
      return pVfs.xDelete( pVfs, zPath, dirSync );
    }
    static int sqlite3OsAccess( sqlite3_vfs pVfs, string zPath, int flags, ref int pResOut )
    {
      DO_OS_MALLOC_TEST( null );
      return pVfs.xAccess( pVfs, zPath, flags, out pResOut );
    }
    static int sqlite3OsFullPathname(
    sqlite3_vfs pVfs,
    string zPath,
    int nPathOut,
    StringBuilder zPathOut
    )
    {
      zPathOut.Length = 0;//zPathOut[0] = 0;
      return pVfs.xFullPathname( pVfs, zPath, nPathOut, zPathOut );
    }
#if !SQLITE_OMIT_LOAD_EXTENSION
    static HANDLE sqlite3OsDlOpen( sqlite3_vfs pVfs, string zPath )
    {
      return pVfs.xDlOpen( pVfs, zPath );
    }

    static void sqlite3OsDlError( sqlite3_vfs pVfs, int nByte, string zBufOut )
    {
      pVfs.xDlError( pVfs, nByte, zBufOut );
    }
    static object sqlite3OsDlSym( sqlite3_vfs pVfs, HANDLE pHdle, ref string zSym )
    {
      return pVfs.xDlSym( pVfs, pHdle, zSym );
    }
    static void sqlite3OsDlClose( sqlite3_vfs pVfs, HANDLE pHandle )
    {
      pVfs.xDlClose( pVfs, pHandle );
    }
#endif
    static int sqlite3OsRandomness( sqlite3_vfs pVfs, int nByte, byte[] zBufOut )
    {
      return pVfs.xRandomness( pVfs, nByte, zBufOut );
    }
    static int sqlite3OsSleep( sqlite3_vfs pVfs, int nMicro )
    {
      return pVfs.xSleep( pVfs, nMicro );
    }

    static int sqlite3OsCurrentTimeInt64( sqlite3_vfs pVfs, ref sqlite3_int64 pTimeOut )
    {
      int rc;
      /* IMPLEMENTATION-OF: R-49045-42493 SQLite will use the xCurrentTimeInt64()
      ** method to get the current date and time if that method is available
      ** (if iVersion is 2 or greater and the function pointer is not NULL) and
      ** will fall back to xCurrentTime() if xCurrentTimeInt64() is
      ** unavailable.
      */
      if ( pVfs.iVersion >= 2 && pVfs.xCurrentTimeInt64 != null )
      {
        rc = pVfs.xCurrentTimeInt64( pVfs, ref pTimeOut );
      }
      else
      {
        double r = 0;
        rc = pVfs.xCurrentTime( pVfs, ref r );
        pTimeOut = (sqlite3_int64)( r * 86400000.0 );
      }
      return rc;
    }

    static int sqlite3OsOpenMalloc(
    ref sqlite3_vfs pVfs,
    string zFile,
    ref sqlite3_file ppFile,
    int flags,
    ref int pOutFlags
    )
    {
      int rc = SQLITE_NOMEM;
      sqlite3_file pFile;
      pFile = new sqlite3_file(); //sqlite3Malloc(ref pVfs.szOsFile);
      if ( pFile != null )
      {
        rc = sqlite3OsOpen( pVfs, zFile, pFile, flags, ref pOutFlags );
        if ( rc != SQLITE_OK )
        {
          pFile = null; // was  sqlite3DbFree(db,ref  pFile);
        }
        else
        {
          ppFile = pFile;
        }
      }
      return rc;
    }
    static int sqlite3OsCloseFree( sqlite3_file pFile )
    {
      int rc = SQLITE_OK;
      Debug.Assert( pFile != null );
      rc = sqlite3OsClose( pFile );
      //sqlite3_free( ref pFile );
      return rc;
    }

    /*
    ** This function is a wrapper around the OS specific implementation of
    ** sqlite3_os_init(). The purpose of the wrapper is to provide the
    ** ability to simulate a malloc failure, so that the handling of an
    ** error in sqlite3_os_init() by the upper layers can be tested.
    */
    static int sqlite3OsInit()
    {
      //void *p = sqlite3_malloc(10);
      //if( p==null ) return SQLITE_NOMEM;
      //sqlite3_free(ref p);
      return sqlite3_os_init();
    }
    /*
    ** The list of all registered VFS implementations.
    */
    static sqlite3_vfs vfsList;
    //#define vfsList GLOBAL(sqlite3_vfs *, vfsList)

    /*
    ** Locate a VFS by name.  If no name is given, simply return the
    ** first VFS on the list.
    */
    static bool isInit = false;

    static public sqlite3_vfs sqlite3_vfs_find(string zVfs)
    {
      sqlite3_vfs pVfs = null;
#if SQLITE_THREADSAFE
      sqlite3_mutex mutex;
#endif
#if !SQLITE_OMIT_AUTOINIT
      int rc = sqlite3_initialize();
      if ( rc != 0 )
        return null;
#endif
#if SQLITE_THREADSAFE
      mutex = sqlite3MutexAlloc( SQLITE_MUTEX_STATIC_MASTER );
#endif
      sqlite3_mutex_enter( mutex );
      for ( pVfs = vfsList; pVfs != null; pVfs = pVfs.pNext )
      {
        if ( zVfs == null || zVfs == "" )
          break;
        if ( zVfs == pVfs.zName )
          break; //strcmp(zVfs, pVfs.zName) == null) break;
      }
      sqlite3_mutex_leave( mutex );
      return pVfs;
    }

    /*
    ** Unlink a VFS from the linked list
    */
    static void vfsUnlink( sqlite3_vfs pVfs )
    {
      Debug.Assert( sqlite3_mutex_held( sqlite3MutexAlloc( SQLITE_MUTEX_STATIC_MASTER ) ) );
      if ( pVfs == null )
      {
        /* No-op */
      }
      else if ( vfsList == pVfs )
      {
        vfsList = pVfs.pNext;
      }
      else if ( vfsList != null )
      {
        sqlite3_vfs p = vfsList;
        while ( p.pNext != null && p.pNext != pVfs )
        {
          p = p.pNext;
        }
        if ( p.pNext == pVfs )
        {
          p.pNext = pVfs.pNext;
        }
      }
    }

    /*
    ** Register a VFS with the system.  It is harmless to register the same
    ** VFS multiple times.  The new VFS becomes the default if makeDflt is
    ** true.
    */
    static public int sqlite3_vfs_register( sqlite3_vfs pVfs, int makeDflt )
    {
      sqlite3_mutex mutex;
#if !SQLITE_OMIT_AUTOINIT
      int rc = sqlite3_initialize();
      if ( rc != 0 )
        return rc;
#endif
      mutex = sqlite3MutexAlloc( SQLITE_MUTEX_STATIC_MASTER );
      sqlite3_mutex_enter( mutex );
      vfsUnlink( pVfs );
      if ( makeDflt != 0 || vfsList == null )
      {
        pVfs.pNext = vfsList;
        vfsList = pVfs;
      }
      else
      {
        pVfs.pNext = vfsList.pNext;
        vfsList.pNext = pVfs;
      }
      Debug.Assert( vfsList != null );
      sqlite3_mutex_leave( mutex );
      return SQLITE_OK;
    }

    /*
    ** Unregister a VFS so that it is no longer accessible.
    */
    static int sqlite3_vfs_unregister( sqlite3_vfs pVfs )
    {
#if SQLITE_THREADSAFE
      sqlite3_mutex mutex = sqlite3MutexAlloc( SQLITE_MUTEX_STATIC_MASTER );
#endif
      sqlite3_mutex_enter( mutex );
      vfsUnlink( pVfs );
      sqlite3_mutex_leave( mutex );
      return SQLITE_OK;
    }
  }
}

