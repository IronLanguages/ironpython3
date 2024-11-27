using System;
using System.Diagnostics;
using System.Text;

using u8 = System.Byte;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2006 June 10
    **
    ** The author disclaims copyright to this source code.  In place of
    ** a legal notice, here is a blessing:
    **
    **    May you do good and not evil.
    **    May you find forgiveness for yourself and forgive others.
    **    May you share freely, never taking more than you give.
    **
    *************************************************************************
    ** This file contains code used to help implement virtual tables.
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2008 Noah B Hart
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    **  SQLITE_SOURCE_ID: 2011-06-23 19:49:22 4374b7e83ea0a3fbc3691f9c0c936272862f32f2
    **
    *************************************************************************
    */
#if !SQLITE_OMIT_VIRTUALTABLE
    //#include "sqliteInt.h"

    /*
    ** Before a virtual table xCreate() or xConnect() method is invoked, the
    ** sqlite3.pVtabCtx member variable is set to point to an instance of
    ** this struct allocated on the stack. It is used by the implementation of 
    ** the sqlite3_declare_vtab() and sqlite3_vtab_config() APIs, both of which
    ** are invoked only from within xCreate and xConnect methods.
    */
    public class VtabCtx
    {
      public Table pTab;
      public VTable pVTable;
    };

    /*
    ** The actual function that does the work of creating a new module.
    ** This function implements the sqlite3_create_module() and
    ** sqlite3_create_module_v2() interfaces.
    */
    static int createModule(
      sqlite3 db,              /* Database in which module is registered */
      string zName,            /* Name assigned to this module */
      sqlite3_module pModule,  /* The definition of the module */
      object pAux,             /* Context pointer for xCreate/xConnect */
      smdxDestroy xDestroy     /* Module destructor function */
    )
    {
      int rc, nName;
      Module pMod;

      sqlite3_mutex_enter( db.mutex );
      nName = sqlite3Strlen30( zName );
      pMod = new Module();//  (Module)sqlite3DbMallocRaw( db, sizeof( Module ) + nName + 1 );
      if ( pMod != null )
      {
        Module pDel;
        string zCopy;// = (char )(&pMod[1]);
        zCopy = zName;//memcpy(zCopy, zName, nName+1);
        pMod.zName = zCopy;
        pMod.pModule = pModule;
        pMod.pAux = pAux;
        pMod.xDestroy = xDestroy;
        pDel = (Module)sqlite3HashInsert( ref db.aModule, zCopy, nName, pMod );
        if ( pDel != null && pDel.xDestroy != null )
        {
          sqlite3ResetInternalSchema( db, -1 );
          pDel.xDestroy( ref pDel.pAux );
        }
        sqlite3DbFree( db, ref pDel );
        //if( pDel==pMod ){
        //  db.mallocFailed = 1;
        //}
      }
      else if ( xDestroy != null )
      {
        xDestroy( ref pAux );
      }
      rc = sqlite3ApiExit( db, SQLITE_OK );
      sqlite3_mutex_leave( db.mutex );
      return rc;
    }


    /*
    ** External API function used to create a new virtual-table module.
    */
    static int sqlite3_create_module(
      sqlite3 db,               /* Database in which module is registered */
      string zName,             /* Name assigned to this module */
      sqlite3_module pModule,   /* The definition of the module */
      object pAux               /* Context pointer for xCreate/xConnect */
    )
    {
      return createModule( db, zName, pModule, pAux, null );
    }

    /*
    ** External API function used to create a new virtual-table module.
    */
    static int sqlite3_create_module_v2(
      sqlite3 db,               /* Database in which module is registered */
      string zName,             /* Name assigned to this module */
      sqlite3_module pModule,   /* The definition of the module */
      sqlite3_vtab pAux,        /* Context pointer for xCreate/xConnect */
      smdxDestroy xDestroy      /* Module destructor function */
    )
    {
      return createModule( db, zName, pModule, pAux, xDestroy );
    }

    /*
    ** Lock the virtual table so that it cannot be disconnected.
    ** Locks nest.  Every lock should have a corresponding unlock.
    ** If an unlock is omitted, resources leaks will occur.  
    **
    ** If a disconnect is attempted while a virtual table is locked,
    ** the disconnect is deferred until all locks have been removed.
    */
    static void sqlite3VtabLock( VTable pVTab )
    {
      pVTab.nRef++;
    }


    /*
    ** pTab is a pointer to a Table structure representing a virtual-table.
    ** Return a pointer to the VTable object used by connection db to access 
    ** this virtual-table, if one has been created, or NULL otherwise.
    */
    static VTable sqlite3GetVTable( sqlite3 db, Table pTab )
    {
      VTable pVtab;
      Debug.Assert( IsVirtual( pTab ) );
      for ( pVtab = pTab.pVTable; pVtab != null && pVtab.db != db; pVtab = pVtab.pNext )
        ;
      return pVtab;
    }

    /*
    ** Decrement the ref-count on a virtual table object. When the ref-count
    ** reaches zero, call the xDisconnect() method to delete the object.
    */
    static void sqlite3VtabUnlock( VTable pVTab )
    {
      sqlite3 db = pVTab.db;

      Debug.Assert( db != null);
      Debug.Assert( pVTab.nRef > 0 );
      Debug.Assert( sqlite3SafetyCheckOk( db ) );

      pVTab.nRef--;
      if ( pVTab.nRef == 0 )
      {
        object p = pVTab.pVtab;
        if ( p != null )
        {
          ((sqlite3_vtab)p).pModule.xDisconnect( ref p );
        }
        sqlite3DbFree( db, ref pVTab );
      }
    }

    /*
    ** Table p is a virtual table. This function moves all elements in the
    ** p.pVTable list to the sqlite3.pDisconnect lists of their associated
    ** database connections to be disconnected at the next opportunity. 
    ** Except, if argument db is not NULL, then the entry associated with
    ** connection db is left in the p.pVTable list.
    */
    static VTable vtabDisconnectAll( sqlite3 db, Table p )
    {
      VTable pRet = null;
      VTable pVTable = p.pVTable;
      p.pVTable = null;

      /* Assert that the mutex (if any) associated with the BtShared database 
      ** that contains table p is held by the caller. See header comments 
      ** above function sqlite3VtabUnlockList() for an explanation of why
      ** this makes it safe to access the sqlite3.pDisconnect list of any
      ** database connection that may have an entry in the p.pVTable list.
      */
      Debug.Assert( db == null || sqlite3SchemaMutexHeld( db, 0, p.pSchema ) );

      while ( pVTable != null )
      {
        sqlite3 db2 = pVTable.db;
        VTable pNext = pVTable.pNext;
        Debug.Assert( db2 != null );
        if ( db2 == db )
        {
          pRet = pVTable;
          p.pVTable = pRet;
          pRet.pNext = null;
        }
        else
        {
          pVTable.pNext = db2.pDisconnect;
          db2.pDisconnect = pVTable;
        }
        pVTable = pNext;
      }

      Debug.Assert( null == db || pRet != null );
      return pRet;
    }


    /*
    ** Disconnect all the virtual table objects in the sqlite3.pDisconnect list.
    **
    ** This function may only be called when the mutexes associated with all
    ** shared b-tree databases opened using connection db are held by the 
    ** caller. This is done to protect the sqlite3.pDisconnect list. The
    ** sqlite3.pDisconnect list is accessed only as follows:
    **
    **   1) By this function. In this case, all BtShared mutexes and the mutex
    **      associated with the database handle itself must be held.
    **
    **   2) By function vtabDisconnectAll(), when it adds a VTable entry to
    **      the sqlite3.pDisconnect list. In this case either the BtShared mutex
    **      associated with the database the virtual table is stored in is held
    **      or, if the virtual table is stored in a non-sharable database, then
    **      the database handle mutex is held.
    **
    ** As a result, a sqlite3.pDisconnect cannot be accessed simultaneously 
    ** by multiple threads. It is thread-safe.
    */
    static void sqlite3VtabUnlockList( sqlite3 db )
    {
      VTable p = db.pDisconnect;
      db.pDisconnect = null;

      Debug.Assert( sqlite3BtreeHoldsAllMutexes( db ) );
      Debug.Assert( sqlite3_mutex_held( db.mutex ) );

      if ( p != null )
      {
        sqlite3ExpirePreparedStatements( db );
        do
        {
          VTable pNext = p.pNext;
          sqlite3VtabUnlock( p );
          p = pNext;
        } while ( p != null );
      }
    }

    /*
    ** Clear any and all virtual-table information from the Table record.
    ** This routine is called, for example, just before deleting the Table
    ** record.
    **
    ** Since it is a virtual-table, the Table structure contains a pointer
    ** to the head of a linked list of VTable structures. Each VTable 
    ** structure is associated with a single sqlite3* user of the schema.
    ** The reference count of the VTable structure associated with database 
    ** connection db is decremented immediately (which may lead to the 
    ** structure being xDisconnected and free). Any other VTable structures
    ** in the list are moved to the sqlite3.pDisconnect list of the associated 
    ** database connection.
    */
    static void sqlite3VtabClear( sqlite3 db, Table p )
    {
      if ( null == db || db.pnBytesFreed == 0 )
        vtabDisconnectAll( null, p );
      if ( p.azModuleArg != null )
      {
        int i;
        for ( i = 0; i < p.nModuleArg; i++ )
        {
          sqlite3DbFree( db, ref p.azModuleArg[i] );
        }
        sqlite3DbFree( db, ref p.azModuleArg );
      }
    }

    /*
    ** Add a new module argument to pTable.azModuleArg[].
    ** The string is not copied - the pointer is stored.  The
    ** string will be freed automatically when the table is
    ** deleted.
    */
    static void addModuleArgument( sqlite3 db, Table pTable, string zArg )
    {
      int i = pTable.nModuleArg++;
      //int nBytes = sizeof(char )*(1+pTable.nModuleArg);
      //string[] azModuleArg;
      //sqlite3DbRealloc( db, pTable.azModuleArg, nBytes );
      if ( pTable.azModuleArg == null || pTable.azModuleArg.Length < pTable.nModuleArg )
        Array.Resize( ref pTable.azModuleArg, 3 + pTable.nModuleArg );
      //if ( azModuleArg == null )
      //{
      //  int j;
      //  for ( j = 0; j < i; j++ )
      //  {
      //    sqlite3DbFree( db, ref pTable.azModuleArg[j] );
      //  }
      //  sqlite3DbFree( db, ref zArg );
      //  sqlite3DbFree( db, ref pTable.azModuleArg );
      //  pTable.nModuleArg = 0;
      //}
      //else
      {
        pTable.azModuleArg[i] = zArg;
        //pTable.azModuleArg[i + 1] = null;
        //azModuleArg[i+1] = 0;
      }
      //pTable.azModuleArg = azModuleArg;
    }

    /*
    ** The parser calls this routine when it first sees a CREATE VIRTUAL TABLE
    ** statement.  The module name has been parsed, but the optional list
    ** of parameters that follow the module name are still pending.
    */
    static void sqlite3VtabBeginParse(
      Parse pParse,        /* Parsing context */
      Token pName1,        /* Name of new table, or database name */
      Token pName2,        /* Name of new table or NULL */
      Token pModuleName    /* Name of the module for the virtual table */
    )
    {
      int iDb;              /* The database the table is being created in */
      Table pTable;        /* The new virtual table */
      sqlite3 db;          /* Database connection */

      sqlite3StartTable( pParse, pName1, pName2, 0, 0, 1, 0 );
      pTable = pParse.pNewTable;
      if ( pTable == null )
        return;
      Debug.Assert( null == pTable.pIndex );

      db = pParse.db;
      iDb = sqlite3SchemaToIndex( db, pTable.pSchema );
      Debug.Assert( iDb >= 0 );

      pTable.tabFlags |= TF_Virtual;
      pTable.nModuleArg = 0;
      addModuleArgument( db, pTable, sqlite3NameFromToken( db, pModuleName ) );
      addModuleArgument( db, pTable, db.aDb[iDb].zName);//sqlite3DbStrDup( db, db.aDb[iDb].zName ) );
      addModuleArgument( db, pTable, pTable.zName );//sqlite3DbStrDup( db, pTable.zName ) );
      pParse.sNameToken.n = pParse.sNameToken.z.Length;//      (int)[pModuleName.n] - pName1.z );

#if !SQLITE_OMIT_AUTHORIZATION
  /* Creating a virtual table invokes the authorization callback twice.
  ** The first invocation, to obtain permission to INSERT a row into the
  ** sqlite_master table, has already been made by sqlite3StartTable().
  ** The second call, to obtain permission to create the table, is made now.
  */
  if( pTable->azModuleArg ){
    sqlite3AuthCheck(pParse, SQLITE_CREATE_VTABLE, pTable->zName, 
            pTable->azModuleArg[0], pParse->db->aDb[iDb].zName);
  }
#endif
    }

    /*
    ** This routine takes the module argument that has been accumulating
    ** in pParse.zArg[] and appends it to the list of arguments on the
    ** virtual table currently under construction in pParse.pTable.
    */
    static void addArgumentToVtab( Parse pParse )
    {
      if ( pParse.sArg.z != null && ALWAYS( pParse.pNewTable ) )
      {
        string z = pParse.sArg.z.Substring( 0, pParse.sArg.n );
        ////int n = pParse.sArg.n;
        sqlite3 db = pParse.db;
        addModuleArgument( db, pParse.pNewTable, z );////sqlite3DbStrNDup( db, z, n ) );
      }
    }

    /*
    ** The parser calls this routine after the CREATE VIRTUAL TABLE statement
    ** has been completely parsed.
    */
    static void sqlite3VtabFinishParse( Parse pParse, Token pEnd )
    {
      Table pTab = pParse.pNewTable;  /* The table being constructed */
      sqlite3 db = pParse.db;         /* The database connection */

      if ( pTab == null )
        return;
      addArgumentToVtab( pParse );
      pParse.sArg.z = "";
      if ( pTab.nModuleArg < 1 )
        return;

      /* If the CREATE VIRTUAL TABLE statement is being entered for the
      ** first time (in other words if the virtual table is actually being
      ** created now instead of just being read out of sqlite_master) then
      ** do additional initialization work and store the statement text
      ** in the sqlite_master table.
      */
      if ( 0 == db.init.busy )
      {
        string zStmt;
        string zWhere;
        int iDb;
        Vdbe v;

        /* Compute the complete text of the CREATE VIRTUAL TABLE statement */
        if ( pEnd != null )
        {
          pParse.sNameToken.n = pParse.sNameToken.z.Length;//(int)( pEnd.z - pParse.sNameToken.z ) + pEnd.n;
        }
        zStmt = sqlite3MPrintf( db, "CREATE VIRTUAL TABLE %T", pParse.sNameToken.z.Substring(0,pParse.sNameToken.n) );

        /* A slot for the record has already been allocated in the 
        ** SQLITE_MASTER table.  We just need to update that slot with all
        ** the information we've collected.  
        **
        ** The VM register number pParse.regRowid holds the rowid of an
        ** entry in the sqlite_master table tht was created for this vtab
        ** by sqlite3StartTable().
        */
        iDb = sqlite3SchemaToIndex( db, pTab.pSchema );
        sqlite3NestedParse( pParse,
          "UPDATE %Q.%s " +
             "SET type='table', name=%Q, tbl_name=%Q, rootpage=0, sql=%Q " +
           "WHERE rowid=#%d",
          db.aDb[iDb].zName, SCHEMA_TABLE( iDb ),
          pTab.zName,
          pTab.zName,
          zStmt,
          pParse.regRowid
        );
        sqlite3DbFree( db, ref zStmt );
        v = sqlite3GetVdbe( pParse );
        sqlite3ChangeCookie( pParse, iDb );

        sqlite3VdbeAddOp2( v, OP_Expire, 0, 0 );
        zWhere = sqlite3MPrintf( db, "name='%q' AND type='table'", pTab.zName );
        sqlite3VdbeAddParseSchemaOp( v, iDb, zWhere );
        sqlite3VdbeAddOp4( v, OP_VCreate, iDb, 0, 0,
                             pTab.zName, sqlite3Strlen30( pTab.zName ) + 1 );
      }

      /* If we are rereading the sqlite_master table create the in-memory
      ** record of the table. The xConnect() method is not called until
      ** the first time the virtual table is used in an SQL statement. This
      ** allows a schema that contains virtual tables to be loaded before
      ** the required virtual table implementations are registered.  */
      else
      {
        Table pOld;
        Schema pSchema = pTab.pSchema;
        string zName = pTab.zName;
        int nName = sqlite3Strlen30( zName );
        Debug.Assert( sqlite3SchemaMutexHeld( db, 0, pSchema ) );
        pOld = sqlite3HashInsert( ref pSchema.tblHash, zName, nName, pTab );
        if ( pOld != null )
        {
          //db.mallocFailed = 1;
          Debug.Assert( pTab == pOld );  /* Malloc must have failed inside HashInsert() */
          return;
        }
        pParse.pNewTable = null;
      }
    }

    /*
    ** The parser calls this routine when it sees the first token
    ** of an argument to the module name in a CREATE VIRTUAL TABLE statement.
    */
    static void sqlite3VtabArgInit( Parse pParse )
    {
      addArgumentToVtab( pParse );
      pParse.sArg.z = null;
      pParse.sArg.n = 0;
    }

    /*
    ** The parser calls this routine for each token after the first token
    ** in an argument to the module name in a CREATE VIRTUAL TABLE statement.
    */
    static void sqlite3VtabArgExtend( Parse pParse, Token p )
    {
      Token pArg = pParse.sArg;
      if ( pArg.z == null )
      {
        pArg.z = p.z;
        pArg.n = p.n;
      }
      else
      {
        //Debug.Assert( pArg.z< p.z );
        pArg.n += p.n+1;//(int)( p.z[p.n] - pArg.z );
      }
    }

    /*
    ** Invoke a virtual table constructor (either xCreate or xConnect). The
    ** pointer to the function to invoke is passed as the fourth parameter
    ** to this procedure.
    */
    static int vtabCallConstructor(
      sqlite3 db,
      Table pTab,
      Module pMod,
      smdxCreateConnect xConstruct,
      ref string pzErr
    )
    {
      VtabCtx sCtx = new VtabCtx();
      VTable pVTable;
      int rc;
      string[] azArg = pTab.azModuleArg;
      int nArg = pTab.nModuleArg;
      string zErr = null;
      string zModuleName = sqlite3MPrintf( db, "%s", pTab.zName );

      //if ( String.IsNullOrEmpty( zModuleName ) )
      //{
      //  return SQLITE_NOMEM;
      //}

      pVTable = new VTable();//sqlite3DbMallocZero( db, sizeof( VTable ) );
      //if ( null == pVTable )
      //{
      //  sqlite3DbFree( db, ref zModuleName );
      //  return SQLITE_NOMEM;
      //}
      pVTable.db = db;
      pVTable.pMod = pMod;

      /* Invoke the virtual table constructor */
      //assert( &db->pVtabCtx );
      Debug.Assert( xConstruct != null );
      sCtx.pTab = pTab;
      sCtx.pVTable = pVTable;
      db.pVtabCtx = sCtx;
      rc = xConstruct( db, pMod.pAux, nArg, azArg, out pVTable.pVtab, out zErr );
      db.pVtabCtx = null;
      //if ( rc == SQLITE_NOMEM )
      //  db.mallocFailed = 1;

      if ( SQLITE_OK != rc )
      {
        if ( zErr == "" )
        {
          pzErr = sqlite3MPrintf( db, "vtable constructor failed: %s", zModuleName );
        }
        else
        {
          pzErr = sqlite3MPrintf( db, "%s", zErr );
          zErr = null;//sqlite3_free( zErr );
        }
        sqlite3DbFree( db, ref pVTable );
      }
      else if ( ALWAYS( pVTable.pVtab ) )
      {
        /* Justification of ALWAYS():  A correct vtab constructor must allocate
        ** the sqlite3_vtab object if successful.  */
        pVTable.pVtab.pModule = pMod.pModule;
        pVTable.nRef = 1;
        if ( sCtx.pTab != null )
        {
          string zFormat = "vtable constructor did not declare schema: %s";
          pzErr = sqlite3MPrintf( db, zFormat, pTab.zName );
          sqlite3VtabUnlock( pVTable );
          rc = SQLITE_ERROR;
        }
        else
        {
          int iCol;
          /* If everything went according to plan, link the new VTable structure
          ** into the linked list headed by pTab->pVTable. Then loop through the 
          ** columns of the table to see if any of them contain the token "hidden".
          ** If so, set the Column.isHidden flag and remove the token from
          ** the type string.  */
          pVTable.pNext = pTab.pVTable;
          pTab.pVTable = pVTable;

          for ( iCol = 0; iCol < pTab.nCol; iCol++ )
          {
            if ( String.IsNullOrEmpty( pTab.aCol[iCol].zType ) )
              continue;
            StringBuilder zType = new StringBuilder( pTab.aCol[iCol].zType);
            int nType;
            int i = 0;
            //if ( zType )
            //  continue;
            nType = sqlite3Strlen30( zType );
            if ( sqlite3StrNICmp( "hidden", 0, zType.ToString(), 6 ) != 0 || ( zType.Length > 6 && zType[6] != ' ' ) )
            {
              for ( i = 0; i < nType; i++ )
              {
                if ( ( 0 == sqlite3StrNICmp( " hidden", zType.ToString().Substring( i ), 7 ) )
                 && ( i+7 == zType.Length || (zType[i + 7] == '\0' || zType[i + 7] == ' ' ))
                )
                {
                  i++;
                  break;
                }
              }
            }
            if ( i < nType )
            {
              int j;
              int nDel = 6 + ( zType.Length > i + 6 ? 1 : 0 );
              for ( j = i; ( j + nDel ) < nType; j++ )
              {
                zType[j] = zType[j + nDel];
              }
              if ( zType[i] == '\0' && i > 0 )
              {
                Debug.Assert( zType[i - 1] == ' ' );
                zType.Length = i;//[i - 1] = '\0';
              }
              pTab.aCol[iCol].isHidden = 1;
              pTab.aCol[iCol].zType = zType.ToString().Substring(0,j);
            }
          }
        }
      }

      sqlite3DbFree( db, ref zModuleName );
      return rc;
    }

    /*
    ** This function is invoked by the parser to call the xConnect() method
    ** of the virtual table pTab. If an error occurs, an error code is returned 
    ** and an error left in pParse.
    **
    ** This call is a no-op if table pTab is not a virtual table.
    */
    static int sqlite3VtabCallConnect( Parse pParse, Table pTab )
    {
      sqlite3 db = pParse.db;
      string zMod;
      Module pMod;
      int rc;

      Debug.Assert( pTab != null);
      if ( ( pTab.tabFlags & TF_Virtual ) == 0 || sqlite3GetVTable( db, pTab )!= null )
      {
        return SQLITE_OK;
      }

      /* Locate the required virtual table module */
      zMod = pTab.azModuleArg[0];
      pMod = (Module)sqlite3HashFind( db.aModule, zMod, sqlite3Strlen30( zMod ), (Module)null );

      if ( null == pMod )
      {
        string zModule = pTab.azModuleArg[0];
        sqlite3ErrorMsg( pParse, "no such module: %s", zModule );
        rc = SQLITE_ERROR;
      }
      else
      {
        string zErr = null;
        rc = vtabCallConstructor( db, pTab, pMod, pMod.pModule.xConnect, ref zErr );
        if ( rc != SQLITE_OK )
        {
          sqlite3ErrorMsg( pParse, "%s", zErr );
        }
        zErr = null;//sqlite3DbFree( db, zErr );
      }

      return rc;
    }
    /*
    ** Grow the db.aVTrans[] array so that there is room for at least one
    ** more v-table. Return SQLITE_NOMEM if a malloc fails, or SQLITE_OK otherwise.
    */
    static int growVTrans( sqlite3 db )
    {
      const int ARRAY_INCR = 5;

      /* Grow the sqlite3.aVTrans array if required */
      if ( ( db.nVTrans % ARRAY_INCR ) == 0 )
      {
        //VTable** aVTrans;
        //int nBytes = sizeof( sqlite3_vtab* ) * ( db.nVTrans + ARRAY_INCR );
        //aVTrans = sqlite3DbRealloc( db, (void)db.aVTrans, nBytes );
        //if ( !aVTrans )
        //{
        //  return SQLITE_NOMEM;
        //}
        //memset( &aVTrans[db.nVTrans], 0, sizeof( sqlite3_vtab* ) * ARRAY_INCR );
        Array.Resize( ref db.aVTrans, db.nVTrans + ARRAY_INCR );
      }

      return SQLITE_OK;
    }

    /*
    ** Add the virtual table pVTab to the array sqlite3.aVTrans[]. Space should
    ** have already been reserved using growVTrans().
    */
    static void addToVTrans( sqlite3 db, VTable pVTab )
    {
      /* Add pVtab to the end of sqlite3.aVTrans */
      db.aVTrans[db.nVTrans++] = pVTab;
      sqlite3VtabLock( pVTab );
    }

    /*
    ** This function is invoked by the vdbe to call the xCreate method
    ** of the virtual table named zTab in database iDb. 
    **
    ** If an error occurs, *pzErr is set to point an an English language
    ** description of the error and an SQLITE_XXX error code is returned.
    ** In this case the caller must call sqlite3DbFree(db, ) on *pzErr.
    */
    static int sqlite3VtabCallCreate( sqlite3 db, int iDb, string zTab, ref string pzErr )
    {
      int rc = SQLITE_OK;
      Table pTab;
      Module pMod;
      string zMod;

      pTab = sqlite3FindTable( db, zTab, db.aDb[iDb].zName );
      Debug.Assert( pTab != null && ( pTab.tabFlags & TF_Virtual ) != 0 && null == pTab.pVTable );

      /* Locate the required virtual table module */
      zMod = pTab.azModuleArg[0];
      pMod = (Module)sqlite3HashFind( db.aModule, zMod, sqlite3Strlen30( zMod ), (Module)null );

      /* If the module has been registered and includes a Create method, 
      ** invoke it now. If the module has not been registered, return an 
      ** error. Otherwise, do nothing.
      */
      if ( null == pMod )
      {
        pzErr = sqlite3MPrintf( db, "no such module: %s", zMod );
        rc = SQLITE_ERROR;
      }
      else
      {
        rc = vtabCallConstructor( db, pTab, pMod, pMod.pModule.xCreate, ref pzErr );
      }

      /* Justification of ALWAYS():  The xConstructor method is required to
      ** create a valid sqlite3_vtab if it returns SQLITE_OK. */
      if ( rc == SQLITE_OK && ALWAYS( sqlite3GetVTable( db, pTab ) ) )
      {
        rc = growVTrans( db );
        if ( rc == SQLITE_OK )
        {
          addToVTrans( db, sqlite3GetVTable( db, pTab ) );
        }
      }

      return rc;
    }

    /*
    ** This function is used to set the schema of a virtual table.  It is only
    ** valid to call this function from within the xCreate() or xConnect() of a
    ** virtual table module.
    */
    static int sqlite3_declare_vtab( sqlite3 db, string zCreateTable )
    {
      Parse pParse;

      int rc = SQLITE_OK;
      Table pTab;
      string zErr = "";

      sqlite3_mutex_enter( db.mutex );
      if ( null == db.pVtabCtx || null == ( pTab = db.pVtabCtx.pTab ) )
      {
        sqlite3Error( db, SQLITE_MISUSE, 0 );
        sqlite3_mutex_leave( db.mutex );
        return SQLITE_MISUSE_BKPT();
      }
      Debug.Assert( ( pTab.tabFlags & TF_Virtual ) != 0 );

      pParse = new Parse();//sqlite3StackAllocZero(db, sizeof(*pParse));
      //if ( pParse == null )
      //{
      //  rc = SQLITE_NOMEM;
      //}
      //else
      {
        pParse.declareVtab = 1;
        pParse.db = db;
        pParse.nQueryLoop = 1;

        if ( SQLITE_OK == sqlite3RunParser( pParse, zCreateTable, ref zErr )
         && pParse.pNewTable != null
         //&& !db.mallocFailed
         && null==pParse.pNewTable.pSelect
         && ( pParse.pNewTable.tabFlags & TF_Virtual ) == 0
        )
        {
          if ( null==pTab.aCol )
          {
            pTab.aCol = pParse.pNewTable.aCol;
            pTab.nCol = pParse.pNewTable.nCol;
            pParse.pNewTable.nCol = 0;
            pParse.pNewTable.aCol = null;
          }
          db.pVtabCtx.pTab = null;
        }
        else
        {
          sqlite3Error( db, SQLITE_ERROR, ( zErr != null ? "%s" : null ), zErr );
          zErr = null;//sqlite3DbFree( db, zErr );
          rc = SQLITE_ERROR;
        }
        pParse.declareVtab = 0;

        if ( pParse.pVdbe !=null)
        {
          sqlite3VdbeFinalize( ref pParse.pVdbe );
        }
        sqlite3DeleteTable( db, ref pParse.pNewTable );
        //sqlite3StackFree( db, pParse );
      }

      Debug.Assert( ( rc & 0xff ) == rc );
      rc = sqlite3ApiExit( db, rc );
      sqlite3_mutex_leave( db.mutex );
      return rc;
    }

    /*
    ** This function is invoked by the vdbe to call the xDestroy method
    ** of the virtual table named zTab in database iDb. This occurs
    ** when a DROP TABLE is mentioned.
    **
    ** This call is a no-op if zTab is not a virtual table.
    */
    static int sqlite3VtabCallDestroy( sqlite3 db, int iDb, string zTab )
    {
      int rc = SQLITE_OK;
      Table pTab;

      pTab = sqlite3FindTable( db, zTab, db.aDb[iDb].zName );
      if ( ALWAYS( pTab != null && pTab.pVTable != null ) )
      {
        VTable p = vtabDisconnectAll( db, pTab );

        Debug.Assert( rc == SQLITE_OK );
        object obj = p.pVtab;
        rc = p.pMod.pModule.xDestroy( ref obj );
        p.pVtab = null;

        /* Remove the sqlite3_vtab* from the aVTrans[] array, if applicable */
        if ( rc == SQLITE_OK )
        {
          Debug.Assert( pTab.pVTable == p && p.pNext == null );
          p.pVtab = null;
          pTab.pVTable = null;
          sqlite3VtabUnlock( p );
        }
      }

      return rc;
    }

    /*
    ** This function invokes either the xRollback or xCommit method
    ** of each of the virtual tables in the sqlite3.aVTrans array. The method
    ** called is identified by the second argument, "offset", which is
    ** the offset of the method to call in the sqlite3_module structure.
    **
    ** The array is cleared after invoking the callbacks. 
    */
    static void callFinaliser( sqlite3 db, int offset )
    {
      int i;
      if ( db.aVTrans != null )
      {
        for ( i = 0; i < db.nVTrans; i++ )
        {
          VTable pVTab = db.aVTrans[i];
          sqlite3_vtab p = pVTab.pVtab;
          if ( p != null )
          {
            //int (*x)(sqlite3_vtab );
            //x = *(int (*)(sqlite3_vtab ))((char )p.pModule + offset);
            //if( x ) x(p);
            if ( offset == 0 )
            {
              if ( p.pModule.xCommit != null )
                p.pModule.xCommit( p );
            }
            else
            {
              if ( p.pModule.xRollback != null )
                p.pModule.xRollback( p );
            }
          }
          pVTab.iSavepoint = 0;
          sqlite3VtabUnlock( pVTab );
        }
        sqlite3DbFree( db, ref db.aVTrans );
        db.nVTrans = 0;
        db.aVTrans = null;
      }
    }

    /*
    ** Invoke the xSync method of all virtual tables in the sqlite3.aVTrans
    ** array. Return the error code for the first error that occurs, or
    ** SQLITE_OK if all xSync operations are successful.
    **
    ** Set *pzErrmsg to point to a buffer that should be released using 
    ** sqlite3DbFree() containing an error message, if one is available.
    */
    static int sqlite3VtabSync( sqlite3 db, ref string pzErrmsg )
    {
      int i;
      int rc = SQLITE_OK;
      VTable[] aVTrans = db.aVTrans;

      db.aVTrans = null;
      for ( i = 0; rc == SQLITE_OK && i < db.nVTrans; i++ )
      {
        smdxFunction x;//int (*x)(sqlite3_vtab );
        sqlite3_vtab pVtab = aVTrans[i].pVtab;
        if ( pVtab != null && ( x = pVtab.pModule.xSync ) != null )
        {
          rc = x( pVtab );
          //sqlite3DbFree(db, ref pzErrmsg);
          pzErrmsg = pVtab.zErrMsg;// sqlite3DbStrDup( db, pVtab.zErrMsg );
          pVtab.zErrMsg = null;//sqlite3_free( ref pVtab.zErrMsg );
        }
      }
      db.aVTrans = aVTrans;
      return rc;
    }

    /*
    ** Invoke the xRollback method of all virtual tables in the 
    ** sqlite3.aVTrans array. Then clear the array itself.
    */
    static int sqlite3VtabRollback( sqlite3 db )
    {
      callFinaliser( db, 1 );//offsetof( sqlite3_module, xRollback ) );
      return SQLITE_OK;
    }

    /*
    ** Invoke the xCommit method of all virtual tables in the 
    ** sqlite3.aVTrans array. Then clear the array itself.
    */
    static int sqlite3VtabCommit( sqlite3 db )
    {
      callFinaliser( db, 0 );//offsetof( sqlite3_module, xCommit ) );
      return SQLITE_OK;
    }

    /*
    ** If the virtual table pVtab supports the transaction interface
    ** (xBegin/xRollback/xCommit and optionally xSync) and a transaction is
    ** not currently open, invoke the xBegin method now.
    **
    ** If the xBegin call is successful, place the sqlite3_vtab pointer
    ** in the sqlite3.aVTrans array.
    */
    static int sqlite3VtabBegin( sqlite3 db, VTable pVTab )
    {
      int rc = SQLITE_OK;
      sqlite3_module pModule;

      /* Special case: If db.aVTrans is NULL and db.nVTrans is greater
      ** than zero, then this function is being called from within a
      ** virtual module xSync() callback. It is illegal to write to 
      ** virtual module tables in this case, so return SQLITE_LOCKED.
      */
      if ( sqlite3VtabInSync( db ) )
      {
        return SQLITE_LOCKED;
      }
      if ( null == pVTab )
      {
        return SQLITE_OK;
      }
      pModule = pVTab.pVtab.pModule;

      if ( pModule.xBegin != null )
      {
        int i;

        /* If pVtab is already in the aVTrans array, return early */
        for ( i = 0; i < db.nVTrans; i++ )
        {
          if ( db.aVTrans[i] == pVTab )
          {
            return SQLITE_OK;
          }
        }

        /* Invoke the xBegin method. If successful, add the vtab to the 
        ** sqlite3.aVTrans[] array. */
        rc = growVTrans( db );
        if ( rc == SQLITE_OK )
        {
          rc = pModule.xBegin( pVTab.pVtab );
          if ( rc == SQLITE_OK )
          {
            addToVTrans( db, pVTab );
          }
        }
      }
      return rc;
    }

    /*
    ** Invoke either the xSavepoint, xRollbackTo or xRelease method of all
    ** virtual tables that currently have an open transaction. Pass iSavepoint
    ** as the second argument to the virtual table method invoked.
    **
    ** If op is SAVEPOINT_BEGIN, the xSavepoint method is invoked. If it is
    ** SAVEPOINT_ROLLBACK, the xRollbackTo method. Otherwise, if op is 
    ** SAVEPOINT_RELEASE, then the xRelease method of each virtual table with
    ** an open transaction is invoked.
    **
    ** If any virtual table method returns an error code other than SQLITE_OK, 
    ** processing is abandoned and the error returned to the caller of this
    ** function immediately. If all calls to virtual table methods are successful,
    ** SQLITE_OK is returned.
    */
    static int sqlite3VtabSavepoint( sqlite3 db, int op, int iSavepoint )
    {
      int rc = SQLITE_OK;

      Debug.Assert( op == SAVEPOINT_RELEASE || op == SAVEPOINT_ROLLBACK || op == SAVEPOINT_BEGIN );
      Debug.Assert( iSavepoint >= 0 );
      if ( db.aVTrans != null )
      {
        int i;
        for ( i = 0; rc == SQLITE_OK && i < db.nVTrans; i++ )
        {
          VTable pVTab = db.aVTrans[i];
          sqlite3_module pMod = pVTab.pMod.pModule;
          if ( pMod.iVersion >= 2 )
          {
            smdxFunctionArg xMethod = null; //int (*xMethod)(sqlite3_vtab *, int);
            switch ( op )
            {
              case SAVEPOINT_BEGIN:
                xMethod = pMod.xSavepoint;
                pVTab.iSavepoint = iSavepoint + 1;
                break;
              case SAVEPOINT_ROLLBACK:
                xMethod = pMod.xRollbackTo;
                break;
              default:
                xMethod = pMod.xRelease;
                break;
            }
            if ( xMethod != null && pVTab.iSavepoint > iSavepoint )
            {
              rc = xMethod( db.aVTrans[i].pVtab, iSavepoint );
            }
          }
        }
      }
      return rc;
    }

    /*
    ** The first parameter (pDef) is a function implementation.  The
    ** second parameter (pExpr) is the first argument to this function.
    ** If pExpr is a column in a virtual table, then let the virtual
    ** table implementation have an opportunity to overload the function.
    **
    ** This routine is used to allow virtual table implementations to
    ** overload MATCH, LIKE, GLOB, and REGEXP operators.
    **
    ** Return either the pDef argument (indicating no change) or a 
    ** new FuncDef structure that is marked as ephemeral using the
    ** SQLITE_FUNC_EPHEM flag.
    */
    static FuncDef sqlite3VtabOverloadFunction(
      sqlite3 db,    /* Database connection for reporting malloc problems */
      FuncDef pDef,  /* Function to possibly overload */
      int nArg,      /* Number of arguments to the function */
      Expr pExpr     /* First argument to the function */
    )
    {
      Table pTab;
      sqlite3_vtab pVtab;
      sqlite3_module pMod;
      dxFunc xFunc = null;//void (*xFunc)(sqlite3_context*,int,sqlite3_value*) = 0;
      object pArg = null;
      FuncDef pNew;
      int rc = 0;
      string zLowerName;
      string z;

      /* Check to see the left operand is a column in a virtual table */
      if ( NEVER( pExpr == null ) )
        return pDef;
      if ( pExpr.op != TK_COLUMN )
        return pDef;
      pTab = pExpr.pTab;
      if ( NEVER( pTab == null ) )
        return pDef;
      if ( ( pTab.tabFlags & TF_Virtual ) == 0 )
        return pDef;
      pVtab = sqlite3GetVTable( db, pTab ).pVtab;
      Debug.Assert( pVtab != null );
      Debug.Assert( pVtab.pModule != null );
      pMod = (sqlite3_module)pVtab.pModule;
      if ( pMod.xFindFunction == null )
        return pDef;

      /* Call the xFindFunction method on the virtual table implementation
      ** to see if the implementation wants to overload this function 
      */
      zLowerName = pDef.zName;//sqlite3DbStrDup(db, pDef.zName);
      if ( zLowerName != null )
      {
        //for(z=(unsigned char)zLowerName; *z; z++){
        //  *z = sqlite3UpperToLower[*z];
        //}
        rc = pMod.xFindFunction( pVtab, nArg, zLowerName.ToLowerInvariant(), ref xFunc, ref pArg );
        sqlite3DbFree( db, ref zLowerName );
      }
      if ( rc == 0 )
      {
        return pDef;
      }

      /* Create a new ephemeral function definition for the overloaded
      ** function */
      //sqlite3DbMallocZero(db, sizeof(*pNew)
      //      + sqlite3Strlen30(pDef.zName) + 1);
      //if ( pNew == null )
      //{
      //  return pDef;
      //}
      pNew = pDef.Copy();
      pNew.zName = pDef.zName;
      //pNew.zName = (char )&pNew[1];
      //memcpy(pNew.zName, pDef.zName, sqlite3Strlen30(pDef.zName)+1);
      pNew.xFunc = xFunc;
      pNew.pUserData = pArg;
      pNew.flags |= SQLITE_FUNC_EPHEM;
      return pNew;
    }

    /*
    ** Make sure virtual table pTab is contained in the pParse.apVirtualLock[]
    ** array so that an OP_VBegin will get generated for it.  Add pTab to the
    ** array if it is missing.  If pTab is already in the array, this routine
    ** is a no-op.
    */
    static void sqlite3VtabMakeWritable( Parse pParse, Table pTab )
    {
      Parse pToplevel = sqlite3ParseToplevel( pParse );
      int i, n;
      //Table[] apVtabLock = null;

      Debug.Assert( IsVirtual( pTab ) );
      for ( i = 0; i < pToplevel.nVtabLock; i++ )
      {
        if ( pTab == pToplevel.apVtabLock[i] )
          return;
      }
      n = pToplevel.apVtabLock == null ? 1 : pToplevel.apVtabLock.Length + 1;//(pToplevel.nVtabLock+1)*sizeof(pToplevel.apVtabLock[0]);
      //sqlite3_realloc( pToplevel.apVtabLock, n );
      //if ( apVtabLock != null )
      {
        Array.Resize( ref pToplevel.apVtabLock, n );// pToplevel.apVtabLock= apVtabLock;
        pToplevel.apVtabLock[pToplevel.nVtabLock++] = pTab;
      }
      //else
      //{
      //  pToplevel.db.mallocFailed = 1;
      //}
    }

    static int[] aMap = new int[] { 
    SQLITE_ROLLBACK, SQLITE_ABORT, SQLITE_FAIL, SQLITE_IGNORE, SQLITE_REPLACE 
  };
    /*
    ** Return the ON CONFLICT resolution mode in effect for the virtual
    ** table update operation currently in progress.
    **
    ** The results of this routine are undefined unless it is called from
    ** within an xUpdate method.
    */
    static int sqlite3_vtab_on_conflict( sqlite3 db ){
  //static const unsigned char aMap[] = { 
  //  SQLITE_ROLLBACK, SQLITE_ABORT, SQLITE_FAIL, SQLITE_IGNORE, SQLITE_REPLACE 
  //};
  Debug.Assert( OE_Rollback==1 && OE_Abort==2 && OE_Fail==3 );
  Debug.Assert( OE_Ignore==4 && OE_Replace==5 );
  Debug.Assert( db.vtabOnConflict>=1 && db.vtabOnConflict<=5 );
  return (int)aMap[db.vtabOnConflict-1];
}

    /*
    ** Call from within the xCreate() or xConnect() methods to provide 
    ** the SQLite core with additional information about the behavior
    ** of the virtual table being implemented.
    */
    static int sqlite3_vtab_config( sqlite3 db, int op, params object[] ap ){ // TODO ...){
  //va_list ap;
  int rc = SQLITE_OK;

  sqlite3_mutex_enter(db.mutex);

  va_start(ap, "op");
  switch( op ){
    case SQLITE_VTAB_CONSTRAINT_SUPPORT: {
      VtabCtx p = db.pVtabCtx;
      if(  null == p ){
        rc = SQLITE_MISUSE_BKPT();
      }else{
        Debug.Assert( p.pTab == null || ( p.pTab.tabFlags & TF_Virtual ) != 0 );
        p.pVTable.bConstraint = (Byte)va_arg(ap, (Int32)0);
      }
      break;
    }
    default:
      rc = SQLITE_MISUSE_BKPT();
      break;
  }
  va_end(ref ap);

  if( rc!=SQLITE_OK ) sqlite3Error(db, rc, 0);
  sqlite3_mutex_leave(db.mutex);
  return rc;
}

#endif //* SQLITE_OMIT_VIRTUALTABLE */
  }
}
