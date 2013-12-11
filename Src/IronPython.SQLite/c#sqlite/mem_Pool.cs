using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using sqlite3_int64 = System.Int64;
using u32 = System.UInt32;
using System.Text;

namespace Community.CsharpSqlite
{
  public partial class Sqlite3
  {
    /*
    *************************************************************************
    **
    ** This file contains low-level memory & pool allocation drivers 
    **
    ** This file contains implementations of the low-level memory allocation
    ** routines specified in the sqlite3_mem_methods object.
    **
    *************************************************************************
    */

    /*
    ** Like malloc(), but remember the size of the allocation
    ** so that we can find it later using sqlite3MemSize().
    **
    ** For this low-level routine, we are guaranteed that nByte>0 because
    ** cases of nByte<=0 will be intercepted and dealt with by higher level
    ** routines.
    */
#if SQLITE_POOL_MEM
#if TRUE
    static byte[] sqlite3MemMalloc( u32 nByte )
    {
      return new byte[nByte];
    }
    static byte[] sqlite3MemMalloc( int nByte )
    {
      return new byte[nByte];
    }
    static int[] sqlite3MemMallocInt( int nInt )
    {
      return new int[nInt];
    }
#else
    static byte[] sqlite3MemMalloc(int nByte)
    {
      byte[] pByte = null;
      int savej = -1;
      int BestSize = int.MaxValue;
      if (nByte > mem0.aByteSize[0])
      {
        int i;
        for (i = 0; i < mem0.aByteSize.Length - 1; i++) if (nByte <= mem0.aByteSize[i]) break;
        mem0.mr_Byte++;
        for (int j = 0; j <= mem0.aByte_used[i]; j++)
          if (mem0.aByte[i][j] != null)
          {
            if (mem0.aByte[i][j].Length == nByte)
            {
              pByte = mem0.aByte[i][j]; mem0.aByte[i][j] = null; mem0.cf_Byte++;
              if (j == mem0.aByte_used[i]) mem0.aByte_used[i]--;
              break;
            }
            if (mem0.aByte[i][j].Length > nByte)
            {
              if (mem0.aByte[i][j].Length < BestSize)
              {
                BestSize = mem0.aByte[i][j].Length;
                savej = j;
              }
            }
          }
        if (pByte == null && savej >= 0)
        {
          pByte = mem0.aByte[i][savej]; mem0.aByte[i][savej] = null; mem0.cf_Byte++;
          Array.Resize(ref pByte, nByte);
        }
        if (mem0.aByte_used[i] >=0 && mem0.aByte[i][mem0.aByte_used[i]] == null) mem0.aByte_used[i]--;
      }
      if (pByte == null) pByte = new byte[nByte];
      return pByte;
    }
    static int[] sqlite3MemMallocInt(int nInt)
    {
      int[] pInt = null;
      int savei = -1;
      int BestSize = int.MaxValue;
      if (nInt >=10)
      {
        mem0.mr_Int++;
        int i;
        for (i = 0; i < mem0.hw_Int; i++)
          if (mem0.aInt[i] != null)
          {
            if (mem0.aInt[i].Length == nInt)
            {
              pInt = mem0.aInt[i]; mem0.aInt[i] = null; mem0.cf_Int++; break;
            }
            if (mem0.aInt[i].Length > nInt)
            {
              if (mem0.aInt[i].Length < BestSize)
              {
                BestSize = mem0.aInt[i].Length;
                savei = i;
              }
            }
          }
        if (pInt == null && savei >= 0)
        {
          pInt = mem0.aInt[savei]; mem0.aInt[savei] = null; mem0.cf_Int++;
        }
      }
      if (pInt == null) pInt = new int[nInt];
      return pInt;
    }
#endif

