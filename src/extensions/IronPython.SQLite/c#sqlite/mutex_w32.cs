using System.Diagnostics;
using DWORD = System.Int32;
using System.Threading;
using System;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2007 August 14
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    ** This file contains the C functions that implement mutexes for win32
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2010-03-09 19:31:43 4ae453ea7be69018d8c16eb8dabe05617397dc4d
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"

    /*
    ** The code in this file is only used if we are compiling multithreaded
    ** on a win32 system.
    */
#if SQLITE_MUTEX_W32


    /*
** Each recursive mutex is an instance of the following structure.
*/
    public partial class sqlite3_mutex
    {
      public Object mutex;    /* Mutex controlling the lock */
      public int id;         /* Mutex type */
      public int nRef;       /* Number of enterances */
      public DWORD owner;    /* Thread holding this mutex */
#if SQLITE_DEBUG
      public int trace;      /* True to trace changes */
#endif

      public sqlite3_mutex()
      {
        mutex = new Object();
      }

      public sqlite3_mutex( Mutex mutex, int id, int nRef, DWORD owner
#if SQLITE_DEBUG
, int trace
#endif
 )
      {
        this.mutex = mutex;
        this.id = id;
        this.nRef = nRef;
        this.owner = owner;
#if SQLITE_DEBUG
        this.trace = 0;
#endif
      }
    };

    //#define SQLITE_W32_MUTEX_INITIALIZER { 0 }
    static Mutex SQLITE_W32_MUTEX_INITIALIZER = null;
#if SQLITE_DEBUG
    //#define SQLITE3_MUTEX_INITIALIZER { SQLITE_W32_MUTEX_INITIALIZER, 0, 0L, (DWORD)0, 0 }
#else
//#define SQLITE3_MUTEX_INITIALIZER { SQLITE_W32_MUTEX_INITIALIZER, 0, 0L, (DWORD)0 }
#endif

    /*
** Return true (non-zero) if we are running under WinNT, Win2K, WinXP,
** or WinCE.  Return false (zero) for Win95, Win98, or WinME.
**
** Here is an interesting observation:  Win95, Win98, and WinME lack
** the LockFileEx() API.  But we can still statically link against that
** API as long as we don't call it win running Win95/98/ME.  A call to
** this routine is used to determine if the host is Win95/98/ME or
** WinNT/2K/XP so that we will know whether or not we can safely call
** the LockFileEx() API.
**
** mutexIsNT() is only used for the TryEnterCriticalSection() API call,
** which is only available if your application was compiled with
** _WIN32_WINNT defined to a value >= 0x0400.  Currently, the only
** call to TryEnterCriticalSection() is #ifdef'ed out, so #if
** this out as well.
*/
#if FALSE
#if SQLITE_OS_WINCE
//# define mutexIsNT()  (1)
#else
static int mutexIsNT(void){
static int osType = 0;
if( osType==0 ){
OSVERSIONINFO sInfo;
sInfo.dwOSVersionInfoSize = sizeof(sInfo);
GetVersionEx(&sInfo);
osType = sInfo.dwPlatformId==VER_PLATFORM_WIN32_NT ? 2 : 1;
}
return osType==2;
}
#endif //* SQLITE_OS_WINCE */
#endif

#if SQLITE_DEBUG
    /*
** The sqlite3_mutex_held() and sqlite3_mutex_notheld() routine are
** intended for use only inside Debug.Assert() statements.
*/
    static bool winMutexHeld( sqlite3_mutex p )
    {
      return p.nRef != 0 && p.owner == GetCurrentThreadId();
    }
    static bool winMutexNotheld2( sqlite3_mutex p, DWORD tid )
    {
      return p.nRef == 0 || p.owner != tid;
    }
    static bool winMutexNotheld( sqlite3_mutex p )
    {
      DWORD tid = GetCurrentThreadId();
      return winMutexNotheld2( p, tid );
    }
