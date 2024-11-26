using System;
using System.Diagnostics;
using System.Text;

using u8 = System.Byte;
using u16 = System.UInt16;
using Pgno = System.UInt32;

namespace Community.CsharpSqlite
{
  using sqlite3_int64 = System.Int64;
  using sqlite3_stmt = Sqlite3.Vdbe;
#if !SQLITE_WINRT 
  using System.Security.Cryptography;
  using System.IO;
#endif

  public partial class Sqlite3
  {
    /*
    *************************************************************************
    **  Included in SQLite3 port to C#-SQLite;  2010 Noah B Hart, Diego Torres
    **  C#-SQLite is an independent reimplementation of the SQLite software library
    **
    *************************************************************************
    */

    /*
    ** SQLCipher
    ** crypto.c developed by Stephen Lombardo (Zetetic LLC)
    ** sjlombardo at zetetic dot net
    ** http://zetetic.net
    **
    ** Copyright (c) 2009, ZETETIC LLC
    ** All rights reserved.
    **
    ** Redistribution and use in source and binary forms, with or without
    ** modification, are permitted provided that the following conditions are met:
    ** * Redistributions of source code must retain the above copyright
    ** notice, this list of conditions and the following disclaimer.
    ** * Redistributions in binary form must reproduce the above copyright
    ** notice, this list of conditions and the following disclaimer in the
    ** documentation and/or other materials provided with the distribution.
    ** * Neither the name of the ZETETIC LLC nor the
    ** names of its contributors may be used to endorse or promote products
    ** derived from this software without specific prior written permission.
    **
    ** THIS SOFTWARE IS PROVIDED BY ZETETIC LLC ''AS IS'' AND ANY
    ** EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
    ** WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
    ** DISCLAIMED. IN NO EVENT SHALL ZETETIC LLC BE LIABLE FOR ANY
    ** DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
    ** (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
    ** LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
    ** ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
    ** (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
    ** SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
    **
    */
    /* BEGIN CRYPTO */
#if SQLITE_HAS_CODEC

    //#include <assert.h>
    //#include <openssl/evp.h>
    //#include <openssl/rand.h>
    //#include <openssl/hmac.h>
    //#include "sqliteInt.h"
    //#include "btreeInt.h"
    //#include "crypto.h"

#if CODEC_DEBUG || TRACE
//#define CODEC_TRACE(X) {printf X;fflush(stdout);}
static void CODEC_TRACE( string T, params object[] ap ) { if ( sqlite3PagerTrace )sqlite3DebugPrintf( T, ap ); }
#else
    //#define CODEC_TRACE(X)
    static void CODEC_TRACE( string T, params object[] ap )
    {
    }
#endif

    //void sqlite3FreeCodecArg(void *pCodecArg);

    public class cipher_ctx
    {//typedef struct {
      public string pass;
      public int pass_sz;
      public bool derive_key;
      public byte[] key;
      public int key_sz;
      public byte[] iv;
      public int iv_sz;
      public ICryptoTransform encryptor;
      public ICryptoTransform decryptor;

      public cipher_ctx Copy()
      {
        cipher_ctx c = new cipher_ctx();
        c.derive_key = derive_key;
        c.pass = pass;
        c.pass_sz = pass_sz;
        if ( key != null )
        {
          c.key = new byte[key.Length];
          key.CopyTo( c.key, 0 );
        }
        c.key_sz = key_sz;
        if ( iv != null )
        {
          c.iv = new byte[iv.Length];
          iv.CopyTo( c.iv, 0 );
        }
        c.iv_sz = iv_sz;
        c.encryptor = encryptor;
        c.decryptor = decryptor;
        return c;
      }

      public void CopyTo( cipher_ctx ct )
      {
        ct.derive_key = derive_key;
        ct.pass = pass;
        ct.pass_sz = pass_sz;
        if ( key != null )
        {
          ct.key = new byte[key.Length];
          key.CopyTo( ct.key, 0 );
        }
        ct.key_sz = key_sz;
        if ( iv != null )
        {
          ct.iv = new byte[iv.Length];
          iv.CopyTo( ct.iv, 0 );
        }
        ct.iv_sz = iv_sz;
        ct.encryptor = encryptor;
        ct.decryptor = decryptor;
      }
    }