    static Mem sqlite3MemMallocMem( Mem dummy )
    {
      Mem pMem;
      mem0.msMem.alloc++;
      if ( mem0.msMem.next > 0 && mem0.aMem[mem0.msMem.next] != null )
      {
        pMem = mem0.aMem[mem0.msMem.next];
        mem0.aMem[mem0.msMem.next] = null;
        mem0.msMem.cached++;
        mem0.msMem.next--;
      }
      else
        pMem = new Mem();
      return pMem;
    }
    static BtCursor sqlite3MemMallocBtCursor( BtCursor dummy )
    {
      BtCursor pBtCursor;
      mem0.msBtCursor.alloc++;
      if ( mem0.msBtCursor.next > 0 && mem0.aBtCursor[mem0.msBtCursor.next] != null )
      {
        pBtCursor = mem0.aBtCursor[mem0.msBtCursor.next];
        Debug.Assert( pBtCursor.pNext == null && pBtCursor.pPrev == null && pBtCursor.wrFlag == 0 );
        mem0.aBtCursor[mem0.msBtCursor.next] = null;
        mem0.msBtCursor.cached++;
        mem0.msBtCursor.next--;
      }
      else
        pBtCursor = new BtCursor();
      return pBtCursor;
    }
#endif
    /*
    ** Free memory.
    */
    // -- overloads ---------------------------------------
    static void sqlite3MemFree<T>( ref T x ) where T : class
    {
      x = null;
    }
    static void sqlite3MemFree( ref string x )
    {
      x = null;
    }
    //

