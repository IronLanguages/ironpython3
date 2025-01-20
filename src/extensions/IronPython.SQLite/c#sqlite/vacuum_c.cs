using System;
using System.Diagnostics;
using System.Text;

using Pgno = System.UInt32;

using u32 = System.UInt32;

namespace Community.CsharpSqlite
{
  using sqlite3_stmt = Sqlite3.Vdbe;

  public partial class Sqlite3
  {
    /*
    ** 2003 April 6
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    ** This file contains code used to implement the VACUUM command.
    **
    ** Most of the code in this file may be omitted by defining the
    ** SQLITE_OMIT_VACUUM macro.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-05-19 13:26:54 ed1da510a239ea767a01dc332b667119fa3c908e
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"
    //#include "vdbeInt.h"

#if !SQLITE_OMIT_VACUUM && !SQLITE_OMIT_ATTACH
    /*
** Finalize a prepared statement.  If there was an error, store the
** text of the error message in *pzErrMsg.  Return the result code.
*/
    static int vacuumFinalize( sqlite3 db, sqlite3_stmt pStmt, string pzErrMsg )
    {
      int rc;
      rc = sqlite3VdbeFinalize( ref pStmt );
      if ( rc != 0 )
      {
        sqlite3SetString( ref pzErrMsg, db, sqlite3_errmsg( db ) );
      }
      return rc;
    }

    /*
** Execute zSql on database db. Return an error code.
*/
    static int execSql( sqlite3 db, string pzErrMsg, string zSql )
    {
      sqlite3_stmt pStmt = null;
#if !NDEBUG
      int rc;
      //VVA_ONLY( int rc; )
#endif
      if ( zSql == null )
      {
        return SQLITE_NOMEM;
      }
      if ( SQLITE_OK != sqlite3_prepare( db, zSql, -1, ref pStmt, 0 ) )
      {
        sqlite3SetString( ref pzErrMsg, db, sqlite3_errmsg( db ) );
        return sqlite3_errcode( db );
      }
#if !NDEBUG
      rc = sqlite3_step( pStmt );
      //VVA_ONLY( rc = ) sqlite3_step(pStmt);
      Debug.Assert( rc != SQLITE_ROW );
#else
sqlite3_step(pStmt);
#endif
      return vacuumFinalize( db, pStmt, pzErrMsg );
    }

    /*
    ** Execute zSql on database db. The statement returns exactly
    ** one column. Execute this as SQL on the same database.
    */
    static int execExecSql( sqlite3 db, string pzErrMsg, string zSql )
    {
      sqlite3_stmt pStmt = null;
      int rc;

      rc = sqlite3_prepare( db, zSql, -1, ref pStmt, 0 );
      if ( rc != SQLITE_OK )
        return rc;

      while ( SQLITE_ROW == sqlite3_step( pStmt ) )
      {
        rc = execSql( db, pzErrMsg, sqlite3_column_text( pStmt, 0 ) );
        if ( rc != SQLITE_OK )
        {
          vacuumFinalize( db, pStmt, pzErrMsg );
          return rc;
        }
      }

      return vacuumFinalize( db, pStmt, pzErrMsg );
    }

    /*
    ** The non-standard VACUUM command is used to clean up the database,
    ** collapse free space, etc.  It is modelled after the VACUUM command
    ** in PostgreSQL.
    **
    ** In version 1.0.x of SQLite, the VACUUM command would call
    ** gdbm_reorganize() on all the database tables.  But beginning
    ** with 2.0.0, SQLite no longer uses GDBM so this command has
    ** become a no-op.
    */
    static void sqlite3Vacuum( Parse pParse )
    {
      Vdbe v = sqlite3GetVdbe( pParse );
      if ( v != null )
      {
        sqlite3VdbeAddOp2( v, OP_Vacuum, 0, 0 );
      }
      return;
    }

