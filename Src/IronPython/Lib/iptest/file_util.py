# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

## BE PLATFORM NETURAL
import os
import shutil
import sys

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
        if sys.implementation.name == 'cpython' or sys.platform == 'win32' and colon not in path:
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

    def delete_files(self, *files):
        for f in files: 
            try:    os.remove(f)
            except: pass

    def clean_directory(self, path, remove=True):
        if os.path.exists(path):
            for f in os.listdir(path):
                try: 
                    os.unlink(os.path.join(path, f))
                except: 
                    pass
            if remove:
                try:
                    shutil.rmtree(path)
                except:
                    pass
            else:
                for f in os.listdir(path):
                    try: 
                        os.unlink(os.path.join(path, f))
                    except: 
                        pass

    
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