    /*
    ** Like free() but works for allocations obtained from sqlite3MemMalloc()
    ** or sqlite3MemRealloc().
    **
    ** For this low-level routine, we already know that pPrior!=0 since
    ** cases where pPrior==0 will have been intecepted and dealt with
    ** by higher-level routines.
    */
#if SQLITE_POOL_MEM
#if TRUE
    static void sqlite3MemFreeInt( ref int[] pPrior )
    {
      pPrior = null;
    }
#else
    static void sqlite3MemFree(ref byte[] pPrior)
    {
      if (pPrior == null) return;
      if (pPrior.Length > mem0.aByteSize[0])
      {
        int savej = -1;
        int Smallest = int.MaxValue;
        int i;
        for (i = 0; i < mem0.aByteSize.Length - 1; i++) if (pPrior.Length <= mem0.aByteSize[i]) break;
#if DEBUG
for (int j = 0; j < mem0.aByte[i].Length; j++) if (mem0.aByte[i][j] != null && mem0.aByte[i][j] == pPrior) Debugger.Break();
#endif
        mem0.mf_Byte++;
        for (int j = 0; j <= mem0.aByte_used[i]; j++)
        {
          if (mem0.aByte[i][j] == null)
          {
            mem0.aByte[i][j] = pPrior;
            pPrior = null;
            return;
          }
          if (mem0.aByte[i][j].Length < Smallest)
          {
            savej = j;
            Smallest = mem0.aByte[i][j].Length;
          }
        }

        if (mem0.aByte_used[i] < mem0.aByte[i].Length - 1) mem0.aByte[i][++mem0.aByte_used[i]] = pPrior;
        else if (savej >= 0) mem0.aByte[i][savej] = pPrior;
      }
      pPrior = null;
      return;
    }
    static void sqlite3MemFreeInt(ref int[] pPrior)
    {
      if (pPrior == null) return;
      if (pPrior.Length >= 10)
      {
        int savei = -1;
        int Smallest = int.MaxValue;
#if DEBUG
for (int i = 0; i < mem0.aInt.Length; i++) if (mem0.aInt[i] != null && mem0.aInt[i] == pPrior) Debugger.Break();
#endif
        mem0.mf_Int++;
        for (int i = 0; i < mem0.aInt.Length; i++)
        {
          if (mem0.aInt[i] == null)
          {
            mem0.aInt[i] = pPrior;
            pPrior = null;
            return;
          }
          if (mem0.aInt[i].Length < Smallest)
          {
            savei = i;
            Smallest = mem0.aInt[i].Length;
          }
        }
        if (savei >= 0) mem0.aInt[savei] = pPrior;
      }
      pPrior = null;
      return;
    }
#endif
    static void sqlite3MemFreeMem( ref Mem pPrior )
    {
      if ( pPrior == null )
        return;
#if FALSE && DEBUG
for (int i = mem0.msMem.next - 1; i >= 0; i--) if (mem0.aMem[i] != null && mem0.aMem[i] == pPrior) Debugger.Break();
#endif
      mem0.msMem.dealloc++;
      if ( mem0.msMem.next < mem0.aMem.Length - 1 )
      {
        pPrior.db = null;
        pPrior._SumCtx = null;
        pPrior._MD5Context = null;
        pPrior._SubProgram = null;
        pPrior.flags = MEM_Null;
        pPrior.r = 0;
        pPrior.u.i = 0;
        pPrior.n = 0;
        if ( pPrior.zBLOB != null )
          sqlite3MemFree( ref pPrior.zBLOB );
        mem0.aMem[++mem0.msMem.next] = pPrior;
        if ( mem0.msMem.next > mem0.msMem.max )
          mem0.msMem.max = mem0.msMem.next;

      }
      //Array.Resize( ref mem0.aMem, (int)(mem0.hw_Mem * 1.5 + 1 ));
      //mem0.aMem[mem0.hw_Mem] = pPrior;
      //mem0.hw_Mem = mem0.aMem.Length;
      pPrior = null;
      return;
    }
    static void sqlite3MemFreeBtCursor( ref BtCursor pPrior )
    {
      if ( pPrior == null )
        return;
#if FALSE && DEBUG
      for ( int i = mem0.msBtCursor.next - 1; i >= 0; i-- ) if ( mem0.aBtCursor[i] != null && mem0.aBtCursor[i] == pPrior ) Debugger.Break();
#endif
      mem0.msBtCursor.dealloc++;
      if ( mem0.msBtCursor.next < mem0.aBtCursor.Length - 1 )
      {
        mem0.aBtCursor[++mem0.msBtCursor.next] = pPrior;
        if ( mem0.msBtCursor.next > mem0.msBtCursor.max )
          mem0.msBtCursor.max = mem0.msBtCursor.next;
      }
      //Array.Resize( ref mem0.aBtCursor, (int)(mem0.hw_BtCursor * 1.5 + 1 ));
      //mem0.aBtCursor[mem0.hw_BtCursor] = pPrior;
      //mem0.hw_BtCursor = mem0.aBtCursor.Length;
      pPrior = null;
      return;
    }
    /*
    ** Like realloc().  Resize an allocation previously obtained from
    ** sqlite3MemMalloc().
    **
    ** For this low-level interface, we know that pPrior!=0.  Cases where
    ** pPrior==0 while have been intercepted by higher-level routine and
    ** redirected to xMalloc.  Similarly, we know that nByte>0 becauses
    ** cases where nByte<=0 will have been intercepted by higher-level
    ** routines and redirected to xFree.
    */
    static byte[] sqlite3MemRealloc( ref byte[] pPrior, int nByte )
    {
      //  sqlite3_int64 p = (sqlite3_int64*)pPrior;
      //  Debug.Assert(pPrior!=0 && nByte>0 );
      //  nByte = ROUND8( nByte );
      //  p = (sqlite3_int64*)pPrior;
      //  p--;
      //  p = realloc(p, nByte+8 );
      //  if( p ){
      //    p[0] = nByte;
      //    p++;
      //  }
      //  return (void*)p;
      Array.Resize( ref pPrior, nByte );
      return pPrior;
    }
#else
    /*
** No-op versions of all memory allocation routines
*/
static byte[] sqlite3MemMalloc(int nByte) { return new byte[nByte]; }
static int[] sqlite3MemMallocInt(int nInt) { return new int[nInt]; }
static Mem sqlite3MemMallocMem( Mem pMem) { return new Mem(); }
static void sqlite3MemFree( ref byte[] pPrior ) { pPrior = null; }
static void sqlite3MemFreeInt( ref int[] pPrior ) { pPrior = null; }
static void sqlite3MemFreeMem(ref Mem pPrior) { pPrior = null; }
static int sqlite3MemInit() { return SQLITE_OK; }
static void sqlite3MemShutdown() { }
static BtCursor sqlite3MemMallocBtCursor( BtCursor dummy ){return new BtCursor();}
static void sqlite3MemFreeBtCursor(ref BtCursor pPrior) { pPrior = null; }
#endif

