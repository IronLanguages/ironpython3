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

from iptest.assert_util import *
skiptest("silverlight")
from iptest.file_util import *
import _random
from exceptions import IOError

import nt
import errno


AreEqual(nt.environ.has_key('COMPUTERNAME') or nt.environ.has_key('computername'), True)

# mkdir,listdir,rmdir,getcwd
def test_mkdir():
    nt.mkdir('dir_create_test')
    AreEqual(nt.listdir(nt.getcwd()).count('dir_create_test'), 1)
    
    nt.rmdir('dir_create_test')
    AreEqual(nt.listdir(nt.getcwd()).count('dir_create_test'), 0)

def test_mkdir_negative():
    nt.mkdir("dir_create_test")
    try:
        nt.mkdir("dir_create_test")
        AssertUnreachable("Cannot create the same directory twice")
    except WindowsError, e:
        AreEqual(e.errno, 17)
        
    #if it fails once...it should fail again
    AssertError(WindowsError, nt.mkdir, "dir_create_test")
    nt.rmdir('dir_create_test')
    nt.mkdir("dir_create_test")
    AssertError(WindowsError, nt.mkdir, "dir_create_test")
    nt.rmdir('dir_create_test')

def test_listdir():
    AssertError(TypeError, nt.listdir, None)
    if is_cpython: #http://ironpython.codeplex.com/workitem/28207
        AreEqual(nt.listdir(nt.getcwd()), nt.listdir('.'))
    else:
        AreEqual(nt.listdir(''), nt.listdir('.'))

# stat,lstat
def test_stat():
    # stat
    AssertError(nt.error, nt.stat, 'doesnotexist.txt')
        
    #lstat
    AssertError(nt.error, nt.lstat, 'doesnotexist.txt')

    AssertErrorWithNumber(WindowsError, 2, nt.stat, 'doesnotexist.txt')
    AssertErrorWithNumber(WindowsError, 22, nt.stat, 'bad?path.txt')

# stat should accept bytes as argument
def test_stat_cp34910():
    AreEqual(nt.stat('/'), nt.stat(b'/'))
    AreEqual(nt.lstat('/'), nt.lstat(b'/'))
 
    
# getcwdu test
def test_getcwdu():
    AreEqual(nt.getcwd(),nt.getcwdu())
    
    nt.mkdir('dir_create_test')
    AreEqual(nt.listdir(nt.getcwdu()).count('dir_create_test'), 1)
    nt.rmdir('dir_create_test')


# getpid test
def test_getpid():
    result = None
    result = nt.getpid()
    Assert(result>=0,
          "processPID should not be less than zero")
    
    result2 = nt.getpid()
    Assert(result2 == result,
           "The processPID in one process should be same")
 
 
# environ test
def test_environ():
    non_exist_key      = "_NOT_EXIST_"
    iron_python_string = "Iron_pythoN"

    try:
        nt.environ[non_exist_key]
        raise AssertionError
    except KeyError:
        pass

    # set
    nt.environ[non_exist_key] = iron_python_string
    AreEqual(nt.environ[non_exist_key], iron_python_string)
    
    import sys
    if is_cli:
        import System
        AreEqual(System.Environment.GetEnvironmentVariable(non_exist_key), iron_python_string)
    
    # update again
    swapped = iron_python_string.swapcase()
    nt.environ[non_exist_key] = swapped
    AreEqual(nt.environ[non_exist_key], swapped)
    if is_cli:
        AreEqual(System.Environment.GetEnvironmentVariable(non_exist_key), swapped)
        
    # remove
    del nt.environ[non_exist_key]
    if is_cli :
        AreEqual(System.Environment.GetEnvironmentVariable(non_exist_key), None)
    
    AreEqual(type(nt.environ), type({}))
 
    
# startfile
def test_startfile():
    AssertError(OSError, nt.startfile, "not_exist_file.txt")
    AssertError(WindowsError, nt.startfile, 'test_nt.py', 'bad')

# chdir tests
def test_chdir():
    currdir = nt.getcwd()
    nt.mkdir('tsd')
    nt.chdir('tsd')
    AreEqual(currdir+'\\tsd', nt.getcwd())
    nt.chdir(currdir)
    AreEqual(currdir, nt.getcwd())
    nt.rmdir('tsd')
    
    # the directory is empty or does not exist
    AssertErrorWithNumber(WindowsError, 22, lambda:nt.chdir(''))
    AssertErrorWithNumber(WindowsError, 2, lambda:nt.chdir('tsd'))

