using System.Diagnostics;

namespace Community.CsharpSqlite
{
  using sqlite3_stmt = Sqlite3.Vdbe;

  public partial class Sqlite3
  {
    /*
    ** 2007 May 1
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
    ** This file contains code used to implement incremental BLOB I/O.
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

#if !SQLITE_OMIT_INCRBLOB
/*
** Valid sqlite3_blob* handles point to Incrblob structures.
*/
typedef struct Incrblob Incrblob;
struct Incrblob {
  int flags;              /* Copy of "flags" passed to sqlite3_blob_open() */
  int nByte;              /* Size of open blob, in bytes */
  int iOffset;            /* Byte offset of blob in cursor data */
  BtCursor *pCsr;         /* Cursor pointing at blob row */
  sqlite3_stmt *pStmt;    /* Statement holding cursor open */
  sqlite3 db;            /* The associated database */
};

/*
** Open a blob handle.
*/
int sqlite3_blob_open(
  sqlite3* db,            /* The database connection */
  string zDb,        /* The attached database containing the blob */
  string zTable,     /* The table containing the blob */
  string zColumn,    /* The column containing the blob */
  sqlite_int64 iRow,      /* The row containing the glob */
  int flags,              /* True -> read/write access, false -> read-only */
  sqlite3_blob **ppBlob   /* Handle for accessing the blob returned here */
){
  int nAttempt = 0;
  int iCol;               /* Index of zColumn in row-record */

  /* This VDBE program seeks a btree cursor to the identified 
  ** db/table/row entry. The reason for using a vdbe program instead
  ** of writing code to use the b-tree layer directly is that the
  ** vdbe program will take advantage of the various transaction,
  ** locking and error handling infrastructure built into the vdbe.
  **
  ** After seeking the cursor, the vdbe executes an OP_ResultRow.
  ** Code external to the Vdbe then "borrows" the b-tree cursor and
  ** uses it to implement the blob_read(), blob_write() and 
  ** blob_bytes() functions.
  **
  ** The sqlite3_blob_close() function finalizes the vdbe program,
  ** which closes the b-tree cursor and (possibly) commits the 
  ** transaction.
  */
  static const VdbeOpList openBlob[] = {
    {OP_Transaction, 0, 0, 0},     /* 0: Start a transaction */
    {OP_VerifyCookie, 0, 0, 0},    /* 1: Check the schema cookie */
    {OP_TableLock, 0, 0, 0},       /* 2: Acquire a read or write lock */

    /* One of the following two instructions is replaced by an OP_Noop. */
    {OP_OpenRead, 0, 0, 0},        /* 3: Open cursor 0 for reading */
    {OP_OpenWrite, 0, 0, 0},       /* 4: Open cursor 0 for read/write */

    {OP_Variable, 1, 1, 1},        /* 5: Push the rowid to the stack */
    {OP_NotExists, 0, 9, 1},       /* 6: Seek the cursor */
    {OP_Column, 0, 0, 1},          /* 7  */
    {OP_ResultRow, 1, 0, 0},       /* 8  */
    {OP_Close, 0, 0, 0},           /* 9  */
    {OP_Halt, 0, 0, 0},            /* 10 */
  };

  Vdbe *v = 0;
  int rc = SQLITE_OK;
  string zErr = 0;
  Table *pTab;
  Parse *pParse;

  *ppBlob = 0;
  sqlite3_mutex_enter(db->mutex);
  pParse = sqlite3StackAllocRaw(db, sizeof(*pParse));
  if( pParse==0 ){
    rc = SQLITE_NOMEM;
    goto blob_open_out;
  }
  do {
    memset(pParse, 0, sizeof(Parse));
    pParse->db = db;

    sqlite3BtreeEnterAll(db);
    pTab = sqlite3LocateTable(pParse, 0, zTable, zDb);
    if( pTab && IsVirtual(pTab) ){
      pTab = 0;
      sqlite3ErrorMsg(pParse, "cannot open virtual table: %s", zTable);
    }
#if !SQLITE_OMIT_VIEW
    if( pTab && pTab->pSelect ){
      pTab = 0;
      sqlite3ErrorMsg(pParse, "cannot open view: %s", zTable);
    }
#endif
    if( null==pTab ){
      if( pParse->zErrMsg ){
        sqlite3DbFree(db, zErr);
        zErr = pParse->zErrMsg;
        pParse->zErrMsg = 0;
      }
      rc = SQLITE_ERROR;
      sqlite3BtreeLeaveAll(db);
      goto blob_open_out;
    }

    /* Now search pTab for the exact column. */
    for(iCol=0; iCol < pTab->nCol; iCol++) {
      if( sqlite3StrICmp(pTab->aCol[iCol].zName, zColumn)==0 ){
        break;
      }
    }
    if( iCol==pTab->nCol ){
      sqlite3DbFree(db, zErr);
      zErr = sqlite3MPrintf(db, "no such column: \"%s\"", zColumn);
      rc = SQLITE_ERROR;
      sqlite3BtreeLeaveAll(db);
      goto blob_open_out;
    }

    /* If the value is being opened for writing, check that the
    ** column is not indexed, and that it is not part of a foreign key. 
    ** It is against the rules to open a column to which either of these
    ** descriptions applies for writing.  */
    if( flags ){
      string zFault = 0;
      Index *pIdx;
#if !SQLITE_OMIT_FOREIGN_KEY
      if( db->flags&SQLITE_ForeignKeys ){
        /* Check that the column is not part of an FK child key definition. It
        ** is not necessary to check if it is part of a parent key, as parent
        ** key columns must be indexed. The check below will pick up this 
        ** case.  */
        FKey *pFKey;
        for(pFKey=pTab->pFKey; pFKey; pFKey=pFKey->pNextFrom){
          int j;
          for(j=0; j<pFKey->nCol; j++){
            if( pFKey->aCol[j].iFrom==iCol ){
              zFault = "foreign key";
            }
          }
        }
      }
#endif
      for(pIdx=pTab->pIndex; pIdx; pIdx=pIdx->pNext){
        int j;
        for(j=0; j<pIdx->nColumn; j++){
          if( pIdx->aiColumn[j]==iCol ){
            zFault = "indexed";
          }
        }
      }
      if( zFault ){
        sqlite3DbFree(db, zErr);
        zErr = sqlite3MPrintf(db, "cannot open %s column for writing", zFault);
        rc = SQLITE_ERROR;
        sqlite3BtreeLeaveAll(db);
        goto blob_open_out;
      }
    }

    v = sqlite3VdbeCreate(db);
    if( v ){
      int iDb = sqlite3SchemaToIndex(db, pTab->pSchema);
      sqlite3VdbeAddOpList(v, sizeof(openBlob)/sizeof(VdbeOpList), openBlob);
      flags = !!flags;                 /* flags = (flags ? 1 : 0); */

      /* Configure the OP_Transaction */
      sqlite3VdbeChangeP1(v, 0, iDb);
      sqlite3VdbeChangeP2(v, 0, flags);

      /* Configure the OP_VerifyCookie */
      sqlite3VdbeChangeP1(v, 1, iDb);
      sqlite3VdbeChangeP2(v, 1, pTab->pSchema->schema_cookie);
      sqlite3VdbeChangeP3(v, 1, pTab->pSchema->iGeneration);

      /* Make sure a mutex is held on the table to be accessed */
      sqlite3VdbeUsesBtree(v, iDb); 

      /* Configure the OP_TableLock instruction */
#if SQLITE_OMIT_SHARED_CACHE
      sqlite3VdbeChangeToNoop(v, 2, 1);
#else
      sqlite3VdbeChangeP1(v, 2, iDb);
      sqlite3VdbeChangeP2(v, 2, pTab->tnum);
      sqlite3VdbeChangeP3(v, 2, flags);
      sqlite3VdbeChangeP4(v, 2, pTab->zName, P4_TRANSIENT);
#endif

      /* Remove either the OP_OpenWrite or OpenRead. Set the P2 
      ** parameter of the other to pTab->tnum.  */
      sqlite3VdbeChangeToNoop(v, 4 - flags, 1);
      sqlite3VdbeChangeP2(v, 3 + flags, pTab->tnum);
      sqlite3VdbeChangeP3(v, 3 + flags, iDb);

      /* Configure the number of columns. Configure the cursor to
      ** think that the table has one more column than it really
      ** does. An OP_Column to retrieve this imaginary column will
      ** always return an SQL NULL. This is useful because it means
      ** we can invoke OP_Column to fill in the vdbe cursors type 
      ** and offset cache without causing any IO.
      */
      sqlite3VdbeChangeP4(v, 3+flags, SQLITE_INT_TO_PTR(pTab->nCol+1),P4_INT32);
      sqlite3VdbeChangeP2(v, 7, pTab->nCol);
      if( null==db->mallocFailed ){
        pParse->nVar = 1;
        pParse->nMem = 1;
        pParse->nTab = 1;
        sqlite3VdbeMakeReady(v, pParse);
      }
    }
   
    sqlite3BtreeLeaveAll(db);
      goto blob_open_out;
    }

    sqlite3_bind_int64((sqlite3_stmt )v, 1, iRow);
    rc = sqlite3_step((sqlite3_stmt )v);
    if( rc!=SQLITE_ROW ){
      nAttempt++;
      rc = sqlite3_finalize((sqlite3_stmt )v);
      sqlite3DbFree(db, zErr);
      zErr = sqlite3MPrintf(db, sqlite3_errmsg(db));
      v = 0;
    }
  } while( nAttempt<5 && rc==SQLITE_SCHEMA );

