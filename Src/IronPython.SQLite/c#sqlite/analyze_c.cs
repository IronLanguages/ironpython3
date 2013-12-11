using System;
using System.Diagnostics;
using System.Text;

using u8 = System.Byte;

namespace Community.CsharpSqlite
{
  using sqlite3_int64 = System.Int64;
  using sqlite3_stmt = Sqlite3.Vdbe;

  public partial class Sqlite3
  {
/*
** 2005 July 8
**
** The author disclaims copyright to this source code.  In place of
** a legal notice, here is a blessing:
**
**    May you do good and not evil.
**    May you find forgiveness for yourself and forgive others.
**    May you share freely, never taking more than you give.
**
*************************************************************************
** This file contains code associated with the ANALYZE command.
*************************************************************************
**  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
**  C#-SQLite is an independent reimplementation of the SQLite software library
**
**  SQLITE_SOURCE_ID: 2011-05-19 13:26:54 ed1da510a239ea767a01dc332b667119fa3c908e
**
*************************************************************************
*/
#if !SQLITE_OMIT_ANALYZE
//#include "sqliteInt.h"

/*
** This routine generates code that opens the sqlite_stat1 table for
** writing with cursor iStatCur. If the library was built with the
** SQLITE_ENABLE_STAT2 macro defined, then the sqlite_stat2 table is
** opened for writing using cursor (iStatCur+1)
**
** If the sqlite_stat1 tables does not previously exist, it is created.
** Similarly, if the sqlite_stat2 table does not exist and the library
** is compiled with SQLITE_ENABLE_STAT2 defined, it is created. 
**
** Argument zWhere may be a pointer to a buffer containing a table name,
** or it may be a NULL pointer. If it is not NULL, then all entries in
** the sqlite_stat1 and (if applicable) sqlite_stat2 tables associated
** with the named table are deleted. If zWhere==0, then code is generated
** to delete all stat table entries.
*/
public struct _aTable
{
  public string zName;
  public string zCols;
  public _aTable( string zName, string zCols )
  {
    this.zName = zName;
    this.zCols = zCols;
  }
};
static _aTable[] aTable = new _aTable[]{
new _aTable( "sqlite_stat1", "tbl,idx,stat" ),
#if SQLITE_ENABLE_STAT2
new _aTable( "sqlite_stat2", "tbl,idx,sampleno,sample" ),
#endif
};

static void openStatTable(
Parse pParse,       /* Parsing context */
int iDb,            /* The database we are looking in */
int iStatCur,       /* Open the sqlite_stat1 table on this cursor */
string zWhere,      /* Delete entries for this table or index */
string zWhereType   /* Either "tbl" or "idx" */
)
{
  int[] aRoot = new int[] { 0, 0 };
  u8[] aCreateTbl = new u8[] { 0, 0 };

  int i;
  sqlite3 db = pParse.db;
  Db pDb;
  Vdbe v = sqlite3GetVdbe( pParse );

  if ( v == null )
    return;
  Debug.Assert( sqlite3BtreeHoldsAllMutexes( db ) );
  Debug.Assert( sqlite3VdbeDb( v ) == db );
  pDb = db.aDb[iDb];

  for ( i = 0; i < ArraySize( aTable ); i++ )
  {
    string zTab = aTable[i].zName;
    Table pStat;
    if ( ( pStat = sqlite3FindTable( db, zTab, pDb.zName ) ) == null )
    {
      /* The sqlite_stat[12] table does not exist. Create it. Note that a 
      ** side-effect of the CREATE TABLE statement is to leave the rootpage 
      ** of the new table in register pParse.regRoot. This is important 
      ** because the OpenWrite opcode below will be needing it. */
      sqlite3NestedParse( pParse,
      "CREATE TABLE %Q.%s(%s)", pDb.zName, zTab, aTable[i].zCols
      );
      aRoot[i] = pParse.regRoot;
      aCreateTbl[i] = 1;
    }
    else
    {
      /* The table already exists. If zWhere is not NULL, delete all entries 
      ** associated with the table zWhere. If zWhere is NULL, delete the
      ** entire contents of the table. */
      aRoot[i] = pStat.tnum;
      sqlite3TableLock( pParse, iDb, aRoot[i], 1, zTab );
      if ( !String.IsNullOrEmpty( zWhere ) )
      {
        sqlite3NestedParse( pParse,
        "DELETE FROM %Q.%s WHERE %s=%Q", pDb.zName, zTab, zWhereType, zWhere
        );
      }
      else
      {
        /* The sqlite_stat[12] table already exists.  Delete all rows. */
        sqlite3VdbeAddOp2( v, OP_Clear, aRoot[i], iDb );
      }
    }
  }

  /* Open the sqlite_stat[12] tables for writing. */
  for ( i = 0; i < ArraySize( aTable ); i++ )
  {
    sqlite3VdbeAddOp3( v, OP_OpenWrite, iStatCur + i, aRoot[i], iDb );
    sqlite3VdbeChangeP4( v, -1, 3, P4_INT32 );
    sqlite3VdbeChangeP5( v, aCreateTbl[i] );
  }
}

/*
** Generate code to do an analysis of all indices associated with
** a single table.
*/
static void analyzeOneTable(
Parse pParse,    /* Parser context */
Table pTab,      /* Table whose indices are to be analyzed */
Index pOnlyIdx,   /* If not NULL, only analyze this one index */
int iStatCur,    /* Index of VdbeCursor that writes the sqlite_stat1 table */
int iMem         /* Available memory locations begin here */
)
{
  sqlite3 db = pParse.db;      /* Database handle */
  Index pIdx;                  /* An index to being analyzed */
  int iIdxCur;                 /* Cursor open on index being analyzed */
  Vdbe v;                      /* The virtual machine being built up */
  int i;                       /* Loop counter */
  int topOfLoop;               /* The top of the loop */
  int endOfLoop;               /* The end of the loop */
  int jZeroRows = -1;          /* Jump from here if number of rows is zero */
  int iDb;                     /* Index of database containing pTab */
  int regTabname = iMem++;     /* Register containing table name */
  int regIdxname = iMem++;     /* Register containing index name */
  int regSampleno = iMem++;    /* Register containing next sample number */
  int regCol = iMem++;         /* Content of a column analyzed table */
  int regRec = iMem++;         /* Register holding completed record */
  int regTemp = iMem++;        /* Temporary use register */
  int regRowid = iMem++;       /* Rowid for the inserted record */

#if SQLITE_ENABLE_STAT2
  int addr = 0;                /* Instruction address */
  int regTemp2 = iMem++;       /* Temporary use register */
  int regSamplerecno = iMem++; /* Index of next sample to record */
  int regRecno = iMem++;       /* Current sample index */
  int regLast = iMem++;        /* Index of last sample to record */
  int regFirst = iMem++;       /* Index of first sample to record */
#endif

  v = sqlite3GetVdbe( pParse );
  if ( v == null || NEVER( pTab == null ) )
  {
    return;
  }
  if ( pTab.tnum == 0 )
  {
    /* Do not gather statistics on views or virtual tables */
    return;
  }
  if ( pTab.zName.StartsWith( "sqlite_", StringComparison.OrdinalIgnoreCase ) )
  {
    /* Do not gather statistics on system tables */
    return;
  }
  Debug.Assert( sqlite3BtreeHoldsAllMutexes( db ) );
  iDb = sqlite3SchemaToIndex( db, pTab.pSchema );
  Debug.Assert( iDb >= 0 );
  Debug.Assert( sqlite3SchemaMutexHeld(db, iDb, null) );
#if !SQLITE_OMIT_AUTHORIZATION
if( sqlite3AuthCheck(pParse, SQLITE_ANALYZE, pTab.zName, 0,
db.aDb[iDb].zName ) ){
return;
}
#endif

  /* Establish a read-lock on the table at the shared-cache level. */
  sqlite3TableLock( pParse, iDb, pTab.tnum, 0, pTab.zName );

  iIdxCur = pParse.nTab++;
  sqlite3VdbeAddOp4( v, OP_String8, 0, regTabname, 0, pTab.zName, 0 );
  for ( pIdx = pTab.pIndex; pIdx != null; pIdx = pIdx.pNext )
  {
    int nCol;
    KeyInfo pKey;
    if ( pOnlyIdx != null && pOnlyIdx != pIdx )
      continue;
    nCol = pIdx.nColumn;
    pKey = sqlite3IndexKeyinfo( pParse, pIdx );

    if ( iMem + 1 + ( nCol * 2 ) > pParse.nMem )
    {
      pParse.nMem = iMem + 1 + ( nCol * 2 );
    }

    /* Open a cursor to the index to be analyzed. */
    Debug.Assert( iDb == sqlite3SchemaToIndex( db, pIdx.pSchema ) );
    sqlite3VdbeAddOp4( v, OP_OpenRead, iIdxCur, pIdx.tnum, iDb,
    pKey, P4_KEYINFO_HANDOFF );
    VdbeComment( v, "%s", pIdx.zName );

    /* Populate the registers containing the index names. */
    sqlite3VdbeAddOp4( v, OP_String8, 0, regIdxname, 0, pIdx.zName, 0 );

#if SQLITE_ENABLE_STAT2

    /* If this iteration of the loop is generating code to analyze the
** first index in the pTab.pIndex list, then register regLast has
** not been populated. In this case populate it now.  */
    if ( pTab.pIndex == pIdx )
    {
      sqlite3VdbeAddOp2( v, OP_Integer, SQLITE_INDEX_SAMPLES, regSamplerecno );
      sqlite3VdbeAddOp2( v, OP_Integer, SQLITE_INDEX_SAMPLES * 2 - 1, regTemp );
      sqlite3VdbeAddOp2( v, OP_Integer, SQLITE_INDEX_SAMPLES * 2, regTemp2 );

      sqlite3VdbeAddOp2( v, OP_Count, iIdxCur, regLast );
      sqlite3VdbeAddOp2( v, OP_Null, 0, regFirst );
      addr = sqlite3VdbeAddOp3( v, OP_Lt, regSamplerecno, 0, regLast );
      sqlite3VdbeAddOp3( v, OP_Divide, regTemp2, regLast, regFirst );
      sqlite3VdbeAddOp3( v, OP_Multiply, regLast, regTemp, regLast );
      sqlite3VdbeAddOp2( v, OP_AddImm, regLast, SQLITE_INDEX_SAMPLES * 2 - 2 );
      sqlite3VdbeAddOp3( v, OP_Divide, regTemp2, regLast, regLast );
      sqlite3VdbeJumpHere( v, addr );
    }

    /* Zero the regSampleno and regRecno registers. */
    sqlite3VdbeAddOp2( v, OP_Integer, 0, regSampleno );
    sqlite3VdbeAddOp2( v, OP_Integer, 0, regRecno );
    sqlite3VdbeAddOp2( v, OP_Copy, regFirst, regSamplerecno );
#endif

    /* The block of memory cells initialized here is used as follows.
**
**    iMem:                
**        The total number of rows in the table.
**
**    iMem+1 .. iMem+nCol: 
**        Number of distinct entries in index considering the 
**        left-most N columns only, where N is between 1 and nCol, 
**        inclusive.
**
**    iMem+nCol+1 .. Mem+2*nCol:  
**        Previous value of indexed columns, from left to right.
**
** Cells iMem through iMem+nCol are initialized to 0. The others are 
** initialized to contain an SQL NULL.
*/
    for ( i = 0; i <= nCol; i++ )
    {
      sqlite3VdbeAddOp2( v, OP_Integer, 0, iMem + i );
    }
    for ( i = 0; i < nCol; i++ )
    {
      sqlite3VdbeAddOp2( v, OP_Null, 0, iMem + nCol + i + 1 );
    }

    /* Start the analysis loop. This loop runs through all the entries in
    ** the index b-tree.  */
    endOfLoop = sqlite3VdbeMakeLabel( v );
    sqlite3VdbeAddOp2( v, OP_Rewind, iIdxCur, endOfLoop );
    topOfLoop = sqlite3VdbeCurrentAddr( v );
    sqlite3VdbeAddOp2( v, OP_AddImm, iMem, 1 );

    for ( i = 0; i < nCol; i++ )
    {
      sqlite3VdbeAddOp3( v, OP_Column, iIdxCur, i, regCol );
      CollSeq pColl;
      if ( i == 0 )
      {
#if SQLITE_ENABLE_STAT2
        /* Check if the record that cursor iIdxCur points to contains a
** value that should be stored in the sqlite_stat2 table. If so,
** store it.  */
        int ne = sqlite3VdbeAddOp3( v, OP_Ne, regRecno, 0, regSamplerecno );
        Debug.Assert( regTabname + 1 == regIdxname
        && regTabname + 2 == regSampleno
        && regTabname + 3 == regCol
        );
        sqlite3VdbeChangeP5( v, SQLITE_JUMPIFNULL );
        sqlite3VdbeAddOp4( v, OP_MakeRecord, regTabname, 4, regRec, "aaab", 0 );
        sqlite3VdbeAddOp2( v, OP_NewRowid, iStatCur + 1, regRowid );
        sqlite3VdbeAddOp3( v, OP_Insert, iStatCur + 1, regRec, regRowid );

        /* Calculate new values for regSamplerecno and regSampleno.
        **
        **   sampleno = sampleno + 1
        **   samplerecno = samplerecno+(remaining records)/(remaining samples)
        */
        sqlite3VdbeAddOp2( v, OP_AddImm, regSampleno, 1 );
        sqlite3VdbeAddOp3( v, OP_Subtract, regRecno, regLast, regTemp );
        sqlite3VdbeAddOp2( v, OP_AddImm, regTemp, -1 );
        sqlite3VdbeAddOp2( v, OP_Integer, SQLITE_INDEX_SAMPLES, regTemp2 );
        sqlite3VdbeAddOp3( v, OP_Subtract, regSampleno, regTemp2, regTemp2 );
        sqlite3VdbeAddOp3( v, OP_Divide, regTemp2, regTemp, regTemp );
        sqlite3VdbeAddOp3( v, OP_Add, regSamplerecno, regTemp, regSamplerecno );

        sqlite3VdbeJumpHere( v, ne );
        sqlite3VdbeAddOp2( v, OP_AddImm, regRecno, 1 );
#endif

        /* Always record the very first row */
        sqlite3VdbeAddOp1( v, OP_IfNot, iMem + 1 );
      }
      Debug.Assert( pIdx.azColl != null );
      Debug.Assert( pIdx.azColl[i] != null );
      pColl = sqlite3LocateCollSeq( pParse, pIdx.azColl[i] );
      sqlite3VdbeAddOp4( v, OP_Ne, regCol, 0, iMem + nCol + i + 1,
      pColl, P4_COLLSEQ );
      sqlite3VdbeChangeP5( v, SQLITE_NULLEQ );
    }
    //if( db.mallocFailed ){
    //  /* If a malloc failure has occurred, then the result of the expression 
    //  ** passed as the second argument to the call to sqlite3VdbeJumpHere() 
    //  ** below may be negative. Which causes an Debug.Assert() to fail (or an
    //  ** out-of-bounds write if SQLITE_DEBUG is not defined).  */
    //  return;
    //}
    sqlite3VdbeAddOp2( v, OP_Goto, 0, endOfLoop );
    for ( i = 0; i < nCol; i++ )
    {
      int addr2 = sqlite3VdbeCurrentAddr( v ) - ( nCol * 2 );
      if ( i == 0 )
      {
        sqlite3VdbeJumpHere( v, addr2 - 1 );  /* Set jump dest for the OP_IfNot */
      }
      sqlite3VdbeJumpHere( v, addr2 );      /* Set jump dest for the OP_Ne */
      sqlite3VdbeAddOp2( v, OP_AddImm, iMem + i + 1, 1 );
      sqlite3VdbeAddOp3( v, OP_Column, iIdxCur, i, iMem + nCol + i + 1 );
    }

    /* End of the analysis loop. */
    sqlite3VdbeResolveLabel( v, endOfLoop );
    sqlite3VdbeAddOp2( v, OP_Next, iIdxCur, topOfLoop );
    sqlite3VdbeAddOp1( v, OP_Close, iIdxCur );

    /* Store the results in sqlite_stat1.
    **
    ** The result is a single row of the sqlite_stat1 table.  The first
    ** two columns are the names of the table and index.  The third column
    ** is a string composed of a list of integer statistics about the
    ** index.  The first integer in the list is the total number of entries
    ** in the index.  There is one additional integer in the list for each
    ** column of the table.  This additional integer is a guess of how many
    ** rows of the table the index will select.  If D is the count of distinct
    ** values and K is the total number of rows, then the integer is computed
    ** as:
    **
    **        I = (K+D-1)/D
    **
    ** If K==0 then no entry is made into the sqlite_stat1 table.  
    ** If K>0 then it is always the case the D>0 so division by zero
    ** is never possible.
    */
    sqlite3VdbeAddOp2( v, OP_SCopy, iMem, regSampleno );
    if ( jZeroRows < 0 )
    {
      jZeroRows = sqlite3VdbeAddOp1( v, OP_IfNot, iMem );
    }
    for ( i = 0; i < nCol; i++ )
    {
      sqlite3VdbeAddOp4( v, OP_String8, 0, regTemp, 0, " ", 0 );
      sqlite3VdbeAddOp3( v, OP_Concat, regTemp, regSampleno, regSampleno );
      sqlite3VdbeAddOp3( v, OP_Add, iMem, iMem + i + 1, regTemp );
      sqlite3VdbeAddOp2( v, OP_AddImm, regTemp, -1 );
      sqlite3VdbeAddOp3( v, OP_Divide, iMem + i + 1, regTemp, regTemp );
      sqlite3VdbeAddOp1( v, OP_ToInt, regTemp );
      sqlite3VdbeAddOp3( v, OP_Concat, regTemp, regSampleno, regSampleno );
    }
    sqlite3VdbeAddOp4( v, OP_MakeRecord, regTabname, 3, regRec, "aaa", 0 );
    sqlite3VdbeAddOp2( v, OP_NewRowid, iStatCur, regRowid );
    sqlite3VdbeAddOp3( v, OP_Insert, iStatCur, regRec, regRowid );
    sqlite3VdbeChangeP5( v, OPFLAG_APPEND );
  }

  /* If the table has no indices, create a single sqlite_stat1 entry
  ** containing NULL as the index name and the row count as the content.
  */
  if ( pTab.pIndex == null )
  {
    sqlite3VdbeAddOp3( v, OP_OpenRead, iIdxCur, pTab.tnum, iDb );
    VdbeComment( v, "%s", pTab.zName );
    sqlite3VdbeAddOp2( v, OP_Count, iIdxCur, regSampleno );
    sqlite3VdbeAddOp1( v, OP_Close, iIdxCur );
    jZeroRows = sqlite3VdbeAddOp1( v, OP_IfNot, regSampleno );
  }
  else
  {
    sqlite3VdbeJumpHere( v, jZeroRows );
    jZeroRows = sqlite3VdbeAddOp0( v, OP_Goto );
  }
  sqlite3VdbeAddOp2( v, OP_Null, 0, regIdxname );
  sqlite3VdbeAddOp4( v, OP_MakeRecord, regTabname, 3, regRec, "aaa", 0 );
  sqlite3VdbeAddOp2( v, OP_NewRowid, iStatCur, regRowid );
  sqlite3VdbeAddOp3( v, OP_Insert, iStatCur, regRec, regRowid );
  sqlite3VdbeChangeP5( v, OPFLAG_APPEND );
  if ( pParse.nMem < regRec )
    pParse.nMem = regRec;
  sqlite3VdbeJumpHere( v, jZeroRows );
}

/*
** Generate code that will cause the most recent index analysis to
** be loaded into internal hash tables where is can be used.
*/
static void loadAnalysis( Parse pParse, int iDb )
{
  Vdbe v = sqlite3GetVdbe( pParse );
  if ( v != null )
  {
    sqlite3VdbeAddOp1( v, OP_LoadAnalysis, iDb );
  }
}

/*
** Generate code that will do an analysis of an entire database
*/
static void analyzeDatabase( Parse pParse, int iDb )
{
  sqlite3 db = pParse.db;
  Schema pSchema = db.aDb[iDb].pSchema;    /* Schema of database iDb */
  HashElem k;
  int iStatCur;
  int iMem;

  sqlite3BeginWriteOperation( pParse, 0, iDb );
  iStatCur = pParse.nTab;
  pParse.nTab += 2;
  openStatTable( pParse, iDb, iStatCur, null, null );
  iMem = pParse.nMem + 1;
  Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
  //for(k=sqliteHashFirst(pSchema.tblHash); k; k=sqliteHashNext(k)){
  for ( k = pSchema.tblHash.first; k != null; k = k.next )
  {
    Table pTab = (Table)k.data;// sqliteHashData( k );
    analyzeOneTable( pParse, pTab, null, iStatCur, iMem );
  }
  loadAnalysis( pParse, iDb );
}

/*
** Generate code that will do an analysis of a single table in
** a database.  If pOnlyIdx is not NULL then it is a single index
** in pTab that should be analyzed.
*/
static void analyzeTable( Parse pParse, Table pTab, Index pOnlyIdx)
{
  int iDb;
  int iStatCur;

  Debug.Assert( pTab != null );
  Debug.Assert( sqlite3BtreeHoldsAllMutexes( pParse.db ) );
  iDb = sqlite3SchemaToIndex( pParse.db, pTab.pSchema );
  sqlite3BeginWriteOperation( pParse, 0, iDb );
  iStatCur = pParse.nTab;
  pParse.nTab += 2;
  if ( pOnlyIdx != null )
  {
    openStatTable( pParse, iDb, iStatCur, pOnlyIdx.zName, "idx" );
  }
  else
  {
    openStatTable( pParse, iDb, iStatCur, pTab.zName, "tbl" );
  }
  analyzeOneTable( pParse, pTab, pOnlyIdx, iStatCur, pParse.nMem + 1 );
  loadAnalysis( pParse, iDb );
}

/*
** Generate code for the ANALYZE command.  The parser calls this routine
** when it recognizes an ANALYZE command.
**
**        ANALYZE                            -- 1
**        ANALYZE  <database>                -- 2
**        ANALYZE  ?<database>.?<tablename>  -- 3
**
** Form 1 causes all indices in all attached databases to be analyzed.
** Form 2 analyzes all indices the single database named.
** Form 3 analyzes all indices associated with the named table.
*/
// OVERLOADS, so I don't need to rewrite parse.c
static void sqlite3Analyze( Parse pParse, int null_2, int null_3 )
{
  sqlite3Analyze( pParse, null, null );
}
static void sqlite3Analyze( Parse pParse, Token pName1, Token pName2 )
{
  sqlite3 db = pParse.db;
  int iDb;
  int i;
  string z, zDb;
  Table pTab;
  Index pIdx;
  Token pTableName = null;

  /* Read the database schema. If an error occurs, leave an error message
  ** and code in pParse and return NULL. */
  Debug.Assert( sqlite3BtreeHoldsAllMutexes( pParse.db ) );
  if ( SQLITE_OK != sqlite3ReadSchema( pParse ) )
  {
    return;
  }

  Debug.Assert( pName2 != null || pName1 == null );
  if ( pName1 == null )
  {
    /* Form 1:  Analyze everything */
    for ( i = 0; i < db.nDb; i++ )
    {
      if ( i == 1 )
        continue;  /* Do not analyze the TEMP database */
      analyzeDatabase( pParse, i );
    }
  }
  else if ( pName2.n == 0 )
  {
    /* Form 2:  Analyze the database or table named */
    iDb = sqlite3FindDb( db, pName1 );
    if ( iDb >= 0 )
    {
      analyzeDatabase( pParse, iDb );
    }
    else
    {
      z = sqlite3NameFromToken( db, pName1 );
      if ( z != null )
      {
        if ( ( pIdx = sqlite3FindIndex( db, z, null ) ) != null )
        {
          analyzeTable( pParse, pIdx.pTable, pIdx );
        }
        else if ( ( pTab = sqlite3LocateTable( pParse, 0, z, null ) ) != null )
        {
          analyzeTable( pParse, pTab, null );
        }
        z = null;//sqlite3DbFree( db, z );
      }
    }
  }
  else
  {
    /* Form 3: Analyze the fully qualified table name */
    iDb = sqlite3TwoPartName( pParse, pName1, pName2, ref pTableName );
    if ( iDb >= 0 )
    {
      zDb = db.aDb[iDb].zName;
      z = sqlite3NameFromToken( db, pTableName );
      if ( z != null )
      {
        if ( ( pIdx = sqlite3FindIndex( db, z, zDb ) ) != null )
        {
          analyzeTable( pParse, pIdx.pTable, pIdx );
        }
        else if ( ( pTab = sqlite3LocateTable( pParse, 0, z, zDb ) ) != null )
        {
          analyzeTable( pParse, pTab, null );
        }
        z = null; //sqlite3DbFree( db, z );
      }
    }
  }
}

/*
** Used to pass information from the analyzer reader through to the
** callback routine.
*/
//typedef struct analysisInfo analysisInfo;
public struct analysisInfo
{
  public sqlite3 db;
  public string zDatabase;
};

/*
** This callback is invoked once for each index when reading the
** sqlite_stat1 table.  
**
**     argv[0] = name of the table
**     argv[1] = name of the index (might be NULL)
**     argv[2] = results of analysis - on integer for each column
**
** Entries for which argv[1]==NULL simply record the number of rows in
** the table.
*/
static int analysisLoader( object pData, sqlite3_int64 argc, object Oargv, object NotUsed )
{
  string[] argv = (string[])Oargv;
  analysisInfo pInfo = (analysisInfo)pData;
  Index pIndex;
  Table pTable;
  int i, c, n;
  int v;
  string z;

  Debug.Assert( argc == 3 );
  UNUSED_PARAMETER2( NotUsed, argc );
  if ( argv == null || argv[0] == null || argv[2] == null )
  {
    return 0;
  }
  pTable = sqlite3FindTable( pInfo.db, argv[0], pInfo.zDatabase );
  if ( pTable == null )
  {
    return 0;
  }
  if ( !String.IsNullOrEmpty( argv[1] ) )
  {
    pIndex = sqlite3FindIndex( pInfo.db, argv[1], pInfo.zDatabase );
  }
  else
  {
    pIndex = null;
  }

  n = pIndex != null ? pIndex.nColumn : 0;
  z = argv[2];
  int zIndex = 0;
  for ( i = 0; z != null && i <= n; i++ )
  {
    v = 0;
    while ( zIndex < z.Length && ( c = z[zIndex] ) >= '0' && c <= '9' )
    {
      v = v * 10 + c - '0';
      zIndex++;
    }
    if ( i == 0 )
      pTable.nRowEst = (uint)v;
    if ( pIndex == null )
      break;
    pIndex.aiRowEst[i] = v;
    if ( zIndex < z.Length && z[zIndex] == ' ' )
      zIndex++;
    if ( z.Substring(zIndex).CompareTo("unordered")==0)//memcmp( z, "unordered", 10 ) == 0 )
    {
      pIndex.bUnordered = 1;
      break;
    }
  }
  return 0;
}

/*
** If the Index.aSample variable is not NULL, delete the aSample[] array
** and its contents.
*/
static void sqlite3DeleteIndexSamples( sqlite3 db, Index pIdx )
{
#if SQLITE_ENABLE_STAT2
  if ( pIdx.aSample != null )
  {
    int j;
    for ( j = 0; j < SQLITE_INDEX_SAMPLES; j++ )
    {
      IndexSample p = pIdx.aSample[j];
      if ( p.eType == SQLITE_TEXT || p.eType == SQLITE_BLOB )
      {
        p.u.z = null;//sqlite3DbFree(db, p.u.z);
        p.u.zBLOB = null;
      }
    }
    sqlite3DbFree( db, ref pIdx.aSample );
  }
#else
UNUSED_PARAMETER(db);
UNUSED_PARAMETER( pIdx );
#endif
}

/*
** Load the content of the sqlite_stat1 and sqlite_stat2 tables. The
** contents of sqlite_stat1 are used to populate the Index.aiRowEst[]
** arrays. The contents of sqlite_stat2 are used to populate the
** Index.aSample[] arrays.
**
** If the sqlite_stat1 table is not present in the database, SQLITE_ERROR
** is returned. In this case, even if SQLITE_ENABLE_STAT2 was defined 
** during compilation and the sqlite_stat2 table is present, no data is 
** read from it.
**
** If SQLITE_ENABLE_STAT2 was defined during compilation and the 
** sqlite_stat2 table is not present in the database, SQLITE_ERROR is
** returned. However, in this case, data is read from the sqlite_stat1
** table (if it is present) before returning.
**
** If an OOM error occurs, this function always sets db.mallocFailed.
** This means if the caller does not care about other errors, the return
** code may be ignored.
*/
static int sqlite3AnalysisLoad( sqlite3 db, int iDb )
{
  analysisInfo sInfo;
  HashElem i;
  string zSql;
  int rc;

  Debug.Assert( iDb >= 0 && iDb < db.nDb );
  Debug.Assert( db.aDb[iDb].pBt != null );
  /* Clear any prior statistics */
  Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
  //for(i=sqliteHashFirst(&db.aDb[iDb].pSchema.idxHash);i;i=sqliteHashNext(i)){
  for ( i = db.aDb[iDb].pSchema.idxHash.first; i != null; i = i.next )
  {
    Index pIdx = (Index)i.data;// sqliteHashData( i );
    sqlite3DefaultRowEst( pIdx );
    sqlite3DeleteIndexSamples( db, pIdx );
    pIdx.aSample = null;
  }

  /* Check to make sure the sqlite_stat1 table exists */
  sInfo.db = db;
  sInfo.zDatabase = db.aDb[iDb].zName;
  if ( sqlite3FindTable( db, "sqlite_stat1", sInfo.zDatabase ) == null )
  {
    return SQLITE_ERROR;
  }


  /* Load new statistics out of the sqlite_stat1 table */
  zSql = sqlite3MPrintf( db,
  "SELECT tbl, idx, stat FROM %Q.sqlite_stat1", sInfo.zDatabase );
  //if ( zSql == null )
  //{
  //  rc = SQLITE_NOMEM;
  //}
  //else
  {
    rc = sqlite3_exec( db, zSql, (dxCallback)analysisLoader, sInfo, 0 );
    sqlite3DbFree( db, ref zSql );
  }


  /* Load the statistics from the sqlite_stat2 table. */
#if SQLITE_ENABLE_STAT2
  if ( rc == SQLITE_OK && null == sqlite3FindTable( db, "sqlite_stat2", sInfo.zDatabase ) )
  {
    rc = SQLITE_ERROR;
  }
  if ( rc == SQLITE_OK )
  {
    sqlite3_stmt pStmt = null;

    zSql = sqlite3MPrintf( db,
    "SELECT idx,sampleno,sample FROM %Q.sqlite_stat2", sInfo.zDatabase );
    //if( null==zSql ){
    //rc = SQLITE_NOMEM;
    //}else{
    rc = sqlite3_prepare( db, zSql, -1, ref pStmt, 0 );
    sqlite3DbFree( db, ref zSql );
    //}

    if ( rc == SQLITE_OK )
    {
      while ( sqlite3_step( pStmt ) == SQLITE_ROW )
      {
        string zIndex;   /* Index name */
        Index pIdx;    /* Pointer to the index object */
        zIndex = sqlite3_column_text( pStmt, 0 );
        pIdx = !String.IsNullOrEmpty( zIndex ) ? sqlite3FindIndex( db, zIndex, sInfo.zDatabase ) : null;
        if ( pIdx != null )
        {
          int iSample = sqlite3_column_int( pStmt, 1 );
          if ( iSample < SQLITE_INDEX_SAMPLES && iSample >= 0 )
          {
            int eType = sqlite3_column_type( pStmt, 2 );

            if ( pIdx.aSample == null )
            {
              //static const int sz = sizeof(IndexSample)*SQLITE_INDEX_SAMPLES;
              //pIdx->aSample = (IndexSample )sqlite3DbMallocRaw(0, sz);
              //if( pIdx.aSample==0 ){
              //db.mallocFailed = 1;
              //break;
              //}
              pIdx.aSample = new IndexSample[SQLITE_INDEX_SAMPLES];//memset(pIdx->aSample, 0, sz);
            }

            //Debug.Assert( pIdx.aSample != null );
            if ( pIdx.aSample[iSample] == null )
              pIdx.aSample[iSample] = new IndexSample();
            IndexSample pSample = pIdx.aSample[iSample];
            {
              pSample.eType = (u8)eType;
              if ( eType == SQLITE_INTEGER || eType == SQLITE_FLOAT )
              {
                pSample.u.r = sqlite3_column_double( pStmt, 2 );
              }
              else if ( eType == SQLITE_TEXT || eType == SQLITE_BLOB )
              {
                string z = null;
                byte[] zBLOB = null;
                //string z = (string )(
                //(eType==SQLITE_BLOB) ?
                //sqlite3_column_blob(pStmt, 2):
                //sqlite3_column_text(pStmt, 2)
                //);
                if ( eType == SQLITE_BLOB )
                  zBLOB = sqlite3_column_blob( pStmt, 2 );
                else
                  z = sqlite3_column_text( pStmt, 2 );
                int n = sqlite3_column_bytes( pStmt, 2 );
                if ( n > 24 )
                {
                  n = 24;
                }
                pSample.nByte = (u8)n;
                if ( n < 1 )
                {
                  pSample.u.z = null;
                  pSample.u.zBLOB = null;
                }
                else
                {
                  pSample.u.z = z;
                  pSample.u.zBLOB = zBLOB;
                  //pSample->u.z = sqlite3DbMallocRaw(dbMem, n);
                  //if( pSample->u.z ){
                  //  memcpy(pSample->u.z, z, n);
                  //}else{
                  //  db->mallocFailed = 1;
                  //  break;
                  //}
                }
              }
            }
          }
        }
      }
      rc = sqlite3_finalize( pStmt );
    }
  }
#endif

  //if( rc==SQLITE_NOMEM ){
  //  db.mallocFailed = 1;
  //}
  return rc;
}

#endif // * SQLITE_OMIT_ANALYZE */
  }
}