#endif


    /*
** Initialize and deinitialize the mutex subsystem.
*/
    //No MACROS under C#; Cannot use SQLITE3_MUTEX_INITIALIZER,
    static sqlite3_mutex[] winMutex_staticMutexes = new sqlite3_mutex[]{
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
new sqlite3_mutex( SQLITE_W32_MUTEX_INITIALIZER, 0, 0, (DWORD)0
#if SQLITE_DEBUG
  , 0 
#endif
  ),//  SQLITE3_MUTEX_INITIALIZER,
};
    static int winMutex_isInit = 0;
    /* As winMutexInit() and winMutexEnd() are called as part
    ** of the sqlite3_initialize and sqlite3_shutdown()
    ** processing, the "interlocked" magic is probably not
    ** strictly necessary.
    */
    static long winMutex_lock = 0;

    private static System.Object lockThis = new System.Object();
    static int winMutexInit()
    {
      /* The first to increment to 1 does actual initialization */

      lock ( lockThis )
      //if ( Interlocked.CompareExchange(ref winMutex_lock, 1, 0 ) == 0 )
      {
        int i;
        for ( i = 0; i < ArraySize( winMutex_staticMutexes ); i++ )
        {
          if (winMutex_staticMutexes[i].mutex== null) winMutex_staticMutexes[i].mutex = new Mutex();
          //InitializeCriticalSection( winMutex_staticMutexes[i].mutex );
        }
        winMutex_isInit = 1;
      }
      //else
      //{
      //  /* Someone else is in the process of initing the static mutexes */
      //  while ( 0 == winMutex_isInit )
      //  {
      //    Thread.Sleep( 1 );
      //  }
      //}
      return SQLITE_OK;
    }

    static int winMutexEnd()
    {
      /* The first to decrement to 0 does actual shutdown
      ** (which should be the last to shutdown.) */
      if ( Interlocked.CompareExchange( ref winMutex_lock, 0, 1 ) == 1 )
      {
        if ( winMutex_isInit == 1 )
        {
          int i;
          for ( i = 0; i < ArraySize( winMutex_staticMutexes ); i++ )
          {
            DeleteCriticalSection( winMutex_staticMutexes[i].mutex );
          }
          winMutex_isInit = 0;
        }
      }
      return SQLITE_OK;
    }

    /*
    ** The sqlite3_mutex_alloc() routine allocates a new
    ** mutex and returns a pointer to it.  If it returns NULL
    ** that means that a mutex could not be allocated.  SQLite
    ** will unwind its stack and return an error.  The argument
    ** to sqlite3_mutex_alloc() is one of these integer constants:
    **
    ** <ul>
    ** <li>  SQLITE_MUTEX_FAST
    ** <li>  SQLITE_MUTEX_RECURSIVE
    ** <li>  SQLITE_MUTEX_STATIC_MASTER
    ** <li>  SQLITE_MUTEX_STATIC_MEM
    ** <li>  SQLITE_MUTEX_STATIC_MEM2
    ** <li>  SQLITE_MUTEX_STATIC_PRNG
    ** <li>  SQLITE_MUTEX_STATIC_LRU
    ** <li>  SQLITE_MUTEX_STATIC_LRU2
    ** </ul>
    **
    ** The first two constants cause sqlite3_mutex_alloc() to create
    ** a new mutex.  The new mutex is recursive when SQLITE_MUTEX_RECURSIVE
    ** is used but not necessarily so when SQLITE_MUTEX_FAST is used.
    ** The mutex implementation does not need to make a distinction
    ** between SQLITE_MUTEX_RECURSIVE and SQLITE_MUTEX_FAST if it does
    ** not want to.  But SQLite will only request a recursive mutex in
    ** cases where it really needs one.  If a faster non-recursive mutex
    ** implementation is available on the host platform, the mutex subsystem
    ** might return such a mutex in response to SQLITE_MUTEX_FAST.
    **
    ** The other allowed parameters to sqlite3_mutex_alloc() each return
    ** a pointer to a static preexisting mutex.  Six static mutexes are
    ** used by the current version of SQLite.  Future versions of SQLite
    ** may add additional static mutexes.  Static mutexes are for internal
    ** use by SQLite only.  Applications that use SQLite mutexes should
    ** use only the dynamic mutexes returned by SQLITE_MUTEX_FAST or
    ** SQLITE_MUTEX_RECURSIVE.
    **
    ** Note that if one of the dynamic mutex parameters (SQLITE_MUTEX_FAST
    ** or SQLITE_MUTEX_RECURSIVE) is used then sqlite3_mutex_alloc()
    ** returns a different mutex on every call.  But for the static
    ** mutex types, the same mutex is returned on every call that has
    ** the same type number.
    */
    static sqlite3_mutex winMutexAlloc( int iType )
    {
      sqlite3_mutex p;

      switch ( iType )
      {
        case SQLITE_MUTEX_FAST:
        case SQLITE_MUTEX_RECURSIVE:
          {
            p = new sqlite3_mutex();//sqlite3MallocZero( sizeof(*p) );
            if ( p != null )
            {
              p.id = iType;
              InitializeCriticalSection( p.mutex );
            }
            break;
          }
        default:
          {
            Debug.Assert( winMutex_isInit == 1 );
            Debug.Assert( iType - 2 >= 0 );
            Debug.Assert( iType - 2 < ArraySize( winMutex_staticMutexes ) );
            p = winMutex_staticMutexes[iType - 2];
            p.id = iType;
            break;
          }
      }
      return p;
    }


    /*
    ** This routine deallocates a previously
    ** allocated mutex.  SQLite is careful to deallocate every
    ** mutex that it allocates.
    */
    static void winMutexFree( sqlite3_mutex p )
    {
      Debug.Assert( p != null );
      Debug.Assert( p.nRef == 0 );
      Debug.Assert( p.id == SQLITE_MUTEX_FAST || p.id == SQLITE_MUTEX_RECURSIVE );
      DeleteCriticalSection( p.mutex );
      p.owner = 0;
      //sqlite3_free( p );
    }

    /*
    ** The sqlite3_mutex_enter() and sqlite3_mutex_try() routines attempt
    ** to enter a mutex.  If another thread is already within the mutex,
    ** sqlite3_mutex_enter() will block and sqlite3_mutex_try() will return
    ** SQLITE_BUSY.  The sqlite3_mutex_try() interface returns SQLITE_OK
    ** upon successful entry.  Mutexes created using SQLITE_MUTEX_RECURSIVE can
    ** be entered multiple times by the same thread.  In such cases the,
    ** mutex must be exited an equal number of times before another thread
    ** can enter.  If the same thread tries to enter any other kind of mutex
    ** more than once, the behavior is undefined.
    */
    static void winMutexEnter( sqlite3_mutex p )
    {
      DWORD tid = GetCurrentThreadId();
      Debug.Assert( p.id == SQLITE_MUTEX_RECURSIVE || winMutexNotheld2( p, tid ) );
      EnterCriticalSection( p.mutex );
      p.owner = tid;
      p.nRef++;
#if SQLITE_DEBUG
      if ( p.trace != 0 )
      {
        printf( "enter mutex {0} ({1}) with nRef={2}\n", p.GetHashCode(), p.owner, p.nRef );
      }
#endif
    }

    static int winMutexTry( sqlite3_mutex p )
    {
      DWORD tid = GetCurrentThreadId();
      int rc = SQLITE_BUSY;
      Debug.Assert( p.id == SQLITE_MUTEX_RECURSIVE || winMutexNotheld2( p, tid ) );
      /*
      ** The sqlite3_mutex_try() routine is very rarely used, and when it
      ** is used it is merely an optimization.  So it is OK for it to always
      ** fail.
      **
      ** The TryEnterCriticalSection() interface is only available on WinNT.
      ** And some windows compilers complain if you try to use it without
      ** first doing some #defines that prevent SQLite from building on Win98.
      ** For that reason, we will omit this optimization for now.  See
      ** ticket #2685.
      */
#if FALSE
if( mutexIsNT() && TryEnterCriticalSection(p.mutex) ){
p.owner = tid;
p.nRef++;
rc = SQLITE_OK;
}
#else
      UNUSED_PARAMETER( p );
#endif
#if SQLITE_DEBUG
      if ( rc == SQLITE_OK && p.trace != 0 )
      {
        printf( "try mutex {0} ({1}) with nRef={2}\n", p.GetHashCode(), p.owner, p.nRef );
      }
#endif
      return rc;
    }

    /*
    ** The sqlite3_mutex_leave() routine exits a mutex that was
    ** previously entered by the same thread.  The behavior
    ** is undefined if the mutex is not currently entered or
    ** is not currently allocated.  SQLite will never do either.
    */
    static void winMutexLeave( sqlite3_mutex p )
    {
      DWORD tid = GetCurrentThreadId();
      Debug.Assert( p.nRef > 0 );
      Debug.Assert( p.owner == tid );
      p.nRef--;
      Debug.Assert( p.nRef == 0 || p.id == SQLITE_MUTEX_RECURSIVE );
      if ( p.nRef == 0 ) p.owner = 0;
      LeaveCriticalSection( p.mutex );
#if SQLITE_DEBUG
      if ( p.trace != 0 )
      {
        printf( "leave mutex {0} ({1}) with nRef={2}\n", p.GetHashCode(), p.owner, p.nRef );
      }
#endif
    }

    static sqlite3_mutex_methods sqlite3DefaultMutex()
    {
      sqlite3_mutex_methods sMutex = new sqlite3_mutex_methods (
(dxMutexInit)winMutexInit,
(dxMutexEnd)winMutexEnd,
(dxMutexAlloc)winMutexAlloc,
(dxMutexFree)winMutexFree,
(dxMutexEnter)winMutexEnter,
(dxMutexTry)winMutexTry,
(dxMutexLeave)winMutexLeave,
#if SQLITE_DEBUG
(dxMutexHeld)winMutexHeld,
(dxMutexNotheld)winMutexNotheld
#else
null,
null
#endif
);

      return sMutex;
    }
#endif // * SQLITE_MUTEX_W32 */
  }
}

