using System;
using System.Diagnostics;
using System.Text;

using Bitmask = System.UInt64;
using u8 = System.Byte;
using u16 = System.UInt16;
using u32 = System.UInt32;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    ** 2009 November 25
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
    ** This file contains code used to insert the values of host parameters
    ** (aka "wildcards") into the SQL text output by sqlite3_trace().
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

#if !SQLITE_OMIT_TRACE

    /*
** zSql is a zero-terminated string of UTF-8 SQL text.  Return the number of
** bytes in this text up to but excluding the first character in
** a host parameter.  If the text contains no host parameters, return
** the total number of bytes in the text.
*/
    static int findNextHostParameter( string zSql, int iOffset, ref int pnToken )
    {
      int tokenType = 0;
      int nTotal = 0;
      int n;

      pnToken = 0;
      while ( iOffset < zSql.Length )
      {
        n = sqlite3GetToken( zSql, iOffset, ref tokenType );
        Debug.Assert( n > 0 && tokenType != TK_ILLEGAL );
        if ( tokenType == TK_VARIABLE )
        {
          pnToken = n;
          break;
        }
        nTotal += n;
        iOffset += n;// zSql += n;
      }
      return nTotal;
    }

    /*
    ** This function returns a pointer to a nul-terminated string in memory
    ** obtained from sqlite3DbMalloc(). If sqlite3.vdbeExecCnt is 1, then the
    ** string contains a copy of zRawSql but with host parameters expanded to 
    ** their current bindings. Or, if sqlite3.vdbeExecCnt is greater than 1, 
    ** then the returned string holds a copy of zRawSql with "-- " prepended
    ** to each line of text.
    **
    ** The calling function is responsible for making sure the memory returned
    ** is eventually freed.
    **
    ** ALGORITHM:  Scan the input string looking for host parameters in any of
    ** these forms:  ?, ?N, $A, @A, :A.  Take care to avoid text within
    ** string literals, quoted identifier names, and comments.  For text forms,
    ** the host parameter index is found by scanning the perpared
    ** statement for the corresponding OP_Variable opcode.  Once the host
    ** parameter index is known, locate the value in p->aVar[].  Then render
    ** the value as a literal in place of the host parameter name.
    */
    static string sqlite3VdbeExpandSql(
    Vdbe p,                  /* The prepared statement being evaluated */
    string zRawSql           /* Raw text of the SQL statement */
    )
    {
      sqlite3 db;              /* The database connection */
      int idx = 0;             /* Index of a host parameter */
      int nextIndex = 1;       /* Index of next ? host parameter */
      int n;                   /* Length of a token prefix */
      int nToken = 0;          /* Length of the parameter token */
      int i;                   /* Loop counter */
      Mem pVar;                /* Value of a host parameter */
      StrAccum _out = new StrAccum( 1000 );               /* Accumulate the _output here */
      int izRawSql = 0;

      db = p.db;
      sqlite3StrAccumInit( _out, null, 100,
            db.aLimit[SQLITE_LIMIT_LENGTH] );
      _out.db = db;
      if ( db.vdbeExecCnt > 1 )
      {
        while ( izRawSql < zRawSql.Length )
        {
          //string zStart = zRawSql;
          while ( zRawSql[izRawSql++] != '\n' && izRawSql < zRawSql.Length )
            ;
          sqlite3StrAccumAppend( _out, "-- ", 3 );
          sqlite3StrAccumAppend( _out, zRawSql, (int)izRawSql );//zRawSql - zStart );
        }
      }
      else
      {
        while ( izRawSql < zRawSql.Length )
        {
          n = findNextHostParameter( zRawSql, izRawSql, ref nToken );
          Debug.Assert( n > 0 );
          sqlite3StrAccumAppend( _out, zRawSql.Substring( izRawSql, n ), n );
          izRawSql += n;
          Debug.Assert( izRawSql < zRawSql.Length || nToken == 0 );
          if ( nToken == 0 )
            break;
          if ( zRawSql[izRawSql] == '?' )
          {
            if ( nToken > 1 )
            {
              Debug.Assert( sqlite3Isdigit( zRawSql[izRawSql + 1] ) );
              sqlite3GetInt32( zRawSql, izRawSql + 1, ref idx );
            }
            else
            {
              idx = nextIndex;
            }
          }
          else
          {
            Debug.Assert( zRawSql[izRawSql] == ':' || zRawSql[izRawSql] == '$' || zRawSql[izRawSql] == '@' );
            testcase( zRawSql[izRawSql] == ':' );
            testcase( zRawSql[izRawSql] == '$' );
            testcase( zRawSql[izRawSql] == '@' );
            idx = sqlite3VdbeParameterIndex( p, zRawSql.Substring( izRawSql, nToken ), nToken );
            Debug.Assert( idx > 0 );
          }
          izRawSql += nToken;
          nextIndex = idx + 1;
          Debug.Assert( idx > 0 && idx <= p.nVar );
          pVar = p.aVar[idx - 1];
          if ( ( pVar.flags & MEM_Null ) != 0 )
          {
            sqlite3StrAccumAppend( _out, "NULL", 4 );
          }
          else if ( ( pVar.flags & MEM_Int ) != 0 )
          {
            sqlite3XPrintf( _out, "%lld", pVar.u.i );
          }
          else if ( ( pVar.flags & MEM_Real ) != 0 )
          {
            sqlite3XPrintf( _out, "%!.15g", pVar.r );
          }
          else if ( ( pVar.flags & MEM_Str ) != 0 )
          {
#if !SQLITE_OMIT_UTF16
u8 enc = ENC(db);
if( enc!=SQLITE_UTF8 ){
Mem utf8;
memset(&utf8, 0, sizeof(utf8));
utf8.db = db;
sqlite3VdbeMemSetStr(&utf8, pVar.z, pVar.n, enc, SQLITE_STATIC);
sqlite3VdbeChangeEncoding(&utf8, SQLITE_UTF8);
sqlite3XPrintf(_out, "'%.*q'", utf8.n, utf8.z);
sqlite3VdbeMemRelease(&utf8);
}else
#endif
            {
              sqlite3XPrintf( _out, "'%.*q'", pVar.n, pVar.z );
            }
          }
          else if ( ( pVar.flags & MEM_Zero ) != 0 )
          {
            sqlite3XPrintf( _out, "zeroblob(%d)", pVar.u.nZero );
          }
          else
          {
            Debug.Assert( ( pVar.flags & MEM_Blob ) != 0 );
            sqlite3StrAccumAppend( _out, "x'", 2 );
            for ( i = 0; i < pVar.n; i++ )
            {
              sqlite3XPrintf( _out, "%02x", pVar.zBLOB[i] & 0xff );
            }
            sqlite3StrAccumAppend( _out, "'", 1 );
          }
        }
      }
      return sqlite3StrAccumFinish( _out );
    }

#endif //* #if !SQLITE_OMIT_TRACE */
  }
}
