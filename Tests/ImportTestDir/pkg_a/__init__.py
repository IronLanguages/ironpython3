
import __builtin__

def new_import(a,b,c,d):
    print "* pkg_a.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import