    /*
    ** This routine implements the OP_Vacuum opcode of the VDBE.
    */
    static int sqlite3RunVacuum( ref string pzErrMsg, sqlite3 db )
    {
      int rc = SQLITE_OK;     /* Return code from service routines */
      Btree pMain;            /* The database being vacuumed */
      Btree pTemp;            /* The temporary database we vacuum into */
      string zSql = "";       /* SQL statements */
      int saved_flags;        /* Saved value of the db.flags */
      int saved_nChange;      /* Saved value of db.nChange */
      int saved_nTotalChange; /* Saved value of db.nTotalChange */
      dxTrace saved_xTrace;   //void (*saved_xTrace)(void*,const char*);  /* Saved db->xTrace */
      Db pDb = null;          /* Database to detach at end of vacuum */
      bool isMemDb;           /* True if vacuuming a :memory: database */
      int nRes;               /* Bytes of reserved space at the end of each page */
      int nDb;                /* Number of attached databases */

      if ( 0 == db.autoCommit )
      {
        sqlite3SetString( ref pzErrMsg, db, "cannot VACUUM from within a transaction" );
        return SQLITE_ERROR;
      }
      if ( db.activeVdbeCnt > 1 )
      {
        sqlite3SetString( ref pzErrMsg, db, "cannot VACUUM - SQL statements in progress" );
        return SQLITE_ERROR;
      }

      /* Save the current value of the database flags so that it can be 
      ** restored before returning. Then set the writable-schema flag, and
      ** disable CHECK and foreign key constraints.  */
      saved_flags = db.flags;
      saved_nChange = db.nChange;
      saved_nTotalChange = db.nTotalChange;
      saved_xTrace = db.xTrace;
      db.flags |= SQLITE_WriteSchema | SQLITE_IgnoreChecks | SQLITE_PreferBuiltin;
      db.flags &= ~( SQLITE_ForeignKeys | SQLITE_ReverseOrder );

      db.xTrace = null;

      pMain = db.aDb[0].pBt;
      isMemDb = sqlite3PagerIsMemdb( sqlite3BtreePager( pMain ) );

      /* Attach the temporary database as 'vacuum_db'. The synchronous pragma
      ** can be set to 'off' for this file, as it is not recovered if a crash
      ** occurs anyway. The integrity of the database is maintained by a
      ** (possibly synchronous) transaction opened on the main database before
      ** sqlite3BtreeCopyFile() is called.
      **
      ** An optimisation would be to use a non-journaled pager.
      ** (Later:) I tried setting "PRAGMA vacuum_db.journal_mode=OFF" but
      ** that actually made the VACUUM run slower.  Very little journalling
      ** actually occurs when doing a vacuum since the vacuum_db is initially
      ** empty.  Only the journal header is written.  Apparently it takes more
      ** time to parse and run the PRAGMA to turn journalling off than it does
      ** to write the journal header file.
      */
      nDb = db.nDb;
      if ( sqlite3TempInMemory( db ) )
      {
        zSql = "ATTACH ':memory:' AS vacuum_db;";
      }
      else
      {
        zSql = "ATTACH '' AS vacuum_db;";
      }
      rc = execSql( db, pzErrMsg, zSql );
      if ( db.nDb > nDb )
      {
        pDb = db.aDb[db.nDb - 1];
        Debug.Assert( pDb.zName == "vacuum_db" );
      }
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;
      pDb = db.aDb[db.nDb - 1];
      Debug.Assert( db.aDb[db.nDb - 1].zName == "vacuum_db" );
      pTemp = db.aDb[db.nDb - 1].pBt;

      /* The call to execSql() to attach the temp database has left the file
      ** locked (as there was more than one active statement when the transaction
      ** to read the schema was concluded. Unlock it here so that this doesn't
      ** cause problems for the call to BtreeSetPageSize() below.  */
      sqlite3BtreeCommit( pTemp );

      nRes = sqlite3BtreeGetReserve( pMain );

      /* A VACUUM cannot change the pagesize of an encrypted database. */
#if SQLITE_HAS_CODEC
      if ( db.nextPagesize != 0 )
      {
        //extern void sqlite3CodecGetKey(sqlite3*, int, void**, int*);
        int nKey;
        string zKey;
        sqlite3CodecGetKey( db, 0, out zKey, out nKey ); // sqlite3CodecGetKey(db, 0, (void**)&zKey, nKey);
        if ( nKey != 0 )
          db.nextPagesize = 0;
      }
#endif

      /* Do not attempt to change the page size for a WAL database */
      if ( sqlite3PagerGetJournalMode( sqlite3BtreePager( pMain ) )
                                                   == PAGER_JOURNALMODE_WAL )
      {
        db.nextPagesize = 0;
      }

      if ( sqlite3BtreeSetPageSize( pTemp, sqlite3BtreeGetPageSize( pMain ), nRes, 0 ) != 0
      || ( !isMemDb && sqlite3BtreeSetPageSize( pTemp, db.nextPagesize, nRes, 0 ) != 0 )
        //|| NEVER( db.mallocFailed != 0 )
      )
      {
        rc = SQLITE_NOMEM;
        goto end_of_vacuum;
      }
      rc = execSql( db, pzErrMsg, "PRAGMA vacuum_db.synchronous=OFF" );
      if ( rc != SQLITE_OK )
      {
        goto end_of_vacuum;
      }

#if !SQLITE_OMIT_AUTOVACUUM
      sqlite3BtreeSetAutoVacuum( pTemp, db.nextAutovac >= 0 ? db.nextAutovac :
                             sqlite3BtreeGetAutoVacuum( pMain ) );
#endif

      /* Begin a transaction */
      rc = execSql( db, pzErrMsg, "BEGIN EXCLUSIVE;" );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;

      /* Query the schema of the main database. Create a mirror schema
      ** in the temporary database.
      */
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'CREATE TABLE vacuum_db.' || substr(sql,14) " +
      "  FROM sqlite_master WHERE type='table' AND name!='sqlite_sequence'" +
      "   AND rootpage>0"
      );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'CREATE INDEX vacuum_db.' || substr(sql,14)" +
      "  FROM sqlite_master WHERE sql LIKE 'CREATE INDEX %' " );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'CREATE UNIQUE INDEX vacuum_db.' || substr(sql,21) " +
      "  FROM sqlite_master WHERE sql LIKE 'CREATE UNIQUE INDEX %'" );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;

      /* Loop through the tables in the main database. For each, do
      ** an "INSERT INTO vacuum_db.xxx SELECT * FROM main.xxx;" to copy
      ** the contents to the temporary database.
      */
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'INSERT INTO vacuum_db.' || quote(name) " +
      "|| ' SELECT * FROM main.' || quote(name) || ';'" +
      "FROM main.sqlite_master " +
      "WHERE type = 'table' AND name!='sqlite_sequence' " +
      "  AND rootpage>0"

      );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;

      /* Copy over the sequence table
      */
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'DELETE FROM vacuum_db.' || quote(name) || ';' " +
      "FROM vacuum_db.sqlite_master WHERE name='sqlite_sequence' "
      );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;
      rc = execExecSql( db, pzErrMsg,
      "SELECT 'INSERT INTO vacuum_db.' || quote(name) " +
      "|| ' SELECT * FROM main.' || quote(name) || ';' " +
      "FROM vacuum_db.sqlite_master WHERE name=='sqlite_sequence';"
      );
      if ( rc != SQLITE_OK )
        goto end_of_vacuum;


      /* Copy the triggers, views, and virtual tables from the main database
      ** over to the temporary database.  None of these objects has any
      ** associated storage, so all we have to do is copy their entries
      ** from the SQLITE_MASTER table.
      */
      rc = execSql( db, pzErrMsg,
      "INSERT INTO vacuum_db.sqlite_master " +
      "  SELECT type, name, tbl_name, rootpage, sql" +
      "    FROM main.sqlite_master" +
      "   WHERE type='view' OR type='trigger'" +
      "      OR (type='table' AND rootpage=0)"
      );
      if ( rc != 0 )
        goto end_of_vacuum;

      /* At this point, unless the main db was completely empty, there is now a
      ** transaction open on the vacuum database, but not on the main database.
      ** Open a btree level transaction on the main database. This allows a
      ** call to sqlite3BtreeCopyFile(). The main database btree level
      ** transaction is then committed, so the SQL level never knows it was
      ** opened for writing. This way, the SQL transaction used to create the
      ** temporary database never needs to be committed.
      */
      {
        u32 meta = 0;
        int i;

        /* This array determines which meta meta values are preserved in the
        ** vacuum.  Even entries are the meta value number and odd entries
        ** are an increment to apply to the meta value after the vacuum.
        ** The increment is used to increase the schema cookie so that other
        ** connections to the same database will know to reread the schema.
        */
        byte[] aCopy = new byte[]  {
BTREE_SCHEMA_VERSION,     1,  /* Add one to the old schema cookie */
BTREE_DEFAULT_CACHE_SIZE, 0,  /* Preserve the default page cache size */
BTREE_TEXT_ENCODING,      0,  /* Preserve the text encoding */
BTREE_USER_VERSION,       0,  /* Preserve the user version */
};

        Debug.Assert( sqlite3BtreeIsInTrans( pTemp ) );
        Debug.Assert( sqlite3BtreeIsInTrans( pMain ) );

        /* Copy Btree meta values */
        for ( i = 0; i < ArraySize( aCopy ); i += 2 )
        {
          /* GetMeta() and UpdateMeta() cannot fail in this context because
          ** we already have page 1 loaded into cache and marked dirty. */
          sqlite3BtreeGetMeta( pMain, aCopy[i], ref meta );
          rc = sqlite3BtreeUpdateMeta( pTemp, aCopy[i], (u32)( meta + aCopy[i + 1] ) );
          if ( NEVER( rc != SQLITE_OK ) )
            goto end_of_vacuum;
        }

        rc = sqlite3BtreeCopyFile( pMain, pTemp );
        if ( rc != SQLITE_OK )
          goto end_of_vacuum;
        rc = sqlite3BtreeCommit( pTemp );
        if ( rc != SQLITE_OK )
          goto end_of_vacuum;
#if !SQLITE_OMIT_AUTOVACUUM
        sqlite3BtreeSetAutoVacuum( pMain, sqlite3BtreeGetAutoVacuum( pTemp ) );
#endif
      }
      Debug.Assert( rc == SQLITE_OK );
      rc = sqlite3BtreeSetPageSize( pMain, sqlite3BtreeGetPageSize( pTemp ), nRes, 1 );

end_of_vacuum:
      /* Restore the original value of db.flags */
      db.flags = saved_flags;
      db.nChange = saved_nChange;
      db.nTotalChange = saved_nTotalChange;
      db.xTrace = saved_xTrace;
      sqlite3BtreeSetPageSize( pMain, -1, -1, 1 );

      /* Currently there is an SQL level transaction open on the vacuum
      ** database. No locks are held on any other files (since the main file
      ** was committed at the btree level). So it safe to end the transaction
      ** by manually setting the autoCommit flag to true and detaching the
      ** vacuum database. The vacuum_db journal file is deleted when the pager
      ** is closed by the DETACH.
      */
      db.autoCommit = 1;

      if ( pDb != null )
      {
        sqlite3BtreeClose( ref pDb.pBt );
        pDb.pBt = null;
        pDb.pSchema = null;
      }

      /* This both clears the schemas and reduces the size of the db->aDb[]
      ** array. */
      sqlite3ResetInternalSchema( db, -1 );

      return rc;
    }
#endif  // * SQLITE_OMIT_VACUUM && SQLITE_OMIT_ATTACH */
  }
}