    public class codec_ctx
    {//typedef struct {
      public int mode_rekey;
      public byte[] buffer;
      public Btree pBt;
      public cipher_ctx read_ctx;
      public cipher_ctx write_ctx;

      public codec_ctx Copy()
      {
        codec_ctx c = new codec_ctx();
        c.mode_rekey = mode_rekey;
        c.buffer = sqlite3MemMalloc( buffer.Length );
        c.pBt = pBt;
        if ( read_ctx != null )
          c.read_ctx = read_ctx.Copy();
        if ( write_ctx != null )
          c.write_ctx = write_ctx.Copy();
        return c;
      }
    }

    const int FILE_HEADER_SZ = 16;      //#define FILE_HEADER_SZ 16
    const string CIPHER = "aes-256-cbc";  //#define CIPHER "aes-256-cbc"
    const int CIPHER_DECRYPT = 0;        //#define CIPHER_DECRYPT 0
    const int CIPHER_ENCRYPT = 1;        //#define CIPHER_ENCRYPT 1

#if NET_2_0
    static RijndaelManaged Aes = new RijndaelManaged();
#else
    static AesManaged Aes = new AesManaged();
#endif

    /* BEGIN CRYPTO */
    static void sqlite3pager_get_codec( Pager pPager, ref codec_ctx ctx )
    {
      ctx = pPager.pCodec;
    }

    static int sqlite3pager_is_mj_pgno( Pager pPager, Pgno pgno )
    {
      return ( PAGER_MJ_PGNO( pPager ) == pgno ) ? 1 : 0;
    }

    static sqlite3_file sqlite3Pager_get_fd( Pager pPager )
    {
      return ( isOpen( pPager.fd ) ) ? pPager.fd : null;
    }

    static void sqlite3pager_sqlite3PagerSetCodec(
    Pager pPager,
    dxCodec xCodec,
    dxCodecSizeChng xCodecSizeChng,
    dxCodecFree xCodecFree,
    codec_ctx pCodec
    )
    {
      sqlite3PagerSetCodec( pPager, xCodec, xCodecSizeChng, xCodecFree, pCodec );
    }
    /* END CRYPTO */

    //static void activate_openssl() {
    //  if(EVP_get_cipherbyname(CIPHER) == null) {
    //    OpenSSL_add_all_algorithms();
    //  }
    //}

    /**
    * Free and wipe memory
    * If ptr is not null memory will be freed.
    * If sz is greater than zero, the memory will be overwritten with zero before it is freed
    */
    static void codec_free( ref byte[] ptr, int sz )
    {
      if ( ptr != null )
      {
        if ( sz > 0 )
          Array.Clear( ptr, 0, sz );//memset( ptr, 0, sz ); 
        sqlite3_free( ref ptr );
      }
    }

    /**
    * Set the raw password / key data for a cipher context
    *
    * returns SQLITE_OK if assignment was successfull
    * returns SQLITE_NOMEM if an error occured allocating memory
    * returns SQLITE_ERROR if the key couldn't be set because the pass was null or size was zero
    */
    static int cipher_ctx_set_pass( cipher_ctx ctx, string zKey, int nKey )
    {
      ctx.pass = null; // codec_free( ctx.pass, ctx.pass_sz );
      ctx.pass_sz = nKey;
      if ( !String.IsNullOrEmpty( zKey ) && nKey > 0 )
      {
        //ctx.pass = sqlite3Malloc(nKey);
        //if(ctx.pass == null) return SQLITE_NOMEM;
        ctx.pass = zKey;//memcpy(ctx.pass, zKey, nKey);
        return SQLITE_OK;
      }
      return SQLITE_ERROR;
    }

