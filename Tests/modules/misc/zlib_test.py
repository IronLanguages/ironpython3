#####################################################################################
#
#  Copyright (c) Pawel Jasinski. All rights reserved.
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

import zlib

# crate test data
deflate_compress = zlib.compressobj(9, zlib.DEFLATED, -zlib.MAX_WBITS)
zlib_compress = zlib.compressobj(9, zlib.DEFLATED, zlib.MAX_WBITS)
# missing gzip support in compression
# gzip_compress = zlib.compressobj(9, zlib.DEFLATED, zlib.MAX_WBITS | 16)

def create_gzip():
    import gzip
    with gzip.open('test_data.gz', 'wb') as f:
        f.write(text)
    with open('test_data.gz', 'r') as f:
        gzip_compress = f.read()
    return gzip_compress

def test_gzip():
    # decompression with gzip header
    do = zlib.decompressobj(zlib.MAX_WBITS | 16)
    AreEqual(do.decompress(gzip_data), text)
    AreEqual(zlib.decompress(gzip_data, zlib.MAX_WBITS | 16), text)

def test_header_auto_detect():
    # autodetect zlib and gzip header
    do = zlib.decompressobj(zlib.MAX_WBITS | 32)
    AreEqual(do.decompress(gzip_data), text)
    do = zlib.decompressobj(zlib.MAX_WBITS | 32)
    AreEqual(do.decompress(zlib_data), text)
    AreEqual(zlib.decompress(gzip_data, zlib.MAX_WBITS | 32), text)
    AreEqual(zlib.decompress(zlib_data, zlib.MAX_WBITS | 32), text)

def test_deflate():
    # raw data, no header
    do = zlib.decompressobj(-zlib.MAX_WBITS)
    AreEqual(do.decompress(deflate_data), text)
    AreEqual(zlib.decompress(deflate_data, -zlib.MAX_WBITS), text)

# gzip header, uncomplete header
def test_gzip_stream():
    for delta in range(1, 25):
        do = zlib.decompressobj(zlib.MAX_WBITS | 16)
        bufs = []
        for i in range(0, len(gzip_data), delta):
            bufs.append(do.decompress(gzip_data[i:i+delta]))
            AreEqual(len(do.unconsumed_tail), 0)
        bufs.append(do.flush())
        AreEqual("".join(bufs), text)

# gzip header with extra field
def test_gzip_with_extra():
    # the file was picked up from boost bug report
    with open('sample.txt.gz') as f:
        gzipped = f.read()
    AreEqual(zlib.decompress(gzipped, zlib.MAX_WBITS | 16), 'hello there\n')


def test_gzip_stream_with_extra():
    with open('sample.txt.gz') as f:
        gzipped = f.read()
    for delta in range(1, 25):
        do = zlib.decompressobj(zlib.MAX_WBITS | 16)
        bufs = []
        for i in range(0, len(gzipped), delta):
            bufs.append(do.decompress(gzipped[i:i+delta]))
            AreEqual(len(do.unconsumed_tail), 0)
        bufs.append(do.flush())
        AreEqual("".join(bufs), 'hello there\n')

text = """
Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Maecenas porttitor congue massa. Fusce posuere, magna sed pulvinar ultricies, purus lectus malesuada libero, sit amet commodo magna eros quis urna.
Nunc viverra imperdiet enim. Fusce est. Vivamus a tellus.
Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas. Proin pharetra nonummy pede. Mauris et orci.
Aenean nec lorem. In porttitor. Donec laoreet nonummy augue.
Suspendisse dui purus, scelerisque at, vulputate vitae, pretium mattis, nunc. Mauris eget neque at sem venenatis eleifend. Ut nonummy.
Fusce aliquet pede non pede. Suspendisse dapibus lorem pellentesque magna. Integer nulla.
Donec blandit feugiat ligula. Donec hendrerit, felis et imperdiet euismod, purus ipsum pretium metus, in lacinia nulla nisl eget sapien. Donec ut est in lectus consequat consequat.
Etiam eget dui. Aliquam erat volutpat. Sed at lorem in nunc porta tristique.
Proin nec augue. Quisque aliquam tempor magna. Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas.
Nunc ac magna. Maecenas odio dolor, vulputate vel, auctor ac, accumsan id, felis. Pellentesque cursus sagittis felis.
Pellentesque porttitor, velit lacinia egestas auctor, diam eros tempus arcu, nec vulputate augue magna vel risus. Cras non magna vel ante adipiscing rhoncus. Vivamus a mi.
Morbi neque. Aliquam erat volutpat. Integer ultrices lobortis eros.
Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas. Proin semper, ante vitae sollicitudin posuere, metus quam iaculis nibh, vitae scelerisque nunc massa eget pede. Sed velit urna, interdum vel, ultricies vel, faucibus at, quam.
Donec elit est, consectetuer eget, consequat quis, tempus quis, wisi. In in nunc. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos hymenaeos.
Donec ullamcorper fringilla eros. Fusce in sapien eu purus dapibus commodo. Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.
Cras faucibus condimentum odio. Sed ac ligula. Aliquam at eros.
Etiam at ligula et tellus ullamcorper ultrices. In fermentum, lorem non cursus porttitor, diam urna accumsan lacus, sed interdum wisi nibh nec nisl. Ut tincidunt volutpat urna.
Mauris eleifend nulla eget mauris. Sed cursus quam id felis. Curabitur posuere quam vel nibh.
Cras dapibus dapibus nisl. Vestibulum quis dolor a felis congue vehicula. Maecenas pede purus, tristique ac, tempus eget, egestas quis, mauris.
Curabitur non eros. Nullam hendrerit bibendum justo. Fusce iaculis, est quis lacinia pretium, pede metus molestie lacus, at gravida wisi ante at libero.
"""
deflate_data = deflate_compress.compress(text) + deflate_compress.flush()
zlib_data = zlib_compress.compress(text) + zlib_compress.flush()
gzip_data = create_gzip()



run_test(__name__)

