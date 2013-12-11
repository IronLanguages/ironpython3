using System;
using System.Diagnostics;
using System.Text;
using i64 = System.Int64;
using u16 = System.UInt16;
using u32 = System.UInt32;
using u64 = System.UInt64;
using u8 = System.Byte;
using sqlite3_int64 = System.Int64;

namespace Community.CsharpSqlite
{
  using Op = Sqlite3.VdbeOp;
  using sqlite_int64 = System.Int64;
  using sqlite3_stmt = Sqlite3.Vdbe;
  using sqlite3_value = Sqlite3.Mem;

  public partial class Sqlite3
  {
    /*
    ** 2004 May 26
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
    ** This file contains code use to implement APIs that are part of the
    ** VDBE.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-06-23 19:49:22 4374b7e83ea0a3fbc3691f9c0c936272862f32f2
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"
    //#include "vdbeInt.h"

#if !SQLITE_OMIT_DEPRECATED
/*
** Return TRUE (non-zero) of the statement supplied as an argument needs
** to be recompiled.  A statement needs to be recompiled whenever the
** execution environment changes in a way that would alter the program
** that sqlite3_prepare() generates.  For example, if new functions or
** collating sequences are registered or if an authorizer function is
** added or changed.
*/
static int sqlite3_expired( sqlite3_stmt pStmt )
{
Vdbe p = (Vdbe)pStmt;
return ( p == null || p.expired ) ? 1 : 0;
}
#endif

    /*
** Check on a Vdbe to make sure it has not been finalized.  Log
** an error and return true if it has been finalized (or is otherwise
** invalid).  Return false if it is ok.
*/
    static bool vdbeSafety( Vdbe p )
    {
      if ( p.db == null )
      {
        sqlite3_log( SQLITE_MISUSE, "API called with finalized prepared statement" );
        return true;
      }
      else
      {
        return false;
      }
    }
    static bool vdbeSafetyNotNull( Vdbe p )
    {
      if ( p == null )
      {
        sqlite3_log( SQLITE_MISUSE, "API called with NULL prepared statement" );
        return true;
      }
      else
      {
        return vdbeSafety( p );
      }
    }

    /*
    ** The following routine destroys a virtual machine that is created by
    ** the sqlite3_compile() routine. The integer returned is an SQLITE_
    ** success/failure code that describes the result of executing the virtual
    ** machine.
    **
    ** This routine sets the error code and string returned by
    ** sqlite3_errcode(), sqlite3_errmsg() and sqlite3_errmsg16().
    */
    public static int sqlite3_finalize( sqlite3_stmt pStmt )
    {
      int rc;
      if ( pStmt == null )
      {
        /* IMPLEMENTATION-OF: R-57228-12904 Invoking sqlite3_finalize() on a NULL
        ** pointer is a harmless no-op. */
        rc = SQLITE_OK;
      }
      else
      {
        Vdbe v = pStmt;
        sqlite3 db = v.db;
#if  SQLITE_THREADSAFE
        sqlite3_mutex mutex;
#endif
        if ( vdbeSafety( v ) )
          return SQLITE_MISUSE_BKPT();
#if SQLITE_THREADSAFE
        mutex = v.db.mutex;
#endif
        sqlite3_mutex_enter( mutex );
        rc = sqlite3VdbeFinalize( ref v );
        rc = sqlite3ApiExit( db, rc );
        sqlite3_mutex_leave( mutex );
      }
      return rc;
    }

    /*
    ** Terminate the current execution of an SQL statement and reset it
    ** back to its starting state so that it can be reused. A success code from
    ** the prior execution is returned.
    **
    ** This routine sets the error code and string returned by
    ** sqlite3_errcode(), sqlite3_errmsg() and sqlite3_errmsg16().
    */
    public static int sqlite3_reset( sqlite3_stmt pStmt )
    {
      int rc;
      if ( pStmt == null )
      {
        rc = SQLITE_OK;
      }
      else
      {
        Vdbe v = (Vdbe)pStmt;
        sqlite3_mutex_enter( v.db.mutex );
        rc = sqlite3VdbeReset( v );
        sqlite3VdbeRewind( v );
        Debug.Assert( ( rc & ( v.db.errMask ) ) == rc );
        rc = sqlite3ApiExit( v.db, rc );
        sqlite3_mutex_leave( v.db.mutex );
      }
      return rc;
    }

    /*
    ** Set all the parameters in the compiled SQL statement to NULL.
    */
    public static int sqlite3_clear_bindings( sqlite3_stmt pStmt )
    {
      int i;
      int rc = SQLITE_OK;
      Vdbe p = (Vdbe)pStmt;
#if  SQLITE_THREADSAFE
      sqlite3_mutex mutex = ( (Vdbe)pStmt ).db.mutex;
#endif
      sqlite3_mutex_enter( mutex );
      for ( i = 0; i < p.nVar; i++ )
      {
        sqlite3VdbeMemRelease( p.aVar[i] );
        p.aVar[i].flags = MEM_Null;
      }
      if ( p.isPrepareV2 && p.expmask != 0 )
      {
        p.expired = true;
      }
      sqlite3_mutex_leave( mutex );
      return rc;
    }


    /**************************** sqlite3_value_  *******************************
    ** The following routines extract information from a Mem or sqlite3_value
    ** structure.
    */
    public static byte[] sqlite3_value_blob( sqlite3_value pVal )
    {
      Mem p = pVal;
      if ( ( p.flags & ( MEM_Blob | MEM_Str ) ) != 0 )
      {
        sqlite3VdbeMemExpandBlob( p );
        if ( p.zBLOB == null && p.z != null )
        {
          if ( p.z.Length == 0 )
            p.zBLOB = sqlite3Malloc( 1 );
          else
          {
            p.zBLOB = sqlite3Malloc( p.z.Length );
            Debug.Assert( p.zBLOB.Length == p.z.Length );
            for ( int i = 0; i < p.zBLOB.Length; i++ )
              p.zBLOB[i] = (u8)p.z[i];
          }
          p.z = null;
        }
        p.flags = (u16)( p.flags & ~MEM_Str );
        p.flags |= MEM_Blob;
        return p.n > 0 ? p.zBLOB : null;
      }
      else
      {
        return sqlite3_value_text( pVal ) == null ? null : Encoding.UTF8.GetBytes( sqlite3_value_text( pVal ) );
      }
    }

    public static int sqlite3_value_bytes( sqlite3_value pVal )
    {
      return sqlite3ValueBytes( pVal, SQLITE_UTF8 );
    }

    public static int sqlite3_value_bytes16( sqlite3_value pVal )
    {
      return sqlite3ValueBytes( pVal, SQLITE_UTF16NATIVE );
    }

    public static double sqlite3_value_double( sqlite3_value pVal )
    {
      return sqlite3VdbeRealValue( pVal );
    }

