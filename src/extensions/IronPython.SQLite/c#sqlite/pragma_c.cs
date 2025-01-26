using System;
using System.Diagnostics;
using System.Text;

using i64 = System.Int64;
using u8 = System.Byte;

using Pgno = System.UInt32;
using sqlite3_int64 = System.Int64;
using System.Globalization;

namespace Community.CsharpSqlite
{
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
    ** This file contains code used to implement the PRAGMA command.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-06-23 19:49:22 4374b7e83ea0a3fbc3691f9c0c936272862f32f2
    **
    *************************************************************************
    */
    //#include "sqliteInt.h"

    /*
** Interpret the given string as a safety level.  Return 0 for OFF,
** 1 for ON or NORMAL and 2 for FULL.  Return 1 for an empty or
** unrecognized string argument.
**
** Note that the values returned are one less that the values that
** should be passed into sqlite3BtreeSetSafetyLevel().  The is done
** to support legacy SQL code.  The safety level used to be boolean
** and older scripts may have used numbers 0 for OFF and 1 for ON.
*/
    static u8 getSafetyLevel( string z )
    {
      //                             /* 123456789 123456789 */
      string zText = "onoffalseyestruefull";
      int[] iOffset = new int[] { 0, 1, 2, 4, 9, 12, 16 };
      int[] iLength = new int[] { 2, 2, 3, 5, 3, 4, 4 };
      u8[] iValue = new u8[] { 1, 0, 0, 0, 1, 1, 2 };
      int i, n;
      if ( sqlite3Isdigit( z[0] ) )
      {
        return (u8)sqlite3Atoi( z );
      }
      n = sqlite3Strlen30( z );
      for ( i = 0; i < ArraySize( iLength ); i++ )
      {
        if ( iLength[i] == n && sqlite3StrNICmp( zText.Substring( iOffset[i] ), z, n ) == 0 )
        {
          return iValue[i];
        }
      }
      return 1;
    }

    /*
    ** Interpret the given string as a boolean value.
    */
    static u8 sqlite3GetBoolean(string z){
  return (u8)( getSafetyLevel( z ) & 1 );//return getSafetyLevel(z)&1;
}

/* The sqlite3GetBoolean() function is used by other modules but the
** remainder of this file is specific to PRAGMA processing.  So omit
** the rest of the file if PRAGMAs are omitted from the build.
*/
#if !(SQLITE_OMIT_PRAGMA)

    /*
    ** Interpret the given string as a locking mode value.
    */
    static int getLockingMode( string z )
    {
      if ( z != null )
      {
        if ( z.Equals( "exclusive", StringComparison.OrdinalIgnoreCase ) )
          return PAGER_LOCKINGMODE_EXCLUSIVE;
        if ( z.Equals( "normal", StringComparison.OrdinalIgnoreCase ) )
          return PAGER_LOCKINGMODE_NORMAL;
      }
      return PAGER_LOCKINGMODE_QUERY;
    }

#if !SQLITE_OMIT_AUTOVACUUM
    /*
** Interpret the given string as an auto-vacuum mode value.
**
** The following strings, "none", "full" and "incremental" are
** acceptable, as are their numeric equivalents: 0, 1 and 2 respectively.
*/
    static u8 getAutoVacuum( string z )
    {
      int i;
      if ( z.Equals( "none", StringComparison.OrdinalIgnoreCase ) )
        return BTREE_AUTOVACUUM_NONE;
      if ( z.Equals( "full", StringComparison.OrdinalIgnoreCase ) )
        return BTREE_AUTOVACUUM_FULL;
      if ( z.Equals( "incremental", StringComparison.OrdinalIgnoreCase ) )
        return BTREE_AUTOVACUUM_INCR;
      i = atoi( z );
      return (u8)( ( i >= 0 && i <= 2 ) ? i : 0 );
    }
#endif // * if !SQLITE_OMIT_AUTOVACUUM */

#if !SQLITE_OMIT_PAGER_PRAGMAS
    /*
** Interpret the given string as a temp db location. Return 1 for file
** backed temporary databases, 2 for the Red-Black tree in memory database
** and 0 to use the compile-time default.
*/
    static int getTempStore( string z )
    {
      if ( z[0] >= '0' && z[0] <= '2' )
      {
        return z[0] - '0';
      }
      else if ( z.Equals( "file", StringComparison.OrdinalIgnoreCase )  )
      {
        return 1;
      }
      else if ( z.Equals( "memory", StringComparison.OrdinalIgnoreCase )  )
      {
        return 2;
      }
      else
      {
        return 0;
      }
    }
#endif // * SQLITE_PAGER_PRAGMAS */

#if !SQLITE_OMIT_PAGER_PRAGMAS
    /*
** Invalidate temp storage, either when the temp storage is changed
** from default, or when 'file' and the temp_store_directory has changed
*/
    static int invalidateTempStorage( Parse pParse )
    {
      sqlite3 db = pParse.db;
      if ( db.aDb[1].pBt != null )
      {
        if ( 0 == db.autoCommit || sqlite3BtreeIsInReadTrans( db.aDb[1].pBt ) )
        {
          sqlite3ErrorMsg( pParse, "temporary storage cannot be changed " +
          "from within a transaction" );
          return SQLITE_ERROR;
        }
        sqlite3BtreeClose( ref db.aDb[1].pBt );
        db.aDb[1].pBt = null;
        sqlite3ResetInternalSchema( db, -1 );
      }
      return SQLITE_OK;
    }
#endif // * SQLITE_PAGER_PRAGMAS */

#if !SQLITE_OMIT_PAGER_PRAGMAS
    /*
** If the TEMP database is open, close it and mark the database schema
** as needing reloading.  This must be done when using the SQLITE_TEMP_STORE
** or DEFAULT_TEMP_STORE pragmas.
*/
    static int changeTempStorage( Parse pParse, string zStorageType )
    {
      int ts = getTempStore( zStorageType );
      sqlite3 db = pParse.db;
      if ( db.temp_store == ts )
        return SQLITE_OK;
      if ( invalidateTempStorage( pParse ) != SQLITE_OK )
      {
        return SQLITE_ERROR;
      }
      db.temp_store = (u8)ts;
      return SQLITE_OK;
    }
#endif // * SQLITE_PAGER_PRAGMAS */

    /*
** Generate code to return a single integer value.
*/
    static void returnSingleInt( Parse pParse, string zLabel, i64 value )
    {
      Vdbe v = sqlite3GetVdbe( pParse );
      int mem = ++pParse.nMem;
      //i64* pI64 = sqlite3DbMallocRaw( pParse->db, sizeof( value ) );
      //if ( pI64 )
      //{
      //  memcpy( pI64, &value, sizeof( value ) );
      //}
      //sqlite3VdbeAddOp4( v, OP_Int64, 0, mem, 0, (char*)pI64, P4_INT64 );
      sqlite3VdbeAddOp4( v, OP_Int64, 0, mem, 0, value, P4_INT64 );
      sqlite3VdbeSetNumCols( v, 1 );
      sqlite3VdbeSetColName( v, 0, COLNAME_NAME, zLabel, SQLITE_STATIC );
      sqlite3VdbeAddOp2( v, OP_ResultRow, mem, 1 );
    }

#if !SQLITE_OMIT_FLAG_PRAGMAS
    /*
** Check to see if zRight and zLeft refer to a pragma that queries
** or changes one of the flags in db.flags.  Return 1 if so and 0 if not.
** Also, implement the pragma.
*/
    struct sPragmaType
    {
      public string zName;  /* Name of the pragma */
      public int mask;           /* Mask for the db.flags value */
      public sPragmaType( string zName, int mask )
      {
        this.zName = zName;
        this.mask = mask;
      }
    }
    static int flagPragma( Parse pParse, string zLeft, string zRight )
    {
      sPragmaType[] aPragma = new sPragmaType[]{
new sPragmaType( "full_column_names",        SQLITE_FullColNames  ),
new sPragmaType( "short_column_names",       SQLITE_ShortColNames ),
new sPragmaType( "count_changes",            SQLITE_CountRows     ),
new sPragmaType( "empty_result_callbacks",   SQLITE_NullCallback  ),
new sPragmaType( "legacy_file_format",       SQLITE_LegacyFileFmt ),
new sPragmaType( "fullfsync",                SQLITE_FullFSync     ),
new sPragmaType( "checkpoint_fullfsync",     SQLITE_CkptFullFSync ),
new sPragmaType(  "reverse_unordered_selects", SQLITE_ReverseOrder),
#if !SQLITE_OMIT_AUTOMATIC_INDEX
new sPragmaType(  "automatic_index",          SQLITE_AutoIndex    ),
#endif
#if SQLITE_DEBUG
new sPragmaType( "sql_trace",                SQLITE_SqlTrace      ),
new sPragmaType( "vdbe_listing",             SQLITE_VdbeListing   ),
new sPragmaType( "vdbe_trace",               SQLITE_VdbeTrace     ),
#endif
#if !SQLITE_OMIT_CHECK
new sPragmaType( "ignore_check_constraints", SQLITE_IgnoreChecks  ),
#endif
/* The following is VERY experimental */
new sPragmaType( "writable_schema",          SQLITE_WriteSchema|SQLITE_RecoveryMode ),
new sPragmaType( "omit_readlock",            SQLITE_NoReadlock    ),

/* TODO: Maybe it shouldn't be possible to change the ReadUncommitted
** flag if there are any active statements. */
new sPragmaType( "read_uncommitted",         SQLITE_ReadUncommitted ),
new sPragmaType( "recursive_triggers",       SQLITE_RecTriggers ),

/* This flag may only be set if both foreign-key and trigger support
** are present in the build.  */
#if !(SQLITE_OMIT_FOREIGN_KEY) && !(SQLITE_OMIT_TRIGGER)
new sPragmaType( "foreign_keys",             SQLITE_ForeignKeys ),
#endif
};
      int i;
      sPragmaType p;
      for ( i = 0; i < ArraySize( aPragma ); i++ )//, p++)
      {
        p = aPragma[i];
        if ( zLeft.Equals( p.zName ,StringComparison.OrdinalIgnoreCase )  )
        {
          sqlite3 db = pParse.db;
          Vdbe v;
          v = sqlite3GetVdbe( pParse );
          Debug.Assert( v != null );  /* Already allocated by sqlite3Pragma() */
          if ( ALWAYS( v ) )
          {
            if ( null == zRight )
            {
              returnSingleInt( pParse, p.zName, ( ( db.flags & p.mask ) != 0 ) ? 1 : 0 );
            }
            else
            {
              int mask = p.mask;          /* Mask of bits to set or clear. */
              if ( db.autoCommit == 0 )
              {
                /* Foreign key support may not be enabled or disabled while not
                ** in auto-commit mode.  */
                mask &= ~( SQLITE_ForeignKeys );
              }
              if ( sqlite3GetBoolean( zRight ) != 0 )
              {
                db.flags |= mask;
              }
              else
              {
                db.flags &= ~mask;
              }

              /* Many of the flag-pragmas modify the code generated by the SQL
              ** compiler (eg. count_changes). So add an opcode to expire all
              ** compiled SQL statements after modifying a pragma value.
              */
              sqlite3VdbeAddOp2( v, OP_Expire, 0, 0 );
            }
          }

          return 1;
        }
      }
      return 0;
    }
#endif // * SQLITE_OMIT_FLAG_PRAGMAS */

