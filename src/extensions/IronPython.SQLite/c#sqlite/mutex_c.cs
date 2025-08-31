using System;
using System.Diagnostics;
using System.Threading;

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
    ** This file contains the C functions that implement mutexes.
    **
    ** This file contains code that is common across all mutex implementations.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-05-19 13:26:54 ed1da510a239ea767a01dc332b667119fa3c908e
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"

#if (SQLITE_DEBUG) && !(SQLITE_MUTEX_OMIT)
/*
** For debugging purposes, record when the mutex subsystem is initialized
** and uninitialized so that we can assert() if there is an attempt to
** allocate a mutex while the system is uninitialized.
*/
static  int mutexIsInit = 0;
#endif //* SQLITE_DEBUG */


#if !SQLITE_MUTEX_OMIT
/*
** Initialize the mutex system.
*/
static int sqlite3MutexInit(){ 
  int rc = SQLITE_OK;
  if( null==sqlite3GlobalConfig.mutex.xMutexAlloc ){
    /* If the xMutexAlloc method has not been set, then the user did not
    ** install a mutex implementation via sqlite3_config() prior to 
    ** sqlite3_initialize() being called. This block copies pointers to
    ** the default implementation into the sqlite3GlobalConfig structure.
    */
    sqlite3_mutex_methods pFrom;
    sqlite3_mutex_methods pTo = sqlite3GlobalConfig.mutex;

    if( sqlite3GlobalConfig.bCoreMutex ){
      pFrom = sqlite3DefaultMutex();
    }else{
      pFrom = sqlite3NoopMutex();
    }
    //memcpy(pTo, pFrom, offsetof(sqlite3_mutex_methods, xMutexAlloc));
    //memcpy(pTo.xMutexFree, pFrom.xMutexFree,
    //       sizeof(*pTo) - offsetof(sqlite3_mutex_methods, xMutexFree));
    pTo.Copy(pFrom);
  }
  rc = sqlite3GlobalConfig.mutex.xMutexInit();

#if SQLITE_DEBUG
  mutexIsInit = 1; //GLOBAL(int, mutexIsInit) = 1;
#endif

  return rc;
}

/*
** Shutdown the mutex system. This call frees resources allocated by
** sqlite3MutexInit().
*/
static int sqlite3MutexEnd(){
  int rc = SQLITE_OK;
  if( sqlite3GlobalConfig.mutex.xMutexEnd !=null){
    rc = sqlite3GlobalConfig.mutex.xMutexEnd();
  }

#if  SQLITE_DEBUG
  mutexIsInit = 0;//GLOBAL(int, mutexIsInit) = 0;
#endif

  return rc;
}

/*
** Retrieve a pointer to a static mutex or allocate a new dynamic one.
*/
static sqlite3_mutex sqlite3_mutex_alloc(int id){
#if !SQLITE_OMIT_AUTOINIT
  if( sqlite3_initialize()!=0 ) return null;
#endif
  return sqlite3GlobalConfig.mutex.xMutexAlloc(id);
}

static sqlite3_mutex sqlite3MutexAlloc(int id){
  if( !sqlite3GlobalConfig.bCoreMutex ){
    return null;
  }
  Debug.Assert( mutexIsInit !=0 );//assert( GLOBAL(int, mutexIsInit) );
  return sqlite3GlobalConfig.mutex.xMutexAlloc(id);
}

/*
** Free a dynamic mutex.
*/
static void sqlite3_mutex_free( sqlite3_mutex p){
  if( p!=null ){
    sqlite3GlobalConfig.mutex.xMutexFree( p);
  }
}

/*
** Obtain the mutex p. If some other thread already has the mutex, block
** until it can be obtained.
*/
static void sqlite3_mutex_enter(sqlite3_mutex p){
  if( p !=null){
    sqlite3GlobalConfig.mutex.xMutexEnter(p);
  }
}

/*
** Obtain the mutex p. If successful, return SQLITE_OK. Otherwise, if another
** thread holds the mutex and it cannot be obtained, return SQLITE_BUSY.
*/
static int sqlite3_mutex_try(sqlite3_mutex p){
  int rc = SQLITE_OK;
  if( p!=null ){
    return sqlite3GlobalConfig.mutex.xMutexTry(p);
  }
  return rc;
}

/*
** The sqlite3_mutex_leave() routine exits a mutex that was previously
** entered by the same thread.  The behavior is undefined if the mutex 
** is not currently entered. If a NULL pointer is passed as an argument
** this function is a no-op.
*/
static void sqlite3_mutex_leave(sqlite3_mutex p){
  if( p !=null){
    sqlite3GlobalConfig.mutex.xMutexLeave(p);
  }
}

#if !NDEBUG
/*
** The sqlite3_mutex_held() and sqlite3_mutex_notheld() routine are
** intended for use inside assert() statements.
*/
static bool sqlite3_mutex_held(sqlite3_mutex p){
  return p==null || sqlite3GlobalConfig.mutex.xMutexHeld(p);
}

    static bool sqlite3_mutex_notheld(sqlite3_mutex p){
  return p == null || sqlite3GlobalConfig.mutex.xMutexNotheld( p );
}
#else
static bool sqlite3_mutex_held(sqlite3_mutex p) {
    return true;
}

static bool sqlite3_mutex_notheld(sqlite3_mutex p) {
    return true;
}
#endif

#endif //* SQLITE_MUTEX_OMIT */
  }
}