    /**
    * Initialize a new cipher_ctx struct. This function will allocate memory
    * for the cipher context and for the key
    *
    * returns SQLITE_OK if initialization was successful
    * returns SQLITE_NOMEM if an error occured allocating memory
    */
    static int cipher_ctx_init( ref cipher_ctx iCtx )
    {
      iCtx = new cipher_ctx();
      //iCtx = sqlite3Malloc( sizeof( cipher_ctx ) );
      //ctx = *iCtx;
      //if ( ctx == null ) return SQLITE_NOMEM;
      //memset( ctx, 0, sizeof( cipher_ctx ) );
      //ctx.key =  sqlite3Malloc( EVP_MAX_KEY_LENGTH );
      //if ( ctx.key == null ) return SQLITE_NOMEM;
      return SQLITE_OK;
    }

    /**
    * free and wipe memory associated with a cipher_ctx
    */
    static void cipher_ctx_free( ref cipher_ctx ictx )
    {
      cipher_ctx ctx = ictx;
      CODEC_TRACE( "cipher_ctx_free: entered ictx=%d\n", ictx );
      ctx.pass = null;//codec_free(ctx.pass, ctx.pass_sz);
      if ( ctx.key != null )
        Array.Clear( ctx.key, 0, ctx.key.Length );//codec_free(ctx.key, ctx.key_sz);
      if ( ctx.iv != null )
        Array.Clear( ctx.iv, 0, ctx.iv.Length );
      ictx = new cipher_ctx();// codec_free( ref ctx, sizeof( cipher_ctx ) );
    }

    /**
    * Copy one cipher_ctx to another. For instance, assuming that read_ctx is a
    * fully initialized context, you could copy it to write_ctx and all yet data
    * and pass information across
    *
    * returns SQLITE_OK if initialization was successful
    * returns SQLITE_NOMEM if an error occured allocating memory
    */
    static int cipher_ctx_copy( cipher_ctx target, cipher_ctx source )
    {
      //byte[] key = target.key;
      CODEC_TRACE( "cipher_ctx_copy: entered target=%d, source=%d\n", target, source );
      //codec_free(target.pass, target.pass_sz);
      source.CopyTo( target );//memcpy(target, source, sizeof(cipher_ctx);

      //target.key = key; //restore pointer to previously allocated key data
      //memcpy(target.key, source.key, EVP_MAX_KEY_LENGTH);
      //target.pass = sqlite3Malloc(source.pass_sz);
      //if(target.pass == null) return SQLITE_NOMEM;
      //memcpy(target.pass, source.pass, source.pass_sz);
      return SQLITE_OK;
    }

    /**
    * Compare one cipher_ctx to another.
    *
    * returns 0 if all the parameters (except the derived key data) are the same
    * returns 1 otherwise
    */
    static int cipher_ctx_cmp( cipher_ctx c1, cipher_ctx c2 )
    {
      CODEC_TRACE( "cipher_ctx_cmp: entered c1=%d c2=%d\n", c1, c2 );

      if ( c1.key_sz == c2.key_sz
      && c1.pass_sz == c2.pass_sz
      && c1.pass == c2.pass
      )
        return 0;
      return 1;
    }

    /**
    * Free and wipe memory associated with a cipher_ctx, including the allocated
    * read_ctx and write_ctx.
    */
    static void codec_ctx_free( ref codec_ctx iCtx )
    {
      codec_ctx ctx = iCtx;
      CODEC_TRACE( "codec_ctx_free: entered iCtx=%d\n", iCtx );
      cipher_ctx_free( ref ctx.read_ctx );
      cipher_ctx_free( ref ctx.write_ctx );
      iCtx = new codec_ctx();//codec_free(ctx, sizeof(codec_ctx);
    }