    /*
** Return a human-readable name for a constraint resolution action.
*/
#if !SQLITE_OMIT_FOREIGN_KEY
    static string actionName( int action )
    {
      string zName;
      switch ( action )
      {
        case OE_SetNull:
          zName = "SET NULL";
          break;
        case OE_SetDflt:
          zName = "SET DEFAULT";
          break;
        case OE_Cascade:
          zName = "CASCADE";
          break;
        case OE_Restrict:
          zName = "RESTRICT";
          break;
        default:
          zName = "NO ACTION";
          Debug.Assert( action == OE_None );
          break;
      }
      return zName;
    }
#endif

    /*
** Parameter eMode must be one of the PAGER_JOURNALMODE_XXX constants
** defined in pager.h. This function returns the associated lowercase
** journal-mode name.
*/
    static string sqlite3JournalModename( int eMode )
    {
      string[] azModeName = {
"delete", "persist", "off", "truncate", "memory"
#if !SQLITE_OMIT_WAL
, "wal"
#endif
};
      Debug.Assert( PAGER_JOURNALMODE_DELETE == 0 );
      Debug.Assert( PAGER_JOURNALMODE_PERSIST == 1 );
      Debug.Assert( PAGER_JOURNALMODE_OFF == 2 );
      Debug.Assert( PAGER_JOURNALMODE_TRUNCATE == 3 );
      Debug.Assert( PAGER_JOURNALMODE_MEMORY == 4 );
      Debug.Assert( PAGER_JOURNALMODE_WAL == 5 );
      Debug.Assert( eMode >= 0 && eMode <= ArraySize( azModeName ) );

      if ( eMode == ArraySize( azModeName ) )
        return null;
      return azModeName[eMode];
    }

    /*
    ** Process a pragma statement.
    **
    ** Pragmas are of this form:
    **
    **      PRAGMA [database.]id [= value]
    **
    ** The identifier might also be a string.  The value is a string, and
    ** identifier, or a number.  If minusFlag is true, then the value is
    ** a number that was preceded by a minus sign.
    **
    ** If the left side is "database.id" then pId1 is the database name
    ** and pId2 is the id.  If the left side is just "id" then pId1 is the
    ** id and pId2 is any empty string.
    */
    class EncName
    {
      public string zName;
      public u8 enc;

      public EncName( string zName, u8 enc )
      {
        this.zName = zName;
        this.enc = enc;
      }
    };

    static EncName[] encnames = new EncName[]  {
new EncName( "UTF8",     SQLITE_UTF8        ),
new EncName( "UTF-8",    SQLITE_UTF8        ),/* Must be element [1] */
new EncName( "UTF-16le (not supported)", SQLITE_UTF16LE     ),/* Must be element [2] */
new EncName( "UTF-16be (not supported)", SQLITE_UTF16BE     ), /* Must be element [3] */
new EncName( "UTF16le (not supported)",  SQLITE_UTF16LE     ),
new EncName( "UTF16be (not supported)",  SQLITE_UTF16BE     ),
new EncName( "UTF-16 (not supported)",   0                  ), /* SQLITE_UTF16NATIVE */
new EncName( "UTF16",    0                  ), /* SQLITE_UTF16NATIVE */
new EncName( null, 0 )
};
    
    // OVERLOADS, so I don't need to rewrite parse.c
    static void sqlite3Pragma( Parse pParse, Token pId1, Token pId2, int null_4, int minusFlag )
    {
      sqlite3Pragma( pParse, pId1, pId2, null, minusFlag );
    }
    static void sqlite3Pragma(
    Parse pParse,
    Token pId1,        /* First part of [database.]id field */
    Token pId2,        /* Second part of [database.]id field, or NULL */
    Token pValue,      /* Token for <value>, or NULL */
    int minusFlag     /* True if a '-' sign preceded <value> */
    )
    {
      string zLeft = null;    /* Nul-terminated UTF-8 string <id> */
      string zRight = null;   /* Nul-terminated UTF-8 string <value>, or NULL */
      string zDb = null;      /* The database name */
      Token pId = new Token();/* Pointer to <id> token */
      int iDb;                /* Database index for <database> */
      sqlite3 db = pParse.db;
      Db pDb;
      Vdbe v = pParse.pVdbe = sqlite3VdbeCreate( db );
      if ( v == null )
        return;
      sqlite3VdbeRunOnlyOnce( v );
      pParse.nMem = 2;

      /* Interpret the [database.] part of the pragma statement. iDb is the
      ** index of the database this pragma is being applied to in db.aDb[]. */
      iDb = sqlite3TwoPartName( pParse, pId1, pId2, ref pId );
      if ( iDb < 0 )
        return;
      pDb = db.aDb[iDb];

      /* If the temp database has been explicitly named as part of the
      ** pragma, make sure it is open.
      */
      if ( iDb == 1 && sqlite3OpenTempDatabase( pParse ) != 0 )
      {
        return;
      }

      zLeft = sqlite3NameFromToken( db, pId );
      if ( String.IsNullOrEmpty(zLeft))
        return;
      if ( minusFlag != 0 )
      {
        zRight = ( pValue == null ) ? "" : sqlite3MPrintf( db, "-%T", pValue );
      }
      else
      {
        zRight = sqlite3NameFromToken( db, pValue );
      }

      Debug.Assert( pId2 != null );
      zDb = pId2.n > 0 ? pDb.zName : null;
#if !SQLITE_OMIT_AUTHORIZATION
if ( sqlite3AuthCheck( pParse, SQLITE_PRAGMA, zLeft, zRight, zDb ) )
{
goto pragma_out;
}
#endif
#if !SQLITE_OMIT_PAGER_PRAGMAS
      /*
**  PRAGMA [database.]default_cache_size
**  PRAGMA [database.]default_cache_size=N
**
** The first form reports the current persistent setting for the
** page cache size.  The value returned is the maximum number of
** pages in the page cache.  The second form sets both the current
** page cache size value and the persistent page cache size value
** stored in the database file.
**
** Older versions of SQLite would set the default cache size to a
** negative number to indicate synchronous=OFF.  These days, synchronous
** is always on by default regardless of the sign of the default cache
** size.  But continue to take the absolute value of the default cache
** size of historical compatibility.
*/
      if ( zLeft.Equals( "default_cache_size", StringComparison.OrdinalIgnoreCase )  )
      {
        VdbeOpList[] getCacheSize = new VdbeOpList[]{
new VdbeOpList( OP_Transaction, 0, 0,        0),                         /* 0 */
new VdbeOpList( OP_ReadCookie,  0, 1,        BTREE_DEFAULT_CACHE_SIZE),  /* 1 */
new VdbeOpList( OP_IfPos,       1, 7,        0),
new VdbeOpList( OP_Integer,     0, 2,        0),
new VdbeOpList( OP_Subtract,    1, 2,        1),
new VdbeOpList( OP_IfPos,       1, 7,        0),
new VdbeOpList( OP_Integer,     0, 1,        0),  /* 6 */
new VdbeOpList( OP_ResultRow,   1, 1,        0),
};
        int addr;
        if ( sqlite3ReadSchema( pParse ) != 0 )
          goto pragma_out;
        sqlite3VdbeUsesBtree( v, iDb );
        if ( null == zRight )
        {
          sqlite3VdbeSetNumCols( v, 1 );
          sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "cache_size", SQLITE_STATIC );
          pParse.nMem += 2;
          addr = sqlite3VdbeAddOpList( v, getCacheSize.Length, getCacheSize );
          sqlite3VdbeChangeP1( v, addr, iDb );
          sqlite3VdbeChangeP1( v, addr + 1, iDb );
          sqlite3VdbeChangeP1( v, addr + 6, SQLITE_DEFAULT_CACHE_SIZE );
        }
        else
        {
          int size = sqlite3AbsInt32(sqlite3Atoi( zRight ));
          sqlite3BeginWriteOperation( pParse, 0, iDb );
          sqlite3VdbeAddOp2( v, OP_Integer, size, 1 );
          sqlite3VdbeAddOp3( v, OP_SetCookie, iDb, BTREE_DEFAULT_CACHE_SIZE, 1 );
          Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
          pDb.pSchema.cache_size = size;
          sqlite3BtreeSetCacheSize( pDb.pBt, pDb.pSchema.cache_size );
        }
      }
      else

