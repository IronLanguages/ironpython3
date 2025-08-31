# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import pyexpat

from iptest import IronPythonTestCase, run_test

class PyExpatTest(IronPythonTestCase):
    def test_incremental_parsing(self):
        """https://github.com/IronLanguages/ironpython3/pull/680"""
        
        res = []
        def start_element(name, attrs):
            res.append("<{}>".format(name))
        def end_element(name):
            res.append("</{}>".format(name))
        def char_data(data):
            res.append(data)
        
        parser = pyexpat.ParserCreate()
        parser.StartElementHandler = start_element
        parser.EndElementHandler = end_element
        parser.CharacterDataHandler = char_data
        
        data = "<e>abc\U00010000xyz</e>"
        parser.Parse(data[:7], False)
        parser.Parse(data[7:], True)
        self.assertEqual(data, "".join(res))

run_test(__name__)