    /**
    * Derive an encryption key for a cipher contex key based on the raw password.
    *
    * If the raw key data is formated as x'hex' and there are exactly enough hex chars to fill
    * the key space (i.e 64 hex chars for a 256 bit key) then the key data will be used directly.
    *
    * Otherwise, a key data will be derived using PBKDF2
    *
    * returns SQLITE_OK if initialization was successful
    * returns SQLITE_NOMEM if the key could't be derived (for instance if pass is null or pass_sz is 0)
    */
    static int codec_key_derive( codec_ctx ctx, cipher_ctx c_ctx )
    {
      CODEC_TRACE( "codec_key_derive: entered c_ctx.pass=%s, c_ctx.pass_sz=%d ctx.iv=%d ctx.iv_sz=%d c_ctx.kdf_iter=%d c_ctx.key_sz=%d\n",
      c_ctx.pass, c_ctx.pass_sz, c_ctx.iv, c_ctx.iv_sz, c_ctx.key_sz );

      if ( c_ctx.pass != null && c_ctx.pass_sz > 0 )
      { // if pass is not null
        if ( ( c_ctx.pass_sz == ( c_ctx.key_sz * 2 ) + 3 ) && c_ctx.pass.StartsWith( "x'", StringComparison.InvariantCultureIgnoreCase ) )
        {
          int n = c_ctx.pass_sz - 3; /* adjust for leading x' and tailing ' */
          string z = c_ctx.pass.Substring( 2 );// + 2; /* adjust lead offset of x' */
          CODEC_TRACE( "codec_key_derive: deriving key from hex\n" );
          c_ctx.key = sqlite3HexToBlob( null, z, n );
        }
        else
        {
          CODEC_TRACE( "codec_key_derive: deriving key using AES256\n" );

          Rfc2898DeriveBytes k1 = new Rfc2898DeriveBytes( c_ctx.pass, c_ctx.iv, 2010 );
          c_ctx.key_sz = 32;
          c_ctx.key = k1.GetBytes( c_ctx.key_sz );
        }
#if NET_2_0
        Aes.BlockSize = 0x80;
        Aes.FeedbackSize = 8;
        Aes.KeySize = 0x100;
        Aes.Mode = CipherMode.CBC;
#endif
        c_ctx.encryptor = Aes.CreateEncryptor( c_ctx.key, c_ctx.iv );
        c_ctx.decryptor = Aes.CreateDecryptor( c_ctx.key, c_ctx.iv );
        return SQLITE_OK;
      };
      return SQLITE_ERROR;
    }

    /*
    * ctx - codec context
    * pgno - page number in database
    * size - size in bytes of input and output buffers
    * mode - 1 to encrypt, 0 to decrypt
    * in - pointer to input bytes
    * out - pouter to output bytes
    */
    static int codec_cipher( cipher_ctx ctx, Pgno pgno, int mode, int size, byte[] bIn, byte[] bOut )
    {
      int iv;
      int tmp_csz, csz;

      CODEC_TRACE( "codec_cipher:entered pgno=%d, mode=%d, size=%d\n", pgno, mode, size );

      /* just copy raw data from in to out when key size is 0
      * i.e. during a rekey of a plaintext database */
      if ( ctx.key_sz == 0 )
      {
        Array.Copy( bIn, bOut, bIn.Length );//memcpy(out, in, size);
        return SQLITE_OK;
      }

      MemoryStream dataStream = new MemoryStream();
      CryptoStream encryptionStream;
      if ( mode == CIPHER_ENCRYPT )
      {
        encryptionStream = new CryptoStream( dataStream, ctx.encryptor, CryptoStreamMode.Write );
      }
      else
      {
        encryptionStream = new CryptoStream( dataStream, ctx.decryptor, CryptoStreamMode.Write );
      }
      encryptionStream.Write( bIn, 0, size );
      encryptionStream.FlushFinalBlock();
      dataStream.Position = 0;

      dataStream.Read( bOut, 0, (int)dataStream.Length );
      encryptionStream.Close();
      dataStream.Close();



      return SQLITE_OK;
    }

    /**
    *
    * when for_ctx == 0 then it will change for read
    * when for_ctx == 1 then it will change for write
    * when for_ctx == 2 then it will change for both
    */
    static int codec_set_cipher_name( sqlite3 db, int nDb, string cipher_name, int for_ctx )
    {
      Db pDb = db.aDb[nDb];
      CODEC_TRACE( "codec_set_cipher_name: entered db=%d nDb=%d cipher_name=%s for_ctx=%d\n", db, nDb, cipher_name, for_ctx );

      if ( pDb.pBt != null )
      {
        codec_ctx ctx = null;
        cipher_ctx c_ctx;
        sqlite3pager_get_codec( pDb.pBt.pBt.pPager, ref ctx );
        c_ctx = for_ctx != 0 ? ctx.write_ctx : ctx.read_ctx;

        c_ctx.derive_key = true;

        if ( for_ctx == 2 )
          cipher_ctx_copy( for_ctx != 0 ? ctx.read_ctx : ctx.write_ctx, c_ctx );
        return SQLITE_OK;
      }
      return SQLITE_ERROR;
    }