    public static int sqlite3_value_int( sqlite3_value pVal )
    {
      return (int)sqlite3VdbeIntValue( pVal );
    }

    public static sqlite_int64 sqlite3_value_int64( sqlite3_value pVal )
    {
      return sqlite3VdbeIntValue( pVal );
    }

    public static string sqlite3_value_text( sqlite3_value pVal )
    {
      return sqlite3ValueText( pVal, SQLITE_UTF8 );
    }

#if  !SQLITE_OMIT_UTF16
public static string sqlite3_value_text16(sqlite3_value pVal){
return sqlite3ValueText(pVal, SQLITE_UTF16NATIVE);
}
public static string  sqlite3_value_text16be(sqlite3_value pVal){
return sqlite3ValueText(pVal, SQLITE_UTF16BE);
}
public static string sqlite3_value_text16le(sqlite3_value pVal){
return sqlite3ValueText(pVal, SQLITE_UTF16LE);
}
#endif // * SQLITE_OMIT_UTF16 */

    public static int sqlite3_value_type( sqlite3_value pval )
    {
      return pval.type;
    }

    /**************************** sqlite3_result_  *******************************
    ** The following routines are used by user-defined functions to specify
    ** the function result.
    **
    ** The setStrOrError() funtion calls sqlite3VdbeMemSetStr() to store the
    ** result as a string or blob but if the string or blob is too large, it
    ** then sets the error code to SQLITE_TOOBIG
    */
    static void setResultStrOrError(
    sqlite3_context pCtx,   /* Function context */
    string z,               /* String pointer */
    int o,                  /* offset into string */
    int n,                  /* Bytes in string, or negative */
    u8 enc,                 /* Encoding of z.  0 for BLOBs */
    dxDel xDel //void (*xDel)(void)     /* Destructor function */
    )
    {
      if ( sqlite3VdbeMemSetStr( pCtx.s, z, o, n, enc, xDel ) == SQLITE_TOOBIG )
      {
        sqlite3_result_error_toobig( pCtx );
      }
    }
    static void setResultStrOrError(
    sqlite3_context pCtx,   /* Function context */
    string z,               /* String pointer */
    int n,                  /* Bytes in string, or negative */
    u8 enc,                 /* Encoding of z.  0 for BLOBs */
    dxDel xDel //void (*xDel)(void)     /* Destructor function */
    )
    {
      if ( sqlite3VdbeMemSetStr( pCtx.s, z, n, enc, xDel ) == SQLITE_TOOBIG )
      {
        sqlite3_result_error_toobig( pCtx );
      }
    }

    public static void sqlite3_result_blob(
    sqlite3_context pCtx,
    string z,
    int n,
    dxDel xDel
    )
    {
      Debug.Assert( n >= 0 );
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      setResultStrOrError( pCtx, z, n, 0, xDel );
    }

    public static void sqlite3_result_double( sqlite3_context pCtx, double rVal )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetDouble( pCtx.s, rVal );
    }

    public static void sqlite3_result_error( sqlite3_context pCtx, string z, int n )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      setResultStrOrError( pCtx, z, n, SQLITE_UTF8, SQLITE_TRANSIENT );
      pCtx.isError = SQLITE_ERROR;
    }

#if  !SQLITE_OMIT_UTF16
//void sqlite3_result_error16(sqlite3_context pCtx, string z, int n){
//  Debug.Assert( sqlite3_mutex_held(pCtx.s.db.mutex) );
//  pCtx.isError = SQLITE_ERROR;
//  sqlite3VdbeMemSetStr(pCtx.s, z, n, SQLITE_UTF16NATIVE, SQLITE_TRANSIENT);
//}
#endif

    public static void sqlite3_result_int( sqlite3_context pCtx, int iVal )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetInt64( pCtx.s, (i64)iVal );
    }

    public static void sqlite3_result_int64( sqlite3_context pCtx, i64 iVal )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetInt64( pCtx.s, iVal );
    }

    public static void sqlite3_result_null( sqlite3_context pCtx )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetNull( pCtx.s );
    }

    public static void sqlite3_result_text(
    sqlite3_context pCtx,
    string z,
    int o,    //Offset
    int n,
    dxDel xDel
    )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      setResultStrOrError( pCtx, z, o, n, SQLITE_UTF8, xDel );
    }

    public static void sqlite3_result_text(
    sqlite3_context pCtx,
    StringBuilder z,
    int n,
    dxDel xDel
    )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      setResultStrOrError( pCtx, z.ToString(), n, SQLITE_UTF8, xDel );
    }


    public static void sqlite3_result_text(
    sqlite3_context pCtx,
    string z,
    int n,
    dxDel xDel
    )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      setResultStrOrError( pCtx, z, n, SQLITE_UTF8, xDel );
    }