        /*
        **  PRAGMA [database.]page_size
        **  PRAGMA [database.]page_size=N
        **
        ** The first form reports the current setting for the
        ** database page size in bytes.  The second form sets the
        ** database page size value.  The value can only be set if
        ** the database has not yet been created.
        */
        if ( zLeft.Equals( "page_size", StringComparison.OrdinalIgnoreCase )  )
        {
          Btree pBt = pDb.pBt;
          Debug.Assert( pBt != null );
          if ( null == zRight )
          {
            int size = ALWAYS( pBt ) ? sqlite3BtreeGetPageSize( pBt ) : 0;
            returnSingleInt( pParse, "page_size", size );
          }
          else
          {
            /* Malloc may fail when setting the page-size, as there is an internal
            ** buffer that the pager module resizes using sqlite3_realloc().
            */
            db.nextPagesize = sqlite3Atoi( zRight );
            if ( SQLITE_NOMEM == sqlite3BtreeSetPageSize( pBt, db.nextPagesize, -1, 0 ) )
            {
              ////        db.mallocFailed = 1;
            }
          }
        }
        else

          /*
          **  PRAGMA [database.]secure_delete
          **  PRAGMA [database.]secure_delete=ON/OFF
          **
          ** The first form reports the current setting for the
          ** secure_delete flag.  The second form changes the secure_delete
          ** flag setting and reports thenew value.
          */
          if ( zLeft.Equals( "secure_delete", StringComparison.OrdinalIgnoreCase )  )
          {
            Btree pBt = pDb.pBt;
            int b = -1;
            Debug.Assert( pBt != null );
            if ( zRight != null )
            {
              b = sqlite3GetBoolean( zRight );
            }
            if ( pId2.n == 0 && b >= 0 )
            {
              int ii;
              for ( ii = 0; ii < db.nDb; ii++ )
              {
                sqlite3BtreeSecureDelete( db.aDb[ii].pBt, b );
              }
            }
            b = sqlite3BtreeSecureDelete( pBt, b );
            returnSingleInt( pParse, "secure_delete", b );
          }
          else
            /*
            **  PRAGMA [database.]max_page_count
            **  PRAGMA [database.]max_page_count=N
            **
            ** The first form reports the current setting for the
            ** maximum number of pages in the database file.  The 
            ** second form attempts to change this setting.  Both
            ** forms return the current setting.
            **
            **  PRAGMA [database.]page_count
            **
            ** Return the number of pages in the specified database.
            */
            if ( zLeft.Equals( "page_count", StringComparison.OrdinalIgnoreCase ) 
            || zLeft.Equals( "max_page_count", StringComparison.OrdinalIgnoreCase ) 
            )
            {
              int iReg;
              if ( sqlite3ReadSchema( pParse ) != 0 )
                goto pragma_out;
              sqlite3CodeVerifySchema( pParse, iDb );
              iReg = ++pParse.nMem;
              if ( zLeft[0] == 'p' )
              {
                sqlite3VdbeAddOp2( v, OP_Pagecount, iDb, iReg );
              }
              else
              {
                sqlite3VdbeAddOp3( v, OP_MaxPgcnt, iDb, iReg, sqlite3Atoi( zRight ) );
              }
              sqlite3VdbeAddOp2( v, OP_ResultRow, iReg, 1 );
              sqlite3VdbeSetNumCols( v, 1 );
              sqlite3VdbeSetColName( v, 0, COLNAME_NAME, zLeft, SQLITE_TRANSIENT );
            }
            else

              /*
              **  PRAGMA [database.]page_count
              **
              ** Return the number of pages in the specified database.
              */
              if ( zLeft.Equals( "page_count", StringComparison.OrdinalIgnoreCase ) )
              {
                Vdbe _v;
                int iReg;
                _v = sqlite3GetVdbe( pParse );
                if ( _v == null || sqlite3ReadSchema( pParse ) != 0 )
                  goto pragma_out;
                sqlite3CodeVerifySchema( pParse, iDb );
                iReg = ++pParse.nMem;
                sqlite3VdbeAddOp2( _v, OP_Pagecount, iDb, iReg );
                sqlite3VdbeAddOp2( _v, OP_ResultRow, iReg, 1 );
                sqlite3VdbeSetNumCols( _v, 1 );
                sqlite3VdbeSetColName( _v, 0, COLNAME_NAME, "page_count", SQLITE_STATIC );
              }
              else

                /*
                **  PRAGMA [database.]locking_mode
                **  PRAGMA [database.]locking_mode = (normal|exclusive)
                */
                if ( zLeft.Equals( "locking_mode", StringComparison.OrdinalIgnoreCase )  )
                {
                  string zRet = "normal";
                  int eMode = getLockingMode( zRight );

                  if ( pId2.n == 0 && eMode == PAGER_LOCKINGMODE_QUERY )
                  {
                    /* Simple "PRAGMA locking_mode;" statement. This is a query for
                    ** the current default locking mode (which may be different to
                    ** the locking-mode of the main database).
                    */
                    eMode = db.dfltLockMode;
                  }
                  else
                  {
                    Pager pPager;
                    if ( pId2.n == 0 )
                    {
                      /* This indicates that no database name was specified as part
                      ** of the PRAGMA command. In this case the locking-mode must be
                      ** set on all attached databases, as well as the main db file.
                      **
                      ** Also, the sqlite3.dfltLockMode variable is set so that
                      ** any subsequently attached databases also use the specified
                      ** locking mode.
                      */
                      int ii;
                      Debug.Assert( pDb == db.aDb[0] );
                      for ( ii = 2; ii < db.nDb; ii++ )
                      {
                        pPager = sqlite3BtreePager( db.aDb[ii].pBt );
                        sqlite3PagerLockingMode( pPager, eMode );
                      }
                      db.dfltLockMode = (u8)eMode;
                    }
                    pPager = sqlite3BtreePager( pDb.pBt );
                    eMode = sqlite3PagerLockingMode( pPager, eMode ) ? 1 : 0;
                  }

                  Debug.Assert( eMode == PAGER_LOCKINGMODE_NORMAL || eMode == PAGER_LOCKINGMODE_EXCLUSIVE );
                  if ( eMode == PAGER_LOCKINGMODE_EXCLUSIVE )
                  {
                    zRet = "exclusive";
                  }
                  sqlite3VdbeSetNumCols( v, 1 );
                  sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "locking_mode", SQLITE_STATIC );
                  sqlite3VdbeAddOp4( v, OP_String8, 0, 1, 0, zRet, 0 );
                  sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
                }
                else
                  /*
                  **  PRAGMA [database.]journal_mode
                  **  PRAGMA [database.]journal_mode =
                  **                      (delete|persist|off|truncate|memory|wal|off)
                  */
                  if ( zLeft.Equals("journal_mode", StringComparison.OrdinalIgnoreCase )  )
                  {
                    int eMode;        /* One of the PAGER_JOURNALMODE_XXX symbols */
                    int ii;           /* Loop counter */

                    /* Force the schema to be loaded on all databases.  This cases all
                    ** database files to be opened and the journal_modes set. */
                    if ( sqlite3ReadSchema( pParse ) != 0 )
                    {
                      goto pragma_out;
                    }

                    sqlite3VdbeSetNumCols( v, 1 );
                    sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "journal_mode", SQLITE_STATIC );
                    if ( null == zRight )
                    {
                      /* If there is no "=MODE" part of the pragma, do a query for the
                      ** current mode */
                      eMode = PAGER_JOURNALMODE_QUERY;
                    }
                    else
                    {
                      string zMode;
                      int n = sqlite3Strlen30( zRight );
                      for ( eMode = 0; ( zMode = sqlite3JournalModename( eMode ) ) != null; eMode++ )
                      {
                        if ( sqlite3StrNICmp( zRight, zMode, n ) == 0 )
                          break;
                      }
                      if ( null == zMode )
                      {
                        /* If the "=MODE" part does not match any known journal mode,
                        ** then do a query */
                        eMode = PAGER_JOURNALMODE_QUERY;
                      }
                    }
                    if ( eMode == PAGER_JOURNALMODE_QUERY && pId2.n == 0 )
                    {
                      /* Convert "PRAGMA journal_mode" into "PRAGMA main.journal_mode" */
                      iDb = 0;
                      pId2.n = 1;
                    }
                    for ( ii = db.nDb - 1; ii >= 0; ii-- )
                    {
                      if ( db.aDb[ii].pBt != null && ( ii == iDb || pId2.n == 0 ) )
                      {
                        sqlite3VdbeUsesBtree( v, ii );
                        sqlite3VdbeAddOp3( v, OP_JournalMode, ii, 1, eMode );
                      }
                    }
                    sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
                  }
                  else

                    /*
                    **  PRAGMA [database.]journal_size_limit
                    **  PRAGMA [database.]journal_size_limit=N
                    **
                    ** Get or set the size limit on rollback journal files.
                    */
                    if ( zLeft.Equals( "journal_size_limit", StringComparison.OrdinalIgnoreCase )  )
                    {
                      Pager pPager = sqlite3BtreePager( pDb.pBt );
                      i64 iLimit = -2;
                      if ( !String.IsNullOrEmpty( zRight ) )
                      {
                        sqlite3Atoi64( zRight, ref iLimit, 1000000, SQLITE_UTF8 );
                        if ( iLimit < -1 )
                          iLimit = -1;
                      }
                      iLimit = sqlite3PagerJournalSizeLimit( pPager, iLimit );
                      returnSingleInt( pParse, "journal_size_limit", iLimit );
                    }
                    else