    static int codec_set_pass_key( sqlite3 db, int nDb, string zKey, int nKey, int for_ctx )
    {
      Db pDb = db.aDb[nDb];
      CODEC_TRACE( "codec_set_pass_key: entered db=%d nDb=%d cipher_name=%s nKey=%d for_ctx=%d\n", db, nDb, zKey, nKey, for_ctx );
      if ( pDb.pBt != null )
      {
        codec_ctx ctx = null;
        cipher_ctx c_ctx;
        sqlite3pager_get_codec( pDb.pBt.pBt.pPager, ref ctx );
        c_ctx = for_ctx != 0 ? ctx.write_ctx : ctx.read_ctx;

        cipher_ctx_set_pass( c_ctx, zKey, nKey );
        c_ctx.derive_key = true;

        if ( for_ctx == 2 )
          cipher_ctx_copy( for_ctx != 0 ? ctx.read_ctx : ctx.write_ctx, c_ctx );
        return SQLITE_OK;
      }
      return SQLITE_ERROR;
    }

    /*
    * sqlite3Codec can be called in multiple modes.
    * encrypt mode - expected to return a pointer to the
    * encrypted data without altering pData.
    * decrypt mode - expected to return a pointer to pData, with
    * the data decrypted in the input buffer
    */
    static byte[] sqlite3Codec( codec_ctx iCtx, byte[] data, Pgno pgno, int mode )
    {
      codec_ctx ctx = (codec_ctx)iCtx;
      int pg_sz = sqlite3BtreeGetPageSize( ctx.pBt );
      int offset = 0;
      byte[] pData = data;

      CODEC_TRACE( "sqlite3Codec: entered pgno=%d, mode=%d, ctx.mode_rekey=%d, pg_sz=%d\n", pgno, mode, ctx.mode_rekey, pg_sz );

      /* derive key on first use if necessary */
      if ( ctx.read_ctx.derive_key )
      {
        codec_key_derive( ctx, ctx.read_ctx );
        ctx.read_ctx.derive_key = false;
      }

      if ( ctx.write_ctx.derive_key )
      {
        if ( cipher_ctx_cmp( ctx.write_ctx, ctx.read_ctx ) == 0 )
        {
          cipher_ctx_copy( ctx.write_ctx, ctx.read_ctx ); // the relevant parameters are the same, just copy read key
        }
        else
        {
          codec_key_derive( ctx, ctx.write_ctx );
          ctx.write_ctx.derive_key = false;
        }
      }



      CODEC_TRACE( "sqlite3Codec: switch mode=%d offset=%d\n", mode, offset );
      if ( ctx.buffer.Length != pg_sz )
        ctx.buffer = sqlite3MemMalloc( pg_sz );
      switch ( mode )
      {
        case SQLITE_DECRYPT:
          codec_cipher( ctx.read_ctx, pgno, CIPHER_DECRYPT, pg_sz, pData, ctx.buffer );
          if ( pgno == 1 )
            Buffer.BlockCopy( Encoding.UTF8.GetBytes( SQLITE_FILE_HEADER ), 0, ctx.buffer, 0, FILE_HEADER_SZ );// memcpy( ctx.buffer, SQLITE_FILE_HEADER, FILE_HEADER_SZ ); /* copy file header to the first 16 bytes of the page */
          Buffer.BlockCopy( ctx.buffer, 0, pData, 0, pg_sz ); //memcpy( pData, ctx.buffer, pg_sz ); /* copy buffer data back to pData and return */
          return pData;
        case SQLITE_ENCRYPT_WRITE_CTX: /* encrypt */
          if ( pgno == 1 )
            Buffer.BlockCopy( ctx.write_ctx.iv, 0, ctx.buffer, 0, FILE_HEADER_SZ );//memcpy( ctx.buffer, ctx.iv, FILE_HEADER_SZ ); /* copy salt to output buffer */
          codec_cipher( ctx.write_ctx, pgno, CIPHER_ENCRYPT, pg_sz, pData, ctx.buffer );
          return ctx.buffer; /* return persistent buffer data, pData remains intact */
        case SQLITE_ENCRYPT_READ_CTX:
          if ( pgno == 1 )
            Buffer.BlockCopy( ctx.read_ctx.iv, 0, ctx.buffer, 0, FILE_HEADER_SZ );//memcpy( ctx.buffer, ctx.iv, FILE_HEADER_SZ ); /* copy salt to output buffer */
          codec_cipher( ctx.read_ctx, pgno, CIPHER_ENCRYPT, pg_sz, pData, ctx.buffer );
          return ctx.buffer; /* return persistent buffer data, pData remains intact */
        default:
          return pData;
      }
    }


