using System;
using System.Diagnostics;
using System.Text;

using u8 = System.Byte;
using u32 = System.UInt32;
namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    ** This file contains the implementation for TRIGGERs
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-06-23 19:49:22 4374b7e83ea0a3fbc3691f9c0c936272862f32f2
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"

#if !SQLITE_OMIT_TRIGGER
    /*
** Delete a linked list of TriggerStep structures.
*/
    static void sqlite3DeleteTriggerStep( sqlite3 db, ref TriggerStep pTriggerStep )
    {
      while ( pTriggerStep != null )
      {
        TriggerStep pTmp = pTriggerStep;
        pTriggerStep = pTriggerStep.pNext;

        sqlite3ExprDelete( db, ref pTmp.pWhere );
        sqlite3ExprListDelete( db, ref pTmp.pExprList );
        sqlite3SelectDelete( db, ref pTmp.pSelect );
        sqlite3IdListDelete( db, ref pTmp.pIdList );

        pTriggerStep = null;
        sqlite3DbFree( db, ref pTmp );
      }
    }

    /*
    ** Given table pTab, return a list of all the triggers attached to
    ** the table. The list is connected by Trigger.pNext pointers.
    **
    ** All of the triggers on pTab that are in the same database as pTab
    ** are already attached to pTab.pTrigger.  But there might be additional
    ** triggers on pTab in the TEMP schema.  This routine prepends all
    ** TEMP triggers on pTab to the beginning of the pTab.pTrigger list
    ** and returns the combined list.
    **
    ** To state it another way:  This routine returns a list of all triggers
    ** that fire off of pTab.  The list will include any TEMP triggers on
    ** pTab as well as the triggers lised in pTab.pTrigger.
    */
    static Trigger sqlite3TriggerList( Parse pParse, Table pTab )
    {
      Schema pTmpSchema = pParse.db.aDb[1].pSchema;
      Trigger pList = null;                  /* List of triggers to return */

      if ( pParse.disableTriggers != 0 )
      {
        return null;
      }

      if ( pTmpSchema != pTab.pSchema )
      {
        HashElem p;
        Debug.Assert( sqlite3SchemaMutexHeld( pParse.db, 0, pTmpSchema ) );
        for ( p = sqliteHashFirst( pTmpSchema.trigHash ); p != null; p = sqliteHashNext( p ) )
        {
          Trigger pTrig = (Trigger)sqliteHashData( p );
          if ( pTrig.pTabSchema == pTab.pSchema
          && pTrig.table.Equals( pTab.zName, StringComparison.OrdinalIgnoreCase ) )
          {
            pTrig.pNext = ( pList != null ? pList : pTab.pTrigger );
            pList = pTrig;
          }
        }
      }

      return ( pList != null ? pList : pTab.pTrigger );
    }

    /*
    ** This is called by the parser when it sees a CREATE TRIGGER statement
    ** up to the point of the BEGIN before the trigger actions.  A Trigger
    ** structure is generated based on the information available and stored
    ** in pParse.pNewTrigger.  After the trigger actions have been parsed, the
    ** sqlite3FinishTrigger() function is called to complete the trigger
    ** construction process.
    */
    static void sqlite3BeginTrigger(
    Parse pParse,      /* The parse context of the CREATE TRIGGER statement */
    Token pName1,      /* The name of the trigger */
    Token pName2,      /* The name of the trigger */
    int tr_tm,         /* One of TK_BEFORE, TK_AFTER, TK_INSTEAD */
    int op,             /* One of TK_INSERT, TK_UPDATE, TK_DELETE */
    IdList pColumns,   /* column list if this is an UPDATE OF trigger */
    SrcList pTableName,/* The name of the table/view the trigger applies to */
    Expr pWhen,        /* WHEN clause */
    int isTemp,        /* True if the TEMPORARY keyword is present */
    int noErr          /* Suppress errors if the trigger already exists */
    )
    {
      Trigger pTrigger = null;      /* The new trigger */
      Table pTab;                   /* Table that the trigger fires off of */
      string zName = null;          /* Name of the trigger */
      sqlite3 db = pParse.db;       /* The database connection */
      int iDb;                      /* The database to store the trigger in */
      Token pName = null;           /* The unqualified db name */
      DbFixer sFix = new DbFixer(); /* State vector for the DB fixer */
      int iTabDb;                   /* Index of the database holding pTab */

      Debug.Assert( pName1 != null );   /* pName1.z might be NULL, but not pName1 itself */
      Debug.Assert( pName2 != null );
      Debug.Assert( op == TK_INSERT || op == TK_UPDATE || op == TK_DELETE );
      Debug.Assert( op > 0 && op < 0xff );
      if ( isTemp != 0 )
      {
        /* If TEMP was specified, then the trigger name may not be qualified. */
        if ( pName2.n > 0 )
        {
          sqlite3ErrorMsg( pParse, "temporary trigger may not have qualified name" );
          goto trigger_cleanup;
        }
        iDb = 1;
        pName = pName1;
      }
      else
      {
        /* Figure out the db that the the trigger will be created in */
        iDb = sqlite3TwoPartName( pParse, pName1, pName2, ref pName );
        if ( iDb < 0 )
        {
          goto trigger_cleanup;
        }
      }
      if ( null == pTableName ) //|| db.mallocFailed 
      {
        goto trigger_cleanup;
      }

      /* A long-standing parser bug is that this syntax was allowed:
      **
      **    CREATE TRIGGER attached.demo AFTER INSERT ON attached.tab ....
      **                                                 ^^^^^^^^
      **
      ** To maintain backwards compatibility, ignore the database
      ** name on pTableName if we are reparsing our of SQLITE_MASTER.
      */
      if ( db.init.busy != 0 && iDb != 1 )
      {
        //sqlite3DbFree( db, pTableName.a[0].zDatabase );
        pTableName.a[0].zDatabase = null;
      }

      /* If the trigger name was unqualified, and the table is a temp table,
      ** then set iDb to 1 to create the trigger in the temporary database.
      ** If sqlite3SrcListLookup() returns 0, indicating the table does not
      ** exist, the error is caught by the block below.
      */
      if ( pTableName == null /*|| db.mallocFailed != 0 */ )
      {
        goto trigger_cleanup;
      }
      pTab = sqlite3SrcListLookup( pParse, pTableName );
      if ( db.init.busy == 0 && pName2.n == 0 && pTab != null
            && pTab.pSchema == db.aDb[1].pSchema )
      {
        iDb = 1;
      }

      /* Ensure the table name matches database name and that the table exists */
      //      if ( db.mallocFailed != 0 ) goto trigger_cleanup;
      Debug.Assert( pTableName.nSrc == 1 );
      if ( sqlite3FixInit( sFix, pParse, iDb, "trigger", pName ) != 0 &&
      sqlite3FixSrcList( sFix, pTableName ) != 0 )
      {
        goto trigger_cleanup;
      }
      pTab = sqlite3SrcListLookup( pParse, pTableName );
      if ( pTab == null )
      {
        /* The table does not exist. */
        if ( db.init.iDb == 1 )
        {
          /* Ticket #3810.
          ** Normally, whenever a table is dropped, all associated triggers are
          ** dropped too.  But if a TEMP trigger is created on a non-TEMP table
          ** and the table is dropped by a different database connection, the
          ** trigger is not visible to the database connection that does the
          ** drop so the trigger cannot be dropped.  This results in an
          ** "orphaned trigger" - a trigger whose associated table is missing.
          */
          db.init.orphanTrigger = 1;
        }
        goto trigger_cleanup;
      }
      if ( IsVirtual( pTab ) )
      {
        sqlite3ErrorMsg( pParse, "cannot create triggers on virtual tables" );
        goto trigger_cleanup;
      }

      /* Check that the trigger name is not reserved and that no trigger of the
      ** specified name exists */
      zName = sqlite3NameFromToken( db, pName );
      if ( zName == null || SQLITE_OK != sqlite3CheckObjectName( pParse, zName ) )
      {
        goto trigger_cleanup;
      }
      Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
      if ( sqlite3HashFind( ( db.aDb[iDb].pSchema.trigHash ),
      zName, sqlite3Strlen30( zName ), (Trigger)null ) != null )
      {
        if ( noErr == 0 )
        {
          sqlite3ErrorMsg( pParse, "trigger %T already exists", pName );
        }
        else
        {
          Debug.Assert( 0==db.init.busy );
          sqlite3CodeVerifySchema( pParse, iDb );
        }
        goto trigger_cleanup;
      }

      /* Do not create a trigger on a system table */
      if ( pTab.zName.StartsWith( "sqlite_", System.StringComparison.OrdinalIgnoreCase ) )
      {
        sqlite3ErrorMsg( pParse, "cannot create trigger on system table" );
        pParse.nErr++;
        goto trigger_cleanup;
      }

      /* INSTEAD of triggers are only for views and views only support INSTEAD
      ** of triggers.
      */
      if ( pTab.pSelect != null && tr_tm != TK_INSTEAD )
      {
        sqlite3ErrorMsg( pParse, "cannot create %s trigger on view: %S",
        ( tr_tm == TK_BEFORE ) ? "BEFORE" : "AFTER", pTableName, 0 );
        goto trigger_cleanup;
      }
      if ( pTab.pSelect == null && tr_tm == TK_INSTEAD )
      {
        sqlite3ErrorMsg( pParse, "cannot create INSTEAD OF" +
        " trigger on table: %S", pTableName, 0 );
        goto trigger_cleanup;
      }
      iTabDb = sqlite3SchemaToIndex( db, pTab.pSchema );

#if !SQLITE_OMIT_AUTHORIZATION
{
int code = SQLITE_CREATE_TRIGGER;
string zDb = db.aDb[iTabDb].zName;
string zDbTrig = isTemp ? db.aDb[1].zName : zDb;
if( iTabDb==1 || isTemp ) code = SQLITE_CREATE_TEMP_TRIGGER;
if( sqlite3AuthCheck(pParse, code, zName, pTab.zName, zDbTrig) ){
goto trigger_cleanup;
}
if( sqlite3AuthCheck(pParse, SQLITE_INSERT, SCHEMA_TABLE(iTabDb),0,zDb)){
goto trigger_cleanup;
}
}
#endif

      /* INSTEAD OF triggers can only appear on views and BEFORE triggers
** cannot appear on views.  So we might as well translate every
** INSTEAD OF trigger into a BEFORE trigger.  It simplifies code
** elsewhere.
*/
      if ( tr_tm == TK_INSTEAD )
      {
        tr_tm = TK_BEFORE;
      }

      /* Build the Trigger object */
      pTrigger = new Trigger();// (Trigger*)sqlite3DbMallocZero( db, sizeof(Trigger ))
      if ( pTrigger == null )
        goto trigger_cleanup;
      pTrigger.zName = zName;
      pTrigger.table = pTableName.a[0].zName;// sqlite3DbStrDup( db, pTableName.a[0].zName );
      pTrigger.pSchema = db.aDb[iDb].pSchema;
      pTrigger.pTabSchema = pTab.pSchema;
      pTrigger.op = (u8)op;
      pTrigger.tr_tm = tr_tm == TK_BEFORE ? TRIGGER_BEFORE : TRIGGER_AFTER;
      pTrigger.pWhen = sqlite3ExprDup( db, pWhen, EXPRDUP_REDUCE );
      pTrigger.pColumns = sqlite3IdListDup( db, pColumns );
      Debug.Assert( pParse.pNewTrigger == null );
      pParse.pNewTrigger = pTrigger;

trigger_cleanup:
      sqlite3DbFree( db, ref zName );
      sqlite3SrcListDelete( db, ref pTableName );
      sqlite3IdListDelete( db, ref pColumns );
      sqlite3ExprDelete( db, ref pWhen );
      if ( pParse.pNewTrigger == null )
      {
        sqlite3DeleteTrigger( db, ref pTrigger );
      }
      else
      {
        Debug.Assert( pParse.pNewTrigger == pTrigger );
      }
    }

    /*
    ** This routine is called after all of the trigger actions have been parsed
    ** in order to complete the process of building the trigger.
    */
    static void sqlite3FinishTrigger(
    Parse pParse,          /* Parser context */
    TriggerStep pStepList, /* The triggered program */
    Token pAll             /* Token that describes the complete CREATE TRIGGER */
    )
    {
      Trigger pTrig = pParse.pNewTrigger; /* Trigger being finished */
      string zName;                       /* Name of trigger */

      sqlite3 db = pParse.db;             /* The database */
      DbFixer sFix = new DbFixer();       /* Fixer object */
      int iDb;                            /* Database containing the trigger */
      Token nameToken = new Token();      /* Trigger name for error reporting */

      pParse.pNewTrigger = null;
      if ( NEVER( pParse.nErr != 0 ) || pTrig == null )
        goto triggerfinish_cleanup;
      zName = pTrig.zName;
      iDb = sqlite3SchemaToIndex( pParse.db, pTrig.pSchema );
      pTrig.step_list = pStepList;
      while ( pStepList != null )
      {
        pStepList.pTrig = pTrig;
        pStepList = pStepList.pNext;
      }
      nameToken.z = pTrig.zName;
      nameToken.n = sqlite3Strlen30( nameToken.z );
      if ( sqlite3FixInit( sFix, pParse, iDb, "trigger", nameToken ) != 0
      && sqlite3FixTriggerStep( sFix, pTrig.step_list ) != 0 )
      {
        goto triggerfinish_cleanup;
      }

      /* if we are not initializing,
      ** build the sqlite_master entry
      */
      if ( 0 == db.init.busy )
      {
        Vdbe v;
        string z;

        /* Make an entry in the sqlite_master table */
        v = sqlite3GetVdbe( pParse );
        if ( v == null )
          goto triggerfinish_cleanup;
        sqlite3BeginWriteOperation( pParse, 0, iDb );
        z = pAll.z.Substring( 0, pAll.n );//sqlite3DbStrNDup( db, (char*)pAll.z, pAll.n );
        sqlite3NestedParse( pParse,
        "INSERT INTO %Q.%s VALUES('trigger',%Q,%Q,0,'CREATE TRIGGER %q')",
        db.aDb[iDb].zName, SCHEMA_TABLE( iDb ), zName,
        pTrig.table, z );
        sqlite3DbFree( db, ref z );
        sqlite3ChangeCookie( pParse, iDb );
        sqlite3VdbeAddParseSchemaOp( v, iDb,
            sqlite3MPrintf( db, "type='trigger' AND name='%q'", zName ) );
      }

      if ( db.init.busy != 0 )
      {
        Trigger pLink = pTrig;
        Hash pHash = db.aDb[iDb].pSchema.trigHash;
        Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
        pTrig = sqlite3HashInsert( ref pHash, zName, sqlite3Strlen30( zName ), pTrig );
        if ( pTrig != null )
        {
          //db.mallocFailed = 1;
        }
        else if ( pLink.pSchema == pLink.pTabSchema )
        {
          Table pTab;
          int n = sqlite3Strlen30( pLink.table );
          pTab = sqlite3HashFind( pLink.pTabSchema.tblHash, pLink.table, n, (Table)null );
          Debug.Assert( pTab != null );
          pLink.pNext = pTab.pTrigger;
          pTab.pTrigger = pLink;
        }
      }

triggerfinish_cleanup:
      sqlite3DeleteTrigger( db, ref pTrig );
      Debug.Assert( pParse.pNewTrigger == null );
      sqlite3DeleteTriggerStep( db, ref pStepList );
    }

    /*
    ** Turn a SELECT statement (that the pSelect parameter points to) into
    ** a trigger step.  Return a pointer to a TriggerStep structure.
    **
    ** The parser calls this routine when it finds a SELECT statement in
    ** body of a TRIGGER.
    */
    static TriggerStep sqlite3TriggerSelectStep( sqlite3 db, Select pSelect )
    {
      TriggerStep pTriggerStep = new TriggerStep();// sqlite3DbMallocZero( db, sizeof(TriggerStep ))
      if ( pTriggerStep == null )
      {
        sqlite3SelectDelete( db, ref pSelect );
        return null;
      }

      pTriggerStep.op = TK_SELECT;
      pTriggerStep.pSelect = pSelect;
      pTriggerStep.orconf = OE_Default;
      return pTriggerStep;
    }

    /*
    ** Allocate space to hold a new trigger step.  The allocated space
    ** holds both the TriggerStep object and the TriggerStep.target.z string.
    **
    ** If an OOM error occurs, NULL is returned and db.mallocFailed is set.
    */
    static TriggerStep triggerStepAllocate(
    sqlite3 db,                /* Database connection */
    u8 op,                     /* Trigger opcode */
    Token pName                /* The target name */
    )
    {
      TriggerStep pTriggerStep;

      pTriggerStep = new TriggerStep();// sqlite3DbMallocZero( db, sizeof( TriggerStep ) + pName.n );
      //if ( pTriggerStep != null )
      //{
      string z;// = (char*)&pTriggerStep[1];
      z = pName.z;// memcpy( z, pName.z, pName.n );
      pTriggerStep.target.z = z;
      pTriggerStep.target.n = pName.n;
      pTriggerStep.op = op;
      //}
      return pTriggerStep;
    }

    /*
    ** Build a trigger step out of an INSERT statement.  Return a pointer
    ** to the new trigger step.
    **
    ** The parser calls this routine when it sees an INSERT inside the
    ** body of a trigger.
    */
    // OVERLOADS, so I don't need to rewrite parse.c
    static TriggerStep sqlite3TriggerInsertStep( sqlite3 db, Token pTableName, IdList pColumn, int null_4, int null_5, u8 orconf )
    {
      return sqlite3TriggerInsertStep( db, pTableName, pColumn, null, null, orconf );
    }
    static TriggerStep sqlite3TriggerInsertStep( sqlite3 db, Token pTableName, IdList pColumn, ExprList pEList, int null_5, u8 orconf )
    {
      return sqlite3TriggerInsertStep( db, pTableName, pColumn, pEList, null, orconf );
    }
    static TriggerStep sqlite3TriggerInsertStep( sqlite3 db, Token pTableName, IdList pColumn, int null_4, Select pSelect, u8 orconf )
    {
      return sqlite3TriggerInsertStep( db, pTableName, pColumn, null, pSelect, orconf );
    }
    static TriggerStep sqlite3TriggerInsertStep(
    sqlite3 db,        /* The database connection */
    Token pTableName,  /* Name of the table into which we insert */
    IdList pColumn,    /* List of columns in pTableName to insert into */
    ExprList pEList,   /* The VALUE clause: a list of values to be inserted */
    Select pSelect,    /* A SELECT statement that supplies values */
    u8 orconf          /* The conflict algorithm (OE_Abort, OE_Replace, etc.) */
    )
    {
      TriggerStep pTriggerStep;

      Debug.Assert( pEList == null || pSelect == null );
      Debug.Assert( pEList != null || pSelect != null /*|| db.mallocFailed != 0 */ );

      pTriggerStep = triggerStepAllocate( db, TK_INSERT, pTableName );
      //if ( pTriggerStep != null )
      //{
      pTriggerStep.pSelect = sqlite3SelectDup( db, pSelect, EXPRDUP_REDUCE );
      pTriggerStep.pIdList = pColumn;
      pTriggerStep.pExprList = sqlite3ExprListDup( db, pEList, EXPRDUP_REDUCE );
      pTriggerStep.orconf = orconf;
      //}
      //else
      //{
      //  sqlite3IdListDelete( db, ref pColumn );
      //}
      sqlite3ExprListDelete( db, ref pEList );
      sqlite3SelectDelete( db, ref pSelect );

      return pTriggerStep;
    }

    /*
    ** Construct a trigger step that implements an UPDATE statement and return
    ** a pointer to that trigger step.  The parser calls this routine when it
    ** sees an UPDATE statement inside the body of a CREATE TRIGGER.
    */
    static TriggerStep sqlite3TriggerUpdateStep(
    sqlite3 db,         /* The database connection */
    Token pTableName,   /* Name of the table to be updated */
    ExprList pEList,    /* The SET clause: list of column and new values */
    Expr pWhere,        /* The WHERE clause */
    u8 orconf           /* The conflict algorithm. (OE_Abort, OE_Ignore, etc) */
    )
    {
      TriggerStep pTriggerStep;

      pTriggerStep = triggerStepAllocate( db, TK_UPDATE, pTableName );
      //if ( pTriggerStep != null )
      //{
      pTriggerStep.pExprList = sqlite3ExprListDup( db, pEList, EXPRDUP_REDUCE );
      pTriggerStep.pWhere = sqlite3ExprDup( db, pWhere, EXPRDUP_REDUCE );
      pTriggerStep.orconf = orconf;
      //}
      sqlite3ExprListDelete( db, ref pEList );
      sqlite3ExprDelete( db, ref pWhere );
      return pTriggerStep;
    }

    /*
    ** Construct a trigger step that implements a DELETE statement and return
    ** a pointer to that trigger step.  The parser calls this routine when it
    ** sees a DELETE statement inside the body of a CREATE TRIGGER.
    */
    static TriggerStep sqlite3TriggerDeleteStep(
    sqlite3 db,            /* Database connection */
    Token pTableName,      /* The table from which rows are deleted */
    Expr pWhere            /* The WHERE clause */
    )
    {
      TriggerStep pTriggerStep;

      pTriggerStep = triggerStepAllocate( db, TK_DELETE, pTableName );
      //if ( pTriggerStep != null )
      //{
      pTriggerStep.pWhere = sqlite3ExprDup( db, pWhere, EXPRDUP_REDUCE );
      pTriggerStep.orconf = OE_Default;
      //}
      sqlite3ExprDelete( db, ref pWhere );
      return pTriggerStep;
    }



    /*
    ** Recursively delete a Trigger structure
    */
    static void sqlite3DeleteTrigger( sqlite3 db, ref Trigger pTrigger )
    {
      if ( pTrigger == null )
        return;
      sqlite3DeleteTriggerStep( db, ref pTrigger.step_list );
      sqlite3DbFree( db, ref pTrigger.zName );
      sqlite3DbFree( db, ref pTrigger.table );
      sqlite3ExprDelete( db, ref pTrigger.pWhen );
      sqlite3IdListDelete( db, ref pTrigger.pColumns );
      pTrigger = null;
      sqlite3DbFree( db, ref pTrigger );
    }

    /*
    ** This function is called to drop a trigger from the database schema.
    **
    ** This may be called directly from the parser and therefore identifies
    ** the trigger by name.  The sqlite3DropTriggerPtr() routine does the
    ** same job as this routine except it takes a pointer to the trigger
    ** instead of the trigger name.
    **/
    static void sqlite3DropTrigger( Parse pParse, SrcList pName, int noErr )
    {
      Trigger pTrigger = null;
      int i;
      string zDb;
      string zName;
      int nName;
      sqlite3 db = pParse.db;

      //      if ( db.mallocFailed != 0 ) goto drop_trigger_cleanup;
      if ( SQLITE_OK != sqlite3ReadSchema( pParse ) )
      {
        goto drop_trigger_cleanup;
      }

      Debug.Assert( pName.nSrc == 1 );
      zDb = pName.a[0].zDatabase;
      zName = pName.a[0].zName;
      nName = sqlite3Strlen30( zName );
      Debug.Assert( zDb != null || sqlite3BtreeHoldsAllMutexes( db ) );
      for ( i = OMIT_TEMPDB; i < db.nDb; i++ )
      {
        int j = ( i < 2 ) ? i ^ 1 : i;  /* Search TEMP before MAIN */
        if ( zDb != null && !db.aDb[j].zName.Equals( zDb ,StringComparison.OrdinalIgnoreCase )  )
          continue;
        Debug.Assert( sqlite3SchemaMutexHeld( db, j, null ) );
        pTrigger = sqlite3HashFind( ( db.aDb[j].pSchema.trigHash ), zName, nName, (Trigger)null );
        if ( pTrigger != null )
          break;
      }
      if ( pTrigger == null )
      {
        if ( noErr == 0 )
        {
          sqlite3ErrorMsg( pParse, "no such trigger: %S", pName, 0 );
        }
        else
        {
          sqlite3CodeVerifyNamedSchema( pParse, zDb );
        }
        pParse.checkSchema = 1;
        goto drop_trigger_cleanup;
      }
      sqlite3DropTriggerPtr( pParse, pTrigger );

drop_trigger_cleanup:
      sqlite3SrcListDelete( db, ref pName );
    }

    /*
    ** Return a pointer to the Table structure for the table that a trigger
    ** is set on.
    */
    static Table tableOfTrigger( Trigger pTrigger )
    {
      int n = sqlite3Strlen30( pTrigger.table );
      return sqlite3HashFind( pTrigger.pTabSchema.tblHash, pTrigger.table, n, (Table)null );
    }


    /*
    ** Drop a trigger given a pointer to that trigger.
    */
    static void sqlite3DropTriggerPtr( Parse pParse, Trigger pTrigger )
    {
      Table pTable;
      Vdbe v;
      sqlite3 db = pParse.db;
      int iDb;

      iDb = sqlite3SchemaToIndex( pParse.db, pTrigger.pSchema );
      Debug.Assert( iDb >= 0 && iDb < db.nDb );
      pTable = tableOfTrigger( pTrigger );
      Debug.Assert( pTable != null );
      Debug.Assert( pTable.pSchema == pTrigger.pSchema || iDb == 1 );
#if !SQLITE_OMIT_AUTHORIZATION
{
int code = SQLITE_DROP_TRIGGER;
string zDb = db.aDb[iDb].zName;
string zTab = SCHEMA_TABLE(iDb);
if( iDb==1 ) code = SQLITE_DROP_TEMP_TRIGGER;
if( sqlite3AuthCheck(pParse, code, pTrigger.name, pTable.zName, zDb) ||
sqlite3AuthCheck(pParse, SQLITE_DELETE, zTab, 0, zDb) ){
return;
}
}
#endif

      /* Generate code to destroy the database record of the trigger.
*/
      Debug.Assert( pTable != null );
      if ( ( v = sqlite3GetVdbe( pParse ) ) != null )
      {
        int _base;
        VdbeOpList[] dropTrigger = new VdbeOpList[]  {
new VdbeOpList( OP_Rewind,     0, ADDR(9),  0),
new VdbeOpList( OP_String8,    0, 1,        0), /* 1 */
new VdbeOpList( OP_Column,     0, 1,        2),
new VdbeOpList( OP_Ne,         2, ADDR(8),  1),
new VdbeOpList( OP_String8,    0, 1,        0), /* 4: "trigger" */
new VdbeOpList( OP_Column,     0, 0,        2),
new VdbeOpList( OP_Ne,         2, ADDR(8),  1),
new VdbeOpList( OP_Delete,     0, 0,        0),
new VdbeOpList( OP_Next,       0, ADDR(1),  0), /* 8 */
};

        sqlite3BeginWriteOperation( pParse, 0, iDb );
        sqlite3OpenMasterTable( pParse, iDb );
        _base = sqlite3VdbeAddOpList( v, dropTrigger.Length, dropTrigger );
        sqlite3VdbeChangeP4( v, _base + 1, pTrigger.zName, P4_TRANSIENT );
        sqlite3VdbeChangeP4( v, _base + 4, "trigger", P4_STATIC );
        sqlite3ChangeCookie( pParse, iDb );
        sqlite3VdbeAddOp2( v, OP_Close, 0, 0 );
        sqlite3VdbeAddOp4( v, OP_DropTrigger, iDb, 0, 0, pTrigger.zName, 0 );
        if ( pParse.nMem < 3 )
        {
          pParse.nMem = 3;
        }
      }
    }

    /*
    ** Remove a trigger from the hash tables of the sqlite* pointer.
    */
    static void sqlite3UnlinkAndDeleteTrigger( sqlite3 db, int iDb, string zName )
    {
      Trigger pTrigger;
      Hash pHash;

      Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
      pHash = ( db.aDb[iDb].pSchema.trigHash );
      pTrigger = sqlite3HashInsert( ref pHash, zName, sqlite3Strlen30( zName ), (Trigger)null );
      if ( ALWAYS( pTrigger != null ) )
      {
        if ( pTrigger.pSchema == pTrigger.pTabSchema )
        {
          Table pTab = tableOfTrigger( pTrigger );
          //Trigger** pp;
          //for ( pp = &pTab.pTrigger ; *pp != pTrigger ; pp = &( (*pp).pNext ) ) ;
          //*pp = (*pp).pNext;
          if ( pTab.pTrigger == pTrigger )
          {
            pTab.pTrigger = pTrigger.pNext;
          }
          else
          {
            Trigger cc = pTab.pTrigger;
            while ( cc != null )
            {
              if ( cc.pNext == pTrigger )
              {
                cc.pNext = cc.pNext.pNext;
                break;
              }
              cc = cc.pNext;
            }
            Debug.Assert( cc != null );
          }
        }
        sqlite3DeleteTrigger( db, ref pTrigger );
        db.flags |= SQLITE_InternChanges;
      }
    }

    /*
    ** pEList is the SET clause of an UPDATE statement.  Each entry
    ** in pEList is of the format <id>=<expr>.  If any of the entries
    ** in pEList have an <id> which matches an identifier in pIdList,
    ** then return TRUE.  If pIdList==NULL, then it is considered a
    ** wildcard that matches anything.  Likewise if pEList==NULL then
    ** it matches anything so always return true.  Return false only
    ** if there is no match.
    */
    static int checkColumnOverlap( IdList pIdList, ExprList pEList )
    {
      int e;
      if ( pIdList == null || NEVER( pEList == null ) )
        return 1;
      for ( e = 0; e < pEList.nExpr; e++ )
      {
        if ( sqlite3IdListIndex( pIdList, pEList.a[e].zName ) >= 0 )
          return 1;
      }
      return 0;
    }

    /*
    ** Return a list of all triggers on table pTab if there exists at least
    ** one trigger that must be fired when an operation of type 'op' is
    ** performed on the table, and, if that operation is an UPDATE, if at
    ** least one of the columns in pChanges is being modified.
    */
    static Trigger sqlite3TriggersExist(
    Parse pParse,          /* Parse context */
    Table pTab,            /* The table the contains the triggers */
    int op,                /* one of TK_DELETE, TK_INSERT, TK_UPDATE */
    ExprList pChanges,     /* Columns that change in an UPDATE statement */
    out int pMask          /* OUT: Mask of TRIGGER_BEFORE|TRIGGER_AFTER */
    )
    {
      int mask = 0;
      Trigger pList = null;
      Trigger p;

      if ( ( pParse.db.flags & SQLITE_EnableTrigger ) != 0 )
      {
        pList = sqlite3TriggerList( pParse, pTab );
      }
      Debug.Assert( pList == null || IsVirtual( pTab ) == false );
      for ( p = pList; p != null; p = p.pNext )
      {
        if ( p.op == op && checkColumnOverlap( p.pColumns, pChanges ) != 0 )
        {
          mask |= p.tr_tm;
        }
      }
      //if ( pMask != 0 )
      {
        pMask = mask;
      }
      return ( mask != 0 ? pList : null );
    }


    /*
    ** Convert the pStep.target token into a SrcList and return a pointer
    ** to that SrcList.
    **
    ** This routine adds a specific database name, if needed, to the target when
    ** forming the SrcList.  This prevents a trigger in one database from
    ** referring to a target in another database.  An exception is when the
    ** trigger is in TEMP in which case it can refer to any other database it
    ** wants.
    */
    static SrcList targetSrcList(
    Parse pParse,       /* The parsing context */
    TriggerStep pStep   /* The trigger containing the target token */
    )
    {
      int iDb;             /* Index of the database to use */
      SrcList pSrc;        /* SrcList to be returned */

      pSrc = sqlite3SrcListAppend( pParse.db, 0, pStep.target, 0 );
      //if ( pSrc != null )
      //{
      Debug.Assert( pSrc.nSrc > 0 );
      Debug.Assert( pSrc.a != null );
      iDb = sqlite3SchemaToIndex( pParse.db, pStep.pTrig.pSchema );
      if ( iDb == 0 || iDb >= 2 )
      {
        sqlite3 db = pParse.db;
        Debug.Assert( iDb < pParse.db.nDb );
        pSrc.a[pSrc.nSrc - 1].zDatabase = db.aDb[iDb].zName;// sqlite3DbStrDup( db, db.aDb[iDb].zName );
      }
      //}
      return pSrc;
    }

    /*
    ** Generate VDBE code for the statements inside the body of a single 
    ** trigger.
    */
    static int codeTriggerProgram(
    Parse pParse,            /* The parser context */
    TriggerStep pStepList,   /* List of statements inside the trigger body */
    int orconf               /* Conflict algorithm. (OE_Abort, etc) */
    )
    {
      TriggerStep pStep;
      Vdbe v = pParse.pVdbe;
      sqlite3 db = pParse.db;

      Debug.Assert( pParse.pTriggerTab != null && pParse.pToplevel != null );
      Debug.Assert( pStepList != null );
      Debug.Assert( v != null );
      for ( pStep = pStepList; pStep != null; pStep = pStep.pNext )
      {
        /* Figure out the ON CONFLICT policy that will be used for this step
        ** of the trigger program. If the statement that caused this trigger
        ** to fire had an explicit ON CONFLICT, then use it. Otherwise, use
        ** the ON CONFLICT policy that was specified as part of the trigger
        ** step statement. Example:
        **
        **   CREATE TRIGGER AFTER INSERT ON t1 BEGIN;
        **     INSERT OR REPLACE INTO t2 VALUES(new.a, new.b);
        **   END;
        **
        **   INSERT INTO t1 ... ;            -- insert into t2 uses REPLACE policy
        **   INSERT OR IGNORE INTO t1 ... ;  -- insert into t2 uses IGNORE policy
        */
        pParse.eOrconf = ( orconf == OE_Default ) ? pStep.orconf : (u8)orconf;

        switch ( pStep.op )
        {
          case TK_UPDATE:
            {
              sqlite3Update( pParse,
                targetSrcList( pParse, pStep ),
                sqlite3ExprListDup( db, pStep.pExprList, 0 ),
                sqlite3ExprDup( db, pStep.pWhere, 0 ),
                pParse.eOrconf
              );
              break;
            }
          case TK_INSERT:
            {
              sqlite3Insert( pParse,
                targetSrcList( pParse, pStep ),
                sqlite3ExprListDup( db, pStep.pExprList, 0 ),
                sqlite3SelectDup( db, pStep.pSelect, 0 ),
                sqlite3IdListDup( db, pStep.pIdList ),
                pParse.eOrconf
              );
              break;
            }
          case TK_DELETE:
            {
              sqlite3DeleteFrom( pParse,
                targetSrcList( pParse, pStep ),
                sqlite3ExprDup( db, pStep.pWhere, 0 )
              );
              break;
            }
          default:
            Debug.Assert( pStep.op == TK_SELECT );
            {
              SelectDest sDest = new SelectDest();
              Select pSelect = sqlite3SelectDup( db, pStep.pSelect, 0 );
              sqlite3SelectDestInit( sDest, SRT_Discard, 0 );
              sqlite3Select( pParse, pSelect, ref sDest );
              sqlite3SelectDelete( db, ref pSelect );
              break;
            }
        }
        if ( pStep.op != TK_SELECT )
        {
          sqlite3VdbeAddOp0( v, OP_ResetCount );
        }
      }

      return 0;
    }