#endif // * SQLITE_OMIT_PAGER_PRAGMAS */

                      /*
**  PRAGMA [database.]auto_vacuum
**  PRAGMA [database.]auto_vacuum=N
**
** Get or set the value of the database 'auto-vacuum' parameter.
** The value is one of:  0 NONE 1 FULL 2 INCREMENTAL
*/
#if !SQLITE_OMIT_AUTOVACUUM
                      if ( zLeft.Equals( "auto_vacuum", StringComparison.OrdinalIgnoreCase )  )
                      {
                        Btree pBt = pDb.pBt;
                        Debug.Assert( pBt != null );
                        if ( sqlite3ReadSchema( pParse ) != 0 )
                        {
                          goto pragma_out;
                        }
                        if ( null == zRight )
                        {
                          int auto_vacuum;
                          if ( ALWAYS( pBt ) )
                          {
                            auto_vacuum = sqlite3BtreeGetAutoVacuum( pBt );
                          }
                          else
                          {
                            auto_vacuum = SQLITE_DEFAULT_AUTOVACUUM;
                          }
                          returnSingleInt( pParse, "auto_vacuum", auto_vacuum );
                        }
                        else
                        {
                          int eAuto = getAutoVacuum( zRight );
                          Debug.Assert( eAuto >= 0 && eAuto <= 2 );
                          db.nextAutovac = (u8)eAuto;
                          if ( ALWAYS( eAuto >= 0 ) )
                          {
                            /* Call SetAutoVacuum() to set initialize the internal auto and
                            ** incr-vacuum flags. This is required in case this connection
                            ** creates the database file. It is important that it is created
                            ** as an auto-vacuum capable db.
                            */
                            int rc = sqlite3BtreeSetAutoVacuum( pBt, eAuto );
                            if ( rc == SQLITE_OK && ( eAuto == 1 || eAuto == 2 ) )
                            {
                              /* When setting the auto_vacuum mode to either "full" or
                              ** "incremental", write the value of meta[6] in the database
                              ** file. Before writing to meta[6], check that meta[3] indicates
                              ** that this really is an auto-vacuum capable database.
                              */
                              VdbeOpList[] setMeta6 = new VdbeOpList[] {
new VdbeOpList( OP_Transaction,    0,               1,        0),    /* 0 */
new VdbeOpList( OP_ReadCookie,     0,               1,        BTREE_LARGEST_ROOT_PAGE),    /* 1 */
new VdbeOpList( OP_If,             1,               0,        0),    /* 2 */
new VdbeOpList( OP_Halt,           SQLITE_OK,       OE_Abort, 0),    /* 3 */
new VdbeOpList( OP_Integer,        0,               1,        0),    /* 4 */
new VdbeOpList( OP_SetCookie,      0,               BTREE_INCR_VACUUM, 1),    /* 5 */
};
                              int iAddr;
                              iAddr = sqlite3VdbeAddOpList( v, ArraySize( setMeta6 ), setMeta6 );
                              sqlite3VdbeChangeP1( v, iAddr, iDb );
                              sqlite3VdbeChangeP1( v, iAddr + 1, iDb );
                              sqlite3VdbeChangeP2( v, iAddr + 2, iAddr + 4 );
                              sqlite3VdbeChangeP1( v, iAddr + 4, eAuto - 1 );
                              sqlite3VdbeChangeP1( v, iAddr + 5, iDb );
                              sqlite3VdbeUsesBtree( v, iDb );
                            }
                          }
                        }
                      }
                      else
#endif

                        /*
**  PRAGMA [database.]incremental_vacuum(N)
**
** Do N steps of incremental vacuuming on a database.
*/
#if !SQLITE_OMIT_AUTOVACUUM
                        if ( zLeft.Equals( "incremental_vacuum", StringComparison.OrdinalIgnoreCase )  )
                        {
                          int iLimit = 0, addr;
                          if ( sqlite3ReadSchema( pParse ) != 0 )
                          {
                            goto pragma_out;
                          }
                          if ( zRight == null || !sqlite3GetInt32( zRight, ref iLimit ) || iLimit <= 0 )
                          {
                            iLimit = 0x7fffffff;
                          }
                          sqlite3BeginWriteOperation( pParse, 0, iDb );
                          sqlite3VdbeAddOp2( v, OP_Integer, iLimit, 1 );
                          addr = sqlite3VdbeAddOp1( v, OP_IncrVacuum, iDb );
                          sqlite3VdbeAddOp1( v, OP_ResultRow, 1 );
                          sqlite3VdbeAddOp2( v, OP_AddImm, 1, -1 );
                          sqlite3VdbeAddOp2( v, OP_IfPos, 1, addr );
                          sqlite3VdbeJumpHere( v, addr );
                        }
                        else
#endif

#if !SQLITE_OMIT_PAGER_PRAGMAS
                          /*
**  PRAGMA [database.]cache_size
**  PRAGMA [database.]cache_size=N
**
** The first form reports the current local setting for the
** page cache size.  The local setting can be different from
** the persistent cache size value that is stored in the database
** file itself.  The value returned is the maximum number of
** pages in the page cache.  The second form sets the local
** page cache size value.  It does not change the persistent
** cache size stored on the disk so the cache size will revert
** to its default value when the database is closed and reopened.
** N should be a positive integer.
*/
                          if ( zLeft.Equals( "cache_size", StringComparison.OrdinalIgnoreCase )  )
                          {
                            if ( sqlite3ReadSchema( pParse ) != 0 )
                              goto pragma_out;
                            Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
                            if ( null == zRight )
                            {
                              returnSingleInt( pParse, "cache_size", pDb.pSchema.cache_size );
                            }
                            else
                            {
                              int size = sqlite3AbsInt32(sqlite3Atoi( zRight ));
                              pDb.pSchema.cache_size = size;
                              sqlite3BtreeSetCacheSize( pDb.pBt, pDb.pSchema.cache_size );
                            }
                          }
                          else

                            /*
                            **   PRAGMA temp_store
                            **   PRAGMA temp_store = "default"|"memory"|"file"
                            **
                            ** Return or set the local value of the temp_store flag.  Changing
                            ** the local value does not make changes to the disk file and the default
                            ** value will be restored the next time the database is opened.
                            **
                            ** Note that it is possible for the library compile-time options to
                            ** override this setting
                            */
                            if ( zLeft.Equals( "temp_store", StringComparison.OrdinalIgnoreCase )  )
                            {
                              if ( zRight == null )
                              {
                                returnSingleInt( pParse, "temp_store", db.temp_store );
                              }
                              else
                              {
                                changeTempStorage( pParse, zRight );
                              }
                            }
                            else

                              /*
                              **   PRAGMA temp_store_directory
                              **   PRAGMA temp_store_directory = ""|"directory_name"
                              **
                              ** Return or set the local value of the temp_store_directory flag.  Changing
                              ** the value sets a specific directory to be used for temporary files.
                              ** Setting to a null string reverts to the default temporary directory search.
                              ** If temporary directory is changed, then invalidateTempStorage.
                              **
                              */
                              if ( zLeft.Equals( "temp_store_directory", StringComparison.OrdinalIgnoreCase )  )
                              {
                                if ( null == zRight )
                                {
                                  if ( sqlite3_temp_directory != "" )
                                  {
                                    sqlite3VdbeSetNumCols( v, 1 );
                                    sqlite3VdbeSetColName( v, 0, COLNAME_NAME,
                                        "temp_store_directory", SQLITE_STATIC );
                                    sqlite3VdbeAddOp4( v, OP_String8, 0, 1, 0, sqlite3_temp_directory, 0 );
                                    sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
                                  }
                                }
                                else
                                {
#if !SQLITE_OMIT_WSD
                                  if ( zRight.Length > 0 )
                                  {
                                    int rc;
                                    int res = 0;
                                    rc = sqlite3OsAccess( db.pVfs, zRight, SQLITE_ACCESS_READWRITE, ref res );
                                    if ( rc != SQLITE_OK || res == 0 )
                                    {
                                      sqlite3ErrorMsg( pParse, "not a writable directory" );
                                      goto pragma_out;
                                    }
                                  }
                                  if ( SQLITE_TEMP_STORE == 0
                                   || ( SQLITE_TEMP_STORE == 1 && db.temp_store <= 1 )
                                   || ( SQLITE_TEMP_STORE == 2 && db.temp_store == 1 )
                                  )
                                  {
                                    invalidateTempStorage( pParse );
                                  }
                                  //sqlite3_free( ref sqlite3_temp_directory );
                                  if ( zRight.Length > 0 )
                                  {
                                    sqlite3_temp_directory = zRight;//sqlite3_mprintf("%s", zRight);
                                  }
                                  else
                                  {
                                    sqlite3_temp_directory = "";
                                  }
#endif //* SQLITE_OMIT_WSD */
                                }
                              }
                              else

#if !(SQLITE_ENABLE_LOCKING_STYLE)
#  if (__APPLE__)
//#    define SQLITE_ENABLE_LOCKING_STYLE 1
#  else
                                //#    define SQLITE_ENABLE_LOCKING_STYLE 0
#  endif
#endif
#if SQLITE_ENABLE_LOCKING_STYLE
/*
**   PRAGMA [database.]lock_proxy_file
**   PRAGMA [database.]lock_proxy_file = ":auto:"|"lock_file_path"
**
** Return or set the value of the lock_proxy_file flag.  Changing
** the value sets a specific file to be used for database access locks.
**
*/
if ( zLeft.Equals( "lock_proxy_file", StringComparison.OrdinalIgnoreCase )  )
{
if ( zRight !="")
{
Pager pPager = sqlite3BtreePager( pDb.pBt );
int proxy_file_path = 0;
sqlite3_file pFile = sqlite3PagerFile( pPager );
sqlite3OsFileControl( pFile, SQLITE_GET_LOCKPROXYFILE,
ref proxy_file_path );

if ( proxy_file_path!=0 )
{
sqlite3VdbeSetNumCols( v, 1 );
sqlite3VdbeSetColName( v, 0, COLNAME_NAME,
"lock_proxy_file", SQLITE_STATIC );
sqlite3VdbeAddOp4( v, OP_String8, 0, 1, 0, proxy_file_path, 0 );
sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
}
}
else
{
Pager pPager = sqlite3BtreePager( pDb.pBt );
sqlite3_file pFile = sqlite3PagerFile( pPager );
int res;
int iDummy = 0;
if ( zRight[0]!=0 )
{
iDummy = zRight[0];
res = sqlite3OsFileControl( pFile, SQLITE_SET_LOCKPROXYFILE,
ref iDummy );
}
else
{
res = sqlite3OsFileControl( pFile, SQLITE_SET_LOCKPROXYFILE,
ref iDummy );
}
if ( res != SQLITE_OK )
{
sqlite3ErrorMsg( pParse, "failed to set lock proxy file" );
goto pragma_out;
}
}
}
else
#endif //* SQLITE_ENABLE_LOCKING_STYLE */

                                /*
**   PRAGMA [database.]synchronous
**   PRAGMA [database.]synchronous=OFF|ON|NORMAL|FULL
**
** Return or set the local value of the synchronous flag.  Changing
** the local value does not make changes to the disk file and the
** default value will be restored the next time the database is
** opened.
*/
                                if ( zLeft.Equals( "synchronous", StringComparison.OrdinalIgnoreCase )  )
                                {
                                  if ( sqlite3ReadSchema( pParse ) != 0 )
                                    goto pragma_out;
                                  if ( null == zRight )
                                  {
                                    returnSingleInt( pParse, "synchronous", pDb.safety_level - 1 );
                                  }
                                  else
                                  {
                                    if ( 0 == db.autoCommit )
                                    {
                                      sqlite3ErrorMsg( pParse,
                                        "Safety level may not be changed inside a transaction" );
                                    }
                                    else
                                    {
                                      pDb.safety_level = (byte)( getSafetyLevel( zRight ) + 1 );
                                    }
                                  }
                                }
                                else
