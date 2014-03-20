#####################################################################################
#
#  Copyright (c) Jeffrey Bester. All rights reserved.
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

from iptest.assert_util import *
import struct
import array

def test_pack():
	# test string format string
	result = struct.pack(">HH", 1, 2)
	SequencesAreEqual(result, b"\x00\x01\x00\x02")

	# test bytes format string
	result = struct.pack(b">HH", 1, 2)
	SequencesAreEqual(result, b"\x00\x01\x00\x02")

def test_unpack():
	# test string/string combination
	a,b = struct.unpack(">HH", "\x00\x01\x00\x02")
	AreEqual(a, 1)
	AreEqual(b, 2)

	# test bytes/string combination
	a,b = struct.unpack(b">HH", "\x00\x01\x00\x02")
	AreEqual(a, 1)
	AreEqual(b, 2)

	# test bytes/string combination
	a,b = struct.unpack(b">HH", b"\x00\x01\x00\x02")
	AreEqual(a, 1)
	AreEqual(b, 2)

	# test string/bytes combination
	a,b = struct.unpack(">HH", b"\x00\x01\x00\x02")
	AreEqual(a, 1)
	AreEqual(b, 2)

def test_unpack_from():
	# test string format string
	a, = struct.unpack_from('>H', buffer("\x00\x01"))
	AreEqual(a, 1)

	# test bytes format string
	a, = struct.unpack_from(b'>H', buffer("\x00\x01"))
	AreEqual(a, 1)

def test_pack_into():
	# test string format string
	result = array.array('b', [0, 0])
	struct.pack_into('>H', result, 0, 0xABCD )
	SequencesAreEqual(result, array.array('b', b"\xAB\xCD"))

	# test bytes format string
	result = array.array('b', [0, 0])
	struct.pack_into(b'>H', result, 0, 0xABCD )
	SequencesAreEqual(result, array.array('b', b"\xAB\xCD"))

run_test(__name__)
