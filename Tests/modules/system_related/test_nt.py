# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import errno
import os
import random
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, run_test, skipUnlessIronPython, stderr_trapper

if sys.platform == "win32":
    import nt

if not is_cli:
    long = type(sys.maxsize + 1)

@unittest.skipUnless(sys.platform == "win32", 'Windows specific test')
class NtTest(IronPythonTestCase):
    def assertRaisesNumber(self, expected_exception, expected_number, callable_obj, *args, **kwargs):
        with self.assertRaises(expected_exception) as cm:
            callable_obj(*args, **kwargs)
        self.assertEqual(cm.exception.errno, expected_number)

    def test_computername(self):
        self.assertTrue('COMPUTERNAME' in nt.environ or 'computername' in nt.environ)

    def test_mkdir(self):
        nt.mkdir('dir_create_test')
        self.assertEqual(nt.listdir(nt.getcwd()).count('dir_create_test'), 1)

        nt.rmdir('dir_create_test')
        self.assertEqual(nt.listdir(nt.getcwd()).count('dir_create_test'), 0)

    def test_mkdir_negative(self):
        nt.mkdir("dir_create_test")
        try:
            nt.mkdir("dir_create_test")
            self.assertUnreachabale("Cannot create the same directory twice")
        except WindowsError as e:
            self.assertEqual(e.errno, 17)

        #if it fails once...it should fail again
        self.assertRaises(WindowsError, nt.mkdir, "dir_create_test")
        nt.rmdir('dir_create_test')
        nt.mkdir("dir_create_test")
        self.assertRaises(WindowsError, nt.mkdir, "dir_create_test")
        nt.rmdir('dir_create_test')

    def test_listdir(self):
        self.assertEqual(nt.listdir(nt.getcwd()), nt.listdir())
        self.assertEqual(nt.listdir(nt.getcwd()), nt.listdir(None))
        self.assertEqual(nt.listdir(nt.getcwd()), nt.listdir('.'))

    # stat,lstat
    def test_stat(self):
        # stat
        self.assertRaises(nt.error, nt.stat, 'doesnotexist.txt')

        #lstat
        self.assertRaises(nt.error, nt.lstat, 'doesnotexist.txt')

        self.assertRaisesNumber(WindowsError, 2, nt.stat, 'doesnotexist.txt')
        if is_netcoreapp:
            self.assertRaisesNumber(WindowsError, 2, nt.stat, 'bad?path.txt')
        else:
            self.assertRaisesNumber(WindowsError, 22, nt.stat, 'bad?path.txt')

    # stat should accept bytes as argument
    def test_stat_cp34910(self):
        self.assertEqual(nt.stat('/'), nt.stat(b'/'))
        self.assertEqual(nt.lstat('/'), nt.lstat(b'/'))

    # getcwdb test
    def test_getcwdb(self):
        self.assertEqual(nt.getcwdb(),nt.getcwd().encode())

        nt.mkdir('dir_create_test')
        if is_cli:
            self.assertEqual(nt.listdir(nt.getcwdb()).count('dir_create_test'), 1)
        else:
            self.assertEqual(nt.listdir(nt.getcwdb()).count(b'dir_create_test'), 1)
        nt.rmdir('dir_create_test')

    # getpid test
    def test_getpid(self):
        result = None
        result = nt.getpid()
        self.assertTrue(result>=0,
            "processPID should not be less than zero")

        result2 = nt.getpid()
        self.assertTrue(result2 == result,
            "The processPID in one process should be same")

    # environ test
    def test_environ(self):
        non_exist_key      = "_NOT_EXIST_"
        iron_python_string = "Iron_pythoN"

        try:
            nt.environ[non_exist_key]
            raise self.assertTrueionError
        except KeyError:
            pass

        # set
        nt.environ[non_exist_key] = iron_python_string
        self.assertEqual(nt.environ[non_exist_key], iron_python_string)

        import sys
        if is_cli:
            import System
            self.assertEqual(System.Environment.GetEnvironmentVariable(non_exist_key), iron_python_string)

        # update again
        swapped = iron_python_string.swapcase()
        nt.environ[non_exist_key] = swapped
        self.assertEqual(nt.environ[non_exist_key], swapped)
        if is_cli:
            self.assertEqual(System.Environment.GetEnvironmentVariable(non_exist_key), swapped)

        # remove
        del nt.environ[non_exist_key]
        if is_cli:
            self.assertEqual(System.Environment.GetEnvironmentVariable(non_exist_key), None)

        self.assertEqual(type(nt.environ), type({}))

    # startfile
    def test_startfile(self):
        self.assertRaises(OSError, nt.startfile, "not_exist_file.txt")
        self.assertRaises(WindowsError, nt.startfile, __file__, 'bad')

    # chdir tests
    def test_chdir(self):
        currdir = nt.getcwd()
        nt.mkdir('tsd')
        nt.chdir('tsd')
        self.assertEqual(os.path.join(currdir, 'tsd'), nt.getcwd())
        nt.chdir(currdir)
        self.assertEqual(currdir, nt.getcwd())
        nt.rmdir('tsd')

        # the directory is empty or does not exist
        self.assertRaisesNumber(WindowsError, 22, lambda:nt.chdir(''))
        self.assertRaisesNumber(WindowsError, 2, lambda:nt.chdir('tsd'))

    @unittest.skipIf(is_cli, "TODO: figure this out")
    def test_fdopen(self):
        fd_lambda = lambda x: nt.dup(x)

        # fd = 0
        result = None
        result = os.fdopen(fd_lambda(0),"r",1024)
        self.assertFalse(result is None,"1,The file object was not returned correctly")

        result = None
        result = os.fdopen(fd_lambda(0),"w",2048)
        self.assertFalse(result is None,"2,The file object was not returned correctly")

        with self.assertRaises(OSError):
            os.fdopen(fd_lambda(0),"a",512)

        # fd = 1
        with self.assertRaises(OSError):
            os.fdopen(fd_lambda(1),"a",1024)

        result = None
        result = os.fdopen(fd_lambda(1),"r",2048)
        self.assertFalse(result is None,"5,The file object was not returned correctly")

        result = None
        result = os.fdopen(fd_lambda(1),"w",512)
        self.assertFalse(result is None,"6,The file object was not returned correctly")

        # fd = 2
        result = None
        result = os.fdopen(fd_lambda(2),"r",1024)
        self.assertFalse(result is None,"7,The file object was not returned correctly")

        with self.assertRaises(OSError):
            os.fdopen(fd_lambda(2),"a",2048)

        result = None
        result = os.fdopen(fd_lambda(2),"w",512)
        self.assertFalse(result is None,"9,The file object was not returned correctly")

        if not is_cli:
            result.close()

        # The file descriptor is not valid
        self.assertRaises(OSError,os.fdopen,3000)
        self.assertRaises(ValueError,os.fdopen,-1)
        self.assertRaises(OSError,os.fdopen,3000, "w")
        self.assertRaises(OSError,os.fdopen,3000, "w", 1024)

        # The file mode does not exist
        self.assertRaises(ValueError,os.fdopen,0,"p")

        stuff = b"\x00a\x01\x02b\x03 \x04  \x05\n\x06_\0xFE\0xFFxyz"
        name = "cp5633.txt"
        fd = nt.open(name, nt.O_CREAT | nt.O_BINARY | nt.O_TRUNC | nt.O_WRONLY)
        f = os.fdopen(fd, 'wb')
        f.write(stuff)
        f.close()
        try:
            with open(name, 'rb') as f:
                self.assertEqual(stuff, f.read())
        finally:
            nt.remove(name)

    # fstat,unlink tests
    def test_fstat(self):
        result = nt.fstat(1)
        self.assertTrue(result!=0,"0,The file stat object was not returned correctly")

        result = None
        tmpfile = "tmpfile1.tmp"
        f = open(tmpfile, "w")
        result = nt.fstat(f.fileno())
        self.assertFalse(result is None,"0,The file stat object was not returned correctly")
        f.close()
        nt.unlink(tmpfile)

        # stdxx file descriptor
        self.assertEqual(10, len(nt.fstat(0)))
        self.assertEqual(10, len(nt.fstat(1)))
        self.assertEqual(10, len(nt.fstat(2)))

        # invalid file descriptor
        self.assertRaises(OSError,nt.fstat,3000)
        self.assertRaises(OSError,nt.fstat,-1)

    def test_chmod(self):
        # chmod tests:
        # BUG 828,830
        nt.mkdir('tmp2')
        nt.chmod('tmp2', 256) # NOTE: change to flag when stat is implemented
        self.assertRaises(OSError, lambda:nt.rmdir('tmp2'))
        nt.chmod('tmp2', 128)
        nt.rmdir('tmp2')
        # /BUG

    ################################################################################################
    # popen/popen2/popen3/unlink tests

    def test_popen(self):
        # open a pipe just for reading...
        pipe_modes = [["ping 127.0.0.1 -n 1", "r"],
                    ["ping 127.0.0.1 -n 1"]]

        for args in pipe_modes:
            x = os.popen(*args)
            text = x.read()
            self.assertTrue(text.lower().index('pinging') != -1)
            self.assertEqual(x.close(), None)

        # write to a pipe
        x = os.popen('sort', 'w')
        x.write('hello\nabc\n')
        x.close()

        # bug 1146
        #x = os.popen('sort', 'w')
        #x.write('hello\nabc\n')
        #self.assertEqual(x.close(), None)

        # once w/ default mode
        self.assertRaises(ValueError, os.popen, "ping 127.0.0.1 -n 1", "a")

        # popen uses cmd.exe to run stuff -- at least sometimes
        dir_pipe = os.popen('dir')
        dir_pipe.read()
        dir_pipe.close()

        tmpfile = 'tmpfile.tmp'
        f = open(tmpfile, 'w')
        f.close()
        nt.unlink(tmpfile)
        try:
            nt.chmod('tmpfile.tmp', 256)
        except Exception:
            pass #should throw when trying to access file deleted by unlink
        else:
            self.assertTrue(False,"Error! Trying to access file deleted by unlink should have thrown.")

        try:
            tmpfile = "tmpfile2.tmp"
            f = open(tmpfile, "w")
            f.write("testing chmod")
            f.close()
            nt.chmod(tmpfile, 256)
            self.assertRaises(OSError, nt.unlink, tmpfile)
            nt.chmod(tmpfile, 128)
            nt.unlink(tmpfile)
            self.assertRaises(IOError, open, tmpfile)
        finally:
            try:
                nt.chmod(tmpfile, 128)
                nt.unlink(tmpfile)
            except Exception as e:
                print("exc", e)

        # verify that nt.stat reports times in seconds, not ticks...

        import time
        tmpfile = 'tmpfile.tmp'
        f = open(tmpfile, 'w')
        f.close()
        t = time.time()
        mt = nt.stat(tmpfile).st_mtime
        nt.unlink(tmpfile) # this deletes the file
        self.assertTrue(abs(t-mt) < 60, "time differs by too much " + str(abs(t-mt)))

        tmpfile = 'tmpfile.tmp' # need to open it again since we deleted it with 'unlink'
        f = open(tmpfile, 'w')
        f.close()
        nt.chmod('tmpfile.tmp', 256)
        nt.chmod('tmpfile.tmp', 128)
        nt.unlink('tmpfile.tmp')

    # utime tests
    def test_utime(self):
        open('temp_file_does_not_exist.txt', 'w').close()
        import nt
        x = nt.stat('.')
        nt.utime('temp_file_does_not_exist.txt', (x[7], x[8]))
        y = nt.stat('temp_file_does_not_exist.txt')
        self.assertEqual(x[7], y[7])
        self.assertEqual(x[8], y[8])
        nt.unlink('temp_file_does_not_exist.txt')

    # times test
    def test_times(self):
        '''
        '''
        #simple sanity check
        utime, stime, zero1, zero2, zero3 = nt.times()
        self.assertTrue(utime>=0)
        self.assertTrue(stime>=0)
        self.assertEqual(zero1, 0)
        self.assertEqual(zero2, 0)
        #BUG - according to the specs this should be 0 for Windows
        #self.assertEqual(zero3, 0)

    # putenv tests
    def test_putenv(self):
        '''
        '''
        #simple sanity check
        nt.putenv("IPY_TEST_ENV_VAR", "xyz")

        #ensure it really does what it claims to do
        self.assertFalse("IPY_TEST_ENV_VAR" in nt.environ)

        #negative cases
        self.assertRaises(TypeError, nt.putenv, None, "xyz")
        #BUG
        #self.assertRaises(TypeError, nt.putenv, "ABC", None)
        self.assertRaises(TypeError, nt.putenv, 1, "xyz")
        self.assertRaises(TypeError, nt.putenv, "ABC", 1)

    @unittest.skipUnless(is_cli, "CPython has no nt.unsetenv function")
    def test_unsetenv(self):
        #simple sanity check
        nt.putenv("ipy_test_env_var", "xyz")
        nt.unsetenv("ipy_test_env_var_unset")
        self.assertFalse("ipy_test_env_var_unset" in nt.environ)

    # remove tests
    def test_remove(self):
        # remove an existing file
        handler = open("create_test_file.txt","w")
        handler.close()
        path1 = nt.getcwd()
        nt.remove(path1+'\\create_test_file.txt')
        self.assertEqual(nt.listdir(nt.getcwd()).count('create_test_file.txt'), 0)

        self.assertRaisesNumber(OSError, 2, nt.remove, path1+'\\create_test_file2.txt')
        self.assertRaisesNumber(OSError, 2, nt.unlink, path1+'\\create_test_file2.txt')
        self.assertRaisesNumber(OSError, 22, nt.remove, path1+'\\create_test_file?.txt')
        self.assertRaisesNumber(OSError, 22, nt.unlink, path1+'\\create_test_file?.txt')

        # the path is a type other than string
        self.assertRaises(TypeError, nt.remove, 1)
        self.assertRaises(TypeError, nt.remove, True)
        self.assertRaises(TypeError, nt.remove, None)

    def test_remove_negative(self):
        import stat
        self.assertRaisesNumber(WindowsError, errno.ENOENT, lambda : nt.remove('some_file_that_does_not_exist'))
        try:
            open('some_test_file.txt', 'w').close()
            nt.chmod('some_test_file.txt', stat.S_IREAD)
            self.assertRaisesNumber(WindowsError, errno.EACCES, lambda : nt.remove('some_test_file.txt'))
            nt.chmod('some_test_file.txt', stat.S_IWRITE)

            with open('some_test_file.txt', 'w+'):
                self.assertRaisesNumber(WindowsError, errno.EACCES, lambda : nt.remove('some_test_file.txt'))
        finally:
            nt.chmod('some_test_file.txt', stat.S_IWRITE)
            nt.unlink('some_test_file.txt')

    # rename tests
    def test_rename(self):
        # normal test
        handler = open("oldnamefile.txt","w")
        handler.close()
        str_old = "oldnamefile.txt"
        dst = "newnamefile.txt"
        nt.rename(str_old,dst)
        self.assertEqual(nt.listdir(nt.getcwd()).count(dst), 1)
        self.assertEqual(nt.listdir(nt.getcwd()).count(str_old), 0)
        nt.remove(dst)

        # the destination name is a directory
        handler = open("oldnamefile.txt","w")
        handler.close()
        str_old = "oldnamefile.txt"
        dst = "newnamefile.txt"
        nt.mkdir(dst)
        self.assertRaises(OSError, nt.rename,str_old,dst)
        nt.rmdir(dst)
        nt.remove(str_old)

        # the dst already exists
        handler1 = open("oldnamefile.txt","w")
        handler1.close()
        handler2 = open("newnamefile.txt","w")
        handler2.close()
        str_old = "oldnamefile.txt"
        dst = "newnamefile.txt"
        self.assertRaises(OSError, nt.rename,str_old,dst)
        nt.remove(str_old)
        nt.remove(dst)

        # the source file specified does not exist
        str_old = "oldnamefile.txt"
        dst = "newnamefile.txt"
        self.assertRaises(OSError, nt.rename,str_old,dst)

    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_spawnle(self):
        ping_cmd = os.path.join(os.environ['windir'], 'system32', 'ping')

        #simple sanity check
        os.spawnle(nt.P_WAIT, ping_cmd , "ping", "/?", {})
        #BUG - the first parameter of spawnle should be "ping"
        #nt.spawnle(nt.P_WAIT, ping_cmd , "ping", "127.0.0.1", {})
        #BUG - even taking "ping" out, multiple args do not work
        #pid = nt.spawnle(nt.P_NOWAIT, ping_cmd ,  "-n", "15", "-w", "1000", "127.0.0.1", {})

        #negative cases
        self.assertRaises(TypeError, os.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", None)
        self.assertRaises(TypeError, os.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", {1: "xyz"})
        self.assertRaises(TypeError, os.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", {"abc": 1})

    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_spawnl(self):
        #sanity check
        #CPython nt has no spawnl function
        pint_cmd = ping_cmd = os.path.join(os.environ['windir'], 'system32', 'ping.exe')
        os.spawnl(nt.P_WAIT, ping_cmd , "ping","127.0.0.1","-n","1")
        os.spawnl(nt.P_WAIT, ping_cmd , "ping","/?")
        os.spawnl(nt.P_WAIT, ping_cmd , "ping")

        # negative case
        cmd = pint_cmd+"oo"
        self.assertRaises(OSError,os.spawnl,nt.P_WAIT,cmd,"ping","/?")

    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_spawnv(self):
        #sanity check
        ping_cmd = os.path.join(os.environ["windir"], "system32", "ping")
        nt.spawnv(nt.P_WAIT, ping_cmd , ["ping"])
        nt.spawnv(nt.P_WAIT, ping_cmd , ["ping","127.0.0.1","-n","1"])
 
    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_spawnve(self):
        ping_cmd = os.path.join(os.environ["windir"], "system32", "ping")

        #simple sanity checks
        nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "/?"], {})
        nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "127.0.0.1"], {})
        nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "-n", "2", "-w", "1000", "127.0.0.1"], {})

        #negative cases
        self.assertRaises(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], None)
        self.assertRaises(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], {1: "xyz"})
        self.assertRaises(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], {"abc": 1})

    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_waitpid(self):
        #sanity check
        ping_cmd = os.path.join(os.environ["windir"], "system32", "ping")
        pid = nt.spawnv(nt.P_NOWAIT, ping_cmd ,  ["ping", "-n", "1", "127.0.0.1"])

        new_pid, exit_stat = nt.waitpid(pid, 0)

        #negative cases
        self.assertRaisesMessage(OSError, "[Errno 10] No child processes", nt.waitpid, -1234, 0)

        self.assertRaises(TypeError, nt.waitpid, "", 0)

    # stat_result test
    def test_stat_result(self):
        #sanity check
        statResult = [0,1,2,3,4,5,6,7,8,9]
        object = None
        object = nt.stat_result(statResult)
        self.assertTrue(object != None,
            "The class did not return an object instance")
        self.assertEqual(object.st_uid,4)
        self.assertEqual(object.st_gid,5)
        self.assertEqual(object.st_nlink,3)
        self.assertEqual(object.st_dev,2)
        self.assertEqual(object.st_ino,1)
        self.assertEqual(object.st_mode,0)
        self.assertEqual(object.st_atime,7)
        self.assertEqual(object.st_mtime,8)
        self.assertEqual(object.st_ctime,9)

        self.assertEqual(str(nt.stat_result(range(12))),
                "os.stat_result(st_mode=0, st_ino=1, st_dev=2, st_nlink=3, st_uid=4, st_gid=5, st_size=6, st_atime=7, st_mtime=8, st_ctime=9)") #CodePlex 8755

        #negative tests
        statResult = [0,1,2,3,4,5,6,7,8,]
        self.assertRaises(TypeError,nt.stat_result,statResult)

        # this should not produce an error
        statResult = ["a","b","c","y","r","a","a","b","d","r","f"]
        x = nt.stat_result(statResult)
        self.assertEqual(x.st_mode, 'a')
        self.assertEqual(x.st_ino, 'b')
        self.assertEqual(x.st_dev, 'c')
        self.assertEqual(x.st_nlink, 'y')
        self.assertEqual(x.st_uid, 'r')
        self.assertEqual(x.st_gid, 'a')
        self.assertEqual(x.st_size, 'a')
        self.assertEqual(x.st_atime, 'f')
        self.assertEqual(x.st_mtime, 'd')
        self.assertEqual(x.st_ctime, 'r')

        # can pass dict to get values...
        x = nt.stat_result(range(10), {'st_atime': 23, 'st_mtime':42, 'st_ctime':2342})
        self.assertEqual(x.st_atime, 23)
        self.assertEqual(x.st_mtime, 42)
        self.assertEqual(x.st_ctime, 2342)

        # positional values take precedence over dict values
        x = nt.stat_result(range(13), {'st_atime': 23, 'st_mtime':42, 'st_ctime':2342})
        self.assertEqual(x.st_atime, 10)
        self.assertEqual(x.st_mtime, 11)
        self.assertEqual(x.st_ctime, 12)

        x = nt.stat_result(range(13))
        self.assertEqual(x.st_atime, 10)
        self.assertEqual(x.st_mtime, 11)
        self.assertEqual(x.st_ctime, 12)

        # other values are ignored...
        x = nt.stat_result(range(13), {'st_dev': 42, 'st_gid': 42, 'st_ino': 42, 'st_mode': 42, 'st_nlink': 42, 'st_size':42, 'st_uid':42})
        self.assertEqual(x.st_mode, 0)
        self.assertEqual(x.st_ino, 1)
        self.assertEqual(x.st_dev, 2)
        self.assertEqual(x.st_nlink, 3)
        self.assertEqual(x.st_uid, 4)
        self.assertEqual(x.st_gid, 5)
        self.assertEqual(x.st_size, 6)
        self.assertEqual(x.st_atime, 10)
        self.assertEqual(x.st_mtime, 11)
        self.assertEqual(x.st_ctime, 12)

        self.assertTrue(isinstance(x, tuple))

        #--Misc

        #+
        x = nt.stat_result(range(10))
        self.assertEqual(x + (), x)
        self.assertEqual(x + tuple(x), tuple(range(10))*2)
        self.assertRaises(TypeError, lambda: x + (1))
        self.assertRaises(TypeError, lambda: x + 1)
        self.assertEqual(x + x, tuple(range(10))*2)

        #> (list/object)
        if is_cli:
            self.assertTrue(nt.stat_result(range(10)) > None)
            self.assertTrue(nt.stat_result(range(10)) > 1)
            self.assertTrue(nt.stat_result(range(10)) > range(10))
        self.assertTrue(nt.stat_result([1 for x in range(10)]) > nt.stat_result(range(10)))
        self.assertTrue(not nt.stat_result(range(10)) > nt.stat_result(range(10)))
        self.assertTrue(not nt.stat_result(range(10)) > nt.stat_result(range(11)))
        self.assertTrue(not nt.stat_result(range(10)) > nt.stat_result([1 for x in range(10)]))
        self.assertTrue(not nt.stat_result(range(11)) > nt.stat_result(range(10)))

        #< (list/object)
        if is_cli:
            self.assertTrue(not nt.stat_result(range(10)) < None)
            self.assertTrue(not nt.stat_result(range(10)) < 1)
            self.assertTrue(not nt.stat_result(range(10)) < range(10))
        self.assertTrue(not nt.stat_result([1 for x in range(10)]) < nt.stat_result(range(10)))
        self.assertTrue(not nt.stat_result(range(10)) < nt.stat_result(range(10)))
        self.assertTrue(not nt.stat_result(range(10)) < nt.stat_result(range(11)))
        self.assertTrue(nt.stat_result(range(10)) < nt.stat_result([1 for x in range(10)]))
        self.assertTrue(not nt.stat_result(range(11)) < nt.stat_result(range(10)))

        #>= (list/object)
        if is_cli:
            self.assertTrue(nt.stat_result(range(10)) >= None)
            self.assertTrue(nt.stat_result(range(10)) >= 1)
            self.assertTrue(nt.stat_result(range(10)) >= range(10))
        self.assertTrue(nt.stat_result([1 for x in range(10)]) >= nt.stat_result(range(10)))
        self.assertTrue(nt.stat_result(range(10)) >= nt.stat_result(range(10)))
        self.assertTrue(nt.stat_result(range(10)) >= nt.stat_result(range(11)))
        self.assertTrue(not nt.stat_result(range(10)) >= nt.stat_result([1 for x in range(10)]))
        self.assertTrue(nt.stat_result(range(11)) >= nt.stat_result(range(10)))

        #<= (list/object)
        if is_cli:
            self.assertTrue(not nt.stat_result(range(10)) <= None)
            self.assertTrue(not nt.stat_result(range(10)) <= 1)
            self.assertTrue(not nt.stat_result(range(10)) <= range(10))
        self.assertTrue(not nt.stat_result([1 for x in range(10)]) <= nt.stat_result(range(10)))
        self.assertTrue(nt.stat_result(range(10)) <= nt.stat_result(range(10)))
        self.assertTrue(nt.stat_result(range(10)) <= nt.stat_result(range(11)))
        self.assertTrue(nt.stat_result(range(10)) <= nt.stat_result([1 for x in range(10)]))
        self.assertTrue(nt.stat_result(range(11)) <= nt.stat_result(range(10)))

        #* (size/stat_result)
        x = nt.stat_result(range(10))
        self.assertEqual(x * 1, tuple(x))
        self.assertEqual(x * 2, tuple(range(10))*2)
        self.assertEqual(1 * x, tuple(x))
        self.assertEqual(3 * x, tuple(range(10))*3)
        self.assertRaises(TypeError, lambda: x * x)
        self.assertRaises(TypeError, lambda: x * 3.14)
        self.assertRaises(TypeError, lambda: x * None)
        self.assertRaises(TypeError, lambda: x * "abc")
        self.assertRaises(TypeError, lambda: "abc" * x)

        #__repr__
        x = nt.stat_result(range(10))
        self.assertEqual(x.__repr__(),
                "os.stat_result(st_mode=0, st_ino=1, st_dev=2, st_nlink=3, st_uid=4, st_gid=5, st_size=6, st_atime=7, st_mtime=8, st_ctime=9)")

        #index get/set
        x = nt.stat_result(range(10))
        for i in range(10):
            self.assertEqual(x[i], i)

        def temp_func():
            z = nt.stat_result(range(10))
            z[3] = 4
        self.assertRaises(TypeError, temp_func)

        #__getslice__
        x = nt.stat_result(range(10))
        self.assertEqual(x[1:3], (1, 2))
        self.assertEqual(x[7:100], (7, 8, 9))
        self.assertEqual(x[7:-100], ())
        self.assertEqual(x[-101:-100], ())
        self.assertEqual(x[-2:8], ())
        self.assertEqual(x[-2:1000], (8,9))

        #__contains__
        x = nt.stat_result(range(10))
        for i in range(10):
            self.assertTrue(i in x)
            x.__contains__(i)
        self.assertTrue(-1 not in x)
        self.assertTrue(None not in x)
        self.assertTrue(20 not in x)

        #GetHashCode
        x = nt.stat_result(range(10))
        self.assertTrue(type(hash(x))==int)

        #IndexOf
        x = nt.stat_result(range(10))
        self.assertEqual(x.__getitem__(0), 0)
        self.assertEqual(x.__getitem__(3), 3)
        self.assertEqual(x.__getitem__(9), 9)
        self.assertEqual(x.__getitem__(-1), 9)
        self.assertRaises(IndexError, lambda: x.__getitem__(10))
        self.assertRaises(IndexError, lambda: x.__getitem__(11))

        #Insert
        x = nt.stat_result(range(10))
        self.assertEqual(x.__add__(()), tuple(x))
        self.assertEqual(x.__add__((1,2,3)), tuple(x) + (1, 2, 3))
        self.assertRaises(TypeError, lambda: x.__add__(3))
        self.assertRaises(TypeError, lambda: x.__add__(None))

        #Remove
        x = nt.stat_result(range(10))
        def temp_func():
            z = nt.stat_result(range(10))
            del z[3]
        self.assertRaises(TypeError, temp_func)

        #enumerate
        x = nt.stat_result(range(10))
        temp_list = []
        for i in x:
            temp_list.append(i)
        self.assertEqual(tuple(x), tuple(temp_list))

        statResult = ["a","b","c","y","r","a","a","b","d","r","f"]
        x = nt.stat_result(statResult)
        temp_list = []
        for i in x:
            temp_list.append(i)
        self.assertEqual(tuple(x), tuple(temp_list))

        temp = Exception()
        statResult = [temp for i in range(10)]
        x = nt.stat_result(statResult)
        temp_list = []
        for i in x:
            temp_list.append(i)
        self.assertEqual(tuple(x), tuple(temp_list))

        with self.assertRaises(TypeError):
            class subclass(nt.stat_result): pass

    # urandom tests
    def test_urandom(self):
        # argument n is a random int
        rand = random.Random()
        n = rand.getrandbits(16)
        str = nt.urandom(n)
        result = len(str)
        self.assertTrue(isinstance(str, bytes))
        self.assertEqual(n,result)

    # write/read tests
    def test_write(self):
        # write the file
        tempfilename = "temp.txt"
        file = open(tempfilename,"w")
        nt.write(file.fileno(), b"Hello,here is the value of test string")
        file.close()

        # read from the file
        file =   open(tempfilename,"r")
        str = nt.read(file.fileno(),100)
        self.assertEqual(str, b"Hello,here is the value of test string")
        file.close()
        nt.unlink(tempfilename)

        # BUG 8783 the argument buffersize in nt.read(fd, buffersize) is less than zero
        # the string written to the file is empty string
        tempfilename = "temp.txt"
        file = open(tempfilename,"w")
        nt.write(file.fileno(), b"bug test")
        file.close()
        file = open(tempfilename,"r")
        self.assertRaises(OSError,nt.read,file.fileno(),-10)
        file.close()
        nt.unlink(tempfilename)

    # open test
    def test_open(self):
        open('temp.txt', 'w+').close()
        try:
            fd = nt.open('temp.txt', nt.O_WRONLY | nt.O_CREAT)
            nt.close(fd)

            self.assertRaisesNumber(OSError, 17, nt.open, 'temp.txt', nt.O_CREAT | nt.O_EXCL)
            for flag in [nt.O_EXCL, nt.O_APPEND]:
                fd = nt.open('temp.txt', nt.O_RDONLY | flag)
                nt.close(fd)

                fd = nt.open('temp.txt', nt.O_WRONLY | flag)
                nt.close(fd)

                fd = nt.open('temp.txt', nt.O_RDWR | flag)
                nt.close(fd)

            # sanity test
            tempfilename = "temp.txt"
            fd = nt.open(tempfilename,256,1)
            nt.close(fd)

            nt.unlink('temp.txt')

            f = nt.open('temp.txt', nt.O_TEMPORARY | nt.O_CREAT)
            nt.close(f)
            self.assertRaises(OSError, nt.stat, 'temp.txt')

            # TODO: These tests should probably test more functionality regarding O_SEQUENTIAL/O_RANDOM
            f = nt.open('temp.txt', nt.O_TEMPORARY | nt.O_CREAT | nt.O_SEQUENTIAL | nt.O_RDWR)
            nt.close(f)
            self.assertRaises(OSError, nt.stat, 'temp.txt')

            f = nt.open('temp.txt', nt.O_TEMPORARY | nt.O_CREAT | nt.O_RANDOM | nt.O_RDWR)
            nt.close(f)
            self.assertRaises(OSError, nt.stat, 'temp.txt')
        finally:
            try:
                # should fail if the file doesn't exist
                nt.unlink('temp.txt')
            except:
                pass

    def test_system_minimal(self):
        self.assertTrue(hasattr(nt, "system"))
        self.assertEqual(nt.system("ping localhost -n 1"), 0)
        self.assertEqual(nt.system('"ping localhost -n 1"'), 0)
        self.assertEqual(nt.system('"ping localhost -n 1'), 0)

        self.assertEqual(nt.system("ping"), 1)

        self.assertEqual(nt.system("some_command_which_is_not_available"), 1)

    # flags test
    def test_flags(self):
        self.assertEqual(nt.P_WAIT,0)
        self.assertEqual(nt.P_NOWAIT,1)
        self.assertEqual(nt.P_NOWAITO,3)
        self.assertEqual(nt.O_APPEND,8)
        self.assertEqual(nt.O_CREAT,256)
        self.assertEqual(nt.O_TRUNC,512)
        self.assertEqual(nt.O_EXCL,1024)
        self.assertEqual(nt.O_NOINHERIT,128)
        self.assertEqual(nt.O_RANDOM,16)
        self.assertEqual(nt.O_SEQUENTIAL,32)
        self.assertEqual(nt.O_SHORT_LIVED,4096)
        self.assertEqual(nt.O_TEMPORARY,64)
        self.assertEqual(nt.O_WRONLY,1)
        self.assertEqual(nt.O_RDONLY,0)
        self.assertEqual(nt.O_RDWR,2)
        self.assertEqual(nt.O_BINARY,32768)
        self.assertEqual(nt.O_TEXT,16384)

    def test_access(self):
        open('new_file_name', 'w').close()

        self.assertEqual(nt.access('new_file_name', nt.F_OK), True)
        self.assertEqual(nt.access('new_file_name', nt.R_OK), True)
        self.assertEqual(nt.access('does_not_exist.py', nt.F_OK), False)
        self.assertEqual(nt.access('does_not_exist.py', nt.R_OK), False)

        nt.chmod('new_file_name', 0x100) # S_IREAD
        self.assertEqual(nt.access('new_file_name', nt.W_OK), False)
        nt.chmod('new_file_name', 0x80)  # S_IWRITE

        nt.unlink('new_file_name')

        nt.mkdir('new_dir_name')
        self.assertEqual(nt.access('new_dir_name', nt.R_OK), True)
        nt.rmdir('new_dir_name')

        self.assertRaises(TypeError, nt.access, None, 1)

    def test_umask(self):
        orig = nt.umask(0)
        try:
            self.assertRaises(TypeError, nt.umask, 3.14)

            for i in [0, 1, 5, int((2**(31))-1)]:
                self.assertEqual(nt.umask(i), 0)

            self.assertRaises(OverflowError, nt.umask, 2**31)
            for i in [None,  "abc", 3j, int]:
                self.assertRaises(TypeError, nt.umask, i)

        finally:
            nt.umask(orig)

    def test_cp16413(self):
        tmpfile = 'tmpfile.tmp'
        f = open(tmpfile, 'w')
        f.close()
        nt.chmod(tmpfile, 0o777)
        nt.unlink(tmpfile)

    @unittest.skipUnless(sys.platform == "win32", "nt only")
    def test__getfullpathname(self):
        self.assertEqual(nt._getfullpathname('.'), nt.getcwd())
        self.assertEqual(nt._getfullpathname('<bad>'), os.path.join(nt.getcwd(), '<bad>'))
        self.assertEqual(nt._getfullpathname('bad:'), os.path.join(nt.getcwd(), 'bad:'))
        self.assertEqual(nt._getfullpathname(':bad:'), os.path.join(nt.getcwd(), ':bad:'))
        self.assertEqual(nt._getfullpathname('::'), '::\\')
        self.assertEqual(nt._getfullpathname('1:'), '1:\\')
        self.assertEqual(nt._getfullpathname('1:a'), '1:\\a')
        self.assertEqual(nt._getfullpathname('1::'), '1:\\:')
        self.assertEqual(nt._getfullpathname('1:\\'), '1:\\')

    @unittest.skipUnless(sys.platform == "win32", "nt only")
    def test__getfullpathname_neg(self):
        for bad in [None, 0, 34, -long(12345), 3.14, object, self.test__getfullpathname]:
            self.assertRaises(TypeError, nt._getfullpathname, bad)

    @unittest.skipIf(is_netcoreapp, 'TODO: figure out')
    @unittest.skipUnless(sys.platform == "win32", 'windir is Windows specific')
    def test_cp15514(self):
        cmd_variation_list = ['%s -c "print(__name__)"' % sys.executable,
                            '"%s -c "print(__name__)" "' % sys.executable,
                            ]
        cmd_cmd = os.path.join(os.environ["windir"], "system32", "cmd")
        for x in cmd_variation_list:
            ec = nt.spawnv(nt.P_WAIT, cmd_cmd , ["cmd", "/C", x])
            self.assertEqual(ec, 0)

    def test_strerror(self):
        test_dict = {
                        0: 'No error',
                        1: 'Operation not permitted',
                        2: 'No such file or directory',
                        3: 'No such process',
                        4: 'Interrupted function call',
                        5: 'Input/output error',
                        6: 'No such device or address',
                        7: 'Arg list too long',
                        8: 'Exec format error',
                        9: 'Bad file descriptor',
                        10: 'No child processes',
                        11: 'Resource temporarily unavailable',
                        12: 'Not enough space',
                        13: 'Permission denied',
                        14: 'Bad address',
                        16: 'Resource device',
                        17: 'File exists',
                        18: 'Improper link',
                        19: 'No such device',
                        20: 'Not a directory',
                        21: 'Is a directory',
                        22: 'Invalid argument',
                        23: 'Too many open files in system',
                        24: 'Too many open files',
                        25: 'Inappropriate I/O control operation',
                        27: 'File too large',
                        28: 'No space left on device',
                        29: 'Invalid seek',
                        30: 'Read-only file system',
                        31: 'Too many links',
                        32: 'Broken pipe',
                        33: 'Domain error',
                        34: 'Result too large',
                        36: 'Resource deadlock avoided',
                        38: 'Filename too long',
                        39: 'No locks available',
                        40: 'Function not implemented',
                        41: 'Directory not empty', 42: 'Illegal byte sequence'
                        }

        for key, value in test_dict.items():
            self.assertEqual(nt.strerror(key), value)

    def test_popen_cp34837(self):
        import subprocess
        p = subprocess.Popen("whoami", env=os.environ)
        self.assertTrue(p!=None)
        p.wait()
 
    def test_fsync(self):
        fsync_file_name = 'text_fsync.txt'
        fd = nt.open(fsync_file_name, nt.O_WRONLY | nt.O_CREAT)

        # negative test, make sure it raises on invalid (closed) fd
        try:
            nt.close(fd+1)
        except:
            pass
        self.assertRaises(OSError, nt.fsync, fd+1)

        # BUG (or implementation detail)
        # On a posix system, once written to a file descriptor
        # it can be read using another fd without any additional intervention.
        # In case of IronPython the data lingers in a stream which
        # is used to simulate file descriptor.
        fd2 = nt.open(fsync_file_name, nt.O_RDONLY)
        self.assertEqual(nt.read(fd2, 1), b'')

        nt.write(fd, b'1')
        if is_cli:
            self.assertEqual(nt.read(fd2, 1), b'') # this should be visible right away, but is not
        nt.fsync(fd)
        self.assertEqual(nt.read(fd2, 1), b'1')

        nt.close(fd)
        nt.close(fd2)

        # fsync on read file descriptor
        fd = nt.open(fsync_file_name, nt.O_RDONLY)
        if not is_cli:
            self.assertRaises(OSError, nt.fsync, fd)
        nt.close(fd)

        # fsync on rdwr file descriptor
        fd = nt.open(fsync_file_name, nt.O_RDWR)
        nt.fsync(fd)
        nt.close(fd)

        # fsync on derived fd
        if not is_cli:
            for mode in ('rb', 'r'):
                with open(fsync_file_name, mode) as f:
                    self.assertRaises(OSError, nt.fsync, f.fileno())

        for mode in ('wb', 'w'):
            with open(fsync_file_name, mode) as f:
                nt.fsync(f.fileno())

        nt.unlink(fsync_file_name)

        # fsync on pipe ends
        r,w = nt.pipe()
        if not is_cli:
            self.assertRaises(OSError, nt.fsync, r)
        nt.write(w, b'1')
        if False:
            nt.fsync(w) # this blocks
        nt.close(w)
        nt.close(r)

#------------------------------------------------------------------------------
try:
    run_test(__name__)
finally:
    #test cleanup - the test functions create the following directories and if any of them
    #fail, the directories may not necessarily be removed.  for this reason we try to remove
    #them again
    for temp_dir in ['dir_create_test', 'tsd', 'tmp2', 'newnamefile.txt']:
        try:
            nt.rmdir(temp_dir)
        except:
            pass