#endif // * SQLITE_OMIT_PAGER_PRAGMAS */

#if !SQLITE_OMIT_FLAG_PRAGMAS
                                  if ( flagPragma( pParse, zLeft, zRight ) != 0 )
                                  {
                                    /* The flagPragma() subroutine also generates any necessary code
                                    ** there is nothing more to do here */
                                  }
                                  else
#endif // * SQLITE_OMIT_FLAG_PRAGMAS */

#if !SQLITE_OMIT_SCHEMA_PRAGMAS
                                    /*
**   PRAGMA table_info(<table>)
**
** Return a single row for each column of the named table. The columns of
** the returned data set are:
**
** cid:        Column id (numbered from left to right, starting at 0)
** name:       Column name
** type:       Column declaration type.
** notnull:    True if 'NOT NULL' is part of column declaration
** dflt_value: The default value for the column, if any.
*/
                                    if ( zLeft.Equals( "table_info", StringComparison.OrdinalIgnoreCase )  && zRight != null )
                                    {
                                      Table pTab;
                                      if ( sqlite3ReadSchema( pParse ) != 0 )
                                        goto pragma_out;
                                      pTab = sqlite3FindTable( db, zRight, zDb );
                                      if ( pTab != null )
                                      {
                                        int i;
                                        int nHidden = 0;
                                        Column pCol;
                                        sqlite3VdbeSetNumCols( v, 6 );
                                        pParse.nMem = 6;
                                        sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "cid", SQLITE_STATIC );
                                        sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "name", SQLITE_STATIC );
                                        sqlite3VdbeSetColName( v, 2, COLNAME_NAME, "type", SQLITE_STATIC );
                                        sqlite3VdbeSetColName( v, 3, COLNAME_NAME, "notnull", SQLITE_STATIC );
                                        sqlite3VdbeSetColName( v, 4, COLNAME_NAME, "dflt_value", SQLITE_STATIC );
                                        sqlite3VdbeSetColName( v, 5, COLNAME_NAME, "pk", SQLITE_STATIC );
                                        sqlite3ViewGetColumnNames( pParse, pTab );
                                        for ( i = 0; i < pTab.nCol; i++ )//, pCol++)
                                        {
                                          pCol = pTab.aCol[i];
                                          if ( IsHiddenColumn( pCol ) )
                                          {
                                            nHidden++;
                                            continue;
                                          }
                                          sqlite3VdbeAddOp2( v, OP_Integer, i - nHidden, 1 );
                                          sqlite3VdbeAddOp4( v, OP_String8, 0, 2, 0, pCol.zName, 0 );
                                          sqlite3VdbeAddOp4( v, OP_String8, 0, 3, 0,
                                             pCol.zType != null ? pCol.zType : "", 0 );
                                          sqlite3VdbeAddOp2( v, OP_Integer, ( pCol.notNull != 0 ? 1 : 0 ), 4 );
                                          if ( pCol.zDflt != null )
                                          {
                                            sqlite3VdbeAddOp4( v, OP_String8, 0, 5, 0, pCol.zDflt, 0 );
                                          }
                                          else
                                          {
                                            sqlite3VdbeAddOp2( v, OP_Null, 0, 5 );
                                          }
                                          sqlite3VdbeAddOp2( v, OP_Integer, pCol.isPrimKey != 0 ? 1 : 0, 6 );
                                          sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 6 );
                                        }
                                      }
                                    }
                                    else

                                      if ( zLeft.Equals( "index_info", StringComparison.OrdinalIgnoreCase )  && zRight != null )
                                      {
                                        Index pIdx;
                                        Table pTab;
                                        if ( sqlite3ReadSchema( pParse ) != 0 )
                                          goto pragma_out;
                                        pIdx = sqlite3FindIndex( db, zRight, zDb );
                                        if ( pIdx != null )
                                        {
                                          int i;
                                          pTab = pIdx.pTable;
                                          sqlite3VdbeSetNumCols( v, 3 );
                                          pParse.nMem = 3;
                                          sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "seqno", SQLITE_STATIC );
                                          sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "cid", SQLITE_STATIC );
                                          sqlite3VdbeSetColName( v, 2, COLNAME_NAME, "name", SQLITE_STATIC );
                                          for ( i = 0; i < pIdx.nColumn; i++ )
                                          {
                                            int cnum = pIdx.aiColumn[i];
                                            sqlite3VdbeAddOp2( v, OP_Integer, i, 1 );
                                            sqlite3VdbeAddOp2( v, OP_Integer, cnum, 2 );
                                            Debug.Assert( pTab.nCol > cnum );
                                            sqlite3VdbeAddOp4( v, OP_String8, 0, 3, 0, pTab.aCol[cnum].zName, 0 );
                                            sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 3 );
                                          }
                                        }
                                      }
                                      else

                                        if ( zLeft.Equals( "index_list", StringComparison.OrdinalIgnoreCase )  && zRight != null )
                                        {
                                          Index pIdx;
                                          Table pTab;
                                          if ( sqlite3ReadSchema( pParse ) != 0 )
                                            goto pragma_out;
                                          pTab = sqlite3FindTable( db, zRight, zDb );
                                          if ( pTab != null )
                                          {
                                            v = sqlite3GetVdbe( pParse );
                                            pIdx = pTab.pIndex;
                                            if ( pIdx != null )
                                            {
                                              int i = 0;
                                              sqlite3VdbeSetNumCols( v, 3 );
                                              pParse.nMem = 3;
                                              sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "seq", SQLITE_STATIC );
                                              sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "name", SQLITE_STATIC );
                                              sqlite3VdbeSetColName( v, 2, COLNAME_NAME, "unique", SQLITE_STATIC );
                                              while ( pIdx != null )
                                              {
                                                sqlite3VdbeAddOp2( v, OP_Integer, i, 1 );
                                                sqlite3VdbeAddOp4( v, OP_String8, 0, 2, 0, pIdx.zName, 0 );
                                                sqlite3VdbeAddOp2( v, OP_Integer, ( pIdx.onError != OE_None ) ? 1 : 0, 3 );
                                                sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 3 );
                                                ++i;
                                                pIdx = pIdx.pNext;
                                              }
                                            }
                                          }
                                        }
                                        else

                                          if ( zLeft.Equals( "database_list", StringComparison.OrdinalIgnoreCase )  )
                                          {
                                            int i;
                                            if ( sqlite3ReadSchema( pParse ) != 0 )
                                              goto pragma_out;
                                            sqlite3VdbeSetNumCols( v, 3 );
                                            pParse.nMem = 3;
                                            sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "seq", SQLITE_STATIC );
                                            sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "name", SQLITE_STATIC );
                                            sqlite3VdbeSetColName( v, 2, COLNAME_NAME, "file", SQLITE_STATIC );
                                            for ( i = 0; i < db.nDb; i++ )
                                            {
                                              if ( db.aDb[i].pBt == null )
                                                continue;
                                              Debug.Assert( db.aDb[i].zName != null );
                                              sqlite3VdbeAddOp2( v, OP_Integer, i, 1 );
                                              sqlite3VdbeAddOp4( v, OP_String8, 0, 2, 0, db.aDb[i].zName, 0 );
                                              sqlite3VdbeAddOp4( v, OP_String8, 0, 3, 0,
                                                   sqlite3BtreeGetFilename( db.aDb[i].pBt ), 0 );
                                              sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 3 );
                                            }
                                          }
                                          else

                                            if ( zLeft.Equals( "collation_list", StringComparison.OrdinalIgnoreCase )  )
                                            {
                                              int i = 0;
                                              HashElem p;
                                              sqlite3VdbeSetNumCols( v, 2 );
                                              pParse.nMem = 2;
                                              sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "seq", SQLITE_STATIC );
                                              sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "name", SQLITE_STATIC );
                                              for ( p = db.aCollSeq.first; p != null; p = p.next )//( p = sqliteHashFirst( db.aCollSeq ) ; p; p = sqliteHashNext( p ) )
                                              {
                                                CollSeq pColl = ( (CollSeq[])p.data )[0];// sqliteHashData( p );
                                                sqlite3VdbeAddOp2( v, OP_Integer, i++, 1 );
                                                sqlite3VdbeAddOp4( v, OP_String8, 0, 2, 0, pColl.zName, 0 );
                                                sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 2 );
                                              }
                                            }
                                            else
#endif // * SQLITE_OMIT_SCHEMA_PRAGMAS */

