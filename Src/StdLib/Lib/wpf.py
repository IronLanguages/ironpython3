#-*- coding: ISO-8859-1 -*-

def _():
    import sys
    if sys.implementation.name == 'ironpython':
        import clr
        clr.AddReference('IronPython.Wpf')
_()
del _

from _wpf import *
