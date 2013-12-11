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

## BE PLATFORM NETURAL
import nt
import sys

colon = ':'
separator = '\\'

def create_new_file(filename):
    f = file(filename, "w")
    f.close()

def append_string_to_file(filename, *lines):
    f = file(filename, "a")
    for x in lines:
        f.writelines(x + "\n")
    f.close()

def directory_exists(path):
    if sys.platform=="win32":
        return nt.access(path, nt.F_OK)
    else:
        try:    
            nt.stat(path)
            return True
        except: 
            return False

def file_exists(file):
    if sys.platform=="win32":
        return nt.access(file, nt.F_OK)
    else:
        try:    
            nt.stat(file)
            return True
        except: 
            return False
        
def file_exists_in_path(file):
    full_path = [nt.environ[x] for x in nt.environ.keys() if x.lower() == "path"]
    if len(full_path)==0:
        return False
    else:
        full_path = full_path[0]
    
    for path in [nt.getcwd()] + full_path.split(";"):
        path = path.lstrip().rstrip()
        if file_exists(path + "\\" + file) == True:
            return True
    
    return False
        

# need consider .. and . later
def fullpath(path):
    if colon not in path:
        return nt.getcwd() + separator + path
    elif sys.platform!="win32":
        from System.IO.Path import GetFullPath
        return GetFullPath(path)
    else: 
        return path

def path_combine(*paths):
    l = len(paths)
    p = ''
    for x in paths[:-1]:
        if len(x)==0 or x[-1] == separator:
            p += x 
        else: 
            p += x + separator
    return p + paths[-1]
    
def get_full_dir_name(path):
    """removes ~# from short file names"""
    if sys.platform == "win32": return path
    import System
    return System.IO.DirectoryInfo(path).FullName
            
def ensure_directory_present(path): 
    path = fullpath(path)
    p = ''
    for x in path.split(separator):
        p += x + separator
        if not directory_exists(p):
            nt.mkdir(p)
        
def write_to_file(filename, content=''):
    filename = fullpath(filename)
    pos = filename.rfind(separator)
    try:
        ensure_directory_present(filename[:pos])
        f = file(filename, 'w')
        f.write(content)
        f.close()
    except: 
        raise AssertionError, 'unable to write to file'
    
def delete_files(*files):
    for f in files: 
        try:    nt.remove(f)
        except: pass
        
def get_parent_directory(path, levels=1):
    while levels:
        pos = path[:-1].rfind(separator)
        if pos < 0:
            return ""
        path = path[:pos]
        levels -= 1
    return path

def samefile(file1, file2):
    return fullpath(file1).lower() == fullpath(file2).lower()
    
def filecopy(oldpath, newpath):
    if samefile(oldpath, newpath):
        raise AssertionError, "%s and %s are same" % (oldpath, newpath)
        
    of, nf = None, None
    try: 
        of = file(oldpath, 'rb')
        nf = file(newpath, 'wb')
        while True:
            b = of.read(1024 * 16)
            if not b: 
                break
            nf.write(b)
    finally:
        if of: of.close()
        if nf: nf.close()
        
def clean_directory(path):
    for f in nt.listdir(path):
        try: 
            nt.unlink(path_combine(path, f))
        except: 
            pass

def get_directory_name(file):
    file = fullpath(file)
    pos = file.rfind(separator)
    return file[:pos]
    
def find_peverify():
    if sys.platform <> 'cli': return None
    
    import System
    for d in System.Environment.GetEnvironmentVariable("PATH").split(';'):
        file = path_combine(d, "peverify.exe")
        if file_exists(file):
            return file

    print """
#################################################
#     peverify.exe not found. Test will fail.   #
#################################################
"""
    return None  

def get_mod_names(filename):
    '''
    Returns a list of all Python modules and subpackages in the same location 
    as filename w/o their ".py" extension.
    '''
    directory = filename
    
    if file_exists(filename):
        directory = get_directory_name(filename)
    else:
        raise Exception("%s does not exist!" % (str(filename)))
    
    #Only look at files with the .py extension and directories.    
    ret_val = [x.rsplit(".py")[0] for x in nt.listdir(directory) if (x.endswith(".py") or "." not in x) \
               and x.lower()!="__init__.py"]
    
    return ret_val
    
        

def delete_all_f(module_name):
    module = sys.modules[module_name]
    for x in dir(module):
        if x.startswith('_f_'):
            fn = getattr(module, x)
            if isinstance(fn, str):
                try:    nt.unlink(fn)
                except: pass
                