#if !SQLITE_OMIT_FOREIGN_KEY
                                              if ( zLeft.Equals( "foreign_key_list", StringComparison.OrdinalIgnoreCase )  && zRight != null )
                                              {
                                                FKey pFK;
                                                Table pTab;
                                                if ( sqlite3ReadSchema( pParse ) != 0 )
                                                  goto pragma_out;
                                                pTab = sqlite3FindTable( db, zRight, zDb );
                                                if ( pTab != null )
                                                {
                                                  v = sqlite3GetVdbe( pParse );
                                                  pFK = pTab.pFKey;
                                                  if ( pFK != null )
                                                  {
                                                    int i = 0;
                                                    sqlite3VdbeSetNumCols( v, 8 );
                                                    pParse.nMem = 8;
                                                    sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "id", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "seq", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 2, COLNAME_NAME, "table", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 3, COLNAME_NAME, "from", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 4, COLNAME_NAME, "to", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 5, COLNAME_NAME, "on_update", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 6, COLNAME_NAME, "on_delete", SQLITE_STATIC );
                                                    sqlite3VdbeSetColName( v, 7, COLNAME_NAME, "match", SQLITE_STATIC );
                                                    while ( pFK != null )
                                                    {
                                                      int j;
                                                      for ( j = 0; j < pFK.nCol; j++ )
                                                      {
                                                        string zCol = pFK.aCol[j].zCol;
                                                        string zOnDelete = actionName( pFK.aAction[0] );
                                                        string zOnUpdate = actionName( pFK.aAction[1] );
                                                        sqlite3VdbeAddOp2( v, OP_Integer, i, 1 );
                                                        sqlite3VdbeAddOp2( v, OP_Integer, j, 2 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 3, 0, pFK.zTo, 0 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 4, 0,
                                                                          pTab.aCol[pFK.aCol[j].iFrom].zName, 0 );
                                                        sqlite3VdbeAddOp4( v, zCol != null ? OP_String8 : OP_Null, 0, 5, 0, zCol, 0 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 6, 0, zOnUpdate, 0 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 7, 0, zOnDelete, 0 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 8, 0, "NONE", 0 );
                                                        sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 8 );
                                                      }
                                                      ++i;
                                                      pFK = pFK.pNextFrom;
                                                    }
                                                  }
                                                }
                                              }
                                              else
#endif // * !SQLITE_OMIT_FOREIGN_KEY) */

#if !NDEBUG
                                                if ( zLeft.Equals( "parser_trace", StringComparison.OrdinalIgnoreCase )  )
                                                {
                                                  if ( zRight != null )
                                                  {
                                                    if ( sqlite3GetBoolean( zRight ) != 0 )
                                                    {
                                                      sqlite3ParserTrace( Console.Out, "parser: " );
                                                    }
                                                    else
                                                    {
                                                      sqlite3ParserTrace( null, "" );
                                                    }
                                                  }
                                                }
                                                else
#endif

                                                  /* Reinstall the LIKE and GLOB functions.  The variant of LIKE
** used will be case sensitive or not depending on the RHS.
*/
                                                  if ( zLeft.Equals( "case_sensitive_like", StringComparison.OrdinalIgnoreCase )  )
                                                  {
                                                    if ( zRight != null )
                                                    {
                                                      sqlite3RegisterLikeFunctions( db, sqlite3GetBoolean( zRight ) );
                                                    }
                                                  }
                                                  else

#if !SQLITE_INTEGRITY_CHECK_ERROR_MAX
                                                    //const int SQLITE_INTEGRITY_CHECK_ERROR_MAX = 100;
#endif

#if !SQLITE_OMIT_INTEGRITY_CHECK
                                                    /* Pragma "quick_check" is an experimental reduced version of
** integrity_check designed to detect most database corruption
** without most of the overhead of a full integrity-check.
*/
                                                    if ( zLeft.Equals( "integrity_check", StringComparison.OrdinalIgnoreCase ) 
                                                     || zLeft.Equals( "quick_check", StringComparison.OrdinalIgnoreCase ) 
                                                    )
                                                    {
                                                      const int SQLITE_INTEGRITY_CHECK_ERROR_MAX = 100;
                                                      int i, j, addr, mxErr;

                                                      /* Code that appears at the end of the integrity check.  If no error
                                                      ** messages have been generated, refput OK.  Otherwise output the
                                                      ** error message
                                                      */
                                                      VdbeOpList[] endCode = new VdbeOpList[]  {
new VdbeOpList( OP_AddImm,      1, 0,        0),    /* 0 */
new                    VdbeOpList( OP_IfNeg,       1, 0,        0),    /* 1 */
new    VdbeOpList( OP_String8,     0, 3,        0),    /* 2 */
new  VdbeOpList( OP_ResultRow,   3, 1,        0),
};

                                                      bool isQuick = ( zLeft[0] == 'q' );

                                                      /* Initialize the VDBE program */
                                                      if ( sqlite3ReadSchema( pParse ) != 0 )
                                                        goto pragma_out;
                                                      pParse.nMem = 6;
                                                      sqlite3VdbeSetNumCols( v, 1 );
                                                      sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "integrity_check", SQLITE_STATIC );

                                                      /* Set the maximum error count */
                                                      mxErr = SQLITE_INTEGRITY_CHECK_ERROR_MAX;
                                                      if ( zRight != null )
                                                      {
                                                        sqlite3GetInt32( zRight, ref mxErr );
                                                        if ( mxErr <= 0 )
                                                        {
                                                          mxErr = SQLITE_INTEGRITY_CHECK_ERROR_MAX;
                                                        }
                                                      }
                                                      sqlite3VdbeAddOp2( v, OP_Integer, mxErr, 1 );  /* reg[1] holds errors left */

                                                      /* Do an integrity check on each database file */
                                                      for ( i = 0; i < db.nDb; i++ )
                                                      {
                                                        HashElem x;
                                                        Hash pTbls;
                                                        int cnt = 0;

                                                        if ( OMIT_TEMPDB != 0 && i == 1 )
                                                          continue;

                                                        sqlite3CodeVerifySchema( pParse, i );
                                                        addr = sqlite3VdbeAddOp1( v, OP_IfPos, 1 ); /* Halt if out of errors */
                                                        sqlite3VdbeAddOp2( v, OP_Halt, 0, 0 );
                                                        sqlite3VdbeJumpHere( v, addr );

                                                        /* Do an integrity check of the B-Tree
                                                        **
                                                        ** Begin by filling registers 2, 3, ... with the root pages numbers
                                                        ** for all tables and indices in the database.
                                                        */
                                                        Debug.Assert( sqlite3SchemaMutexHeld( db, iDb, null ) );
                                                        pTbls = db.aDb[i].pSchema.tblHash;
                                                        for ( x = pTbls.first; x != null; x = x.next )
                                                        {//          for(x=sqliteHashFirst(pTbls); x; x=sqliteHashNext(x)){
                                                          Table pTab = (Table)x.data;// sqliteHashData( x );
                                                          Index pIdx;
                                                          sqlite3VdbeAddOp2( v, OP_Integer, pTab.tnum, 2 + cnt );
                                                          cnt++;
                                                          for ( pIdx = pTab.pIndex; pIdx != null; pIdx = pIdx.pNext )
                                                          {
                                                            sqlite3VdbeAddOp2( v, OP_Integer, pIdx.tnum, 2 + cnt );
                                                            cnt++;
                                                          }
                                                        }

                                                        /* Make sure sufficient number of registers have been allocated */
                                                        if ( pParse.nMem < cnt + 4 )
                                                        {
                                                          pParse.nMem = cnt + 4;
                                                        }

                                                        /* Do the b-tree integrity checks */
                                                        sqlite3VdbeAddOp3( v, OP_IntegrityCk, 2, cnt, 1 );
                                                        sqlite3VdbeChangeP5( v, (u8)i );
                                                        addr = sqlite3VdbeAddOp1( v, OP_IsNull, 2 );
                                                        sqlite3VdbeAddOp4( v, OP_String8, 0, 3, 0,
                                                           sqlite3MPrintf( db, "*** in database %s ***\n", db.aDb[i].zName ),
                                                           P4_DYNAMIC );
                                                        sqlite3VdbeAddOp3( v, OP_Move, 2, 4, 1 );
                                                        sqlite3VdbeAddOp3( v, OP_Concat, 4, 3, 2 );
                                                        sqlite3VdbeAddOp2( v, OP_ResultRow, 2, 1 );
                                                        sqlite3VdbeJumpHere( v, addr );

                                                        /* Make sure all the indices are constructed correctly.
                                                        */
                                                        for ( x = pTbls.first; x != null && !isQuick; x = x.next )
                                                        {
                                                          ;//          for(x=sqliteHashFirst(pTbls); x && !isQuick; x=sqliteHashNext(x)){
                                                          Table pTab = (Table)x.data;// sqliteHashData( x );
                                                          Index pIdx;
                                                          int loopTop;

                                                          if ( pTab.pIndex == null )
                                                            continue;
                                                          addr = sqlite3VdbeAddOp1( v, OP_IfPos, 1 );  /* Stop if out of errors */
                                                          sqlite3VdbeAddOp2( v, OP_Halt, 0, 0 );
                                                          sqlite3VdbeJumpHere( v, addr );
                                                          sqlite3OpenTableAndIndices( pParse, pTab, 1, OP_OpenRead );
                                                          sqlite3VdbeAddOp2( v, OP_Integer, 0, 2 );  /* reg(2) will count entries */
                                                          loopTop = sqlite3VdbeAddOp2( v, OP_Rewind, 1, 0 );
                                                          sqlite3VdbeAddOp2( v, OP_AddImm, 2, 1 );   /* increment entry count */
                                                          for ( j = 0, pIdx = pTab.pIndex; pIdx != null; pIdx = pIdx.pNext, j++ )
                                                          {
                                                            int jmp2;
                                                            int r1;
                                                            VdbeOpList[] idxErr = new VdbeOpList[]  {
new VdbeOpList( OP_AddImm,      1, -1,  0),
new VdbeOpList( OP_String8,     0,  3,  0),    /* 1 */
new VdbeOpList( OP_Rowid,       1,  4,  0),
new VdbeOpList( OP_String8,     0,  5,  0),    /* 3 */
new VdbeOpList( OP_String8,     0,  6,  0),    /* 4 */
new VdbeOpList( OP_Concat,      4,  3,  3),
new VdbeOpList( OP_Concat,      5,  3,  3),
new VdbeOpList( OP_Concat,      6,  3,  3),
new VdbeOpList( OP_ResultRow,   3,  1,  0),
new VdbeOpList(  OP_IfPos,       1,  0,  0),    /* 9 */
new VdbeOpList(  OP_Halt,        0,  0,  0),
};
                                                            r1 = sqlite3GenerateIndexKey( pParse, pIdx, 1, 3, false );
                                                            jmp2 = sqlite3VdbeAddOp4Int( v, OP_Found, j + 2, 0, r1, pIdx.nColumn + 1 );
                                                            addr = sqlite3VdbeAddOpList( v, ArraySize( idxErr ), idxErr );
                                                            sqlite3VdbeChangeP4( v, addr + 1, "rowid ", SQLITE_STATIC );
                                                            sqlite3VdbeChangeP4( v, addr + 3, " missing from index ", SQLITE_STATIC );
                                                            sqlite3VdbeChangeP4( v, addr + 4, pIdx.zName, P4_TRANSIENT );
                                                            sqlite3VdbeJumpHere( v, addr + 9 );
                                                            sqlite3VdbeJumpHere( v, jmp2 );
                                                          }
                                                          sqlite3VdbeAddOp2( v, OP_Next, 1, loopTop + 1 );
                                                          sqlite3VdbeJumpHere( v, loopTop );
                                                          for ( j = 0, pIdx = pTab.pIndex; pIdx != null; pIdx = pIdx.pNext, j++ )
                                                          {
                                                            VdbeOpList[] cntIdx = new VdbeOpList[] {
new VdbeOpList( OP_Integer,      0,  3,  0),
new VdbeOpList( OP_Rewind,       0,  0,  0),  /* 1 */
new VdbeOpList( OP_AddImm,       3,  1,  0),
new VdbeOpList( OP_Next,         0,  0,  0),  /* 3 */
new VdbeOpList( OP_Eq,           2,  0,  3),  /* 4 */
new VdbeOpList( OP_AddImm,       1, -1,  0),
new VdbeOpList( OP_String8,      0,  2,  0),  /* 6 */
new VdbeOpList( OP_String8,      0,  3,  0),  /* 7 */
new VdbeOpList( OP_Concat,       3,  2,  2),
new VdbeOpList( OP_ResultRow,    2,  1,  0),
};
                                                            addr = sqlite3VdbeAddOp1( v, OP_IfPos, 1 );
                                                            sqlite3VdbeAddOp2( v, OP_Halt, 0, 0 );
                                                            sqlite3VdbeJumpHere( v, addr );
                                                            addr = sqlite3VdbeAddOpList( v, ArraySize( cntIdx ), cntIdx );
                                                            sqlite3VdbeChangeP1( v, addr + 1, j + 2 );
                                                            sqlite3VdbeChangeP2( v, addr + 1, addr + 4 );
                                                            sqlite3VdbeChangeP1( v, addr + 3, j + 2 );
                                                            sqlite3VdbeChangeP2( v, addr + 3, addr + 2 );
                                                            sqlite3VdbeJumpHere( v, addr + 4 );
                                                            sqlite3VdbeChangeP4( v, addr + 6,
                                                                       "wrong # of entries in index ", P4_STATIC );
                                                            sqlite3VdbeChangeP4( v, addr + 7, pIdx.zName, P4_TRANSIENT );
                                                          }
                                                        }
                                                      }
                                                      addr = sqlite3VdbeAddOpList( v, ArraySize( endCode ), endCode );
                                                      sqlite3VdbeChangeP2( v, addr, -mxErr );
                                                      sqlite3VdbeJumpHere( v, addr + 1 );
                                                      sqlite3VdbeChangeP4( v, addr + 2, "ok", P4_STATIC );
                                                    }
                                                    else