#if  !SQLITE_OMIT_UTF16
void sqlite3_result_text16(
sqlite3_context pCtx,
string z,
int n,
dxDel xDel
){
Debug.Assert( sqlite3_mutex_held(pCtx.s.db.mutex) );
sqlite3VdbeMemSetStr(pCtx.s, z, n, SQLITE_UTF16NATIVE, xDel);
}
void sqlite3_result_text16be(
sqlite3_context pCtx,
string z,
int n,
dxDel xDel
){
Debug.Assert( sqlite3_mutex_held(pCtx.s.db.mutex) );
sqlite3VdbeMemSetStr(pCtx.s, z, n, SQLITE_UTF16BE, xDel);
}
void sqlite3_result_text16le(
sqlite3_context pCtx,
string z,
int n,
dxDel xDel
){
Debug.Assert( sqlite3_mutex_held(pCtx.s.db.mutex) );
sqlite3VdbeMemSetStr(pCtx.s, z, n, SQLITE_UTF16LE, xDel);
}
#endif // * SQLITE_OMIT_UTF16 */

    public static void sqlite3_result_value( sqlite3_context pCtx, sqlite3_value pValue )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemCopy( pCtx.s, pValue );
    }

    public static void sqlite3_result_zeroblob( sqlite3_context pCtx, int n )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetZeroBlob( pCtx.s, n );
    }

    public static void sqlite3_result_error_code( sqlite3_context pCtx, int errCode )
    {
      pCtx.isError = errCode;
      if ( ( pCtx.s.flags & MEM_Null ) != 0 )
      {
        setResultStrOrError( pCtx, sqlite3ErrStr( errCode ), -1,
        SQLITE_UTF8, SQLITE_STATIC );
      }
    }

    /* Force an SQLITE_TOOBIG error. */

    public static void sqlite3_result_error_toobig( sqlite3_context pCtx )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      pCtx.isError = SQLITE_ERROR;
      setResultStrOrError( pCtx, "string or blob too big", -1,
      SQLITE_UTF8, SQLITE_STATIC );
    }

    /* An SQLITE_NOMEM error. */
    public static void sqlite3_result_error_nomem( sqlite3_context pCtx )
    {
      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      sqlite3VdbeMemSetNull( pCtx.s );
      pCtx.isError = SQLITE_NOMEM;
      //pCtx.s.db.mallocFailed = 1;
    }

    /*
    ** This function is called after a transaction has been committed. It 
    ** invokes callbacks registered with sqlite3_wal_hook() as required.
    */
    static int doWalCallbacks( sqlite3 db )
    {
      int rc = SQLITE_OK;
#if !SQLITE_OMIT_WAL
int i;
for(i=0; i<db->nDb; i++){
Btree *pBt = db->aDb[i].pBt;
if( pBt ){
int nEntry = sqlite3PagerWalCallback(sqlite3BtreePager(pBt));
if( db->xWalCallback && nEntry>0 && rc==SQLITE_OK ){
rc = db->xWalCallback(db->pWalArg, db, db->aDb[i].zName, nEntry);
}
}
}
#endif
      return rc;
    }
    /*
    ** Execute the statement pStmt, either until a row of data is ready, the
    ** statement is completely executed or an error occurs.
    **
    ** This routine implements the bulk of the logic behind the sqlite_step()
    ** API.  The only thing omitted is the automatic recompile if a
    ** schema change has occurred.  That detail is handled by the
    ** outer sqlite3_step() wrapper procedure.
    */
    static int sqlite3Step( Vdbe p )
    {
      sqlite3 db;
      int rc;

      Debug.Assert( p != null );
      if ( p.magic != VDBE_MAGIC_RUN )
      {
        /* We used to require that sqlite3_reset() be called before retrying
        ** sqlite3_step() after any error or after SQLITE_DONE.  But beginning
        ** with version 3.7.0, we changed this so that sqlite3_reset() would
        ** be called automatically instead of throwing the SQLITE_MISUSE error.
        ** This "automatic-reset" change is not technically an incompatibility, 
        ** since any application that receives an SQLITE_MISUSE is broken by
        ** definition.
        **
        ** Nevertheless, some published applications that were originally written
        ** for version 3.6.23 or earlier do in fact depend on SQLITE_MISUSE 
        ** returns, and the so were broken by the automatic-reset change.  As a
        ** a work-around, the SQLITE_OMIT_AUTORESET compile-time restores the
        ** legacy behavior of returning SQLITE_MISUSE for cases where the 
        ** previous sqlite3_step() returned something other than a SQLITE_LOCKED
        ** or SQLITE_BUSY error.
        */
#if SQLITE_OMIT_AUTORESET
if( p.rc==SQLITE_BUSY || p.rc==SQLITE_LOCKED ){
sqlite3_reset((sqlite3_stmt)p);
}else{
return SQLITE_MISUSE_BKPT();
}
#else
        sqlite3_reset( (sqlite3_stmt)p );
#endif
      }

      /* Check that malloc() has not failed. If it has, return early. */
      db = p.db;
      //if ( db.mallocFailed != 0 )
      //{
      //p->rc = SQLITE_NOMEM;
      //  return SQLITE_NOMEM;
      //}

      if ( p.pc <= 0 && p.expired )
      {
        p.rc = SQLITE_SCHEMA;
        rc = SQLITE_ERROR;
        goto end_of_step;
      }
      if ( p.pc < 0 )
      {
        /* If there are no other statements currently running, then
        ** reset the interrupt flag.  This prevents a call to sqlite3_interrupt
        ** from interrupting a statement that has not yet started.
        */
        if ( db.activeVdbeCnt == 0 )
        {
          db.u1.isInterrupted = false;
        }

        Debug.Assert( db.writeVdbeCnt > 0 || db.autoCommit == 0 || db.nDeferredCons == 0 );
#if  !SQLITE_OMIT_TRACE
        if ( db.xProfile != null && 0 == db.init.busy )
        {
          sqlite3OsCurrentTimeInt64( db.pVfs, ref p.startTime );
        }
#endif

        db.activeVdbeCnt++;
        if ( p.readOnly == false )
          db.writeVdbeCnt++;
        p.pc = 0;
      }
#if  !SQLITE_OMIT_EXPLAIN
      if ( p.explain != 0 )
      {
        rc = sqlite3VdbeList( p );
      }
      else
#endif // * SQLITE_OMIT_EXPLAIN */
      {

        db.vdbeExecCnt++;
        rc = sqlite3VdbeExec( p );
        db.vdbeExecCnt--;
      }

#if  !SQLITE_OMIT_TRACE
      /* Invoke the profile callback if there is one
*/
      if ( rc != SQLITE_ROW && db.xProfile != null && 0 == db.init.busy && p.zSql != null )
      {
        sqlite3_int64 iNow = 0;
        sqlite3OsCurrentTimeInt64( db.pVfs, ref iNow );
        db.xProfile( db.pProfileArg, p.zSql, ( iNow - p.startTime ) * 1000000 );
      }
#endif

      if ( rc == SQLITE_DONE )
      {
        Debug.Assert( p.rc == SQLITE_OK );
        p.rc = doWalCallbacks( db );
        if ( p.rc != SQLITE_OK )
        {
          rc = SQLITE_ERROR;
        }
      }

      db.errCode = rc;
      if ( SQLITE_NOMEM == sqlite3ApiExit( p.db, p.rc ) )
      {
        p.rc = SQLITE_NOMEM;
      }
end_of_step:
      /* At this point local variable rc holds the value that should be
      ** returned if this statement was compiled using the legacy
      ** sqlite3_prepare() interface. According to the docs, this can only
      ** be one of the values in the first Debug.Assert() below. Variable p.rc
      ** contains the value that would be returned if sqlite3_finalize()
      ** were called on statement p.
      */
      Debug.Assert( rc == SQLITE_ROW || rc == SQLITE_DONE || rc == SQLITE_ERROR
      || rc == SQLITE_BUSY || rc == SQLITE_MISUSE
      );
      Debug.Assert( p.rc != SQLITE_ROW && p.rc != SQLITE_DONE );
      if ( p.isPrepareV2 && rc != SQLITE_ROW && rc != SQLITE_DONE )
      {
        /* If this statement was prepared using sqlite3_prepare_v2(), and an
        ** error has occured, then return the error code in p.rc to the
        ** caller. Set the error code in the database handle to the same value.
        */
        rc = db.errCode = p.rc;
      }
      return ( rc & db.errMask );
    }

/*
** The maximum number of times that a statement will try to reparse
** itself before giving up and returning SQLITE_SCHEMA.
*/
#if !SQLITE_MAX_SCHEMA_RETRY
//# define SQLITE_MAX_SCHEMA_RETRY 5
    public const int SQLITE_MAX_SCHEMA_RETRY = 5;
