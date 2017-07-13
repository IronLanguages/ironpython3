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
import os
import sys
from .test_env import is_netstandard

colon = ':'
separator = os.sep
line_sep = os.linesep

class FileUtil(object):
    def file_exists_in_path(self, file):
        full_path = [os.environ[x] for x in list(os.environ.keys()) if x.lower() == "path"]
        if not full_path:
            return False
        else:
            full_path = full_path[0]
        
        for path in [os.getcwd()] + full_path.split(os.pathsep):
            path = path.strip()
            if self.file_exists(os.path.join(path, file)):
                return True

        return False

    def fullpath(self, path):
        if sys.platform == 'win32' and colon not in path:
            return os.path.join(os.getcwd(), path)
        elif sys.platform != 'win32':
            from System.IO.Path import GetFullPath
            return GetFullPath(path)
        else: 
            return path

    def write_to_file(self, filename, content=''):
        filename = self.fullpath(filename)
        try:
            self.ensure_directory_present(os.path.dirname(filename))
            with open(filename, 'w') as f:
                f.write(content)
        except:
            self.fail("unable to write to file '%s'" % filename)

    def ensure_directory_present(self, path):
        if not os.path.exists(path):
            os.makedirs(path)

    def get_temp_dir(self):
        temp = self.get_environ_variable("TMP")
        if not temp: temp = self.get_environ_variable("TEMP")
        if (not temp or ' ' in temp) and os.name == 'nt': 
            temp = r"C:\temp"
        if (not temp or ' ' in temp) and os.name == 'posix': 
            temp = "/tmp"

        return temp

    temp_dir = property(get_temp_dir)

    def delete_files(self, *files):
        for f in files: 
            try:    os.remove(f)
            except: pass

    def clean_directory(self, path, remove=False):
        if os.path.exists(path):
            for f in os.listdir(path):
                try: 
                    os.unlink(os.path.join(path, f))
                except: 
                    pass
            if remove:
                try:
                    os.rmdir(path)
                except:
                    pass

# def create_new_file(filename):
#     f = file(filename, "w")
#     f.close()

# def append_string_to_file(filename, *lines):
#     f = file(filename, "a")
#     for x in lines:
#         f.writelines(x + "\n")
#     f.close()

# def directory_exists(path):
#     if sys.platform=="win32":
#         return os.access(path, os.F_OK)
#     else:
#         try:
#             os.stat(path)
#             return True
#         except: 
#             return False

# def file_exists(file):
#     if sys.platform=="win32":
#         return os.access(file, os.F_OK)
#     else:
#         try:    
#             os.stat(file)
#             return True
#         except: 
#             return False
        

        

   
# def get_full_dir_name(path):
#     """removes ~# from short file names"""
#     if sys.platform == "win32": return path
#     import System
#     if is_netstandard:
#         import clr
#         clr.AddReference("System.IO.FileSystem")
#     return System.IO.DirectoryInfo(path).FullName
            
# def ensure_directory_present(path): 
#     path = fullpath(path)
#     p = ''
#     for x in path.split(separator):
#         p += x + separator
#         if not directory_exists(p):
#             os.mkdir(p)
        
# def write_to_file(filename, content=''):
#     filename = fullpath(filename)
#     pos = filename.rfind(separator)
#     try:
#         ensure_directory_present(filename[:pos])
#         f = file(filename, 'w')
#         f.write(content)
#         f.close()
#     except: 
#         raise AssertionError, 'unable to write to file'
    
# def delete_files(*files):
#     for f in files: 
#         try:    os.remove(f)
#         except: pass
        
# def get_parent_directory(path, levels=1):
#     while levels:
#         pos = path[:-1].rfind(separator)
#         if pos < 0:
#             return ""
#         path = path[:pos]
#         levels -= 1
#     return path

# def samefile(file1, file2):
#     return fullpath(file1).lower() == fullpath(file2).lower()
    
# def filecopy(oldpath, newpath):
#     if samefile(oldpath, newpath):
#         raise AssertionError, "%s and %s are same" % (oldpath, newpath)
        
#     of, nf = None, None
#     try: 
#         of = file(oldpath, 'rb')
#         nf = file(newpath, 'wb')
#         while True:
#             b = of.read(1024 * 16)
#             if not b: 
#                 break
#             nf.write(b)
#     finally:
#         if of: of.close()
#         if nf: nf.close()
        


# def get_directory_name(file):
#     file = fullpath(file)
#     pos = file.rfind(separator)
#     return file[:pos]
    
# def find_peverify():
#     if sys.platform <> 'cli': return None
    
#     import System
#     for d in System.Environment.GetEnvironmentVariable("PATH").split(os.pathsep):
#         if is_posix:
#             file = path_combine(d, "peverify")
#         else:
#             file = path_combine(d, "peverify.exe")
#         if file_exists(file):
#             return file

#     print """
# #################################################
# #     peverify.exe not found. Test will fail.   #
# #################################################
# """
#     return None  

# def get_mod_names(filename):
#     '''
#     Returns a list of all Python modules and subpackages in the same location 
#     as filename w/o their ".py" extension.
#     '''
#     directory = filename
    
#     if file_exists(filename):
#         directory = get_directory_name(filename)
#     else:
#         raise Exception("%s does not exist!" % (str(filename)))
    
#     #Only look at files with the .py extension and directories.    
#     ret_val = [x.rsplit(".py")[0] for x in os.listdir(directory) if (x.endswith(".py") or "." not in x) \
#                and x.lower()!="__init__.py"]
    
#     return ret_val
    
def delete_all_f(module_name, remove_folders=False):
    module = sys.modules[module_name]
    folders = {}
    for x in dir(module):
        if x.startswith('_f_'):
            fn = getattr(module, x)
            if isinstance(fn, str):
                try:
                    os.unlink(fn)
                    name = os.path.dirname(fn)
                    if os.path.isdir(name):
                        folders[name] = None
                except: pass
    if remove_folders:
        for x in sorted(iter(list(folders.keys())), reverse=True):
            try: os.rmdir(x)
            except: pass