#endif // * SQLITE_OMIT_INTEGRITY_CHECK */

                                                      /*
**   PRAGMA encoding
**   PRAGMA encoding = "utf-8"|"utf-16"|"utf-16le"|"utf-16be"
**
** In its first form, this pragma returns the encoding of the main
** database. If the database is not initialized, it is initialized now.
**
** The second form of this pragma is a no-op if the main database file
** has not already been initialized. In this case it sets the default
** encoding that will be used for the main database file if a new file
** is created. If an existing main database file is opened, then the
** default text encoding for the existing database is used.
**
** In all cases new databases created using the ATTACH command are
** created to use the same default text encoding as the main database. If
** the main database has not been initialized and/or created when ATTACH
** is executed, this is done before the ATTACH operation.
**
** In the second form this pragma sets the text encoding to be used in
** new database files created using this database handle. It is only
** useful if invoked immediately after the main database i
*/
                                                      if ( zLeft.Equals( "encoding", StringComparison.OrdinalIgnoreCase )  )
                                                      {                                                        
                                                        int iEnc;
                                                        if ( null == zRight )
                                                        {    /* "PRAGMA encoding" */
                                                          if ( sqlite3ReadSchema( pParse ) != 0 )
                                                          {
                                                            pParse.nErr = 0;
                                                            pParse.zErrMsg = null;
                                                            pParse.rc = 0;//  reset errors goto pragma_out;
                                                          }
                                                          sqlite3VdbeSetNumCols( v, 1 );
                                                          sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "encoding", SQLITE_STATIC );
                                                          sqlite3VdbeAddOp2( v, OP_String8, 0, 1 );
                                                          Debug.Assert( encnames[SQLITE_UTF8].enc == SQLITE_UTF8 );
                                                          Debug.Assert( encnames[SQLITE_UTF16LE].enc == SQLITE_UTF16LE );
                                                          Debug.Assert( encnames[SQLITE_UTF16BE].enc == SQLITE_UTF16BE );
                                                          sqlite3VdbeChangeP4( v, -1, encnames[ENC( pParse.db )].zName, P4_STATIC );
                                                          sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
                                                        }
#if !SQLITE_OMIT_UTF16
else
{                        /* "PRAGMA encoding = XXX" */
/* Only change the value of sqlite.enc if the database handle is not
** initialized. If the main database exists, the new sqlite.enc value
** will be overwritten when the schema is next loaded. If it does not
** already exists, it will be created to use the new encoding value.
*/
if (
//!(DbHasProperty(db, 0, DB_SchemaLoaded)) ||
//DbHasProperty(db, 0, DB_Empty)
( db.flags & DB_SchemaLoaded ) != DB_SchemaLoaded || ( db.flags & DB_Empty ) == DB_Empty
)
{
for ( iEnc = 0 ; encnames[iEnc].zName != null ; iEnc++ )
{
if ( zRight.Equals( encnames[iEnc].zName ,StringComparison.OrdinalIgnoreCase ) )
{
pParse.db.aDbStatic[0].pSchema.enc = encnames[iEnc].enc != 0 ? encnames[iEnc].enc : SQLITE_UTF16NATIVE;
break;
}
}
if ( encnames[iEnc].zName == null )
{
sqlite3ErrorMsg( pParse, "unsupported encoding: %s", zRight );
}
}
}
#endif
                                                      }
                                                      else

#if !SQLITE_OMIT_SCHEMA_VERSION_PRAGMAS
                                                        /*
**   PRAGMA [database.]schema_version
**   PRAGMA [database.]schema_version = <integer>
**
**   PRAGMA [database.]user_version
**   PRAGMA [database.]user_version = <integer>
**
** The pragma's schema_version and user_version are used to set or get
** the value of the schema-version and user-version, respectively. Both
** the schema-version and the user-version are 32-bit signed integers
** stored in the database header.
**
** The schema-cookie is usually only manipulated internally by SQLite. It
** is incremented by SQLite whenever the database schema is modified (by
** creating or dropping a table or index). The schema version is used by
** SQLite each time a query is executed to ensure that the internal cache
** of the schema used when compiling the SQL query matches the schema of
** the database against which the compiled query is actually executed.
** Subverting this mechanism by using "PRAGMA schema_version" to modify
** the schema-version is potentially dangerous and may lead to program
** crashes or database corruption. Use with caution!
**
** The user-version is not used internally by SQLite. It may be used by
** applications for any purpose.
*/
                                                        if ( zLeft.Equals( "schema_version", StringComparison.OrdinalIgnoreCase ) 
                                                         || zLeft.Equals( "user_version", StringComparison.OrdinalIgnoreCase ) 
                                                         || zLeft.Equals( "freelist_count", StringComparison.OrdinalIgnoreCase ) 
                                                        )
                                                        {
                                                          int iCookie;   /* Cookie index. 1 for schema-cookie, 6 for user-cookie. */
                                                          sqlite3VdbeUsesBtree( v, iDb );
                                                          switch ( zLeft[0] )
                                                          {
                                                            case 'f':
                                                            case 'F':
                                                              iCookie = BTREE_FREE_PAGE_COUNT;
                                                              break;
                                                            case 's':
                                                            case 'S':
                                                              iCookie = BTREE_SCHEMA_VERSION;
                                                              break;
                                                            default:
                                                              iCookie = BTREE_USER_VERSION;
                                                              break;
                                                          }

                                                          if ( zRight != null && iCookie != BTREE_FREE_PAGE_COUNT )
                                                          {
                                                            /* Write the specified cookie value */
                                                            VdbeOpList[] setCookie = new VdbeOpList[] {
new VdbeOpList( OP_Transaction,    0,  1,  0),    /* 0 */
new   VdbeOpList( OP_Integer,        0,  1,  0),    /* 1 */
new VdbeOpList( OP_SetCookie,      0,  0,  1),    /* 2 */
};
                                                            int addr = sqlite3VdbeAddOpList( v, ArraySize( setCookie ), setCookie );
                                                            sqlite3VdbeChangeP1( v, addr, iDb );
                                                            sqlite3VdbeChangeP1( v, addr + 1, sqlite3Atoi( zRight ) );
                                                            sqlite3VdbeChangeP1( v, addr + 2, iDb );
                                                            sqlite3VdbeChangeP2( v, addr + 2, iCookie );
                                                          }
                                                          else
                                                          {
                                                            /* Read the specified cookie value */
                                                            VdbeOpList[] readCookie = new VdbeOpList[]  {
new VdbeOpList( OP_Transaction,     0,  0,  0),    /* 0 */
new VdbeOpList( OP_ReadCookie,      0,  1,  0),    /* 1 */
new VdbeOpList( OP_ResultRow,       1,  1,  0)
};
                                                            int addr = sqlite3VdbeAddOpList( v, readCookie.Length, readCookie );// ArraySize(readCookie), readCookie);
                                                            sqlite3VdbeChangeP1( v, addr, iDb );
                                                            sqlite3VdbeChangeP1( v, addr + 1, iDb );
                                                            sqlite3VdbeChangeP3( v, addr + 1, iCookie );
                                                            sqlite3VdbeSetNumCols( v, 1 );
                                                            sqlite3VdbeSetColName( v, 0, COLNAME_NAME, zLeft, SQLITE_TRANSIENT );
                                                          }
                                                        }
                                                        else if ( zLeft.Equals( "reload_schema", StringComparison.OrdinalIgnoreCase )  )
                                                        {
                                                          /* force schema reloading*/
                                                          sqlite3ResetInternalSchema( db, -1 );
                                                        }
                                                        else if ( zLeft.Equals( "file_format", StringComparison.OrdinalIgnoreCase )  )
                                                        {
                                                          pDb.pSchema.file_format = (u8)atoi( zRight );
                                                          sqlite3ResetInternalSchema( db, -1 );
                                                        }
                                                        else