    static int sqlite3CodecAttach( sqlite3 db, int nDb, string zKey, int nKey )
    {
      Db pDb = db.aDb[nDb];

      CODEC_TRACE( "sqlite3CodecAttach: entered nDb=%d zKey=%s, nKey=%d\n", nDb, zKey, nKey );
      //activate_openssl();

      if ( zKey != null && pDb.pBt != null )
      {
        Aes.KeySize = 256;
#if !SQLITE_SILVERLIGHT
        Aes.Padding = PaddingMode.None;
#endif
        codec_ctx ctx;
        int rc;
        ////Pager pPager = pDb.pBt.pBt.pPager;
        sqlite3_file fd;

        ctx = new codec_ctx();//sqlite3Malloc(sizeof(codec_ctx);
        //if(ctx == null) return SQLITE_NOMEM;
        //memset(ctx, 0, sizeof(codec_ctx); /* initialize all pointers and values to 0 */

        ctx.pBt = pDb.pBt; /* assign pointer to database btree structure */

        if ( ( rc = cipher_ctx_init( ref ctx.read_ctx ) ) != SQLITE_OK )
          return rc;
        if ( ( rc = cipher_ctx_init( ref ctx.write_ctx ) ) != SQLITE_OK )
          return rc;

        /* pre-allocate a page buffer of PageSize bytes. This will
        be used as a persistent buffer for encryption and decryption
        operations to avoid overhead of multiple memory allocations*/
        ctx.buffer = sqlite3MemMalloc( sqlite3BtreeGetPageSize( ctx.pBt ) );//sqlite3Malloc(sqlite3BtreeGetPageSize(ctx.pBt);
        //if(ctx.buffer == null) return SQLITE_NOMEM;

        /* allocate space for salt data. Then read the first 16 bytes header as the salt for the key derivation */
        ctx.read_ctx.iv_sz = FILE_HEADER_SZ;
        ctx.read_ctx.iv = new byte[ctx.read_ctx.iv_sz];//sqlite3Malloc( ctx.iv_sz );

        Buffer.BlockCopy( Encoding.UTF8.GetBytes( SQLITE_FILE_HEADER ), 0, ctx.read_ctx.iv, 0, FILE_HEADER_SZ );

        sqlite3pager_sqlite3PagerSetCodec( sqlite3BtreePager( pDb.pBt ), sqlite3Codec, null, sqlite3FreeCodecArg, ctx );

        codec_set_cipher_name( db, nDb, CIPHER, 0 );
        codec_set_pass_key( db, nDb, zKey, nKey, 0 );
        cipher_ctx_copy( ctx.write_ctx, ctx.read_ctx );

        //sqlite3BtreeSetPageSize( ctx.pBt, sqlite3BtreeGetPageSize( ctx.pBt ), MAX_IV_LENGTH, 0 );
      }
      return SQLITE_OK;
    }

    static void sqlite3FreeCodecArg( ref codec_ctx pCodecArg )
    {
      if ( pCodecArg == null )
        return;
      codec_ctx_free( ref pCodecArg ); // wipe and free allocated memory for the context
    }