# fdopen tests
def test_fdopen():
    
    # IronPython does not implement the nt.dup function
    if not is_cli:
        fd_lambda = lambda x: nt.dup(x)
    else:
        AssertError(AttributeError, lambda: nt.dup)
        fd_lambda = lambda x: x
    
    # fd = 0    
    result = None
    result = nt.fdopen(fd_lambda(0),"r",1024)
    Assert(result!=None,"1,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(0),"w",2048)
    Assert(result!=None,"2,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(0),"a",512)
    Assert(result!=None,"3,The file object was not returned correctly")
    
    # fd = 1
    result = None
    result = nt.fdopen(fd_lambda(1),"a",1024)
    Assert(result!=None,"4,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(1),"r",2048)
    Assert(result!=None,"5,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(1),"w",512)
    Assert(result!=None,"6,The file object was not returned correctly")
    
    # fd = 2
    result = None
    result = nt.fdopen(fd_lambda(2),"r",1024)
    Assert(result!=None,"7,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(2),"a",2048)
    Assert(result!=None,"8,The file object was not returned correctly")
    
    result = None
    result = nt.fdopen(fd_lambda(2),"w",512)
    Assert(result!=None,"9,The file object was not returned correctly")
    
    if not is_cli:
        result.close()
         
    # The file descriptor is not valid
    AssertError(OSError,nt.fdopen,3000)
    AssertError(OSError,nt.fdopen,-1)
    AssertError(OSError,nt.fdopen,3000, "w")
    AssertError(OSError,nt.fdopen,3000, "w", 1024)
    
    # The file mode does not exist
    AssertError(ValueError,nt.fdopen,0,"p")
 
    stuff = "\x00a\x01\x02b\x03 \x04  \x05\n\x06_\0xFE\0xFFxyz"
    name = "cp5633.txt"
    fd = nt.open(name, nt.O_CREAT | nt.O_BINARY | nt.O_TRUNC | nt.O_WRONLY)
    f = nt.fdopen(fd, 'wb')
    f.write(stuff)
    f.close()
    f = file(name, 'rb')
    try:
        AreEqual(stuff, f.read())
    finally:
        f.close()
        nt.remove(name)
        
# fstat,unlink tests
def test_fstat():
    result = nt.fstat(1)
    Assert(result!=0,"0,The file stat object was not returned correctly")
    
    result = None
    tmpfile = "tmpfile1.tmp"
    f = open(tmpfile, "w")
    result = nt.fstat(f.fileno())
    Assert(result!=None,"0,The file stat object was not returned correctly")
    f.close()
    nt.unlink(tmpfile)
    
    # stdxx file descriptor
    AreEqual(10, len(nt.fstat(0)))
    AreEqual(10, len(nt.fstat(1)))
    AreEqual(10, len(nt.fstat(2)))
    
    # invalid file descriptor
    AssertError(OSError,nt.fstat,3000)
    AssertError(OSError,nt.fstat,-1)

def test_chmod():
    # chmod tests:
    # BUG 828,830
    nt.mkdir('tmp2')
    nt.chmod('tmp2', 256) # NOTE: change to flag when stat is implemented
    AssertError(OSError, lambda:nt.rmdir('tmp2'))
    nt.chmod('tmp2', 128)
    nt.rmdir('tmp2')
    # /BUG

################################################################################################
# popen/popen2/popen3/unlink tests

def test_popen():
    # open a pipe just for reading...
    pipe_modes = [["ping 127.0.0.1", "r"],
                  ["ping 127.0.0.1"]]
    if is_cli:
        pipe_modes.append(["ping 127.0.0.1", ""])
        
    for args in pipe_modes:
        x = nt.popen(*args)
        text = x.read()
        Assert(text.lower().index('pinging') != -1)
        AreEqual(x.close(), None)

    # write to a pipe
    x = nt.popen('sort', 'w')
    x.write('hello\nabc\n')
    x.close()

    # bug 1146
    #x = nt.popen('sort', 'w')
    #x.write('hello\nabc\n')
    #AreEqual(x.close(), None)

    # once w/ default mode
    AssertError(ValueError, nt.popen, "ping 127.0.0.1", "a")

    # popen uses cmd.exe to run stuff -- at least sometimes
    dir_pipe = nt.popen('dir')
    dir_pipe.read()
    dir_pipe.close()

    # once w/ no mode
    stdin, stdout = nt.popen2('sort')
    stdin.write('hello\nabc\n')
    AreEqual(stdin.close(), None)
    AreEqual(stdout.read(), 'abc\nhello\n')
    AreEqual(stdout.close(), None)

    # bug 1146
    # and once w/ each mode
    #for mode in ['b', 't']:
    #    stdin, stdout = nt.popen2('sort', mode)
    #    stdin.write('hello\nabc\n')
    #    AreEqual(stdin.close(), None)
    #    AreEqual(stdout.read(), 'abc\nhello\n')
    #    AreEqual(stdout.close(), None)
        

    # popen3: once w/ no mode
    stdin, stdout, stderr = nt.popen3('sort')
    stdin.write('hello\nabc\n')
    AreEqual(stdin.close(), None)
    AreEqual(stdout.read(), 'abc\nhello\n')
    AreEqual(stdout.close(), None)
    AreEqual(stderr.read(), '')
    AreEqual(stderr.close(), None)

    # bug 1146
    # popen3: and once w/ each mode
    #for mode in ['b', 't']:
    #    stdin, stdout, stderr = nt.popen3('sort', mode)
    #    stdin.write('hello\nabc\n')
    #    AreEqual(stdin.close(), None)
    #    AreEqual(stdout.read(), 'abc\nhello\n')
    #    AreEqual(stdout.close(), None)
    #    AreEqual(stderr.read(), '')
    #    AreEqual(stderr.close(), None)
    
    tmpfile = 'tmpfile.tmp'
    f = open(tmpfile, 'w')
    f.close()
    nt.unlink(tmpfile)
    try:
        nt.chmod('tmpfile.tmp', 256)
    except Exception:
        pass #should throw when trying to access file deleted by unlink
    else:
        Assert(False,"Error! Trying to access file deleted by unlink should have thrown.")

    try:
        tmpfile = "tmpfile2.tmp"
        f = open(tmpfile, "w")
        f.write("testing chmod")
        f.close()
        nt.chmod(tmpfile, 256)
        AssertError(OSError, nt.unlink, tmpfile)
        nt.chmod(tmpfile, 128)
        nt.unlink(tmpfile)
        AssertError(IOError, file, tmpfile)
    finally:
        try:
            nt.chmod(tmpfile, 128)
            nt.unlink(tmpfile)
        except Exception, e:
            print "exc", e

    # verify that nt.stat reports times in seconds, not ticks...

    import time
    tmpfile = 'tmpfile.tmp'
    f = open(tmpfile, 'w')
    f.close()
    t = time.time()
    mt = nt.stat(tmpfile).st_mtime
    nt.unlink(tmpfile) # this deletes the file
    Assert(abs(t-mt) < 60, "time differs by too much " + str(abs(t-mt)))

    tmpfile = 'tmpfile.tmp' # need to open it again since we deleted it with 'unlink'
    f = open(tmpfile, 'w')
    f.close()
    nt.chmod('tmpfile.tmp', 256)
    nt.chmod('tmpfile.tmp', 128)
    nt.unlink('tmpfile.tmp')

 
# utime tests
def test_utime():
    f = file('temp_file_does_not_exist.txt', 'w')
    f.close()
    import nt
    x = nt.stat('.')
    nt.utime('temp_file_does_not_exist.txt', (x[7], x[8]))
    y = nt.stat('temp_file_does_not_exist.txt')
    AreEqual(x[7], y[7])
    AreEqual(x[8], y[8])
    nt.unlink('temp_file_does_not_exist.txt')
    
def test_tempnam_broken_prefixes():
    for prefix in ["pre", None]:
        AreEqual(type(nt.tempnam("", prefix)), str)

def test_tempnam():
    '''
    '''
    #sanity checks
    AreEqual(type(nt.tempnam()), str)
    AreEqual(type(nt.tempnam("garbage name should still work")), str)
    
    #Very basic case
    joe = nt.tempnam()
    last_dir = joe.rfind("\\")
    temp_dir = joe[:last_dir+1]
    Assert(directory_exists(temp_dir))
    Assert(not file_exists(joe))
    
    #Basic case where we give it an existing directory and ensure
    #it uses that directory
    joe = nt.tempnam(get_temp_dir())
    last_dir = joe.rfind("\\")
    temp_dir = joe[:last_dir+1]
    Assert(directory_exists(temp_dir))
    Assert(not file_exists(joe))
    # The next line is not guaranteed to be true in some scenarios.
    #AreEqual(nt.stat(temp_dir.strip("\\")), nt.stat(get_temp_dir()))
    
    #few random prefixes
    prefix_names = ["", "a", "1", "_", ".", "sillyprefix",
                    "                                ",
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    ]
    #test a few directory names that shouldn't really work
    dir_names = ["b", "2", "_", ".", "anotherprefix",
                 "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                 None]
    
    for dir_name in dir_names:
        #just try the directory name on it's own
        joe = nt.tempnam(dir_name)
        last_dir = joe.rfind("\\")
        temp_dir = joe[:last_dir+1]
        Assert(directory_exists(temp_dir))
        Assert(not file_exists(joe))
        Assert(temp_dir != dir_name)
            
        #now try every prefix
        for prefix_name in prefix_names:
            joe = nt.tempnam(dir_name, prefix_name)
            last_dir = joe.rfind("\\")
            temp_dir = joe[:last_dir+1]
            file_name = joe[last_dir+1:]
            Assert(directory_exists(temp_dir))
            Assert(not file_exists(joe))
            Assert(temp_dir != dir_name)
            Assert(file_name.startswith(prefix_name))

@skip("cli", "silverlight") #CodePlex 24299
def test_tempnam_warning():
    with stderr_trapper() as trapper:
        temp = nt.tempnam()
    
    Assert(trapper.messages[0].endswith("RuntimeWarning: tempnam is a potential security risk to your program"), trapper.messages)

# BUG 8777,Should IronPython throw a warning when tmpnam is called ?
# tmpnam test
def test_tmpnam():
    str = nt.tmpnam()
    AreEqual(isinstance(str,type("string")),True)
    if is_cli:
        Assert(str.find(colon)!=-1,
               "1,the returned path is invalid")
        Assert(str.find(separator)!=-1,
               "2,the returned path is invalid")


# times test
def test_times():
    '''
    '''
    #simple sanity check
    utime, stime, zero1, zero2, zero3 = nt.times()
    Assert(utime>=0)
    Assert(stime>=0)
    AreEqual(zero1, 0)
    AreEqual(zero2, 0)
    #BUG - according to the specs this should be 0 for Windows
    #AreEqual(zero3, 0)
    

# putenv tests
def test_putenv():
    '''
    '''
    #simple sanity check
    nt.putenv("IPY_TEST_ENV_VAR", "xyz")
       
    #ensure it really does what it claims to do
    Assert(not nt.environ.has_key("IPY_TEST_ENV_VAR"))
    
    #negative cases
    AssertError(TypeError, nt.putenv, None, "xyz")
    #BUG
    #AssertError(TypeError, nt.putenv, "ABC", None)
    AssertError(TypeError, nt.putenv, 1, "xyz")
    AssertError(TypeError, nt.putenv, "ABC", 1)
  

# unsetenv tests
def test_unsetenv():
    #CPython nt has no unsetenv function
    #simple sanity check
    if is_cli:
        nt.putenv("ipy_test_env_var", "xyz")
        nt.unsetenv("ipy_test_env_var_unset")
        Assert(not nt.environ.has_key("ipy_test_env_var_unset"))
     

# remove tests
def test_remove():
    # remove an existing file
    handler = open("create_test_file.txt","w")
    handler.close()
    path1 = nt.getcwd()
    nt.remove(path1+'\\create_test_file.txt')
    AreEqual(nt.listdir(nt.getcwd()).count('create_test_file.txt'), 0)
    
    AssertErrorWithNumber(OSError, 2, nt.remove, path1+'\\create_test_file2.txt')
    AssertErrorWithNumber(OSError, 2, nt.unlink, path1+'\\create_test_file2.txt')
    AssertErrorWithNumber(OSError, 22, nt.remove, path1+'\\create_test_file?.txt')
    AssertErrorWithNumber(OSError, 22, nt.unlink, path1+'\\create_test_file?.txt')
    
    # the path is a type other than string
    AssertError(TypeError, nt.remove, 1)
    AssertError(TypeError, nt.remove, True)
    AssertError(TypeError, nt.remove, None)
  
def test_remove_negative():
    import stat
    AssertErrorWithNumber(WindowsError, errno.ENOENT, lambda : nt.remove('some_file_that_does_not_exist'))
    try:
        file('some_test_file.txt', 'w').close()
        nt.chmod('some_test_file.txt', stat.S_IREAD)
        AssertErrorWithNumber(WindowsError, errno.EACCES, lambda : nt.remove('some_test_file.txt'))
        nt.chmod('some_test_file.txt', stat.S_IWRITE)
        
        f = file('some_test_file.txt', 'w+')
        AssertErrorWithNumber(WindowsError, errno.EACCES, lambda : nt.remove('some_test_file.txt'))
        f.close()
    finally:
        nt.chmod('some_test_file.txt', stat.S_IWRITE)
        nt.unlink('some_test_file.txt')
        
        

# rename tests
def test_rename():
    # normal test
    handler = open("oldnamefile.txt","w")
    handler.close()
    str_old = "oldnamefile.txt"
    dst = "newnamefile.txt"
    nt.rename(str_old,dst)
    AreEqual(nt.listdir(nt.getcwd()).count(dst), 1)
    AreEqual(nt.listdir(nt.getcwd()).count(str_old), 0)
    nt.remove(dst)
    
    # the destination name is a directory
    handler = open("oldnamefile.txt","w")
    handler.close()
    str_old = "oldnamefile.txt"
    dst = "newnamefile.txt"
    nt.mkdir(dst)
    AssertError(OSError, nt.rename,str_old,dst)
    nt.rmdir(dst)
    nt.remove(str_old)
    
    # the dst already exists
    handler1 = open("oldnamefile.txt","w")
    handler1.close()
    handler2 = open("newnamefile.txt","w")
    handler2.close()
    str_old = "oldnamefile.txt"
    dst = "newnamefile.txt"
    AssertError(OSError, nt.rename,str_old,dst)
    nt.remove(str_old)
    nt.remove(dst)
    
    # the source file specified does not exist
    str_old = "oldnamefile.txt"
    dst = "newnamefile.txt"
    AssertError(OSError, nt.rename,str_old,dst)


# spawnle tests
def test_spawnle():
    '''
    '''
    #BUG?
    #CPython nt has no spawnle function
    if is_cli == False:
        return
    
    ping_cmd = get_environ_variable("windir") + "\system32\ping"
    
    #simple sanity check
    nt.spawnle(nt.P_WAIT, ping_cmd , "ping", "/?", {})
    #BUG - the first parameter of spawnle should be "ping"
    #nt.spawnle(nt.P_WAIT, ping_cmd , "ping", "127.0.0.1", {})
    #BUG - even taking "ping" out, multiple args do not work
    #pid = nt.spawnle(nt.P_NOWAIT, ping_cmd ,  "-n", "15", "-w", "1000", "127.0.0.1", {})
    
    #negative cases
    AssertError(TypeError, nt.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", None)
    AssertError(TypeError, nt.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", {1: "xyz"})
    AssertError(TypeError, nt.spawnle, nt.P_WAIT, ping_cmd , "ping", "/?", {"abc": 1})


# spawnl tests
def test_spawnl():
    if is_cli == False:
        return
    
    #sanity check
    #CPython nt has no spawnl function
    pint_cmd = ping_cmd = get_environ_variable("windir") + "\system32\ping.exe"
    nt.spawnl(nt.P_WAIT, ping_cmd , "ping","127.0.0.1")
    nt.spawnl(nt.P_WAIT, ping_cmd , "ping","/?")
    nt.spawnl(nt.P_WAIT, ping_cmd , "ping")
    
    # negative case
    cmd = pint_cmd+"oo"
    AssertError(OSError,nt.spawnl,nt.P_WAIT,cmd,"ping","/?")


# spawnve tests
def test_spawnv():
    #sanity check
    ping_cmd = get_environ_variable("windir") + "\system32\ping"
    nt.spawnv(nt.P_WAIT, ping_cmd , ["ping"])
    nt.spawnv(nt.P_WAIT, ping_cmd , ["ping","127.0.0.1"])
    nt.spawnv(nt.P_WAIT, ping_cmd, ["ping", "-n", "5", "-w", "5000", "127.0.0.1"])
    
        
# spawnve tests
def test_spawnve():
    '''
    '''
    ping_cmd = get_environ_variable("windir") + "\system32\ping"
    
    #simple sanity checks
    nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "/?"], {})
    nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "127.0.0.1"], {})
    nt.spawnve(nt.P_WAIT, ping_cmd, ["ping", "-n", "6", "-w", "1000", "127.0.0.1"], {})
    
    #negative cases
    AssertError(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], None)
    AssertError(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], {1: "xyz"})
    AssertError(TypeError, nt.spawnve, nt.P_WAIT, ping_cmd , ["ping", "/?"], {"abc": 1})
    
    
# tmpfile tests
#for some strange reason this fails on some Vista machines with an OSError related
#to permissions problems
@skip("win32")
def test_tmpfile():
    '''
    '''
    #sanity check
    joe = nt.tmpfile()
    AreEqual(type(joe), file)
    joe.close()


# waitpid tests
def test_waitpid():
    '''
    '''
    #sanity check
    ping_cmd = get_environ_variable("windir") + "\system32\ping"
    pid = nt.spawnv(nt.P_NOWAIT, ping_cmd ,  ["ping", "-n", "5", "-w", "1000", "127.0.0.1"])
    
    new_pid, exit_stat = nt.waitpid(pid, 0)
    
    #negative cases
    AssertErrorWithMessage(OSError, "[Errno 10] No child processes", nt.waitpid, -1234, 0)
        
    AssertError(TypeError, nt.waitpid, "", 0)


# stat_result test
def test_stat_result():
    #sanity check
    statResult = [0,1,2,3,4,5,6,7,8,9]
    object = None
    object = nt.stat_result(statResult)
    Assert(object != None,
           "The class did not return an object instance")
    AreEqual(object.st_uid,4)
    AreEqual(object.st_gid,5)
    AreEqual(object.st_nlink,3)
    AreEqual(object.st_dev,2)
    AreEqual(object.st_ino,1)
    AreEqual(object.st_mode,0)
    AreEqual(object.st_atime,7)
    AreEqual(object.st_mtime,8)
    AreEqual(object.st_ctime,9)
    
    if is_cli: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21917
        AreEqual(str(nt.stat_result(range(12))), "(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)") #CodePlex 8755
    else:
        AreEqual(str(nt.stat_result(range(12))), 
                 "nt.stat_result(st_mode=0, st_ino=1, st_dev=2, st_nlink=3, st_uid=4, st_gid=5, st_size=6, st_atime=7, st_mtime=8, st_ctime=9)") #CodePlex 8755
    
    #negative tests
    statResult = [0,1,2,3,4,5,6,7,8,]
    AssertError(TypeError,nt.stat_result,statResult)
    
    # this should not produce an error
    statResult = ["a","b","c","y","r","a","a","b","d","r","f"]
    x = nt.stat_result(statResult)
    AreEqual(x.st_mode, 'a')
    AreEqual(x.st_ino, 'b')
    AreEqual(x.st_dev, 'c')
    AreEqual(x.st_nlink, 'y')
    AreEqual(x.st_uid, 'r')
    AreEqual(x.st_gid, 'a')
    AreEqual(x.st_size, 'a')
    AreEqual(x.st_atime, 'f')
    AreEqual(x.st_mtime, 'd')
    AreEqual(x.st_ctime, 'r')
    
    # can pass dict to get values...
    x = nt.stat_result(xrange(10), {'st_atime': 23, 'st_mtime':42, 'st_ctime':2342})
    AreEqual(x.st_atime, 23)
    AreEqual(x.st_mtime, 42)
    AreEqual(x.st_ctime, 2342)
    
    # positional values take precedence over dict values
    x = nt.stat_result(xrange(13), {'st_atime': 23, 'st_mtime':42, 'st_ctime':2342})
    AreEqual(x.st_atime, 10)
    AreEqual(x.st_mtime, 11)
    AreEqual(x.st_ctime, 12)

    x = nt.stat_result(xrange(13))
    AreEqual(x.st_atime, 10)
    AreEqual(x.st_mtime, 11)
    AreEqual(x.st_ctime, 12)
    
    # other values are ignored...
    x = nt.stat_result(xrange(13), {'st_dev': 42, 'st_gid': 42, 'st_ino': 42, 'st_mode': 42, 'st_nlink': 42, 'st_size':42, 'st_uid':42})
    AreEqual(x.st_mode, 0)
    AreEqual(x.st_ino, 1)
    AreEqual(x.st_dev, 2)
    AreEqual(x.st_nlink, 3)
    AreEqual(x.st_uid, 4)
    AreEqual(x.st_gid, 5)
    AreEqual(x.st_size, 6)
    AreEqual(x.st_atime, 10)
    AreEqual(x.st_mtime, 11)
    AreEqual(x.st_ctime, 12)
    
    Assert(not isinstance(x, tuple))
    
    #--Misc
    
    #+
    x = nt.stat_result(range(10))
    AreEqual(x + (), x)
    AreEqual(x + tuple(x), tuple(range(10)*2))
    AssertError(TypeError, lambda: x + (1))
    AssertError(TypeError, lambda: x + 1)
    AssertError(TypeError, lambda: x + x)
    
    #> (list/object)
    Assert(nt.stat_result(range(10)) > None)
    Assert(nt.stat_result(range(10)) > 1)
    Assert(nt.stat_result(range(10)) > range(10))
    Assert(nt.stat_result([1 for x in range(10)]) > nt.stat_result(range(10)))
    Assert(not nt.stat_result(range(10)) > nt.stat_result(range(10)))
    Assert(not nt.stat_result(range(10)) > nt.stat_result(range(11)))
    Assert(not nt.stat_result(range(10)) > nt.stat_result([1 for x in range(10)]))
    Assert(not nt.stat_result(range(11)) > nt.stat_result(range(10)))
    
    #< (list/object)
    Assert(not nt.stat_result(range(10)) < None)
    Assert(not nt.stat_result(range(10)) < 1)
    Assert(not nt.stat_result(range(10)) < range(10))
    Assert(not nt.stat_result([1 for x in range(10)]) < nt.stat_result(range(10)))
    Assert(not nt.stat_result(range(10)) < nt.stat_result(range(10)))
    Assert(not nt.stat_result(range(10)) < nt.stat_result(range(11)))
    Assert(nt.stat_result(range(10)) < nt.stat_result([1 for x in range(10)]))
    Assert(not nt.stat_result(range(11)) < nt.stat_result(range(10)))
    
    #>= (list/object)
    Assert(nt.stat_result(range(10)) >= None)
    Assert(nt.stat_result(range(10)) >= 1)
    Assert(nt.stat_result(range(10)) >= range(10))
    Assert(nt.stat_result([1 for x in range(10)]) >= nt.stat_result(range(10)))
    Assert(nt.stat_result(range(10)) >= nt.stat_result(range(10)))
    Assert(nt.stat_result(range(10)) >= nt.stat_result(range(11)))
    Assert(not nt.stat_result(range(10)) >= nt.stat_result([1 for x in range(10)]))
    Assert(nt.stat_result(range(11)) >= nt.stat_result(range(10)))
    
    #<= (list/object)
    Assert(not nt.stat_result(range(10)) <= None)
    Assert(not nt.stat_result(range(10)) <= 1)
    Assert(not nt.stat_result(range(10)) <= range(10))
    Assert(not nt.stat_result([1 for x in range(10)]) <= nt.stat_result(range(10)))
    Assert(nt.stat_result(range(10)) <= nt.stat_result(range(10)))
    Assert(nt.stat_result(range(10)) <= nt.stat_result(range(11)))
    Assert(nt.stat_result(range(10)) <= nt.stat_result([1 for x in range(10)]))
    Assert(nt.stat_result(range(11)) <= nt.stat_result(range(10)))
    
    #* (size/stat_result)
    x = nt.stat_result(range(10))
    AreEqual(x * 1, tuple(x))
    AreEqual(x * 2, tuple(range(10)*2))
    AreEqual(1 * x, tuple(x))
    AreEqual(3 * x, tuple(range(10)*3))
    AssertError(TypeError, lambda: x * x)
    AssertError(TypeError, lambda: x * 3.14)
    AssertError(TypeError, lambda: x * None)
    AssertError(TypeError, lambda: x * "abc")
    AssertError(TypeError, lambda: "abc" * x)
    
    #__repr__
    x = nt.stat_result(range(10))
    if is_cpython:
        AreEqual(x.__repr__(),
                 "nt.stat_result(st_mode=0, st_ino=1, st_dev=2, st_nlink=3, st_uid=4, st_gid=5, st_size=6, st_atime=7, st_mtime=8, st_ctime=9)")
    else:
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21917
        AreEqual(x.__repr__(),
                 "(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)")
    
    #index get/set
    x = nt.stat_result(range(10))
    for i in xrange(10):
        AreEqual(x[i], i)
    
    def temp_func():        
        z = nt.stat_result(range(10))
        z[3] = 4
    AssertError(TypeError, temp_func)
    
    #__getslice__
    x = nt.stat_result(range(10))
    AreEqual(x[1:3], (1, 2))
    AreEqual(x[7:100], (7, 8, 9))
    AreEqual(x[7:-100], ())
    AreEqual(x[-101:-100], ())
    AreEqual(x[-2:8], ())
    AreEqual(x[-2:1000], (8,9))
    
    #__contains__
    x = nt.stat_result(range(10))
    for i in xrange(10):
        Assert(i in x)
        x.__contains__(i)
    Assert(-1 not in x)
    Assert(None not in x)
    Assert(20 not in x)
    
    #GetHashCode
    x = nt.stat_result(range(10))
    Assert(type(hash(x))==int)
    
    #IndexOf
    x = nt.stat_result(range(10))
    AreEqual(x.__getitem__(0), 0)
    AreEqual(x.__getitem__(3), 3)
    AreEqual(x.__getitem__(9), 9)
    AreEqual(x.__getitem__(-1), 9)
    AssertError(IndexError, lambda: x.__getitem__(10))
    AssertError(IndexError, lambda: x.__getitem__(11))
    
    #Insert
    x = nt.stat_result(range(10))
    AreEqual(x.__add__(()), tuple(x))
    AreEqual(x.__add__((1,2,3)), tuple(x) + (1, 2, 3))
    AssertError(TypeError, lambda: x.__add__(3))
    AssertError(TypeError, lambda: x.__add__(None))
    
    #Remove
    x = nt.stat_result(range(10))
    def temp_func():
        z = nt.stat_result(range(10))
        del z[3]
    AssertError(TypeError, temp_func)
    
    #enumerate
    x = nt.stat_result(range(10))
    temp_list = []
    for i in x:
        temp_list.append(i)
    AreEqual(tuple(x), tuple(temp_list))
    
    statResult = ["a","b","c","y","r","a","a","b","d","r","f"]
    x = nt.stat_result(statResult)
    temp_list = []
    for i in x:
        temp_list.append(i)
    AreEqual(tuple(x), tuple(temp_list))
    
    temp = Exception()
    statResult = [temp for i in xrange(10)]
    x = nt.stat_result(statResult)
    temp_list = []
    for i in x:
        temp_list.append(i)
    AreEqual(tuple(x), tuple(temp_list))
    

# urandom tests
def test_urandom():
    # argument n is a random int
    rand = _random.Random()
    n = rand.getrandbits(16)
    str = nt.urandom(n)
    result = len(str)
    AreEqual(isinstance(str,type("string")),True)
    AreEqual(n,result)


# write/read tests
def test_write():
    # write the file
    tempfilename = "temp.txt"
    file = open(tempfilename,"w")
    nt.write(file.fileno(),"Hello,here is the value of test string")
    file.close()
    
    # read from the file
    file =   open(tempfilename,"r")
    str = nt.read(file.fileno(),100)
    AreEqual(str,"Hello,here is the value of test string")
    file.close()
    nt.unlink(tempfilename)
    
    # BUG 8783 the argument buffersize in nt.read(fd, buffersize) is less than zero
    # the string written to the file is empty string
    tempfilename = "temp.txt"
    file = open(tempfilename,"w")
    nt.write(file.fileno(),"bug test")
    file.close()
    file = open(tempfilename,"r")
    AssertError(OSError,nt.read,file.fileno(),-10)
    file.close()
    nt.unlink(tempfilename)

# open test
def test_open():
    file('temp.txt', 'w+').close()
    try:
        fd = nt.open('temp.txt', nt.O_WRONLY | nt.O_CREAT)
        nt.close(fd)

        AssertErrorWithNumber(OSError, 17, nt.open, 'temp.txt', nt.O_CREAT | nt.O_EXCL)
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
        AssertError(OSError, nt.stat, 'temp.txt')
    
        # TODO: These tests should probably test more functionality regarding O_SEQUENTIAL/O_RANDOM
        f = nt.open('temp.txt', nt.O_TEMPORARY | nt.O_CREAT | nt.O_SEQUENTIAL | nt.O_RDWR)
        nt.close(f)
        AssertError(OSError, nt.stat, 'temp.txt')
        
        f = nt.open('temp.txt', nt.O_TEMPORARY | nt.O_CREAT | nt.O_RANDOM | nt.O_RDWR)
        nt.close(f)
        AssertError(OSError, nt.stat, 'temp.txt')
    finally:
        try:    
            # should fail if the file doesn't exist
            nt.unlink('temp.txt')
        except: 
            pass

def test_system_minimal():
    Assert(hasattr(nt, "system"))
    AreEqual(nt.system("ping localhost"), 0)
    AreEqual(nt.system('"ping localhost"'), 0)
    AreEqual(nt.system('"ping localhost'), 0)
        
    AreEqual(nt.system("ping"), 1)
    
    AreEqual(nt.system("some_command_which_is_not_available"), 1)

# flags test
def test_flags():
    AreEqual(nt.P_WAIT,0)
    AreEqual(nt.P_NOWAIT,1)
    AreEqual(nt.P_NOWAITO,3)
    AreEqual(nt.O_APPEND,8)
    AreEqual(nt.O_CREAT,256)
    AreEqual(nt.O_TRUNC,512)
    AreEqual(nt.O_EXCL,1024)
    AreEqual(nt.O_NOINHERIT,128)
    AreEqual(nt.O_RANDOM,16)
    AreEqual(nt.O_SEQUENTIAL,32)
    AreEqual(nt.O_SHORT_LIVED,4096)
    AreEqual(nt.O_TEMPORARY,64)
    AreEqual(nt.O_WRONLY,1)
    AreEqual(nt.O_RDONLY,0)
    AreEqual(nt.O_RDWR,2)
    AreEqual(nt.O_BINARY,32768)
    AreEqual(nt.O_TEXT,16384)

def test_access():
    f = file('new_file_name', 'w')
    f.close()
    
    AreEqual(nt.access('new_file_name', nt.F_OK), True)
    AreEqual(nt.access('does_not_exist.py', nt.F_OK), False)

    nt.chmod('new_file_name', 0x100) # S_IREAD
    AreEqual(nt.access('new_file_name', nt.W_OK), False)
    nt.chmod('new_file_name', 0x80)  # S_IWRITE
        
    nt.unlink('new_file_name')
    
    nt.mkdir('new_dir_name')
    AreEqual(nt.access('new_dir_name', nt.R_OK), True)
    nt.rmdir('new_dir_name')
    
    AssertError(TypeError, nt.access, None, 1)

def test_umask():
    orig = nt.umask(0)
    try:
       
        if is_cpython: #http://ironpython.codeplex.com/workitem/28208
            AssertError(TypeError, nt.umask, 3.14)
        else:
            AreEqual(nt.umask(3.14), 0)

        for i in [0, 1, 5, int((2**(31))-1)]:
            AreEqual(nt.umask(i), 0)
            
        AssertError(OverflowError, nt.umask, 2**31)
        for i in [None,  "abc", 3j, int]:
            AssertError(TypeError, nt.umask, i)
        
    finally:
        nt.umask(orig)

def test_cp16413():
    tmpfile = 'tmpfile.tmp'
    f = open(tmpfile, 'w')
    f.close()
    nt.chmod(tmpfile, 0777)
    nt.unlink(tmpfile)
    
def test__getfullpathname():
    AreEqual(nt._getfullpathname('.'), nt.getcwd())
    AreEqual(nt._getfullpathname('<bad>'), path_combine(nt.getcwd(), '<bad>'))
    AreEqual(nt._getfullpathname('bad:'), path_combine(nt.getcwd(), 'bad:'))
    AreEqual(nt._getfullpathname(':bad:'), path_combine(nt.getcwd(), ':bad:'))
    AreEqual(nt._getfullpathname('::'), '::\\')
    AreEqual(nt._getfullpathname('1:'), '1:\\')
    AreEqual(nt._getfullpathname('1:a'), '1:\\a')
    AreEqual(nt._getfullpathname('1::'), '1:\\:')
    AreEqual(nt._getfullpathname('1:\\'), '1:\\')
    
def test__getfullpathname_neg():
    for bad in [None, 0, 34, -12345L, 3.14, object, test__getfullpathname]:
        AssertError(TypeError, nt._getfullpathname, bad)

def test_cp15514():
    cmd_variation_list = ['%s -c "print __name__"' % sys.executable,
                          '"%s -c "print __name__""' % sys.executable,
                          ]
    cmd_cmd = get_environ_variable("windir") + "\system32\cmd"
    for x in cmd_variation_list:
        ec = nt.spawnv(nt.P_WAIT, cmd_cmd , ["cmd", "/C", 
                                             x])
        AreEqual(ec, 0)
   
def test_strerror():
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
                    
    for key, value in test_dict.iteritems():
        AreEqual(nt.strerror(key), value)


def test_popen_cp34837():
    import subprocess
    import os
    p = subprocess.Popen("whoami", env=os.environ)
    Assert(p!=None)
    p.wait()


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