#endif // * SQLITE_OMIT_SCHEMA_VERSION_PRAGMAS */

#if !SQLITE_OMIT_COMPILEOPTION_DIAGS
                                                          /*
**   PRAGMA compile_options
**
** Return the names of all compile-time options used in this build,
** one option per row.
*/
                                                          if ( zLeft.Equals( "compile_options", StringComparison.OrdinalIgnoreCase )  )
                                                          {
                                                            int i = 0;
                                                            string zOpt;
                                                            sqlite3VdbeSetNumCols( v, 1 );
                                                            pParse.nMem = 1;
                                                            sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "compile_option", SQLITE_STATIC );
                                                            while ( ( zOpt = sqlite3_compileoption_get( i++ ) ) != null )
                                                            {
                                                              sqlite3VdbeAddOp4( v, OP_String8, 0, 1, 0, zOpt, 0 );
                                                              sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 1 );
                                                            }
                                                          }
                                                          else
#endif //* SQLITE_OMIT_COMPILEOPTION_DIAGS */


#if !SQLITE_OMIT_WAL
  /*
  **   PRAGMA [database.]wal_checkpoint = passive|full|restart
  **
  ** Checkpoint the database.
  */
  if( sqlite3StrICmp(zLeft, "wal_checkpoint")==0 ){
    int iBt = (pId2->z?iDb:SQLITE_MAX_ATTACHED);
    int eMode = SQLITE_CHECKPOINT_PASSIVE;
    if( zRight ){
      if( sqlite3StrICmp(zRight, "full")==0 ){
        eMode = SQLITE_CHECKPOINT_FULL;
      }else if( sqlite3StrICmp(zRight, "restart")==0 ){
        eMode = SQLITE_CHECKPOINT_RESTART;
      }
    }
    if( sqlite3ReadSchema(pParse) ) goto pragma_out;
    sqlite3VdbeSetNumCols(v, 3);
    pParse->nMem = 3;
    sqlite3VdbeSetColName(v, 0, COLNAME_NAME, "busy", SQLITE_STATIC);
    sqlite3VdbeSetColName(v, 1, COLNAME_NAME, "log", SQLITE_STATIC);
    sqlite3VdbeSetColName(v, 2, COLNAME_NAME, "checkpointed", SQLITE_STATIC);

    sqlite3VdbeAddOp3(v, OP_Checkpoint, iBt, eMode, 1);
    sqlite3VdbeAddOp2(v, OP_ResultRow, 1, 3);
  }else

  /*
  **   PRAGMA wal_autocheckpoint
  **   PRAGMA wal_autocheckpoint = N
  **
  ** Configure a database connection to automatically checkpoint a database
  ** after accumulating N frames in the log. Or query for the current value
  ** of N.
  */
  if( sqlite3StrICmp(zLeft, "wal_autocheckpoint")==0 ){
    if( zRight ){
      sqlite3_wal_autocheckpoint(db, sqlite3Atoi(zRight));
    }
    returnSingleInt(pParse, "wal_autocheckpoint", 
       db->xWalCallback==sqlite3WalDefaultHook ? 
           SQLITE_PTR_TO_INT(db->pWalArg) : 0);
  }else
#endif

#if SQLITE_DEBUG || SQLITE_TEST
                                                            /*
** Report the current state of file logs for all databases
*/
                                                            if ( zLeft.Equals( "lock_status", StringComparison.OrdinalIgnoreCase )  )
                                                            {
                                                              string[] azLockName = {
"unlocked", "shared", "reserved", "pending", "exclusive"
};
                                                              int i;
                                                              sqlite3VdbeSetNumCols( v, 2 );
                                                              pParse.nMem = 2;
                                                              sqlite3VdbeSetColName( v, 0, COLNAME_NAME, "database", SQLITE_STATIC );
                                                              sqlite3VdbeSetColName( v, 1, COLNAME_NAME, "status", SQLITE_STATIC );
                                                              for ( i = 0; i < db.nDb; i++ )
                                                              {
                                                                Btree pBt;
                                                                Pager pPager;
                                                                string zState = "unknown";
                                                                sqlite3_int64 j = 0;
                                                                if ( db.aDb[i].zName == null )
                                                                  continue;
                                                                sqlite3VdbeAddOp4( v, OP_String8, 0, 1, 0, db.aDb[i].zName, P4_STATIC );
                                                                pBt = db.aDb[i].pBt;
                                                                if ( pBt == null || ( pPager = sqlite3BtreePager( pBt ) ) == null )
                                                                {
                                                                  zState = "closed";
                                                                }
                                                                else if ( sqlite3_file_control( db, i != 0 ? db.aDb[i].zName : null,
                                                         SQLITE_FCNTL_LOCKSTATE, ref j ) == SQLITE_OK )
                                                                {
                                                                  zState = azLockName[j];
                                                                }
                                                                sqlite3VdbeAddOp4( v, OP_String8, 0, 2, 0, zState, P4_STATIC );
                                                                sqlite3VdbeAddOp2( v, OP_ResultRow, 1, 2 );
                                                              }
                                                            }
                                                            else
#endif

#if SQLITE_HAS_CODEC
                                                              // needed to support key/rekey/hexrekey with pragma cmds
                                                              if ( zLeft.Equals( "key", StringComparison.OrdinalIgnoreCase )  && !String.IsNullOrEmpty( zRight ) )
                                                              {
                                                                sqlite3_key( db, zRight, sqlite3Strlen30( zRight ) );
                                                              }
                                                              else
                                                                if ( zLeft.Equals( "rekey", StringComparison.OrdinalIgnoreCase )  && !String.IsNullOrEmpty( zRight ) )
                                                                {
                                                                  sqlite3_rekey( db, zRight, sqlite3Strlen30( zRight ) );
                                                                }
                                                                else
                                                                  if ( !String.IsNullOrEmpty( zRight ) && ( zLeft.Equals( "hexkey", StringComparison.OrdinalIgnoreCase )  ||
                                                                  zLeft.Equals( "hexrekey", StringComparison.OrdinalIgnoreCase )  ) )
                                                                  {
                                                                    StringBuilder zKey = new StringBuilder( 40 );
                                                                    zRight.ToLower( new CultureInfo( "en-us" ) );
                                                                    // expected '0x0102030405060708090a0b0c0d0e0f10'
                                                                    if ( zRight.Length != 34 )
                                                                      return;

                                                                    for ( int i = 2; i < zRight.Length; i += 2 )
                                                                    {
                                                                      int h1 = zRight[i];
                                                                      int h2 = zRight[i + 1];
                                                                      h1 += 9 * ( 1 & ( h1 >> 6 ) );
                                                                      h2 += 9 * ( 1 & ( h2 >> 6 ) );
                                                                      zKey.Append( Convert.ToChar( ( h2 & 0x0f ) | ( ( h1 & 0xf ) << 4 ) ) );
                                                                    }
                                                                    if ( ( zLeft[3] & 0xf ) == 0xb )
                                                                    {
                                                                      sqlite3_key( db, zKey.ToString(), zKey.Length );
                                                                    }
                                                                    else
                                                                    {
                                                                      sqlite3_rekey( db, zKey.ToString(), zKey.Length );
                                                                    }
                                                                  }
                                                                  else
#endif
#if SQLITE_HAS_CODEC || SQLITE_ENABLE_CEROD
                                                                    if ( zLeft.Equals( "activate_extensions", StringComparison.OrdinalIgnoreCase )  )
                                                                    {
#if SQLITE_HAS_CODEC
                                                                      if ( !String.IsNullOrEmpty( zRight ) && zRight.Length > 4 && sqlite3StrNICmp( zRight, "see-", 4 ) == 0 )
                                                                      {
                                                                        sqlite3_activate_see( zRight.Substring( 4 ) );
                                                                      }
#endif
#if SQLITE_ENABLE_CEROD
if(  !String.IsNullOrEmpty( zRight ) &&  zRight.StartsWith("cerod-", StringComparison.OrdinalIgnoreCase))
{
sqlite3_activate_cerod( zRight.Substring( 6 ));
}
#endif
                                                                    }
                                                                    else
#endif
                                                                    { /* Empty ELSE clause */
                                                                    }

      /*
      ** Reset the safety level, in case the fullfsync flag or synchronous
      ** setting changed.
      */
#if !SQLITE_OMIT_PAGER_PRAGMAS
      if ( db.autoCommit != 0 )
      {
        sqlite3BtreeSetSafetyLevel( pDb.pBt, pDb.safety_level,
        ( ( db.flags & SQLITE_FullFSync ) != 0 ) ? 1 : 0,
        ( ( db.flags & SQLITE_CkptFullFSync ) != 0 ) ? 1 : 0 );
      }
#endif
pragma_out:
      sqlite3DbFree( db, ref zLeft );
      sqlite3DbFree( db, ref zRight );
      ;
    }

#endif // * SQLITE_OMIT_PRAGMA
  }
}
