using System;
using System.Diagnostics;
using System.Threading;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2008 October 07
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    ** This file contains the C functions that implement mutexes.
    **
    ** This implementation in this file does not provide any mutual
    ** exclusion and is thus suitable for use only in applications
    ** that use SQLite in a single thread.  The routines defined
    ** here are place-holders.  Applications can substitute working
    ** mutex routines at start-time using the
    **
    **     sqlite3_config(SQLITE_CONFIG_MUTEX,...)
    **
    ** interface.
    **
    ** If compiled with SQLITE_DEBUG, then additional logic is inserted
    ** that does error checking on mutexes to make sure they are being
    ** called correctly.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2009-12-07 16:39:13 1ed88e9d01e9eda5cbc622e7614277f29bcc551c
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"


#if !SQLITE_DEBUG
/*
** Stub routines for all mutex methods.
**
** This routines provide no mutual exclusion or error checking.
*/
static int noopMutexHeld(sqlite3_mutex p){ return 1; }
static int noopMutexNotheld(sqlite3_mutex p){ return 1; }
static int noopMutexInit(){ return SQLITE_OK; }
static int noopMutexEnd(){ return SQLITE_OK; }
static sqlite3_mutex noopMutexAlloc(int id){ return new sqlite3_mutex(); }
static void noopMutexFree(sqlite3_mutex p){  }
static void noopMutexEnter(sqlite3_mutex p){  }
static int noopMutexTry(sqlite3_mutex p){ return SQLITE_OK; }
static void noopMutexLeave(sqlite3_mutex p){  }

sqlite3_mutex_methods sqlite3NoopMutex(){
sqlite3_mutex_methods sMutex = new sqlite3_mutex_methods(
(dxMutexInit)noopMutexInit,
(dxMutexEnd)noopMutexEnd,
(dxMutexAlloc)noopMutexAlloc,
(dxMutexFree)noopMutexFree,
(dxMutexEnter)noopMutexEnter,
(dxMutexTry)noopMutexTry,
(dxMutexLeave)noopMutexLeave,
#if SQLITE_DEBUG
(dxMutexHeld)noopMutexHeld,
(dxMutexNotheld)noopMutexNotheld
#else
null,
null
#endif
);

return sMutex;
}
#endif //* !SQLITE_DEBUG */

#if SQLITE_DEBUG && !SQLITE_MUTEX_OMIT
    /*
** In this implementation, error checking is provided for testing
** and debugging purposes.  The mutexes still do not provide any
** mutual exclusion.
*/

    /*
    ** The mutex object
    */
    public class sqlite3_debug_mutex : sqlite3_mutex
    {
      //public int id;     /* The mutex type */
      public int cnt;    /* Number of entries without a matching leave */
    };

    /*
    ** The sqlite3_mutex_held() and sqlite3_mutex_notheld() routine are
    ** intended for use inside Debug.Assert() statements.
    */
    static bool debugMutexHeld( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      return p == null || p.cnt > 0;
    }
    static bool debugMutexNotheld( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      return p == null || p.cnt == 0;
    }

    /*
    ** Initialize and deinitialize the mutex subsystem.
    */
    static int debugMutexInit()
    {
      return SQLITE_OK;
    }
    static int debugMutexEnd()
    {
      return SQLITE_OK;
    }

    /*
    ** The sqlite3_mutex_alloc() routine allocates a new
    ** mutex and returns a pointer to it.  If it returns NULL
    ** that means that a mutex could not be allocated.
    */
    static sqlite3_mutex debugMutexAlloc( int id )
    {
      sqlite3_debug_mutex[] aStatic = new sqlite3_debug_mutex[6];
      sqlite3_debug_mutex pNew = null;
      switch ( id )
      {
        case SQLITE_MUTEX_FAST:
        case SQLITE_MUTEX_RECURSIVE:
          {
            pNew = new sqlite3_debug_mutex();//sqlite3Malloc(sizeof(*pNew));
            if ( pNew != null )
            {
              pNew.id = id;
              pNew.cnt = 0;
            }
            break;
          }
        default:
          {
            Debug.Assert( id - 2 >= 0 );
            Debug.Assert( id - 2 < aStatic.Length );//(int)(sizeof(aStatic)/sizeof(aStatic[0])) );
            pNew = aStatic[id - 2];
            pNew.id = id;
            break;
          }
      }
      return pNew;
    }

    /*
    ** This routine deallocates a previously allocated mutex.
    */
    static void debugMutexFree( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      Debug.Assert( p.cnt == 0 );
      Debug.Assert( p.id == SQLITE_MUTEX_FAST || p.id == SQLITE_MUTEX_RECURSIVE );
      //sqlite3_free(ref p);
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
    static void debugMutexEnter( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      Debug.Assert( p.id == SQLITE_MUTEX_RECURSIVE || debugMutexNotheld( p ) );
      p.cnt++;
    }

    static int debugMutexTry( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      Debug.Assert( p.id == SQLITE_MUTEX_RECURSIVE || debugMutexNotheld( p ) );
      p.cnt++;
      return SQLITE_OK;
    }

    /*
    ** The sqlite3_mutex_leave() routine exits a mutex that was
    ** previously entered by the same thread.  The behavior
    ** is undefined if the mutex is not currently entered or
    ** is not currently allocated.  SQLite will never do either.
    */
    static void debugMutexLeave( sqlite3_mutex pX )
    {
      sqlite3_debug_mutex p = (sqlite3_debug_mutex)pX;
      Debug.Assert( debugMutexHeld( p ) );
      p.cnt--;
      Debug.Assert( p.id == SQLITE_MUTEX_RECURSIVE || debugMutexNotheld( p ) );
    }

    static sqlite3_mutex_methods sqlite3NoopMutex()
    {
      sqlite3_mutex_methods sMutex = new sqlite3_mutex_methods(
      (dxMutexInit)debugMutexInit,
      (dxMutexEnd)debugMutexEnd,
      (dxMutexAlloc)debugMutexAlloc,
      (dxMutexFree)debugMutexFree,
      (dxMutexEnter)debugMutexEnter,
      (dxMutexTry)debugMutexTry,
      (dxMutexLeave)debugMutexLeave,

      (dxMutexHeld)debugMutexHeld,
      (dxMutexNotheld)debugMutexNotheld
      );

      return sMutex;
    }
#endif //* SQLITE_DEBUG */

    /*
** If compiled with SQLITE_MUTEX_NOOP, then the no-op mutex implementation
** is used regardless of the run-time threadsafety setting.
*/
#if SQLITE_MUTEX_NOOP
sqlite3_mutex_methods const sqlite3DefaultMutex(void){
return sqlite3NoopMutex();
}
#endif //* SQLITE_MUTEX_NOOP */

  }
}
