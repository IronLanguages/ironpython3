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

"""Test cases for features that require WinForms and may display UI"""

import unittest

from iptest import IronPythonTestCase, is_cli, is_netstandard, is_posix, run_test, skipUnlessIronPython

class AgeQualifier(object):
    def __get__(self, instance, ctx):
        if instance.Age < 13: return 'young'
        if instance.Age < 20: return 'teen'
        if instance.Age < 30: return 'twenties'
        if instance.Age < 40: return 'thirties'
        if instance.Age < 50: return 'forties'
        return 'old'

SAMPLE_DATA = [('Joe', 23, 'twenties'),  ('Bob', 8, 'young'),  ('Thomas', 32, 'thirties'),  ('Patrick', 41, 'forties'),  ('Kathy', 19, 'teen'),  ('Sue', 77, 'old'),]

if is_cli:
    import System
    class Person(System.Object):
        def __init__(self, name, age):
            self._name = name
            self._age = age
        def get_name(self):
            return self._name
        def set_name(self, value):
            self._name = value
        Name = property(get_name, set_name)
        def get_age(self):
            return self._age
        def set_age(self, value):
            self._age = value
        Age = property(get_age, set_age)
        AgeDescription = AgeQualifier()


@unittest.skipIf(is_posix or is_netstandard, 'little or no System.Windows.Forms support')
@skipUnlessIronPython()
class WinformsTest(IronPythonTestCase):
    def setUp(self):
        super(WinformsTest, self).setUp()
        import clr
        self.load_iron_python_test()
        clr.AddReference('System.Windows.Forms')

    def test_databinding_auto(self):
        import System.Windows.Forms as SWF
        class Form(SWF.Form):
            def __init__(self):
                SWF.Form.__init__(self)
                self._people = people = []
                for name, age, ignored in SAMPLE_DATA:
                    people.append(Person(name, age))
                grid = SWF.DataGridView()
                grid.AutoGenerateColumns = True
                grid.DataSource = people
                grid.Dock = SWF.DockStyle.Fill
                self.grid = grid
                self.Controls.Add(grid)
        
        form = Form()
        def close_form():
            while not form.Visible:
                System.Threading.Thread.Sleep(100)
            System.Threading.Thread.Sleep(1000)
            
            for i in xrange(len(SAMPLE_DATA)):
                row = form.grid.Rows[i]
                self.assertEqual(int(row.Cells[0].FormattedValue), SAMPLE_DATA[i][1])
                self.assertEqual(row.Cells[1].FormattedValue, SAMPLE_DATA[i][0])
                self.assertEqual(row.Cells[3].FormattedValue, SAMPLE_DATA[i][2])

            form.Close()
        th = System.Threading.Thread(System.Threading.ThreadStart(close_form))
        th.Start()
        SWF.Application.Run(form)
    

    def test_databinding_manual(self):
        import System.Windows.Forms as SWF
        class Form(SWF.Form):
            def __init__(self):
                SWF.Form.__init__(self)
                self._people = people = []
                for name, age, ignored in SAMPLE_DATA:
                    people.append(Person(name, age))
                grid = SWF.DataGridView()
                grid.AutoGenerateColumns = False
                grid.Columns.Add('Name', 'Name')
                grid.Columns[0].DataPropertyName = 'Name'
                grid.Columns.Add('Age', 'Age')
                grid.Columns[1].DataPropertyName = 'Age'
                grid.Columns.Add('AgeDescription', 'AgeDescription')
                grid.Columns[2].DataPropertyName = 'AgeDescription'
                grid.DataSource = people
                grid.Dock = SWF.DockStyle.Fill
                self.grid = grid
                self.Controls.Add(grid)
        
        form = Form()
        def close_form():
            while not form.Visible:
                System.Threading.Thread.Sleep(100)
            System.Threading.Thread.Sleep(1000)
                
            for i in xrange(len(SAMPLE_DATA)):
                row = form.grid.Rows[i]
                self.assertEqual(row.Cells[0].FormattedValue, SAMPLE_DATA[i][0])
                self.assertEqual(int(row.Cells[1].FormattedValue), SAMPLE_DATA[i][1])
                self.assertEqual(row.Cells[2].FormattedValue, SAMPLE_DATA[i][2])
            form.Close()
        th = System.Threading.Thread(System.Threading.ThreadStart(close_form))
        th.Start()
        SWF.Application.Run(form)

    def test_class_name(self):
        '''This failed under IP 2.0A4.'''
        import System.Windows.Forms as SWF
        self.assertEqual(SWF.TextBox.__name__, "TextBox")
        self.assertEqual(SWF.TextBox().__class__.__name__, "TextBox")
    
    def test_cp17072(self):
        import System.Windows.Forms as SWF
        tb_class_dir = dir(SWF.TextBox)
        tb_instance_dir = dir(SWF.TextBox())
        for x in [  #Protected??? "AccessibilityNotifyClients", #overloaded
                    "BeginInvoke", #overloaded
                    "AppendText", #inherited
                    "BringToFront", #inherited
                    "CreateObjRef", #inherited
                    "AcceptsReturn", #property
                    "AutoSizeChanged", #inherited
                    "OnDragEnter", #explicit interface implementation
                    "__reduce_ex__", #arbitrary Python method
                    ]:
            self.assertTrue(x in tb_class_dir, "%s not in dir(SWF.TextBox)" % x)
            self.assertTrue(x in tb_instance_dir, "%s not in dir(SWF.TextBox())" % x)

    def test_cp9908(self):
        from System.Windows.Forms import Panel
        from System.Windows.Forms.Layout import LayoutEngine
        
        class LE(LayoutEngine):
            def Layout(self, parent, eventArgs):
                LayoutEngine.Layout(self, parent, eventArgs)
        
        le = LE()
        
        class P(Panel):
            def get_LayoutEngine(self): return le
        
        p = P()
        self.assertEqual(p.LayoutEngine, le)

    def test_cp13405(self):
        import System.Windows.Forms as SWF
        from System.Windows.Forms import PropertyGrid, DockStyle, Form
        grid = PropertyGrid()
        grid.Dock = DockStyle.Fill
        form = Form()
        form.Controls.Add(grid)
        form.Show()
        class PropertyExample(object):
            def __init__(self, X):
                self._X = X
            def _getX(self): return self._X
            def _setX(self, value): self._X = value
            X = property(_getX, _setX)

        example = PropertyExample(3)
        grid.SelectedObject = example
        
        def close_form():
            while not form.Visible:
                System.Threading.Thread.Sleep(100)
            System.Threading.Thread.Sleep(3000)
            form.Close()
            
        th = System.Threading.Thread(System.Threading.ThreadStart(close_form))
        th.Start()
        SWF.Application.Run(form)

run_test(__name__)