    static byte[] sqlite3MemRealloc( byte[] pPrior, int nByte )
    {
      Array.Resize( ref pPrior, nByte );
      return pPrior;
    }

    /*
    ** Report the allocated size of a prior return from xMalloc()
    ** or xRealloc().
    */
    static int sqlite3MemSize( byte[] pPrior )
    {
      //  sqlite3_int64 p;
      //  if( pPrior==0 ) return 0;
      //  p = (sqlite3_int64*)pPrior;
      //  p--;
      //  return p[0];
      return pPrior == null ? 0 : (int)pPrior.Length;
    }

    /*
    ** Round up a request size to the next valid allocation size.
    */
    static int sqlite3MemRoundup( int n )
    {
      return n;//      ROUND8( n );
    }

    /*
    ** Initialize this module.
    */
    static int sqlite3MemInit( object NotUsed )
    {
      UNUSED_PARAMETER( NotUsed );
      if ( !sqlite3GlobalConfig.bMemstat )
      {
        /* If memory status is enabled, then the malloc.c wrapper will already
        ** hold the STATIC_MEM mutex when the routines here are invoked. */
        mem0.mutex = sqlite3MutexAlloc( SQLITE_MUTEX_STATIC_MEM );
      }
      return SQLITE_OK;
    }

    /*
    ** Deinitialize this module.
    */
    static void sqlite3MemShutdown( object NotUsed )
    {

      UNUSED_PARAMETER( NotUsed );
      return;
    }

    /*
    ** This routine is the only routine in this file with external linkage.
    **
    ** Populate the low-level memory allocation function pointers in
    ** sqlite3GlobalConfig.m with pointers to the routines in this file.
    */
    static void sqlite3MemSetDefault()
    {
      sqlite3_mem_methods defaultMethods = new sqlite3_mem_methods(
      sqlite3MemMalloc,
      sqlite3MemMallocInt,
      sqlite3MemMallocMem,
      sqlite3MemFree,
      sqlite3MemFreeInt,
      sqlite3MemFreeMem,
      sqlite3MemRealloc,
      sqlite3MemSize,
      sqlite3MemRoundup,
      (dxMemInit)sqlite3MemInit,
      (dxMemShutdown)sqlite3MemShutdown,
      0
      );
      sqlite3_config( SQLITE_CONFIG_MALLOC, defaultMethods );
    }

    static void sqlite3DbFree( sqlite3 db, ref int[] pPrior )
    {
      if ( pPrior != null )
        sqlite3MemFreeInt( ref pPrior );
    }
    static void sqlite3DbFree( sqlite3 db, ref Mem pPrior )
    {
      if ( pPrior != null )
        sqlite3MemFreeMem( ref pPrior );
    }
    static void sqlite3DbFree( sqlite3 db, ref Mem[] pPrior )
    {
      if ( pPrior != null )
        for ( int i = 0; i < pPrior.Length; i++ )
          sqlite3MemFreeMem( ref pPrior[i] );
    }
    static void sqlite3DbFree<T>( sqlite3 db, ref T pT ) where T : class
    {
    }
    static void sqlite3DbFree( sqlite3 db, ref string pString )
    {
    }
  }
}