    static void sqlite3_activate_see( string zPassword )
    {
      /* do nothing, security enhancements are always active */
    }

    static public int sqlite3_key( sqlite3 db, string pKey, int nKey )
    {
      CODEC_TRACE( "sqlite3_key: entered db=%d pKey=%s nKey=%d\n", db, pKey, nKey );
      /* attach key if db and pKey are not null and nKey is > 0 */
      if ( db != null && pKey != null )
      {
        sqlite3CodecAttach( db, 0, pKey, nKey ); // operate only on the main db
        //
        // If we are reopening an existing database, redo the header information setup 
        //
        BtShared pBt = db.aDb[0].pBt.pBt;
        byte[] zDbHeader = sqlite3MemMalloc( (int)pBt.pageSize );// pBt.pPager.pCodec.buffer;
        sqlite3PagerReadFileheader( pBt.pPager, zDbHeader.Length, zDbHeader );
        if ( sqlite3Get4byte( zDbHeader ) > 0 ) // Existing Database, need to reset some values
        {
          CODEC2( pBt.pPager, zDbHeader, 2, SQLITE_DECRYPT, ref zDbHeader );
          byte nReserve = zDbHeader[20];
          pBt.pageSize = (uint)( ( zDbHeader[16] << 8 ) | ( zDbHeader[17] << 16 ) );
          if ( pBt.pageSize < 512 || pBt.pageSize > SQLITE_MAX_PAGE_SIZE
          || ( ( pBt.pageSize - 1 ) & pBt.pageSize ) != 0 )
            pBt.pageSize = 0;
          pBt.pageSizeFixed = true;
#if !SQLITE_OMIT_AUTOVACUUM
          pBt.autoVacuum = sqlite3Get4byte( zDbHeader, 36 + 4 * 4 ) != 0;
          pBt.incrVacuum = sqlite3Get4byte( zDbHeader, 36 + 7 * 4 ) != 0;
#endif
          sqlite3PagerSetPagesize( pBt.pPager, ref pBt.pageSize, nReserve );
          pBt.usableSize = (u16)( pBt.pageSize - nReserve );
        }

        return SQLITE_OK;
      }
      return SQLITE_ERROR;
    }