#endif

    /*
    ** This is the top-level implementation of sqlite3_step().  Call
    ** sqlite3Step() to do most of the work.  If a schema error occurs,
    ** call sqlite3Reprepare() and try again.
    */
    public static int sqlite3_step( sqlite3_stmt pStmt )
    {
      int rc = SQLITE_OK;      /* Result from sqlite3Step() */
      int rc2 = SQLITE_OK;     /* Result from sqlite3Reprepare() */
      Vdbe v = (Vdbe)pStmt;    /* the prepared statement */
      int cnt = 0;             /* Counter to prevent infinite loop of reprepares */
      sqlite3 db;              /* The database connection */

      if ( vdbeSafetyNotNull( v ) )
      {
        return SQLITE_MISUSE_BKPT();
      }
      db = v.db;
      sqlite3_mutex_enter( db.mutex );
      while ( ( rc = sqlite3Step( v ) ) == SQLITE_SCHEMA
      && cnt++ < SQLITE_MAX_SCHEMA_RETRY
      && ( rc2 = rc = sqlite3Reprepare( v ) ) == SQLITE_OK )
      {
        sqlite3_reset( pStmt );
        v.expired = false;
      }
      if ( rc2 != SQLITE_OK && ALWAYS( v.isPrepareV2 ) && ALWAYS( db.pErr != null ) )
      {
        /* This case occurs after failing to recompile an sql statement.
        ** The error message from the SQL compiler has already been loaded
        ** into the database handle. This block copies the error message
        ** from the database handle into the statement and sets the statement
        ** program counter to 0 to ensure that when the statement is
        ** finalized or reset the parser error message is available via
        ** sqlite3_errmsg() and sqlite3_errcode().
        */
        string zErr = sqlite3_value_text( db.pErr );
        sqlite3DbFree( db, ref v.zErrMsg );
        //if ( 0 == db.mallocFailed )
        {
          v.zErrMsg = zErr;// sqlite3DbStrDup(db, zErr);
          v.rc = rc2;
        }
        //else
        //{
        //  v.zErrMsg = "";
        //  v->rc = rc = SQLITE_NOMEM;
        //}
      }
      rc = sqlite3ApiExit( db, rc );
      sqlite3_mutex_leave( db.mutex );
      return rc;
    }

    /*
    ** Extract the user data from a sqlite3_context structure and return a
    ** pointer to it.
    **
    ** IMPLEMENTATION-OF: R-46798-50301 The sqlite3_context_db_handle() interface
    ** returns a copy of the pointer to the database connection (the 1st
    ** parameter) of the sqlite3_create_function() and
    ** sqlite3_create_function16() routines that originally registered the
    ** application defined function.
    */
    public static object sqlite3_user_data( sqlite3_context p )
    {
      Debug.Assert( p != null && p.pFunc != null );
      return p.pFunc.pUserData;
    }

    /*
    ** Extract the user data from a sqlite3_context structure and return a
    ** pointer to it.
    */
    public static sqlite3 sqlite3_context_db_handle( sqlite3_context p )
    {
      Debug.Assert( p != null && p.pFunc != null );
      return p.s.db;
    }

    /*
    ** The following is the implementation of an SQL function that always
    ** fails with an error message stating that the function is used in the
    ** wrong context.  The sqlite3_overload_function() API might construct
    ** SQL function that use this routine so that the functions will exist
    ** for name resolution but are actually overloaded by the xFindFunction
    ** method of virtual tables.
    */
    public static void sqlite3InvalidFunction(
    sqlite3_context context, /* The function calling context */
    int NotUsed,                /* Number of arguments to the function */
    sqlite3_value[] NotUsed2       /* Value of each argument */
    )
    {
      string zName = context.pFunc.zName;
      string zErr;
      UNUSED_PARAMETER2( NotUsed, NotUsed2 );
      zErr = sqlite3_mprintf(
      "unable to use function %s in the requested context", zName );
      sqlite3_result_error( context, zErr, -1 );
      //sqlite3_free( ref zErr );
    }

    /*
    ** Allocate or return the aggregate context for a user function.  A new
    ** context is allocated on the first call.  Subsequent calls return the
    ** same context that was returned on prior calls.
    */
    public static Mem sqlite3_aggregate_context( sqlite3_context p, int nByte )
    {
      Mem pMem;
      Debug.Assert( p != null && p.pFunc != null && p.pFunc.xStep != null );
      Debug.Assert( sqlite3_mutex_held( p.s.db.mutex ) );
      pMem = p.pMem;
      testcase( nByte < 0 );
      if ( ( pMem.flags & MEM_Agg ) == 0 )
      {
        if ( nByte <= 0 )
        {
          sqlite3VdbeMemReleaseExternal( pMem );
          pMem.flags = 0;
          pMem.z = null;
        }
        else
        {
          sqlite3VdbeMemGrow( pMem, nByte, 0 );
          pMem.flags = MEM_Agg;
          pMem.u.pDef = p.pFunc;
          if ( pMem.z != null )
          {
            pMem.z = null;
          }
          pMem._Mem = sqlite3Malloc( pMem._Mem );
          pMem._Mem.flags = 0;
          pMem._Mem.z = null;
        }
      }
      return pMem._Mem;
    }

    /*
    ** Return the auxillary data pointer, if any, for the iArg'th argument to
    ** the user-function defined by pCtx.
    */
    public static object sqlite3_get_auxdata( sqlite3_context pCtx, int iArg )
    {
      VdbeFunc pVdbeFunc;

      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      pVdbeFunc = pCtx.pVdbeFunc;
      if ( null == pVdbeFunc || iArg >= pVdbeFunc.nAux || iArg < 0 )
      {
        return null;
      }
      return pVdbeFunc.apAux[iArg].pAux;
    }

    /*
    ** Set the auxillary data pointer and delete function, for the iArg'th
    ** argument to the user-function defined by pCtx. Any previous value is
    ** deleted by calling the delete function specified when it was set.
    */
    public static void sqlite3_set_auxdata(
    sqlite3_context pCtx,
    int iArg,
    object pAux
    //void (*xDelete)(void)
    )
    {
      AuxData pAuxData;
      VdbeFunc pVdbeFunc;
      if ( iArg < 0 )
        goto failed;

      Debug.Assert( sqlite3_mutex_held( pCtx.s.db.mutex ) );
      pVdbeFunc = pCtx.pVdbeFunc;
      if ( null == pVdbeFunc || pVdbeFunc.nAux <= iArg )
      {
        int nAux = ( pVdbeFunc != null ? pVdbeFunc.nAux : 0 );
        ////int nMalloc = iArg;
        ////VdbeFunc+ sizeof(struct AuxData)*iArg;
        if ( pVdbeFunc == null )
        {
          //pVdbeFunc = (VdbeFunc)sqlite3DbRealloc( pCtx.s.db, pVdbeFunc, nMalloc );
          pVdbeFunc = new VdbeFunc();
          if ( null == pVdbeFunc )
          {
            goto failed;
          }
          pCtx.pVdbeFunc = pVdbeFunc;
        }
        pVdbeFunc.apAux[nAux] = new AuxData();//memset(pVdbeFunc.apAux[nAux], 0, sizeof(struct AuxData)*(iArg+1-nAux));
        pVdbeFunc.nAux = iArg + 1;
        pVdbeFunc.pFunc = pCtx.pFunc;
      }

      pAuxData = pVdbeFunc.apAux[iArg];
      if ( pAuxData.pAux != null && pAuxData.pAux is IDisposable )
      {
        (pAuxData.pAux as IDisposable).Dispose();
      }
      pAuxData.pAux = pAux;
      return;

failed:
      if ( pAux != null  && pAux is IDisposable)
      {
        (pAux as IDisposable).Dispose();
      }
    }

