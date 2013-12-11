/*
*************************************************************************
**  Custom classes used by C#
*************************************************************************
*/
using System;
using System.Diagnostics;
using System.IO;
#if !(SQLITE_SILVERLIGHT || WINDOWS_MOBILE || SQLITE_WINRT)
using System.Management;
#endif
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
#if SQLITE_WINRT
using System.Reflection;
#endif

using i64 = System.Int64;

using u32 = System.UInt32;
using time_t = System.Int64;

namespace Community.CsharpSqlite
{
  using sqlite3_value = Sqlite3.Mem;

  public partial class Sqlite3
  {

static int atoi( byte[] inStr )
{
  return atoi( Encoding.UTF8.GetString( inStr, 0, inStr.Length ) );
}

static int atoi( string inStr )
{
  int i;
  for ( i = 0; i < inStr.Length; i++ )
  {
    if ( !sqlite3Isdigit( inStr[i] ) && inStr[i] != '-' )
      break;
  }
  int result = 0;
#if WINDOWS_MOBILE 
  try { result = Int32.Parse(inStr.Substring(0, i)); }
  catch { }
  return result;
#else
  return ( Int32.TryParse( inStr.Substring( 0, i ), out result ) ? result : 0 );
#endif
}

static void fprintf( TextWriter tw, string zFormat, params object[] ap )
{
  tw.Write( sqlite3_mprintf( zFormat, ap ) );
}
static void printf( string zFormat, params object[] ap )
{
#if !SQLITE_WINRT
  Console.Out.Write( sqlite3_mprintf( zFormat, ap ) );
#endif
}


//Byte Buffer Testing
static int memcmp( byte[] bA, byte[] bB, int Limit )
{
  if ( bA.Length < Limit )
    return ( bA.Length < bB.Length ) ? -1 : +1;
  if ( bB.Length < Limit )
    return +1;
  for ( int i = 0; i < Limit; i++ )
  {
    if ( bA[i] != bB[i] )
      return ( bA[i] < bB[i] ) ? -1 : 1;
  }
  return 0;
}

//Byte Buffer  & String Testing
static int memcmp( string A, byte[] bB, int Limit )
{
  if ( A.Length < Limit )
    return ( A.Length < bB.Length ) ? -1 : +1;
  if ( bB.Length < Limit )
    return +1;
  char[] cA = A.ToCharArray();
  for ( int i = 0; i < Limit; i++ )
  {
    if ( cA[i] != bB[i] )
      return ( cA[i] < bB[i] ) ? -1 : 1;
  }
  return 0;
}

//byte with Offset & String Testing
static int memcmp( byte[] a, int Offset, byte[] b, int Limit )
{
  if ( a.Length < Offset + Limit )
    return ( a.Length - Offset < b.Length ) ? -1 : +1;
  if ( b.Length < Limit )
    return +1;
  for ( int i = 0; i < Limit; i++ )
  {
    if ( a[i + Offset] != b[i] )
      return ( a[i + Offset] < b[i] ) ? -1 : 1;
  }
  return 0;
}

//byte with Offset & String Testing
static int memcmp( byte[] a, int Aoffset, byte[] b, int Boffset, int Limit )
{
  if ( a.Length < Aoffset + Limit )
    return ( a.Length - Aoffset < b.Length - Boffset ) ? -1 : +1;
  if ( b.Length < Boffset + Limit )
    return +1;
  for ( int i = 0; i < Limit; i++ )
  {
    if ( a[i + Aoffset] != b[i + Boffset] )
      return ( a[i + Aoffset] < b[i + Boffset] ) ? -1 : 1;
  }
  return 0;
}

static int memcmp( byte[] a, int Offset, string b, int Limit )
{
  if ( a.Length < Offset + Limit )
    return ( a.Length - Offset < b.Length ) ? -1 : +1;
  if ( b.Length < Limit )
    return +1;
  for ( int i = 0; i < Limit; i++ )
  {
    if ( a[i + Offset] != b[i] )
      return ( a[i + Offset] < b[i] ) ? -1 : 1;
  }
  return 0;
}
//String Testing
static int memcmp( string A, string B, int Limit )
{
  if ( A.Length < Limit )
    return ( A.Length < B.Length ) ? -1 : +1;
  if ( B.Length < Limit )
    return +1;
  int rc;
  if ( ( rc = String.Compare( A, 0, B, 0, Limit, StringComparison.Ordinal ) ) == 0 )
    return 0;
  return rc < 0 ? -1 : +1;
}


// ----------------------------
// ** Builtin Functions
// ----------------------------


static Regex oRegex = null;
/*
** The regexp() function.  two arguments are both strings
** Collating sequences are not used.
*/
static void regexpFunc(
sqlite3_context context,
int argc,
sqlite3_value[] argv
)
{
  string zTest;       /* The input string A */
  string zRegex;      /* The regex string B */

  Debug.Assert( argc == 2 );
  UNUSED_PARAMETER( argc );
  zRegex = sqlite3_value_text( argv[0] );
  zTest = sqlite3_value_text( argv[1] );

  if ( zTest == null || String.IsNullOrEmpty( zRegex ) )
  {
    sqlite3_result_int( context, 0 );
    return;
  }

  if ( oRegex == null || oRegex.ToString() == zRegex )
  {
    oRegex = new Regex( zRegex, RegexOptions.IgnoreCase );
  }
  sqlite3_result_int( context, oRegex.IsMatch( zTest ) ? 1 : 0 );
}


// ----------------------------
// ** Convertion routines
// ----------------------------
static Object lock_va_list = new Object();

static string vaFORMAT;
static int vaNEXT;

static void va_start( object[] ap, string zFormat )
{
  vaFORMAT = zFormat;
  vaNEXT = 0;
}

static Boolean va_arg( object[] ap, Boolean sysType )
{
  return Convert.ToBoolean( ap[vaNEXT++] );
}

static Byte[] va_arg( object[] ap, Byte[] sysType )
{
  return (Byte[])ap[vaNEXT++];
}

static Byte[][] va_arg( object[] ap, Byte[][] sysType )
{
  if ( ap[vaNEXT] == null )
  {
    {
      vaNEXT++;
      return null;
    }
  }
  else
  {
    return (Byte[][])ap[vaNEXT++];
  }
}

static Char va_arg( object[] ap, Char sysType )
{
  if ( ap[vaNEXT] is Int32 && (int)ap[vaNEXT] == 0 )
  {
    vaNEXT++;
    return (char)'0';
  }
  else
  {
    if ( ap[vaNEXT] is Int64 )
      if ( (i64)ap[vaNEXT] == 0 )
      {
        vaNEXT++;
        return (char)'0';
      }
      else
        return (char)( (i64)ap[vaNEXT++] );
    else
      return (char)ap[vaNEXT++];
  }

}

static Double va_arg( object[] ap, Double sysType )
{
  return Convert.ToDouble( ap[vaNEXT++] );
}

static dxLog va_arg( object[] ap, dxLog sysType )
{
  return (dxLog)ap[vaNEXT++];
}

static Int64 va_arg( object[] ap, Int64 sysType )
{
  if ( ap[vaNEXT] is System.Int64)
    return Convert.ToInt64( ap[vaNEXT++] );
  else
    return (Int64)( ap[vaNEXT++].GetHashCode() );
}

static Int32 va_arg( object[] ap, Int32 sysType )
{
  if ( Convert.ToInt64( ap[vaNEXT] ) > 0 && ( Convert.ToUInt32( ap[vaNEXT] ) > Int32.MaxValue ) )
    return (Int32)( Convert.ToUInt32( ap[vaNEXT++] ) - System.UInt32.MaxValue - 1 );
  else
    return (Int32)Convert.ToInt32( ap[vaNEXT++] );
}

static Int32[] va_arg( object[] ap, Int32[] sysType )
{
  if ( ap[vaNEXT] == null )
  {
    {
      vaNEXT++;
      return null;
    }
  }
  else
  {
    return (Int32[])ap[vaNEXT++];
  }
}

static MemPage va_arg( object[] ap, MemPage sysType )
{
  return (MemPage)ap[vaNEXT++];
}

static Object va_arg( object[] ap, Object sysType )
{
  return (Object)ap[vaNEXT++];
}

static sqlite3 va_arg( object[] ap, sqlite3 sysType )
{
  return (sqlite3)ap[vaNEXT++];
}

static sqlite3_mem_methods va_arg( object[] ap, sqlite3_mem_methods sysType )
{
  return (sqlite3_mem_methods)ap[vaNEXT++];
}

static sqlite3_mutex_methods va_arg( object[] ap, sqlite3_mutex_methods sysType )
{
  return (sqlite3_mutex_methods)ap[vaNEXT++];
}

static SrcList va_arg( object[] ap, SrcList sysType )
{
  return (SrcList)ap[vaNEXT++];
}

static String va_arg( object[] ap, String sysType )
{
  if ( ap.Length < vaNEXT - 1 || ap[vaNEXT] == null )
  {
    vaNEXT++;
    return "NULL";
  }
  else
  {
    if ( ap[vaNEXT] is Byte[] )
      if ( Encoding.UTF8.GetString( (byte[])ap[vaNEXT], 0, ( (byte[])ap[vaNEXT] ).Length ) == "\0" )
      {
        vaNEXT++;
        return "";
      }
      else
        return Encoding.UTF8.GetString( (byte[])ap[vaNEXT], 0, ( (byte[])ap[vaNEXT++] ).Length );
    else if ( ap[vaNEXT] is Int32 )
    {
      vaNEXT++;
      return null;
    }
    else if ( ap[vaNEXT] is StringBuilder )
      return (String)ap[vaNEXT++].ToString();
    else if ( ap[vaNEXT] is Char )
      return ( (Char)ap[vaNEXT++] ).ToString();
    else
      return (String)ap[vaNEXT++];
  }
}

static Token va_arg( object[] ap, Token sysType )
{
  return (Token)ap[vaNEXT++];
}

static UInt32 va_arg( object[] ap, UInt32 sysType )
{
#if SQLITE_WINRT
  Type t = ap[vaNEXT].GetType();
  if ( t.GetTypeInfo().IsClass )
#else
  if ( ap[vaNEXT].GetType().IsClass )
#endif
  {
    return (UInt32)ap[vaNEXT++].GetHashCode();
  }
  else
  {
    return (UInt32)Convert.ToUInt32( ap[vaNEXT++] );
  }
}

static UInt64 va_arg( object[] ap, UInt64 sysType )
{
#if SQLITE_WINRT
  Type t = ap[vaNEXT].GetType();
  if (t.GetTypeInfo().IsClass)
#else
  if ( ap[vaNEXT].GetType().IsClass )
#endif
  {
    return (UInt64)ap[vaNEXT++].GetHashCode();
  }
  else
  {
    return (UInt64)Convert.ToUInt64( ap[vaNEXT++] );
  }
}

static void_function va_arg( object[] ap, void_function sysType )
{
  return (void_function)ap[vaNEXT++];
}


static void va_end( ref string[] ap )
{
  ap = null;
  vaNEXT = -1;
  vaFORMAT = "";
}
static void va_end( ref object[] ap )
{
  ap = null;
  vaNEXT = -1;
  vaFORMAT = "";
}


public static tm localtime( time_t baseTime )
{
  System.DateTime RefTime = new System.DateTime( 1970, 1, 1, 0, 0, 0, 0 );
  RefTime = RefTime.AddSeconds( Convert.ToDouble( baseTime ) ).ToLocalTime();
  tm tm = new tm();
  tm.tm_sec = RefTime.Second;
  tm.tm_min = RefTime.Minute;
  tm.tm_hour = RefTime.Hour;
  tm.tm_mday = RefTime.Day;
  tm.tm_mon = RefTime.Month;
  tm.tm_year = RefTime.Year;
  tm.tm_wday = (int)RefTime.DayOfWeek;
  tm.tm_yday = RefTime.DayOfYear;
  tm.tm_isdst = RefTime.IsDaylightSavingTime() ? 1 : 0;
  return tm;
}

public static long ToUnixtime( System.DateTime date )
{
  System.DateTime unixStartTime = new System.DateTime( 1970, 1, 1, 0, 0, 0, 0 );
  System.TimeSpan timeSpan = date - unixStartTime;
  return Convert.ToInt64( timeSpan.TotalSeconds );
}

public static System.DateTime ToCSharpTime( long unixTime )
{
  System.DateTime unixStartTime = new System.DateTime( 1970, 1, 1, 0, 0, 0, 0 );
  return unixStartTime.AddSeconds( Convert.ToDouble( unixTime ) );
}

public class tm
{
  public int tm_sec;     /* seconds after the minute - [0,59] */
  public int tm_min;     /* minutes after the hour - [0,59] */
  public int tm_hour;    /* hours since midnight - [0,23] */
  public int tm_mday;    /* day of the month - [1,31] */
  public int tm_mon;     /* months since January - [0,11] */
  public int tm_year;    /* years since 1900 */
  public int tm_wday;    /* days since Sunday - [0,6] */
  public int tm_yday;    /* days since January 1 - [0,365] */
  public int tm_isdst;   /* daylight savings time flag */
};

public struct FILETIME
{
  public u32 dwLowDateTime;
  public u32 dwHighDateTime;
}

// Example (C#)
public static int GetbytesPerSector( StringBuilder diskPath )
{
#if !(SQLITE_SILVERLIGHT || WINDOWS_MOBILE || SQLITE_WINRT)
  ManagementObjectSearcher mosLogicalDisks = new ManagementObjectSearcher( "select * from Win32_LogicalDisk where DeviceID = '" + diskPath.ToString().Remove( diskPath.Length - 1, 1 ) + "'" );
  try
  {
    foreach ( ManagementObject moLogDisk in mosLogicalDisks.Get() )
    {
      ManagementObjectSearcher mosDiskDrives = new ManagementObjectSearcher( "select * from Win32_DiskDrive where SystemName = '" + moLogDisk["SystemName"] + "'" );
      foreach ( ManagementObject moPDisk in mosDiskDrives.Get() )
      {
        return int.Parse( moPDisk["BytesPerSector"].ToString() );
      }
    }
  }
  catch
  {
  }
  return 4096;
#else
    return 4096;
#endif
}

static void SWAP<T>( ref T A, ref T B )
{
  T t = A;
  A = B;
  B = t;
}

static void x_CountStep(
sqlite3_context context,
int argc,
sqlite3_value[] argv
)
{
  SumCtx p;

  int type;
  Debug.Assert( argc <= 1 );
  Mem pMem = sqlite3_aggregate_context( context, 1 );//sizeof(*p));
  if ( pMem._SumCtx == null )
    pMem._SumCtx = new SumCtx();
  p = pMem._SumCtx;
  if ( p.Context == null )
    p.Context = pMem;
  if ( argc == 0 || SQLITE_NULL == sqlite3_value_type( argv[0] ) )
  {
    p.cnt++;
    p.iSum += 1;
  }
  else
  {
    type = sqlite3_value_numeric_type( argv[0] );
    if ( p != null && type != SQLITE_NULL )
    {
      p.cnt++;
      if ( type == SQLITE_INTEGER )
      {
        i64 v = sqlite3_value_int64( argv[0] );
        if ( v == 40 || v == 41 )
        {
          sqlite3_result_error( context, "value of " + v + " handed to x_count", -1 );
          return;
        }
        else
        {
          p.iSum += v;
          if ( !( p.approx | p.overflow != 0 ) )
          {
            i64 iNewSum = p.iSum + v;
            int s1 = (int)( p.iSum >> ( sizeof( i64 ) * 8 - 1 ) );
            int s2 = (int)( v >> ( sizeof( i64 ) * 8 - 1 ) );
            int s3 = (int)( iNewSum >> ( sizeof( i64 ) * 8 - 1 ) );
            p.overflow = ( ( s1 & s2 & ~s3 ) | ( ~s1 & ~s2 & s3 ) ) != 0 ? 1 : 0;
            p.iSum = iNewSum;
          }
        }
      }
      else
      {
        p.rSum += sqlite3_value_double( argv[0] );
        p.approx = true;
      }
    }
  }
}
static void x_CountFinalize( sqlite3_context context )
{
  SumCtx p;
  Mem pMem = sqlite3_aggregate_context( context, 0 );
  p = pMem._SumCtx;
  if ( p != null && p.cnt > 0 )
  {
    if ( p.overflow != 0 )
    {
      sqlite3_result_error( context, "integer overflow", -1 );
    }
    else if ( p.approx )
    {
      sqlite3_result_double( context, p.rSum );
    }
    else if ( p.iSum == 42 )
    {
      sqlite3_result_error( context, "x_count totals to 42", -1 );
    }
    else
    {
      sqlite3_result_int64( context, p.iSum );
    }
  }
}

#if SQLITE_MUTEX_W32
//---------------------WIN32 Definitions
static int GetCurrentThreadId()
{
  return Thread.CurrentThread.ManagedThreadId;
}
static long InterlockedIncrement( long location )
{
  Interlocked.Increment( ref location );
  return location;
}
static void EnterCriticalSection( Object mtx )
{
  //long mid = mtx.GetHashCode();
  //int tid = Thread.CurrentThread.ManagedThreadId;
  //long ticks = cnt++;
  //Debug.WriteLine(String.Format( "{2}: +EnterCriticalSection; Mutex {0} Thread {1}", mtx.GetHashCode(), Thread.CurrentThread.ManagedThreadId, ticks) );
  Monitor.Enter( mtx );
}
static void InitializeCriticalSection( Object mtx )
{
  //Debug.WriteLine(String.Format( "{2}: +InitializeCriticalSection; Mutex {0} Thread {1}", mtx.GetHashCode(), Thread.CurrentThread.ManagedThreadId, System.DateTime.Now.Ticks ));
}
static void DeleteCriticalSection( Object mtx )
{
  //Debug.WriteLine(String.Format( "{2}: +DeleteCriticalSection; Mutex {0} Thread {1}", mtx.GetHashCode(), Thread.CurrentThread.ManagedThreadId, System.DateTime.Now.Ticks) );
}
static void LeaveCriticalSection( Object mtx )
{
  //Debug.WriteLine(String.Format("{2}: +LeaveCriticalSection; Mutex {0} Thread {1}", mtx.GetHashCode(), Thread.CurrentThread.ManagedThreadId, System.DateTime.Now.Ticks ));
  Monitor.Exit( mtx );
}
#endif

// Miscellaneous Windows Constants
//#define ERROR_FILE_NOT_FOUND             2L
//#define ERROR_HANDLE_DISK_FULL           39L
//#define ERROR_NOT_SUPPORTED              50L
//#define ERROR_DISK_FULL                  112L
const long ERROR_FILE_NOT_FOUND = 2L;
const long ERROR_HANDLE_DISK_FULL = 39L;
const long ERROR_NOT_SUPPORTED = 50L;
const long ERROR_DISK_FULL = 112L;

private class SQLite3UpperToLower
{
  static int[] sqlite3UpperToLower = new int[]  {
#if SQLITE_ASCII
0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17,
18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35,
36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 97, 98, 99,100,101,102,103,
104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,
122, 91, 92, 93, 94, 95, 96, 97, 98, 99,100,101,102,103,104,105,106,107,
108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125,
126,127,128,129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,
144,145,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,161,
162,163,164,165,166,167,168,169,170,171,172,173,174,175,176,177,178,179,
180,181,182,183,184,185,186,187,188,189,190,191,192,193,194,195,196,197,
198,199,200,201,202,203,204,205,206,207,208,209,210,211,212,213,214,215,
216,217,218,219,220,221,222,223,224,225,226,227,228,229,230,231,232,233,
234,235,236,237,238,239,240,241,242,243,244,245,246,247,248,249,250,251,
252,253,254,255
#endif
};
  public int this[int index]
  {
    get
    {
      if ( index < sqlite3UpperToLower.Length )
        return sqlite3UpperToLower[index];
      else
        return index;
    }
  }

  public int this[u32 index]
  {
    get
    {
      if ( index < sqlite3UpperToLower.Length )
        return sqlite3UpperToLower[index];
      else
        return (int)index;
    }
  }
}

static SQLite3UpperToLower sqlite3UpperToLower = new SQLite3UpperToLower();
static SQLite3UpperToLower UpperToLower = sqlite3UpperToLower;

  }
}