  if( rc==SQLITE_ROW ){
    /* The row-record has been opened successfully. Check that the
    ** column in question contains text or a blob. If it contains
    ** text, it is up to the caller to get the encoding right.
    */
    Incrblob *pBlob;
    u32 type = v->apCsr[0]->aType[iCol];

    if( type<12 ){
      sqlite3DbFree(db, zErr);
      zErr = sqlite3MPrintf(db, "cannot open value of type %s",
          type==0?"null": type==7?"real": "integer"
      );
      rc = SQLITE_ERROR;
      goto blob_open_out;
    }
    pBlob = (Incrblob )sqlite3DbMallocZero(db, sizeof(Incrblob));
    if( db->mallocFailed ){
      sqlite3DbFree(db, ref pBlob);
      goto blob_open_out;
    }
    pBlob->flags = flags;
    pBlob->pCsr =  v->apCsr[0]->pCursor;
    sqlite3BtreeEnterCursor(pBlob->pCsr);
    sqlite3BtreeCacheOverflow(pBlob->pCsr);
    sqlite3BtreeLeaveCursor(pBlob->pCsr);
    pBlob->pStmt = (sqlite3_stmt )v;
    pBlob->iOffset = v->apCsr[0]->aOffset[iCol];
    pBlob->nByte = sqlite3VdbeSerialTypeLen(type);
    pBlob->db = db;
    *ppBlob = (sqlite3_blob )pBlob;
    rc = SQLITE_OK;
  }else if( rc==SQLITE_OK ){
    sqlite3DbFree(db, zErr);
    zErr = sqlite3MPrintf(db, "no such rowid: %lld", iRow);
    rc = SQLITE_ERROR;
  }

blob_open_out:
  if( v && (rc!=SQLITE_OK || db->mallocFailed) ){
    sqlite3VdbeFinalize(v);
  }
  sqlite3Error(db, rc, zErr);
  sqlite3DbFree(db, zErr);
  sqlite3StackFree(db, pParse);
  rc = sqlite3ApiExit(db, rc);
  sqlite3_mutex_leave(db->mutex);
  return rc;
}

/*
** Close a blob handle that was previously created using
** sqlite3_blob_open().
*/
int sqlite3_blob_close(sqlite3_blob *pBlob){
  Incrblob *p = (Incrblob )pBlob;
  int rc;
  sqlite3 db;

  if( p ){
    db = p->db;
    sqlite3_mutex_enter(db->mutex);
    rc = sqlite3_finalize(p->pStmt);
    sqlite3DbFree(db, ref p);
    sqlite3_mutex_leave(db->mutex);
  }else{
    rc = SQLITE_OK;
  }
  return rc;
}

/*
** Perform a read or write operation on a blob
*/
static int blobReadWrite(
  sqlite3_blob *pBlob, 
  void *z, 
  int n, 
  int iOffset, 
  int (*xCall)(BtCursor*, u32, u32, void)
){
  int rc;
  Incrblob *p = (Incrblob )pBlob;
  Vdbe *v;
  sqlite3 db;

  if( p==0 ) return SQLITE_MISUSE_BKPT();
  db = p->db;
  sqlite3_mutex_enter(db->mutex);
  v = (Vdbe)p->pStmt;

  if( n<0 || iOffset<0 || (iOffset+n)>p->nByte ){
    /* Request is out of range. Return a transient error. */
    rc = SQLITE_ERROR;
    sqlite3Error(db, SQLITE_ERROR, 0);
  } else if( v==0 ){
    /* If there is no statement handle, then the blob-handle has
    ** already been invalidated. Return SQLITE_ABORT in this case.
    */
    rc = SQLITE_ABORT;
  }else{
    /* Call either BtreeData() or BtreePutData(). If SQLITE_ABORT is
    ** returned, clean-up the statement handle.
    */
    Debug.Assert( db == v->db );
    sqlite3BtreeEnterCursor(p->pCsr);
    rc = xCall(p->pCsr, iOffset+p->iOffset, n, z);
    sqlite3BtreeLeaveCursor(p->pCsr);
    if( rc==SQLITE_ABORT ){
      sqlite3VdbeFinalize(v);
      p->pStmt = null;
    }else{
      db->errCode = rc;
      v->rc = rc;
    }
  }
  rc = sqlite3ApiExit(db, rc);
  sqlite3_mutex_leave(db->mutex);
  return rc;
}

/*
** Read data from a blob handle.
*/
int sqlite3_blob_read(sqlite3_blob *pBlob, object  *z, int n, int iOffset){
  return blobReadWrite(pBlob, z, n, iOffset, sqlite3BtreeData);
}

/*
** Write data to a blob handle.
*/
int sqlite3_blob_write(sqlite3_blob *pBlob, string z, int n, int iOffset){
  return blobReadWrite(pBlob, (void )z, n, iOffset, sqlite3BtreePutData);
}

/*
** Query a blob handle for the size of the data.
**
** The Incrblob.nByte field is fixed for the lifetime of the Incrblob
** so no mutex is required for access.
*/
int sqlite3_blob_bytes(sqlite3_blob *pBlob){
  Incrblob *p = (Incrblob )pBlob;
  return p ? p->nByte : 0;
}

#endif // * #if !SQLITE_OMIT_INCRBLOB */
  }
}