#if SQLITE_DEBUG
    /*
** This function is used to add VdbeComment() annotations to a VDBE
** program. It is not used in production code, only for debugging.
*/
    static string onErrorText( int onError )
    {
      switch ( onError )
      {
        case OE_Abort:
          return "abort";
        case OE_Rollback:
          return "rollback";
        case OE_Fail:
          return "fail";
        case OE_Replace:
          return "replace";
        case OE_Ignore:
          return "ignore";
        case OE_Default:
          return "default";
      }
      return "n/a";
    }
#endif

    /*
** Parse context structure pFrom has just been used to create a sub-vdbe
** (trigger program). If an error has occurred, transfer error information
** from pFrom to pTo.
*/
    static void transferParseError( Parse pTo, Parse pFrom )
    {
      Debug.Assert( String.IsNullOrEmpty( pFrom.zErrMsg ) || pFrom.nErr != 0 );
      Debug.Assert( String.IsNullOrEmpty( pTo.zErrMsg ) || pTo.nErr != 0 );
      if ( pTo.nErr == 0 )
      {
        pTo.zErrMsg = pFrom.zErrMsg;
        pTo.nErr = pFrom.nErr;
      }
      else
      {
        sqlite3DbFree( pFrom.db, ref pFrom.zErrMsg );
      }
    }

    /*
    ** Create and populate a new TriggerPrg object with a sub-program 
    ** implementing trigger pTrigger with ON CONFLICT policy orconf.
    */
    static TriggerPrg codeRowTrigger(
      Parse pParse,        /* Current parse context */
      Trigger pTrigger,    /* Trigger to code */
      Table pTab,          /* The table pTrigger is attached to */
      int orconf           /* ON CONFLICT policy to code trigger program with */
    )
    {
      Parse pTop = sqlite3ParseToplevel( pParse );
      sqlite3 db = pParse.db;     /* Database handle */
      TriggerPrg pPrg;            /* Value to return */
      Expr pWhen = null;          /* Duplicate of trigger WHEN expression */
      Vdbe v;                     /* Temporary VM */
      NameContext sNC;            /* Name context for sub-vdbe */
      SubProgram pProgram = null; /* Sub-vdbe for trigger program */
      Parse pSubParse;            /* Parse context for sub-vdbe */
      int iEndTrigger = 0;        /* Label to jump to if WHEN is false */

      Debug.Assert( pTrigger.zName == null || pTab == tableOfTrigger( pTrigger ) );
      Debug.Assert( pTop.pVdbe != null );

      /* Allocate the TriggerPrg and SubProgram objects. To ensure that they
      ** are freed if an error occurs, link them into the Parse.pTriggerPrg 
      ** list of the top-level Parse object sooner rather than later.  */
      pPrg = new TriggerPrg();// sqlite3DbMallocZero( db, sizeof( TriggerPrg ) );
      //if ( null == pPrg ) return 0;
      pPrg.pNext = pTop.pTriggerPrg;
      pTop.pTriggerPrg = pPrg;
      pPrg.pProgram = pProgram = new SubProgram();// sqlite3DbMallocZero( db, sizeof( SubProgram ) );
      //if( null==pProgram ) return 0;
      sqlite3VdbeLinkSubProgram( pTop.pVdbe, pProgram );
      pPrg.pTrigger = pTrigger;
      pPrg.orconf = orconf;
      pPrg.aColmask[0] = 0xffffffff;
      pPrg.aColmask[1] = 0xffffffff;


      /* Allocate and populate a new Parse context to use for coding the 
      ** trigger sub-program.  */
      pSubParse = new Parse();// sqlite3StackAllocZero( db, sizeof( Parse ) );
      //if ( null == pSubParse ) return null;
      sNC = new NameContext();// memset( &sNC, 0, sizeof( sNC ) );
      sNC.pParse = pSubParse;
      pSubParse.db = db;
      pSubParse.pTriggerTab = pTab;
      pSubParse.pToplevel = pTop;
      pSubParse.zAuthContext = pTrigger.zName;
      pSubParse.eTriggerOp = pTrigger.op;
      pSubParse.nQueryLoop = pParse.nQueryLoop;

      v = sqlite3GetVdbe( pSubParse );
      if ( v != null )
      {
#if SQLITE_DEBUG
        VdbeComment( v, "Start: %s.%s (%s %s%s%s ON %s)",
          pTrigger.zName != null ? pTrigger.zName : "", onErrorText( orconf ),
          ( pTrigger.tr_tm == TRIGGER_BEFORE ? "BEFORE" : "AFTER" ),
            ( pTrigger.op == TK_UPDATE ? "UPDATE" : "" ),
            ( pTrigger.op == TK_INSERT ? "INSERT" : "" ),
            ( pTrigger.op == TK_DELETE ? "DELETE" : "" ),
          pTab.zName
        );
#endif
#if !SQLITE_OMIT_TRACE
        sqlite3VdbeChangeP4( v, -1,
          sqlite3MPrintf( db, "-- TRIGGER %s", pTrigger.zName ), P4_DYNAMIC
        );
#endif

        /* If one was specified, code the WHEN clause. If it evaluates to false
    ** (or NULL) the sub-vdbe is immediately halted by jumping to the 
    ** OP_Halt inserted at the end of the program.  */
        if ( pTrigger.pWhen != null )
        {
          pWhen = sqlite3ExprDup( db, pTrigger.pWhen, 0 );
          if ( SQLITE_OK == sqlite3ResolveExprNames( sNC, ref pWhen )
            //&& db.mallocFailed==0 
          )
          {
            iEndTrigger = sqlite3VdbeMakeLabel( v );
            sqlite3ExprIfFalse( pSubParse, pWhen, iEndTrigger, SQLITE_JUMPIFNULL );
          }
          sqlite3ExprDelete( db, ref pWhen );
        }

        /* Code the trigger program into the sub-vdbe. */
        codeTriggerProgram( pSubParse, pTrigger.step_list, orconf );

        /* Insert an OP_Halt at the end of the sub-program. */
        if ( iEndTrigger != 0 )
        {
          sqlite3VdbeResolveLabel( v, iEndTrigger );
        }
        sqlite3VdbeAddOp0( v, OP_Halt );
#if SQLITE_DEBUG
        VdbeComment( v, "End: %s.%s", pTrigger.zName, onErrorText( orconf ) );
#endif
        transferParseError( pParse, pSubParse );
        //if( db.mallocFailed==0 ){
        pProgram.aOp = sqlite3VdbeTakeOpArray( v, ref pProgram.nOp, ref pTop.nMaxArg );
        //}
        pProgram.nMem = pSubParse.nMem;
        pProgram.nCsr = pSubParse.nTab;
        pProgram.token = pTrigger.GetHashCode();
        pPrg.aColmask[0] = pSubParse.oldmask;
        pPrg.aColmask[1] = pSubParse.newmask;
        sqlite3VdbeDelete( ref v );
      }

      Debug.Assert( null == pSubParse.pAinc && null == pSubParse.pZombieTab );
      Debug.Assert( null == pSubParse.pTriggerPrg && 0 == pSubParse.nMaxArg );
      //sqlite3StackFree(db, pSubParse);

      return pPrg;
    }

    /*
    ** Return a pointer to a TriggerPrg object containing the sub-program for
    ** trigger pTrigger with default ON CONFLICT algorithm orconf. If no such
    ** TriggerPrg object exists, a new object is allocated and populated before
    ** being returned.
    */
    static TriggerPrg getRowTrigger(
      Parse pParse,        /* Current parse context */
      Trigger pTrigger,    /* Trigger to code */
      Table pTab,          /* The table trigger pTrigger is attached to */
      int orconf           /* ON CONFLICT algorithm. */
    )
    {
      Parse pRoot = sqlite3ParseToplevel( pParse );
      TriggerPrg pPrg;

      Debug.Assert( pTrigger.zName == null || pTab == tableOfTrigger( pTrigger ) );

      /* It may be that this trigger has already been coded (or is in the
      ** process of being coded). If this is the case, then an entry with
      ** a matching TriggerPrg.pTrigger field will be present somewhere
      ** in the Parse.pTriggerPrg list. Search for such an entry.  */
      for ( pPrg = pRoot.pTriggerPrg;
          pPrg != null && ( pPrg.pTrigger != pTrigger || pPrg.orconf != orconf );
          pPrg = pPrg.pNext
      )
        ;

      /* If an existing TriggerPrg could not be located, create a new one. */
      if ( null == pPrg )
      {
        pPrg = codeRowTrigger( pParse, pTrigger, pTab, orconf );
      }

      return pPrg;
    }

    /*
    ** Generate code for the trigger program associated with trigger p on 
    ** table pTab. The reg, orconf and ignoreJump parameters passed to this
    ** function are the same as those described in the header function for
    ** sqlite3CodeRowTrigger()
    */
    static void sqlite3CodeRowTriggerDirect(
      Parse pParse,        /* Parse context */
      Trigger p,           /* Trigger to code */
      Table pTab,          /* The table to code triggers from */
      int reg,             /* Reg array containing OLD.* and NEW.* values */
      int orconf,          /* ON CONFLICT policy */
      int ignoreJump       /* Instruction to jump to for RAISE(IGNORE) */
    )
    {
      Vdbe v = sqlite3GetVdbe( pParse ); /* Main VM */
      TriggerPrg pPrg;
      pPrg = getRowTrigger( pParse, p, pTab, orconf );
      Debug.Assert( pPrg != null || pParse.nErr != 0 );//|| pParse.db.mallocFailed );

      /* Code the OP_Program opcode in the parent VDBE. P4 of the OP_Program 
      ** is a pointer to the sub-vdbe containing the trigger program.  */
      if ( pPrg != null )
      {
        bool bRecursive = ( !String.IsNullOrEmpty( p.zName ) && 0 == ( pParse.db.flags & SQLITE_RecTriggers ) );
        sqlite3VdbeAddOp3( v, OP_Program, reg, ignoreJump, ++pParse.nMem );
        sqlite3VdbeChangeP4( v, -1, pPrg.pProgram, P4_SUBPROGRAM );
#if SQLITE_DEBUG
        VdbeComment
            ( v, "Call: %s.%s", ( !String.IsNullOrEmpty( p.zName ) ? p.zName : "fkey" ), onErrorText( orconf ) );
#endif

        /* Set the P5 operand of the OP_Program instruction to non-zero if
    ** recursive invocation of this trigger program is disallowed. Recursive
    ** invocation is disallowed if (a) the sub-program is really a trigger,
    ** not a foreign key action, and (b) the flag to enable recursive triggers
    ** is clear.  */
        sqlite3VdbeChangeP5( v, (u8)( bRecursive ? 1 : 0 ) );
      }
    }

    /*
    ** This is called to code the required FOR EACH ROW triggers for an operation
    ** on table pTab. The operation to code triggers for (INSERT, UPDATE or DELETE)
    ** is given by the op paramater. The tr_tm parameter determines whether the
    ** BEFORE or AFTER triggers are coded. If the operation is an UPDATE, then
    ** parameter pChanges is passed the list of columns being modified.
    **
    ** If there are no triggers that fire at the specified time for the specified
    ** operation on pTab, this function is a no-op.
    **
    ** The reg argument is the address of the first in an array of registers 
    ** that contain the values substituted for the new.* and old.* references
    ** in the trigger program. If N is the number of columns in table pTab
    ** (a copy of pTab.nCol), then registers are populated as follows:
    **
    **   Register       Contains
    **   ------------------------------------------------------
    **   reg+0          OLD.rowid
    **   reg+1          OLD.* value of left-most column of pTab
    **   ...            ...
    **   reg+N          OLD.* value of right-most column of pTab
    **   reg+N+1        NEW.rowid
    **   reg+N+2        OLD.* value of left-most column of pTab
    **   ...            ...
    **   reg+N+N+1      NEW.* value of right-most column of pTab
    **
    ** For ON DELETE triggers, the registers containing the NEW.* values will
    ** never be accessed by the trigger program, so they are not allocated or 
    ** populated by the caller (there is no data to populate them with anyway). 
    ** Similarly, for ON INSERT triggers the values stored in the OLD.* registers
    ** are never accessed, and so are not allocated by the caller. So, for an
    ** ON INSERT trigger, the value passed to this function as parameter reg
    ** is not a readable register, although registers (reg+N) through 
    ** (reg+N+N+1) are.
    **
    ** Parameter orconf is the default conflict resolution algorithm for the
    ** trigger program to use (REPLACE, IGNORE etc.). Parameter ignoreJump
    ** is the instruction that control should jump to if a trigger program
    ** raises an IGNORE exception.
    */
    static void sqlite3CodeRowTrigger(
    Parse pParse,        /* Parse context */
    Trigger pTrigger,    /* List of triggers on table pTab */
    int op,              /* One of TK_UPDATE, TK_INSERT, TK_DELETE */
    ExprList pChanges,   /* Changes list for any UPDATE OF triggers */
    int tr_tm,           /* One of TRIGGER_BEFORE, TRIGGER_AFTER */
    Table pTab,          /* The table to code triggers from */
    int reg,             /* The first in an array of registers (see above) */
    int orconf,          /* ON CONFLICT policy */
    int ignoreJump       /* Instruction to jump to for RAISE(IGNORE) */
    )
    {
      Trigger p;         /* Used to iterate through pTrigger list */

      Debug.Assert( op == TK_UPDATE || op == TK_INSERT || op == TK_DELETE );
      Debug.Assert( tr_tm == TRIGGER_BEFORE || tr_tm == TRIGGER_AFTER );
      Debug.Assert( ( op == TK_UPDATE ) == ( pChanges != null ) );

      for ( p = pTrigger; p != null; p = p.pNext )
      {

        /* Sanity checking:  The schema for the trigger and for the table are
        ** always defined.  The trigger must be in the same schema as the table
        ** or else it must be a TEMP trigger. */
        Debug.Assert( p.pSchema != null );
        Debug.Assert( p.pTabSchema != null );
        Debug.Assert( p.pSchema == p.pTabSchema
             || p.pSchema == pParse.db.aDb[1].pSchema );

        /* Determine whether we should code this trigger */
        if ( p.op == op
         && p.tr_tm == tr_tm
         && checkColumnOverlap( p.pColumns, pChanges ) != 0
        )
        {
          sqlite3CodeRowTriggerDirect( pParse, p, pTab, reg, orconf, ignoreJump );
        }
      }
    }

    /*
    ** Triggers may access values stored in the old.* or new.* pseudo-table. 
    ** This function returns a 32-bit bitmask indicating which columns of the 
    ** old.* or new.* tables actually are used by triggers. This information 
    ** may be used by the caller, for example, to avoid having to load the entire
    ** old.* record into memory when executing an UPDATE or DELETE command.
    **
    ** Bit 0 of the returned mask is set if the left-most column of the
    ** table may be accessed using an [old|new].<col> reference. Bit 1 is set if
    ** the second leftmost column value is required, and so on. If there
    ** are more than 32 columns in the table, and at least one of the columns
    ** with an index greater than 32 may be accessed, 0xffffffff is returned.
    **
    ** It is not possible to determine if the old.rowid or new.rowid column is 
    ** accessed by triggers. The caller must always assume that it is.
    **
    ** Parameter isNew must be either 1 or 0. If it is 0, then the mask returned
    ** applies to the old.* table. If 1, the new.* table.
    **
    ** Parameter tr_tm must be a mask with one or both of the TRIGGER_BEFORE
    ** and TRIGGER_AFTER bits set. Values accessed by BEFORE triggers are only
    ** included in the returned mask if the TRIGGER_BEFORE bit is set in the
    ** tr_tm parameter. Similarly, values accessed by AFTER triggers are only
    ** included in the returned mask if the TRIGGER_AFTER bit is set in tr_tm.
    */
    static u32 sqlite3TriggerColmask(
      Parse pParse,        /* Parse context */
      Trigger pTrigger,    /* List of triggers on table pTab */
      ExprList pChanges,   /* Changes list for any UPDATE OF triggers */
      int isNew,           /* 1 for new.* ref mask, 0 for old.* ref mask */
      int tr_tm,           /* Mask of TRIGGER_BEFORE|TRIGGER_AFTER */
      Table pTab,          /* The table to code triggers from */
      int orconf           /* Default ON CONFLICT policy for trigger steps */
    )
    {
      int op = pChanges != null ? TK_UPDATE : TK_DELETE;
      u32 mask = 0;
      Trigger p;

      Debug.Assert( isNew == 1 || isNew == 0 );
      for ( p = pTrigger; p != null; p = p.pNext )
      {
        if ( p.op == op && ( tr_tm & p.tr_tm ) != 0
         && checkColumnOverlap( p.pColumns, pChanges ) != 0
        )
        {
          TriggerPrg pPrg;
          pPrg = getRowTrigger( pParse, p, pTab, orconf );
          if ( pPrg != null )
          {
            mask |= pPrg.aColmask[isNew];
          }
        }
      }

      return mask;
    }
#endif // * !SQLITE_OMIT_TRIGGER) */

  }
}