#if !SQLITE_OMIT_DEPRECATED
/*
** Return the number of times the Step function of a aggregate has been
** called.
**
** This function is deprecated.  Do not use it for new code.  It is
** provide only to avoid breaking legacy code.  New aggregate function
** implementations should keep their own counts within their aggregate
** context.
*/
static int sqlite3_aggregate_count( sqlite3_context p )
{
Debug.Assert( p != null && p.pMem != null && p.pFunc != null && p.pFunc.xStep != null );
return p.pMem.n;
}
#endif

    /*
** Return the number of columns in the result set for the statement pStmt.
*/
    public static int sqlite3_column_count( sqlite3_stmt pStmt )
    {
      Vdbe pVm = pStmt;
      return pVm != null ? (int)pVm.nResColumn : 0;
    }

    /*
    ** Return the number of values available from the current row of the
    ** currently executing statement pStmt.
    */
    public static int sqlite3_data_count( sqlite3_stmt pStmt )
    {
      Vdbe pVm = pStmt;
      if ( pVm == null || pVm.pResultSet == null )
        return 0;
      return pVm.nResColumn;
    }


    /*
    ** Check to see if column iCol of the given statement is valid.  If
    ** it is, return a pointer to the Mem for the value of that column.
    ** If iCol is not valid, return a pointer to a Mem which has a value
    ** of NULL.
    */
    static Mem columnMem( sqlite3_stmt pStmt, int i )
    {
      Vdbe pVm;
      Mem pOut;

      pVm = (Vdbe)pStmt;
      if ( pVm != null && pVm.pResultSet != null && i < pVm.nResColumn && i >= 0 )
      {
        sqlite3_mutex_enter( pVm.db.mutex );
        pOut = pVm.pResultSet[i];
      }
      else
      {
        /* If the value passed as the second argument is out of range, return
        ** a pointer to the following public static Mem object which contains the
        ** value SQL NULL. Even though the Mem structure contains an element
        ** of type i64, on certain architecture (x86) with certain compiler
        ** switches (-Os), gcc may align this Mem object on a 4-byte boundary
        ** instead of an 8-byte one. This all works fine, except that when
        ** running with SQLITE_DEBUG defined the SQLite code sometimes Debug.Assert()s
        ** that a Mem structure is located on an 8-byte boundary. To prevent
        ** this Debug.Assert() from failing, when building with SQLITE_DEBUG defined
        ** using gcc, force nullMem to be 8-byte aligned using the magical
        ** __attribute__((aligned(8))) macro.  */
        //    static const Mem nullMem 
        //#if defined(SQLITE_DEBUG) && defined(__GNUC__)
        //      __attribute__((aligned(8))) 
        //#endif
        //      = {0, "", (double)0, {0}, 0, MEM_Null, SQLITE_NULL, 0,
        //#if SQLITE_DEBUG
        //         0, 0,  /* pScopyFrom, pFiller */
        //#endif
        //         0, 0 };
        Mem nullMem = new Mem( null, "", (double)0, 0, 0, MEM_Null, SQLITE_NULL, 0
#if SQLITE_DEBUG
         , null, null  /* pScopyFrom, pFiller */
#endif
 );

        if ( pVm != null && ALWAYS( pVm.db != null ) )
        {
          sqlite3_mutex_enter( pVm.db.mutex );
          sqlite3Error( pVm.db, SQLITE_RANGE, 0 );
        }
        pOut = nullMem;
      }
      return pOut;
    }

    /*
    ** This function is called after invoking an sqlite3_value_XXX function on a
    ** column value (i.e. a value returned by evaluating an SQL expression in the
    ** select list of a SELECT statement) that may cause a malloc() failure. If
    ** malloc() has failed, the threads mallocFailed flag is cleared and the result
    ** code of statement pStmt set to SQLITE_NOMEM.
    **
    ** Specifically, this is called from within:
    **
    **     sqlite3_column_int()
    **     sqlite3_column_int64()
    **     sqlite3_column_text()
    **     sqlite3_column_text16()
    **     sqlite3_column_real()
    **     sqlite3_column_bytes()
    **     sqlite3_column_bytes16()
    **     sqlite3_column_blob()
    */
    static void columnMallocFailure( sqlite3_stmt pStmt )
    {
      /* If malloc() failed during an encoding conversion within an
      ** sqlite3_column_XXX API, then set the return code of the statement to
      ** SQLITE_NOMEM. The next call to _step() (if any) will return SQLITE_ERROR
      ** and _finalize() will return NOMEM.
      */
      Vdbe p = pStmt;
      if ( p != null )
      {
        p.rc = sqlite3ApiExit( p.db, p.rc );
        sqlite3_mutex_leave( p.db.mutex );
      }
    }

    /**************************** sqlite3_column_  *******************************
    ** The following routines are used to access elements of the current row
    ** in the result set.
    */
    public static byte[] sqlite3_column_blob( sqlite3_stmt pStmt, int i )
    {
      byte[] val;
      val = sqlite3_value_blob( columnMem( pStmt, i ) );
      /* Even though there is no encoding conversion, value_blob() might
      ** need to call malloc() to expand the result of a zeroblob()
      ** expression.
      */
      columnMallocFailure( pStmt );
      return val;
    }

    public static int sqlite3_column_bytes( sqlite3_stmt pStmt, int i )
    {
      int val = sqlite3_value_bytes( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }

    public static int sqlite3_column_bytes16( sqlite3_stmt pStmt, int i )
    {
      int val = sqlite3_value_bytes16( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }

    public static double sqlite3_column_double( sqlite3_stmt pStmt, int i )
    {
      double val = sqlite3_value_double( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }

    public static int sqlite3_column_int( sqlite3_stmt pStmt, int i )
    {
      int val = sqlite3_value_int( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }


    public static sqlite_int64 sqlite3_column_int64( sqlite3_stmt pStmt, int i )
    {
      sqlite_int64 val = sqlite3_value_int64( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }

    public static string sqlite3_column_text( sqlite3_stmt pStmt, int i )
    {
      string val = sqlite3_value_text( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return val;
    }

    public static sqlite3_value sqlite3_column_value( sqlite3_stmt pStmt, int i )
    {
      Mem pOut = columnMem( pStmt, i );
      if ( ( pOut.flags & MEM_Static ) != 0 )
      {
        pOut.flags = (u16)( pOut.flags & ~MEM_Static );
        pOut.flags |= MEM_Ephem;
      }
      columnMallocFailure( pStmt );
      return (sqlite3_value)pOut;
    }
#if  !SQLITE_OMIT_UTF16
//const void *sqlite3_column_text16(sqlite3_stmt pStmt, int i){
//  const void *val = sqlite3_value_text16( columnMem(pStmt,i) );
//  columnMallocFailure(pStmt);
//  return val;
//}
#endif // * SQLITE_OMIT_UTF16 */

    public static int sqlite3_column_type( sqlite3_stmt pStmt, int i )
    {
      int iType = sqlite3_value_type( columnMem( pStmt, i ) );
      columnMallocFailure( pStmt );
      return iType;
    }

    /* The following function is experimental and subject to change or
    ** removal */
    /*int sqlite3_column_numeric_type(sqlite3_stmt pStmt, int i){
    **  return sqlite3_value_numeric_type( columnMem(pStmt,i) );
    **}
    */

    /*
    ** Convert the N-th element of pStmt.pColName[] into a string using
    ** xFunc() then return that string.  If N is out of range, return 0.
    **
    ** There are up to 5 names for each column.  useType determines which
    ** name is returned.  Here are the names:
    **
    **    0      The column name as it should be displayed for output
    **    1      The datatype name for the column
    **    2      The name of the database that the column derives from
    **    3      The name of the table that the column derives from
    **    4      The name of the table column that the result column derives from
    **
    ** If the result is not a simple column reference (if it is an expression
    ** or a constant) then useTypes 2, 3, and 4 return NULL.
    */
    public static string columnName(
    sqlite3_stmt pStmt,
    int N,
    dxColname xFunc,
    int useType
    )
    {
      string ret = null;
      Vdbe p = pStmt;
      int n;
      sqlite3 db = p.db;

      Debug.Assert( db != null );

      n = sqlite3_column_count( pStmt );
      if ( N < n && N >= 0 )
      {
        N += useType * n;
        sqlite3_mutex_enter( db.mutex );
        //Debug.Assert( db.mallocFailed == 0 );
        ret = xFunc( p.aColName[N] );

        /* A malloc may have failed inside of the xFunc() call. If this
        ** is the case, clear the mallocFailed flag and return NULL.
        */
        //if ( db.mallocFailed != 0 )
        //{
        //  //db.mallocFailed = 0;
        //  ret = null;
        //}
        sqlite3_mutex_leave( db.mutex );
      }
      return ret;
    }

    /*
    ** Return the name of the Nth column of the result set returned by SQL
    ** statement pStmt.
    */
    public static string sqlite3_column_name( sqlite3_stmt pStmt, int N )
    {
      return columnName(
      pStmt, N, sqlite3_value_text, COLNAME_NAME );
    }
#if  !SQLITE_OMIT_UTF16
public static string sqlite3_column_name16(sqlite3_stmt pStmt, int N){
return columnName(
pStmt, N,  sqlite3_value_text16, COLNAME_NAME);
}
#endif

    /*
** Constraint:  If you have ENABLE_COLUMN_METADATA then you must
** not define OMIT_DECLTYPE.
*/
#if SQLITE_OMIT_DECLTYPE && SQLITE_ENABLE_COLUMN_METADATA
# error "Must not define both SQLITE_OMIT_DECLTYPE and SQLITE_ENABLE_COLUMN_METADATA"
#endif

#if !SQLITE_OMIT_DECLTYPE
    /*
** Return the column declaration type (if applicable) of the 'i'th column
** of the result set of SQL statement pStmt.
*/
    public static string sqlite3_column_decltype( sqlite3_stmt pStmt, int N )
    {
      return columnName(
      pStmt, N, sqlite3_value_text, COLNAME_DECLTYPE );
    }
#if  !SQLITE_OMIT_UTF16
//const void *sqlite3_column_decltype16(sqlite3_stmt pStmt, int N){
//  return columnName(
//      pStmt, N, (const void*()(Mem))sqlite3_value_text16, COLNAME_DECLTYPE);
//}
#endif // * SQLITE_OMIT_UTF16 */
#endif // * SQLITE_OMIT_DECLTYPE */

#if  SQLITE_ENABLE_COLUMN_METADATA

/*
** Return the name of the database from which a result column derives.
** NULL is returned if the result column is an expression or constant or
** anything else which is not an unabiguous reference to a database column.
*/
    public static string sqlite3_column_database_name( sqlite3_stmt pStmt, int N )
    {
      return columnName(
      pStmt, N, sqlite3_value_text, COLNAME_DATABASE );
    }
#if !SQLITE_OMIT_UTF16
const void *sqlite3_column_database_name16(sqlite3_stmt pStmt, int N){
return columnName(
pStmt, N, (const void*()(Mem))sqlite3_value_text16, COLNAME_DATABASE);
}
#endif //* SQLITE_OMIT_UTF16 */

/*
** Return the name of the table from which a result column derives.
** NULL is returned if the result column is an expression or constant or
** anything else which is not an unabiguous reference to a database column.
*/
    public static string sqlite3_column_table_name( sqlite3_stmt pStmt, int N )
    {
      return columnName(
      pStmt, N, sqlite3_value_text, COLNAME_TABLE );
    }
#if !SQLITE_OMIT_UTF16
const void *sqlite3_column_table_name16(sqlite3_stmt pStmt, int N){
return columnName(
pStmt, N, (const void*()(Mem))sqlite3_value_text16, COLNAME_TABLE);
}
#endif //* SQLITE_OMIT_UTF16 */

/*
** Return the name of the table column from which a result column derives.
** NULL is returned if the result column is an expression or constant or
** anything else which is not an unabiguous reference to a database column.
*/
    public static string sqlite3_column_origin_name( sqlite3_stmt pStmt, int N )
    {
      return columnName(
      pStmt, N, sqlite3_value_text, COLNAME_COLUMN );
    }
#if !SQLITE_OMIT_UTF16
const void *sqlite3_column_origin_name16(sqlite3_stmt pStmt, int N){
return columnName(
pStmt, N, (const void*()(Mem))sqlite3_value_text16, COLNAME_COLUMN);
}
#endif ///* SQLITE_OMIT_UTF16 */
#endif // * SQLITE_ENABLE_COLUMN_METADATA */


    /******************************* sqlite3_bind_  ***************************
**
** Routines used to attach values to wildcards in a compiled SQL statement.
*/
    /*
    ** Unbind the value bound to variable i in virtual machine p. This is the
    ** the same as binding a NULL value to the column. If the "i" parameter is
    ** out of range, then SQLITE_RANGE is returned. Othewise SQLITE_OK.
    **
    ** A successful evaluation of this routine acquires the mutex on p.
    ** the mutex is released if any kind of error occurs.
    **
    ** The error code stored in database p.db is overwritten with the return
    ** value in any case.
    */
    public static int vdbeUnbind( Vdbe p, int i )
    {
      Mem pVar;
      if ( vdbeSafetyNotNull( p ) )
      {
        return SQLITE_MISUSE_BKPT();
      }
      sqlite3_mutex_enter( p.db.mutex );
      if ( p.magic != VDBE_MAGIC_RUN || p.pc >= 0 )
      {
        sqlite3Error( p.db, SQLITE_MISUSE, 0 );
        sqlite3_mutex_leave( p.db.mutex );
        sqlite3_log( SQLITE_MISUSE,
        "bind on a busy prepared statement: [%s]", p.zSql );
        return SQLITE_MISUSE_BKPT();
      }
      if ( i < 1 || i > p.nVar )
      {
        sqlite3Error( p.db, SQLITE_RANGE, 0 );
        sqlite3_mutex_leave( p.db.mutex );
        return SQLITE_RANGE;
      }
      i--;
      pVar = p.aVar[i];
      sqlite3VdbeMemRelease( pVar );
      pVar.flags = MEM_Null;
      sqlite3Error( p.db, SQLITE_OK, 0 );

      /* If the bit corresponding to this variable in Vdbe.expmask is set, then 
      ** binding a new value to this variable invalidates the current query plan.
      **
      ** IMPLEMENTATION-OF: R-48440-37595 If the specific value bound to host
      ** parameter in the WHERE clause might influence the choice of query plan
      ** for a statement, then the statement will be automatically recompiled,
      ** as if there had been a schema change, on the first sqlite3_step() call
      ** following any change to the bindings of that parameter.
      */
      if ( p.isPrepareV2 &&
      ( ( i < 32 && p.expmask != 0 & ( (u32)1 << i ) != 0 ) || p.expmask == 0xffffffff )
      )
      {
        p.expired = true;
      }

      return SQLITE_OK;
    }

    /*
    ** Bind a text or BLOB value.
    */
    static int bindBlob(
    sqlite3_stmt pStmt,   /* The statement to bind against */
    int i,                /* Index of the parameter to bind */
    byte[] zData,         /* Pointer to the data to be bound */
    int nData,            /* Number of bytes of data to be bound */
    dxDel xDel,           /* Destructor for the data */
    u8 encoding          /* Encoding for the data */
    )
    {
      Vdbe p = pStmt;
      Mem pVar;
      int rc;

      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        if ( zData != null )
        {
          pVar = p.aVar[i - 1];
          rc = sqlite3VdbeMemSetBlob( pVar, zData, nData, encoding, xDel );
          if ( rc == SQLITE_OK && encoding != 0 )
          {
            rc = sqlite3VdbeChangeEncoding( pVar, ENC( p.db ) );
          }
          sqlite3Error( p.db, rc, 0 );
          rc = sqlite3ApiExit( p.db, rc );
        }
        sqlite3_mutex_leave( p.db.mutex );
      }
      return rc;
    }

    /*
    ** Bind a text value.
    */
    public static int bindText(
    sqlite3_stmt pStmt,   /* The statement to bind against */
    int i,                /* Index of the parameter to bind */
    string zData,         /* Pointer to the data to be bound */
    int nData,            /* Number of bytes of data to be bound */
    dxDel xDel,           /* Destructor for the data */
    u8 encoding          /* Encoding for the data */
    )
    {
      Vdbe p = pStmt;
      Mem pVar;
      int rc;

      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        if ( zData != null )
        {
          pVar = p.aVar[i - 1];
          rc = sqlite3VdbeMemSetStr( pVar, zData, nData, encoding, xDel );
          if ( rc == SQLITE_OK && encoding != 0 )
          {
            rc = sqlite3VdbeChangeEncoding( pVar, ENC( p.db ) );
          }
          sqlite3Error( p.db, rc, 0 );
          rc = sqlite3ApiExit( p.db, rc );
        }
        sqlite3_mutex_leave( p.db.mutex );
      }
      else if ( xDel != SQLITE_STATIC && xDel != SQLITE_TRANSIENT )
      {
        xDel( ref zData );
      }
      return rc;
    }

    public static int sqlite3_bind_double( sqlite3_stmt pStmt, int i, double rValue )
    {
      int rc;
      Vdbe p = pStmt;
      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        sqlite3VdbeMemSetDouble( p.aVar[i - 1], rValue );
        sqlite3_mutex_leave( p.db.mutex );
      }
      return rc;
    }

    public static int sqlite3_bind_int( sqlite3_stmt p, int i, int iValue )
    {
      return sqlite3_bind_int64( p, i, (i64)iValue );
    }

    public static int sqlite3_bind_int64( sqlite3_stmt pStmt, int i, sqlite_int64 iValue )
    {
      int rc;
      Vdbe p = pStmt;
      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        sqlite3VdbeMemSetInt64( p.aVar[i - 1], iValue );
        sqlite3_mutex_leave( p.db.mutex );
      }
      return rc;
    }

    public static int sqlite3_bind_null( sqlite3_stmt pStmt, int i )
    {
      int rc;
      Vdbe p = (Vdbe)pStmt;
      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        sqlite3_mutex_leave( p.db.mutex );
      }
      return rc;
    }

    public static int sqlite3_bind_text(
    sqlite3_stmt pStmt,
    int i,
    string zData,
    int nData,
    dxDel xDel
    )
    {
      return bindText( pStmt, i, zData, nData, xDel, SQLITE_UTF8 );
    }

    public static int sqlite3_bind_blob(
    sqlite3_stmt pStmt,
    int i,
    byte[] zData,
    int nData,
    dxDel xDel
    )
    {
      return bindBlob( pStmt, i, zData, nData >= 0 ? nData : zData.Length, xDel, 0 );
    }

#if  !SQLITE_OMIT_UTF16
static int sqlite3_bind_text16(
sqlite3_stmt pStmt,
int i,
string zData,
int nData,
dxDel xDel
){
return bindText(pStmt, i, zData, nData, xDel, SQLITE_UTF16NATIVE);
}
#endif // * SQLITE_OMIT_UTF16 */

    public static int sqlite3_bind_value( sqlite3_stmt pStmt, int i, sqlite3_value pValue )
    {
      int rc;
      switch ( pValue.type )
      {
        case SQLITE_INTEGER:
          {
            rc = sqlite3_bind_int64( pStmt, i, pValue.u.i );
            break;
          }
        case SQLITE_FLOAT:
          {
            rc = sqlite3_bind_double( pStmt, i, pValue.r );
            break;
          }
        case SQLITE_BLOB:
          {
            if ( ( pValue.flags & MEM_Zero ) != 0 )
            {
              rc = sqlite3_bind_zeroblob( pStmt, i, pValue.u.nZero );
            }
            else
            {
              rc = sqlite3_bind_blob( pStmt, i, pValue.zBLOB, pValue.n, SQLITE_TRANSIENT );
            }
            break;
          }
        case SQLITE_TEXT:
          {
            rc = bindText( pStmt, i, pValue.z, pValue.n, SQLITE_TRANSIENT,
            pValue.enc );
            break;
          }
        default:
          {
            rc = sqlite3_bind_null( pStmt, i );
            break;
          }
      }
      return rc;
    }

    public static int sqlite3_bind_zeroblob( sqlite3_stmt pStmt, int i, int n )
    {
      int rc;
      Vdbe p = pStmt;
      rc = vdbeUnbind( p, i );
      if ( rc == SQLITE_OK )
      {
        sqlite3VdbeMemSetZeroBlob( p.aVar[i - 1], n );
        sqlite3_mutex_leave( p.db.mutex );
      }
      return rc;
    }

    /*
    ** Return the number of wildcards that can be potentially bound to.
    ** This routine is added to support DBD::SQLite.
    */
    public static int sqlite3_bind_parameter_count( sqlite3_stmt pStmt )
    {
      Vdbe p = (Vdbe)pStmt;
      return ( p != null ) ? (int)p.nVar : 0;
    }

    /*
    ** Return the name of a wildcard parameter.  Return NULL if the index
    ** is out of range or if the wildcard is unnamed.
    **
    ** The result is always UTF-8.
    */
    public static string sqlite3_bind_parameter_name( sqlite3_stmt pStmt, int i )
    {
      Vdbe p = (Vdbe)pStmt;
      if ( p == null || i < 1 || i > p.nzVar )
      {
        return "";
      }
      return p.azVar[i - 1];
    }

    /*
    ** Given a wildcard parameter name, return the index of the variable
    ** with that name.  If there is no variable with the given name,
    ** return 0.
    */
    public static int sqlite3VdbeParameterIndex( Vdbe p, string zName, int nName )
    {
      int i;
      if ( p == null )
      {
        return 0;
      }
      if ( zName != null && zName != "" )
      {
        for ( i = 0; i < p.nzVar; i++ )
        {
          string z = p.azVar[i];
          if ( z != null && z == zName )//&& memcmp(z,zName,nName)==0 && z[nName]==0)
          {
            return i + 1;
          }
        }
      }
      return 0;
    }

    public static int sqlite3_bind_parameter_index( sqlite3_stmt pStmt, string zName )
    {
      return sqlite3VdbeParameterIndex( (Vdbe)pStmt, zName, sqlite3Strlen30( zName ) );
    }

    /*
    ** Transfer all bindings from the first statement over to the second.
    */
    public static int sqlite3TransferBindings( sqlite3_stmt pFromStmt, sqlite3_stmt pToStmt )
    {
      Vdbe pFrom = (Vdbe)pFromStmt;
      Vdbe pTo = (Vdbe)pToStmt;
      int i;
      Debug.Assert( pTo.db == pFrom.db );
      Debug.Assert( pTo.nVar == pFrom.nVar );
      sqlite3_mutex_enter( pTo.db.mutex );
      for ( i = 0; i < pFrom.nVar; i++ )
      {
        sqlite3VdbeMemMove( pTo.aVar[i], pFrom.aVar[i] );
      }
      sqlite3_mutex_leave( pTo.db.mutex );
      return SQLITE_OK;
    }

#if !SQLITE_OMIT_DEPRECATED
/*
** Deprecated external interface.  Internal/core SQLite code
** should call sqlite3TransferBindings.
**
** Is is misuse to call this routine with statements from different
** database connections.  But as this is a deprecated interface, we
** will not bother to check for that condition.
**
** If the two statements contain a different number of bindings, then
** an SQLITE_ERROR is returned.  Nothing else can go wrong, so otherwise
** SQLITE_OK is returned.
*/
static int sqlite3_transfer_bindings( sqlite3_stmt pFromStmt, sqlite3_stmt pToStmt )
{
Vdbe pFrom = (Vdbe)pFromStmt;
Vdbe pTo = (Vdbe)pToStmt;
if ( pFrom.nVar != pTo.nVar )
{
return SQLITE_ERROR;
}
if( pTo.isPrepareV2 && pTo.expmask ){
pTo.expired = 1;
}
if( pFrom.isPrepareV2 && pFrom.expmask ){
pFrom.expired = 1;
}
return sqlite3TransferBindings( pFromStmt, pToStmt );
}
#endif

    /*
** Return the sqlite3* database handle to which the prepared statement given
** in the argument belongs.  This is the same database handle that was
** the first argument to the sqlite3_prepare() that was used to create
** the statement in the first place.
*/
    public static sqlite3 sqlite3_db_handle( sqlite3_stmt pStmt )
    {
      return pStmt != null ? ( (Vdbe)pStmt ).db : null;
    }


    /*
    ** Return true if the prepared statement is guaranteed to not modify the
    ** database.
    */
    static bool sqlite3_stmt_readonly( sqlite3_stmt pStmt )
    {
      return pStmt != null ? ( (Vdbe)pStmt ).readOnly : true;
    }

    /*
    ** Return a pointer to the next prepared statement after pStmt associated
    ** with database connection pDb.  If pStmt is NULL, return the first
    ** prepared statement for the database connection.  Return NULL if there
    ** are no more.
    */

    public static sqlite3_stmt sqlite3_next_stmt( sqlite3 pDb, sqlite3_stmt pStmt )
    {
      sqlite3_stmt pNext;
      sqlite3_mutex_enter( pDb.mutex );
      if ( pStmt == null )
      {
        pNext = (sqlite3_stmt)pDb.pVdbe;
      }
      else
      {
        pNext = (sqlite3_stmt)( (Vdbe)pStmt ).pNext;
      }
      sqlite3_mutex_leave( pDb.mutex );
      return pNext;
    }
    /*
    ** Return the value of a status counter for a prepared statement
    */
    public static int sqlite3_stmt_status( sqlite3_stmt pStmt, int op, int resetFlag )
    {
      Vdbe pVdbe = (Vdbe)pStmt;
      int v = pVdbe.aCounter[op - 1];
      if ( resetFlag != 0 )
        pVdbe.aCounter[op - 1] = 0;
      return v;
    }
  }
}
