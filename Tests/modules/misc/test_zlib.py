# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Pawel Jasinski.
#

import os
import unittest
import zlib

from iptest import IronPythonTestCase, run_test

def create_gzip(text):
    import gzip
    with gzip.open('test_data.gz', 'wb') as f:
        f.write(text)
    with open('test_data.gz', 'r') as f:
        gzip_compress = f.read()
    return gzip_compress

class ZlibTest(IronPythonTestCase):
    def setUp(self):
        super(ZlibTest, self).setUp()
        self.text = """
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
        deflate_compress = zlib.compressobj(9, zlib.DEFLATED, -zlib.MAX_WBITS)
        zlib_compress = zlib.compressobj(9, zlib.DEFLATED, zlib.MAX_WBITS)
        self.deflate_data = deflate_compress.compress(self.text) + deflate_compress.flush()
        self.zlib_data = zlib_compress.compress(self.text) + zlib_compress.flush()
        self.gzip_data = create_gzip(self.text)

    def test_gzip(self):
        """decompression with gzip header"""
        do = zlib.decompressobj(zlib.MAX_WBITS | 16)
        self.assertEqual(do.decompress(self.gzip_data), self.text)
        self.assertEqual(zlib.decompress(self.gzip_data, zlib.MAX_WBITS | 16), self.text)

    def test_header_auto_detect(self):
        """autodetect zlib and gzip header"""
        do = zlib.decompressobj(zlib.MAX_WBITS | 32)
        self.assertEqual(do.decompress(self.gzip_data), self.text)
        do = zlib.decompressobj(zlib.MAX_WBITS | 32)
        self.assertEqual(do.decompress(self.zlib_data), self.text)
        self.assertEqual(zlib.decompress(self.gzip_data, zlib.MAX_WBITS | 32), self.text)
        self.assertEqual(zlib.decompress(self.zlib_data, zlib.MAX_WBITS | 32), self.text)

    def test_deflate(self):
        """raw data, no header"""
        do = zlib.decompressobj(-zlib.MAX_WBITS)
        self.assertEqual(do.decompress(self.deflate_data), self.text)
        self.assertEqual(zlib.decompress(self.deflate_data, -zlib.MAX_WBITS), self.text)

    def test_gzip_stream(self):
        """gzip header, uncomplete header"""
        for delta in range(1, 25):
            do = zlib.decompressobj(zlib.MAX_WBITS | 16)
            bufs = []
            for i in range(0, len(self.gzip_data), delta):
                bufs.append(do.decompress(self.gzip_data[i:i+delta]))
                self.assertEqual(len(do.unconsumed_tail), 0)
            bufs.append(do.flush())
            self.assertEqual("".join(bufs), self.text)


    def test_gzip_with_extra(self):
        """gzip header with extra field"""
        # the file was picked up from boost bug report
        with open(os.path.join(self.test_dir, 'sample.txt.gz')) as f:
            gzipped = f.read()
        self.assertEqual(zlib.decompress(gzipped, zlib.MAX_WBITS | 16), 'hello there\n')


    def test_gzip_stream_with_extra(self):
        with open(os.path.join(self.test_dir, 'sample.txt.gz')) as f:
            gzipped = f.read()
        for delta in range(1, 25):
            do = zlib.decompressobj(zlib.MAX_WBITS | 16)
            bufs = []
            for i in range(0, len(gzipped), delta):
                bufs.append(do.decompress(gzipped[i:i+delta]))
                self.assertEqual(len(do.unconsumed_tail), 0)
            bufs.append(do.flush())
            self.assertEqual("".join(bufs), 'hello there\n')

run_test(__name__)