    /* sqlite3_rekey
    ** Given a database, this will reencrypt the database using a new key.
    ** There are two possible modes of operation. The first is rekeying
    ** an existing database that was not previously encrypted. The second
    ** is to change the key on an existing database.
    **
    ** The proposed logic for this function follows:
    ** 1. Determine if there is already a key present
    ** 2. If there is NOT already a key present, create one and attach a codec (key would be null)
    ** 3. Initialize a ctx.rekey parameter of the codec
    **
    ** Note: this will require modifications to the sqlite3Codec to support rekey
    **
    */
    public static int sqlite3_rekey( sqlite3 db, string pKey, int nKey )
    {
      CODEC_TRACE( "sqlite3_rekey: entered db=%d pKey=%s, nKey=%d\n", db, pKey, nKey );
      //activate_openssl();
      if ( db != null && pKey != null )
      {
        Db pDb = db.aDb[0];
        CODEC_TRACE( "sqlite3_rekey: database pDb=%d\n", pDb );
        if ( pDb.pBt != null )
        {
          codec_ctx ctx = null;
          int rc;
          Pgno page_count = 0;
          Pgno pgno;
          PgHdr page = null;
          Pager pPager = pDb.pBt.pBt.pPager;

          sqlite3pager_get_codec( pDb.pBt.pBt.pPager, ref ctx );

          if ( ctx == null )
          {
            CODEC_TRACE( "sqlite3_rekey: no codec attached to db, attaching now\n" );
            /* there was no codec attached to this database,so attach one now with a null password */
            sqlite3CodecAttach( db, 0, pKey, nKey );
            sqlite3pager_get_codec( pDb.pBt.pBt.pPager, ref ctx );

            /* prepare this setup as if it had already been initialized */
            Buffer.BlockCopy( Encoding.UTF8.GetBytes( SQLITE_FILE_HEADER ), 0, ctx.read_ctx.iv, 0, FILE_HEADER_SZ );
            ctx.read_ctx.key_sz = ctx.read_ctx.iv_sz = ctx.read_ctx.pass_sz = 0;
          }

          //if ( ctx.read_ctx.iv_sz != ctx.write_ctx.iv_sz )
          //{
          //  string error = "";
          //  CODEC_TRACE( "sqlite3_rekey: updating page size for iv_sz change from %d to %d\n", ctx.read_ctx.iv_sz, ctx.write_ctx.iv_sz );
          //  db.nextPagesize = sqlite3BtreeGetPageSize( pDb.pBt );
          //  pDb.pBt.pBt.pageSizeFixed = false; /* required for sqlite3BtreeSetPageSize to modify pagesize setting */
          //  sqlite3BtreeSetPageSize( pDb.pBt, db.nextPagesize, MAX_IV_LENGTH, 0 );
          //  sqlite3RunVacuum( ref error, db );
          //}

          codec_set_pass_key( db, 0, pKey, nKey, 1 );
          ctx.mode_rekey = 1;
          /* do stuff here to rewrite the database
          ** 1. Create a transaction on the database
          ** 2. Iterate through each page, reading it and then writing it.
          ** 3. If that goes ok then commit and put ctx.rekey into ctx.key
          ** note: don't deallocate rekey since it may be used in a subsequent iteration
          */
          rc = sqlite3BtreeBeginTrans( pDb.pBt, 1 ); /* begin write transaction */
          sqlite3PagerPagecount( pPager, out page_count );
          for ( pgno = 1; rc == SQLITE_OK && pgno <= page_count; pgno++ )
          { /* pgno's start at 1 see pager.c:pagerAcquire */
            if ( 0 == sqlite3pager_is_mj_pgno( pPager, pgno ) )
            { /* skip this page (see pager.c:pagerAcquire for reasoning) */
              rc = sqlite3PagerGet( pPager, pgno, ref page );
              if ( rc == SQLITE_OK )
              { /* write page see pager_incr_changecounter for example */
                rc = sqlite3PagerWrite( page );
                //printf("sqlite3PagerWrite(%d)\n", pgno);
                if ( rc == SQLITE_OK )
                {
                  sqlite3PagerUnref( page );
                }
              }
            }
          }

          /* if commit was successful commit and copy the rekey data to current key, else rollback to release locks */
          if ( rc == SQLITE_OK )
          {
            CODEC_TRACE( "sqlite3_rekey: committing\n" );
            db.nextPagesize = sqlite3BtreeGetPageSize( pDb.pBt );
            rc = sqlite3BtreeCommit( pDb.pBt );
            if ( ctx != null )
              cipher_ctx_copy( ctx.read_ctx, ctx.write_ctx );
          }
          else
          {
            CODEC_TRACE( "sqlite3_rekey: rollback\n" );
            sqlite3BtreeRollback( pDb.pBt );
          }

          ctx.mode_rekey = 0;
        }
        return SQLITE_OK;
      }
      return SQLITE_ERROR;
    }

    static void sqlite3CodecGetKey( sqlite3 db, int nDb, out string zKey, out int nKey )
    {
      Db pDb = db.aDb[nDb];
      CODEC_TRACE( "sqlite3CodecGetKey: entered db=%d, nDb=%d\n", db, nDb );

      if ( pDb.pBt != null )
      {
        codec_ctx ctx = null;
        sqlite3pager_get_codec( pDb.pBt.pBt.pPager, ref ctx );

        if ( ctx != null )
        { /* if the codec has an attached codec_context user the raw key data */
          zKey = ctx.read_ctx.pass;
          nKey = ctx.read_ctx.pass_sz;
          return;
        }
      }
      zKey = null;
      nKey = 0;
    }
    /* END CRYPTO */
#endif
    const int SQLITE_ENCRYPT_WRITE_CTX = 6; /* Encode page */
    const int SQLITE_ENCRYPT_READ_CTX = 7; /* Encode page */
    const int SQLITE_DECRYPT = 3; /* Decode page */
  }
}
