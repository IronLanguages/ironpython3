#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

'''
This tests what CPythons test_sha.py does not hit.
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

import _sha512

#--GLOBALS---------------------------------------------------------------------

#--HELPERS---------------------------------------------------------------------

#--TEST CASES------------------------------------------------------------------
def test_sanity():
    Assert("__doc__" in dir(_sha512))
    if is_cli:
        AreEqual(_sha512.__doc__, "SHA512 hash algorithm")
    Assert("__name__" in dir(_sha512))
    Assert("sha384" in dir (_sha512))
    Assert("sha512" in dir(_sha512))
    AreEqual(len(dir(_sha512)), 5)#, "There should only be five attributes in the _sha512 module!")

def test_sha512_sanity():
    x = _sha512.sha512()
    AreEqual(x.block_size, 128)
    AreEqual(x.digest(),
             "\xcf\x83\xe15~\xef\xb8\xbd\xf1T(P\xd6m\x80\x07\xd6 \xe4\x05\x0bW\x15\xdc\x83\xf4\xa9!\xd3l\xe9\xceG\xd0\xd1<]\x85\xf2\xb0\xff\x83\x18\xd2\x87~\xec/c\xb91\xbdGAz\x81\xa582z\xf9'\xda>")
    AreEqual(x.digest_size, 64)
    AreEqual(x.digest_size, x.digestsize)
    AreEqual(x.hexdigest(),
             'cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e')
    AreEqual(x.name, "SHA512")
    x.update("abc")
    AreEqual(x.hexdigest(),
             'ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f')
    
    x_copy = x.copy()
    Assert(x!=x_copy)
    AreEqual(x.hexdigest(), x_copy.hexdigest())
    
def test_sha384_sanity():
    x = _sha512.sha384()
    AreEqual(x.block_size, 128)
    AreEqual(x.digest(),
             "8\xb0`\xa7Q\xac\x968L\xd92~\xb1\xb1\xe3j!\xfd\xb7\x11\x14\xbe\x07CL\x0c\xc7\xbfc\xf6\xe1\xda'N\xde\xbf\xe7oe\xfb\xd5\x1a\xd2\xf1H\x98\xb9[")
    AreEqual(x.digest_size, 48)
    AreEqual(x.digest_size, x.digestsize)
    AreEqual(x.hexdigest(),
             '38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b')
    AreEqual(x.name, "SHA384")
    x.update("abc")
    AreEqual(x.hexdigest(),
             'cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7')
    
    x_copy = x.copy()
    Assert(x!=x_copy)
    AreEqual(x.hexdigest(), x_copy.hexdigest())

#--MAIN------------------------------------------------------------------------
run_test(__name__)
