# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import re
from generate import generate
from collections import OrderedDict

systems = ['linux', 'macos', 'windows']
linux_idx, darwin_idx, windows_idx = range(len(systems))

# python3 -c 'import errno;print(dict(sorted(errno.errorcode.items())))'
errorcode_linux   = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EAGAIN', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 15: 'ENOTBLK', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 26: 'ETXTBSY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 35: 'EDEADLOCK', 36: 'ENAMETOOLONG', 37: 'ENOLCK', 38: 'ENOSYS', 39: 'ENOTEMPTY', 40: 'ELOOP', 42: 'ENOMSG', 43: 'EIDRM', 44: 'ECHRNG', 45: 'EL2NSYNC', 46: 'EL3HLT', 47: 'EL3RST', 48: 'ELNRNG', 49: 'EUNATCH', 50: 'ENOCSI', 51: 'EL2HLT', 52: 'EBADE', 53: 'EBADR', 54: 'EXFULL', 55: 'ENOANO', 56: 'EBADRQC', 57: 'EBADSLT', 59: 'EBFONT', 60: 'ENOSTR', 61: 'ENODATA', 62: 'ETIME', 63: 'ENOSR', 64: 'ENONET', 65: 'ENOPKG', 66: 'EREMOTE', 67: 'ENOLINK', 68: 'EADV', 69: 'ESRMNT', 70: 'ECOMM', 71: 'EPROTO', 72: 'EMULTIHOP', 73: 'EDOTDOT', 74: 'EBADMSG', 75: 'EOVERFLOW', 76: 'ENOTUNIQ', 77: 'EBADFD', 78: 'EREMCHG', 79: 'ELIBACC', 80: 'ELIBBAD', 81: 'ELIBSCN', 82: 'ELIBMAX', 83: 'ELIBEXEC', 84: 'EILSEQ', 85: 'ERESTART', 86: 'ESTRPIPE', 87: 'EUSERS', 88: 'ENOTSOCK', 89: 'EDESTADDRREQ', 90: 'EMSGSIZE', 91: 'EPROTOTYPE', 92: 'ENOPROTOOPT', 93: 'EPROTONOSUPPORT', 94: 'ESOCKTNOSUPPORT', 95: 'ENOTSUP', 96: 'EPFNOSUPPORT', 97: 'EAFNOSUPPORT', 98: 'EADDRINUSE', 99: 'EADDRNOTAVAIL', 100: 'ENETDOWN', 101: 'ENETUNREACH', 102: 'ENETRESET', 103: 'ECONNABORTED', 104: 'ECONNRESET', 105: 'ENOBUFS', 106: 'EISCONN', 107: 'ENOTCONN', 108: 'ESHUTDOWN', 109: 'ETOOMANYREFS', 110: 'ETIMEDOUT', 111: 'ECONNREFUSED', 112: 'EHOSTDOWN', 113: 'EHOSTUNREACH', 114: 'EALREADY', 115: 'EINPROGRESS', 116: 'ESTALE', 117: 'EUCLEAN', 118: 'ENOTNAM', 119: 'ENAVAIL', 120: 'EISNAM', 121: 'EREMOTEIO', 122: 'EDQUOT', 123: 'ENOMEDIUM', 124: 'EMEDIUMTYPE', 125: 'ECANCELED', 126: 'ENOKEY', 127: 'EKEYEXPIRED', 128: 'EKEYREVOKED', 129: 'EKEYREJECTED', 130: 'EOWNERDEAD', 131: 'ENOTRECOVERABLE', 132: 'ERFKILL'}
errorcode_darwin  = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EDEADLK', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 15: 'ENOTBLK', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 26: 'ETXTBSY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 35: 'EAGAIN', 36: 'EINPROGRESS', 37: 'EALREADY', 38: 'ENOTSOCK', 39: 'EDESTADDRREQ', 40: 'EMSGSIZE', 41: 'EPROTOTYPE', 42: 'ENOPROTOOPT', 43: 'EPROTONOSUPPORT', 44: 'ESOCKTNOSUPPORT', 45: 'ENOTSUP', 46: 'EPFNOSUPPORT', 47: 'EAFNOSUPPORT', 48: 'EADDRINUSE', 49: 'EADDRNOTAVAIL', 50: 'ENETDOWN', 51: 'ENETUNREACH', 52: 'ENETRESET', 53: 'ECONNABORTED', 54: 'ECONNRESET', 55: 'ENOBUFS', 56: 'EISCONN', 57: 'ENOTCONN', 58: 'ESHUTDOWN', 59: 'ETOOMANYREFS', 60: 'ETIMEDOUT', 61: 'ECONNREFUSED', 62: 'ELOOP', 63: 'ENAMETOOLONG', 64: 'EHOSTDOWN', 65: 'EHOSTUNREACH', 66: 'ENOTEMPTY', 67: 'EPROCLIM', 68: 'EUSERS', 69: 'EDQUOT', 70: 'ESTALE', 71: 'EREMOTE', 72: 'EBADRPC', 73: 'ERPCMISMATCH', 74: 'EPROGUNAVAIL', 75: 'EPROGMISMATCH', 76: 'EPROCUNAVAIL', 77: 'ENOLCK', 78: 'ENOSYS', 79: 'EFTYPE', 80: 'EAUTH', 81: 'ENEEDAUTH', 82: 'EPWROFF', 83: 'EDEVERR', 84: 'EOVERFLOW', 85: 'EBADEXEC', 86: 'EBADARCH', 87: 'ESHLIBVERS', 88: 'EBADMACHO', 89: 'ECANCELED', 90: 'EIDRM', 91: 'ENOMSG', 92: 'EILSEQ', 93: 'ENOATTR', 94: 'EBADMSG', 95: 'EMULTIHOP', 96: 'ENODATA', 97: 'ENOLINK', 98: 'ENOSR', 99: 'ENOSTR', 100: 'EPROTO', 101: 'ETIME', 102: 'EOPNOTSUPP', 103: 'ENOPOLICY', 104: 'ENOTRECOVERABLE', 105: 'EOWNERDEAD'}
errorcode_windows = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EAGAIN', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 36: 'EDEADLOCK', 38: 'ENAMETOOLONG', 39: 'ENOLCK', 40: 'ENOSYS', 41: 'ENOTEMPTY', 42: 'EILSEQ', 104: 'EBADMSG', 105: 'ECANCELED', 111: 'EIDRM', 120: 'ENODATA', 121: 'ENOLINK', 122: 'ENOMSG', 124: 'ENOSR', 125: 'ENOSTR', 127: 'ENOTRECOVERABLE', 129: 'ENOTSUP', 132: 'EOVERFLOW', 133: 'EOWNERDEAD', 134: 'EPROTO', 137: 'ETIME', 139: 'ETXTBSY', 10000: 'WSABASEERR', 10004: 'WSAEINTR', 10009: 'WSAEBADF', 10013: 'WSAEACCES', 10014: 'WSAEFAULT', 10022: 'WSAEINVAL', 10024: 'WSAEMFILE', 10035: 'WSAEWOULDBLOCK', 10036: 'WSAEINPROGRESS', 10037: 'WSAEALREADY', 10038: 'WSAENOTSOCK', 10039: 'WSAEDESTADDRREQ', 10040: 'WSAEMSGSIZE', 10041: 'WSAEPROTOTYPE', 10042: 'WSAENOPROTOOPT', 10043: 'WSAEPROTONOSUPPORT', 10044: 'WSAESOCKTNOSUPPORT', 10045: 'WSAEOPNOTSUPP', 10046: 'WSAEPFNOSUPPORT', 10047: 'WSAEAFNOSUPPORT', 10048: 'WSAEADDRINUSE', 10049: 'WSAEADDRNOTAVAIL', 10050: 'WSAENETDOWN', 10051: 'WSAENETUNREACH', 10052: 'WSAENETRESET', 10053: 'WSAECONNABORTED', 10054: 'WSAECONNRESET', 10055: 'WSAENOBUFS', 10056: 'WSAEISCONN', 10057: 'WSAENOTCONN', 10058: 'WSAESHUTDOWN', 10059: 'WSAETOOMANYREFS', 10060: 'WSAETIMEDOUT', 10061: 'WSAECONNREFUSED', 10062: 'WSAELOOP', 10063: 'WSAENAMETOOLONG', 10064: 'WSAEHOSTDOWN', 10065: 'WSAEHOSTUNREACH', 10066: 'WSAENOTEMPTY', 10067: 'WSAEPROCLIM', 10068: 'WSAEUSERS', 10069: 'WSAEDQUOT', 10070: 'WSAESTALE', 10071: 'WSAEREMOTE', 10091: 'WSASYSNOTREADY', 10092: 'WSAVERNOTSUPPORTED', 10093: 'WSANOTINITIALISED', 10101: 'WSAEDISCON'}
linux_aliases = {'EOPNOTSUPP' : 'ENOTSUP', 'EDEADLK': 'EDEADLOCK', 'EWOULDBLOCK': 'EAGAIN'}
darwin_aliases = {'EWOULDBLOCK': 'EAGAIN'}
aliases = {**linux_aliases, **darwin_aliases}


def set_value(codeval, name, value, index):
    if name not in codeval:
        codeval[name] = [None] * len(systems)
    codeval[name][index] = value


def collect_errornames():
    errorval = {}
    for code in errorcode_linux:
        set_value(errorval, errorcode_linux[code], code,  linux_idx)
    for code in errorcode_darwin:
        set_value(errorval, errorcode_darwin[code], code, darwin_idx)
    for code in errorcode_windows:
        set_value(errorval, errorcode_windows[code], code, windows_idx)

    # WSA-codes are also available as E-codes if such code name is in use on Posix systems.
    known_symbols = set(errorval) | set(aliases)
    for symbol in errorcode_windows.values():
        esymbol = symbol[3:]
        if symbol.startswith("WSAE") and esymbol in known_symbols and (esymbol not in errorval or not errorval[esymbol][windows_idx]):
            set_value(errorval, esymbol, errorval[symbol][windows_idx], windows_idx)

    # set aliases
    for alias, target in aliases.items():
        for idx in range(len(systems)):
            if errorval[target][idx] and not errorval[alias][idx]:
                errorval[alias][idx] = errorval[target][idx]

    return errorval

errorvalues = collect_errornames()

def generate_errno_codes(cw):
    def priority(error):
        codes = errorvalues[error]
        shifts = 0
        while codes[0] is None:
            codes = codes[1:] + [0]
            shifts += 1
        res = 0
        for code in codes:
            res *= 100000
            res += code if code else 0
        return res + shifts * 100000 ** (len(systems) - 1) // 10

    names = sorted(errorvalues, key = priority)
    for name in names:
        codes = errorvalues[name]
        hidden_on = []
        cw.writeline()
        if not codes[windows_idx]:
            hidden_on = ["PlatformsAttribute.PlatformFamily.Windows"]
        if not codes[linux_idx] and not codes[darwin_idx]:
            assert not hidden_on, "Cannot hide on both Unix and Windows"
            hidden_on = ["PlatformsAttribute.PlatformFamily.Unix"]
        else:
            if not codes[linux_idx]:
                hidden_on += ["PlatformID.Unix"]
            if not codes[darwin_idx]:
                hidden_on += ["PlatformID.MacOSX"]
        if hidden_on:
            cw.write(f"[PythonHidden({', '.join(hidden_on)})]")

        value = windows_code_expr(codes, str)
        cw.write(f"public static int {name} => {value};")

def windows_code_expr(codes, fmt):
    if codes[windows_idx] is not None:
        if all(c is None for c in codes[:windows_idx]) or all(x == codes[windows_idx] for x in codes[:windows_idx]):
            return fmt(codes[windows_idx])
        else:
            return f"RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? {fmt(codes[windows_idx])} : {darwin_code_expr(codes, fmt)}"
    else:
        return darwin_code_expr(codes, fmt)

def darwin_code_expr(codes, fmt):
    if codes[darwin_idx] is not None:
        if all(c is None for c in codes[:darwin_idx]) or all(x == codes[darwin_idx] for x in codes[:darwin_idx]):
            return fmt(codes[darwin_idx])
        else:
            return f"RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? {fmt(codes[darwin_idx])} : {linux_code_expr(codes, fmt)}"
    else:
        return linux_code_expr(codes, fmt)

def linux_code_expr(codes, fmt):
    return fmt(codes[linux_idx])


common_errno_codes = ['ENOENT', 'E2BIG', 'ENOEXEC', 'EBADF', 'ECHILD', 'EAGAIN', 'ENOMEM', 'EACCES', 'EEXIST', 'EXDEV', 'ENOTDIR', 'EMFILE', 'ENOSPC', 'EPIPE', 'ENOTEMPTY', 'EILSEQ', 'EINVAL']

def generate_common_errno_codes(cw):
    for name in common_errno_codes:
        codes = errorvalues[name]

        value = windows_code_expr(codes, str)
        if (all(c.isdigit() for c in value)):
            cw.write(f"internal const int {name} = {value};")
        else:
            cw.write(f"internal static int {name} => {value};")


def generate_errno_names(cw):
    def is_windows_alias(name):
        return "WSA" + name in errorvalues and name in errorvalues and errorvalues["WSA" + name][windows_idx] == errorvalues[name][windows_idx]

    cw.write("// names defined on all platforms")
    exclusions = set(aliases)
    common_names = sorted(name for name in errorvalues if all(errorvalues[name]) and not is_windows_alias(name) and name not in exclusions)
    for name in common_names:
        cw.write(f'errorcode[{name}] = "{name}";')

    cw.write("// names defined on Posix platforms")
    exclusions =  set(common_names) | set(aliases)
    posix_names = sorted(name for name in errorvalues if errorvalues[name][linux_idx] and errorvalues[name][darwin_idx] and name not in exclusions)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))")
    for name in posix_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on Linux")
    exclusions =  set(common_names) | set(posix_names) | set(linux_aliases)
    linux_names = sorted(name for name in errorvalues if errorvalues[name][linux_idx] and name not in exclusions)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))")
    for name in linux_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on macOS")
    exclusions =  set(common_names) | set(posix_names) | set(darwin_aliases)
    darwin_names = sorted(name for name in errorvalues if errorvalues[name][darwin_idx] and name not in exclusions)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))")
    for name in darwin_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on Windows")
    windows_names = sorted(name for name in errorvalues if errorvalues[name][windows_idx] and name not in common_names)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))")
    for name in windows_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()


# python3 -c 'import os;print(dict(sorted((s, getattr(os, s)) for s in dir(os) if s.startswith("O_"))))'
O_flags_linux = {'O_ACCMODE': 3, 'O_APPEND': 1024, 'O_ASYNC': 8192, 'O_CLOEXEC': 524288, 'O_CREAT': 64, 'O_DIRECT': 16384, 'O_DIRECTORY': 65536, 'O_DSYNC': 4096, 'O_EXCL': 128, 'O_FSYNC': 1052672, 'O_LARGEFILE': 0, 'O_NDELAY': 2048, 'O_NOATIME': 262144, 'O_NOCTTY': 256, 'O_NOFOLLOW': 131072, 'O_NONBLOCK': 2048, 'O_PATH': 2097152, 'O_RDONLY': 0, 'O_RDWR': 2, 'O_RSYNC': 1052672, 'O_SYNC': 1052672, 'O_TMPFILE': 4259840, 'O_TRUNC': 512, 'O_WRONLY': 1}
O_flags_darwin = {'O_ACCMODE': 3, 'O_APPEND': 8, 'O_ASYNC': 64, 'O_CLOEXEC': 16777216, 'O_CREAT': 512, 'O_DIRECTORY': 1048576, 'O_DSYNC': 4194304, 'O_EXCL': 2048, 'O_EXEC': 1073741824, 'O_EXLOCK': 32, 'O_NDELAY': 4, 'O_NOCTTY': 131072, 'O_NOFOLLOW': 256, 'O_NONBLOCK': 4, 'O_RDONLY': 0, 'O_RDWR': 2, 'O_SEARCH': 1074790400, 'O_SHLOCK': 16, 'O_SYNC': 128, 'O_TRUNC': 1024, 'O_WRONLY': 1}
O_flags_windows = {'O_APPEND': 8, 'O_BINARY': 32768, 'O_CREAT': 256, 'O_EXCL': 1024, 'O_NOINHERIT': 128, 'O_RANDOM': 16, 'O_RDONLY': 0, 'O_RDWR': 2, 'O_SEQUENTIAL': 32, 'O_SHORT_LIVED': 4096, 'O_TEMPORARY': 64, 'O_TEXT': 16384, 'O_TRUNC': 512, 'O_WRONLY': 1}

O_flags_optional = {'O_ASYNC', 'O_DIRECT', 'O_DIRECTORY', 'O_NOFOLLOW', 'O_NOATIME', 'O_PATH', 'O_TMPFILE', 'O_SHLOCK', 'O_EXLOCK'}
O_flags_python3_10 = {'O_EVTONLY', 'O_FSYNC', 'O_SYMLINK', 'O_NOFOLLOW_ANY'}

def collect_codes():
    codeval = {}
    for name in O_flags_linux:
        set_value(codeval, name, O_flags_linux[name], linux_idx)
    for name in O_flags_darwin:
        set_value(codeval, name, O_flags_darwin[name], darwin_idx)
    for name in O_flags_windows:
        set_value(codeval, name, O_flags_windows[name], windows_idx)
    for name in O_flags_optional | O_flags_python3_10:
        codeval.pop(name, None)
    return OrderedDict(sorted(codeval.items()))

O_flagvalues = collect_codes()


def generate_codes(cw, codeval, access, fmt, unix_only=False):
    for name in codeval.keys():
        codes = codeval[name]
        all_systems = set(systems)
        if unix_only:
            all_systems.discard(systems[windows_idx])
        hidden_on = []
        supported_on = set(all_systems)
        cw.writeline()
        if codes[windows_idx] is None and not unix_only:
            hidden_on = ["PlatformsAttribute.PlatformFamily.Windows"]
            supported_on.discard(systems[windows_idx])
        if codes[linux_idx] is None and codes[darwin_idx] is None:
            assert not hidden_on, "Cannot hide on both Unix and Windows"
            hidden_on = ["PlatformsAttribute.PlatformFamily.Unix"]
            supported_on.discard(systems[linux_idx])
            supported_on.discard(systems[darwin_idx])
        else:
            if codes[linux_idx] is None:
                hidden_on += ["PlatformID.Unix"]
                supported_on.discard(systems[linux_idx])
            if codes[darwin_idx] is None:
                hidden_on += ["PlatformID.MacOSX"]
                supported_on.discard(systems[darwin_idx])
        if hidden_on and (access == 'public' or access == 'protected' or access == 'protected internal'):
            cw.write(f"[PythonHidden({', '.join(hidden_on)})]")
        if len(supported_on) != len(all_systems):
            for s in sorted(supported_on):
                cw.write(f'[SupportedOSPlatform("{s}")]')

        value = windows_code_expr(codes, fmt)
        typ = "int"
        for match in re.findall(r'0x[0-9a-fA-F]+', value):
            n = eval(match)
            if n > 2**31 - 1 or n < -2**31:
                typ = "long"
                break
        cw.write(f"{access} static {typ} {name} => {value};")


def generate_all_O_flags(cw):
    generate_codes(cw, O_flagvalues, 'public', hex)


common_O_flags = ['O_RDONLY', 'O_WRONLY', 'O_RDWR', 'O_APPEND', 'O_CREAT', 'O_TRUNC', 'O_EXCL', 'O_CLOEXEC', 'O_BINARY', 'O_NOINHERIT']

def generate_common_O_flags(cw):
    generate_codes(cw, OrderedDict((f, O_flagvalues[f]) for f in common_O_flags), 'private', hex)


# python3 -c 'import fcntl;print(dict(sorted((s, getattr(fcntl, s)) for s in dir(fcntl) if s.startswith("F_"))))'
# Python 3.6.15 [GCC 12.2.0] on linux 6.10.14
FD_commands_linux = {'F_ADD_SEALS': 1033, 'F_DUPFD': 0, 'F_DUPFD_CLOEXEC': 1030, 'F_EXLCK': 4, 'F_GETFD': 1, 'F_GETFL': 3, 'F_GETLEASE': 1025, 'F_GETLK': 5, 'F_GETLK64': 5, 'F_GETOWN': 9, 'F_GETPIPE_SZ': 1032, 'F_GETSIG': 11, 'F_GET_SEALS': 1034, 'F_NOTIFY': 1026, 'F_OFD_GETLK': 36, 'F_OFD_SETLK': 37, 'F_OFD_SETLKW': 38, 'F_RDLCK': 0, 'F_SEAL_GROW': 4, 'F_SEAL_SEAL': 1, 'F_SEAL_SHRINK': 2, 'F_SEAL_WRITE': 8, 'F_SETFD': 2, 'F_SETFL': 4, 'F_SETLEASE': 1024, 'F_SETLK': 6, 'F_SETLK64': 6, 'F_SETLKW': 7, 'F_SETLKW64': 7, 'F_SETOWN': 8, 'F_SETPIPE_SZ': 1031, 'F_SETSIG': 10, 'F_SHLCK': 8, 'F_UNLCK': 2, 'F_WRLCK': 1}
# Unsupported by Mono.Unix 7.1.0-final.1.21458.1 on linux
FD_commands_linux_unsupported = ['F_DUPFD_CLOEXEC', 'F_GETPIPE_SZ', 'F_SETPIPE_SZ']
# Python 3.7.0 [Clang 4.0.1 ] on darwin 24.2.0
FD_commands_darwin = {'F_DUPFD': 0, 'F_DUPFD_CLOEXEC': 67, 'F_FULLFSYNC': 51, 'F_GETFD': 1, 'F_GETFL': 3, 'F_GETLK': 7, 'F_GETOWN': 5, 'F_NOCACHE': 48, 'F_RDLCK': 1, 'F_SETFD': 2, 'F_SETFL': 4, 'F_SETLK': 8, 'F_SETLKW': 9, 'F_SETOWN': 6, 'F_UNLCK': 2, 'F_WRLCK': 3}
# Unsupported by Mono.Unix 7.1.0-final.1.21458.1 on darwin
FD_commands_darwin_unsupported = ['F_DUPFD_CLOEXEC', 'F_FULLFSYNC']

def generate_FD_commands(cw):
    codeval = {}
    for name in FD_commands_linux:
        if name not in FD_commands_linux_unsupported:
            set_value(codeval, name, FD_commands_linux[name], linux_idx)
    for name in FD_commands_darwin:
        if name not in FD_commands_darwin_unsupported:
            set_value(codeval, name, FD_commands_darwin[name], darwin_idx)
    codeval = OrderedDict(sorted(codeval.items()))
    generate_codes(cw, codeval, 'public', str, unix_only=True)


# python3 -c 'import fcntl;print(dict(sorted((s, getattr(fcntl, s)) for s in dir(fcntl) if s.startswith("DN_"))))'
# Python 3.6.15 [GCC 12.2.0] on linux 6.10.14
# Python 3.12.3 [GCC 13.2.0] on linux 6.8.0
DN_flags_linux = {'DN_ACCESS': 1, 'DN_ATTRIB': 32, 'DN_CREATE': 4, 'DN_DELETE': 8, 'DN_MODIFY': 2, 'DN_MULTISHOT': 2147483648, 'DN_RENAME': 16}

def generate_DN_flags(cw):
    codeval = {}
    for name in DN_flags_linux:
        set_value(codeval, name, DN_flags_linux[name], linux_idx)
    codeval = OrderedDict(sorted(codeval.items()))
    generate_codes(cw, codeval, 'public', hex, unix_only=True)


# python3 -c 'import fcntl;print(dict(sorted((s, getattr(fcntl, s)) for s in dir(fcntl) if s.startswith("LOCK_"))))'
# Python 3.6.15 [GCC 12.2.0] on linux 6.10.14
# Python 3.12.3 [GCC 13.2.0] on linux 6.8.0
LOCK_flags_linux = {'LOCK_EX': 2, 'LOCK_MAND': 32, 'LOCK_NB': 4, 'LOCK_READ': 64, 'LOCK_RW': 192, 'LOCK_SH': 1, 'LOCK_UN': 8, 'LOCK_WRITE': 128}
# Python 3.7.0 [Clang 4.0.1 ] on darwin 24.2.0
# Python 3.12.0 [Clang 14.0.6 ] on darwin 24.2.0
LOCK_flags_darwin = {'LOCK_EX': 2, 'LOCK_NB': 4, 'LOCK_SH': 1, 'LOCK_UN': 8}

def generate_LOCK_flags(cw):
    codeval = {}
    for name in LOCK_flags_linux:
        set_value(codeval, name, LOCK_flags_linux[name], linux_idx)
    for name in LOCK_flags_darwin:
        set_value(codeval, name, LOCK_flags_darwin[name], darwin_idx)
    codeval = OrderedDict(sorted(codeval.items()))
    generate_codes(cw, codeval, 'public', hex, unix_only=True)


# python3 -c 'import termios;print(dict(sorted((s, getattr(termios, s)) for s in dir(termios) if s.startswith("TIOC"))))'
# Python 3.6.15 [GCC 12.2.0] on linux 6.10.14
# Python 3.12.3 [GCC 13.2.0] on linux 6.8.0
TIOC_cmd_linux = {'TIOCCONS': 21533, 'TIOCEXCL': 21516, 'TIOCGETD': 21540, 'TIOCGICOUNT': 21597, 'TIOCGLCKTRMIOS': 21590, 'TIOCGPGRP': 21519, 'TIOCGSERIAL': 21534, 'TIOCGSOFTCAR': 21529, 'TIOCGWINSZ': 21523, 'TIOCINQ': 21531, 'TIOCLINUX': 21532, 'TIOCMBIC': 21527, 'TIOCMBIS': 21526, 'TIOCMGET': 21525, 'TIOCMIWAIT': 21596, 'TIOCMSET': 21528, 'TIOCM_CAR': 64, 'TIOCM_CD': 64, 'TIOCM_CTS': 32, 'TIOCM_DSR': 256, 'TIOCM_DTR': 2, 'TIOCM_LE': 1, 'TIOCM_RI': 128, 'TIOCM_RNG': 128, 'TIOCM_RTS': 4, 'TIOCM_SR': 16, 'TIOCM_ST': 8, 'TIOCNOTTY': 21538, 'TIOCNXCL': 21517, 'TIOCOUTQ': 21521, 'TIOCPKT': 21536, 'TIOCPKT_DATA': 0, 'TIOCPKT_DOSTOP': 32, 'TIOCPKT_FLUSHREAD': 1, 'TIOCPKT_FLUSHWRITE': 2, 'TIOCPKT_NOSTOP': 16, 'TIOCPKT_START': 8, 'TIOCPKT_STOP': 4, 'TIOCSCTTY': 21518, 'TIOCSERCONFIG': 21587, 'TIOCSERGETLSR': 21593, 'TIOCSERGETMULTI': 21594, 'TIOCSERGSTRUCT': 21592, 'TIOCSERGWILD': 21588, 'TIOCSERSETMULTI': 21595, 'TIOCSERSWILD': 21589, 'TIOCSER_TEMT': 1, 'TIOCSETD': 21539, 'TIOCSLCKTRMIOS': 21591, 'TIOCSPGRP': 21520, 'TIOCSSERIAL': 21535, 'TIOCSSOFTCAR': 21530, 'TIOCSTI': 21522, 'TIOCSWINSZ': 21524}
# Python 3.12.0 [Clang 14.0.6 ] on darwin 24.2.0
TIOC_cmd_darwin = {'TIOCCONS': 2147775586, 'TIOCEXCL': 536900621, 'TIOCGETD': 1074033690, 'TIOCGPGRP': 1074033783, 'TIOCGSIZE': 1074295912, 'TIOCGWINSZ': 1074295912, 'TIOCMBIC': 2147775595, 'TIOCMBIS': 2147775596, 'TIOCMGET': 1074033770, 'TIOCMSET': 2147775597, 'TIOCM_CAR': 64, 'TIOCM_CD': 64, 'TIOCM_CTS': 32, 'TIOCM_DSR': 256, 'TIOCM_DTR': 2, 'TIOCM_LE': 1, 'TIOCM_RI': 128, 'TIOCM_RNG': 128, 'TIOCM_RTS': 4, 'TIOCM_SR': 16, 'TIOCM_ST': 8, 'TIOCNOTTY': 536900721, 'TIOCNXCL': 536900622, 'TIOCOUTQ': 1074033779, 'TIOCPKT': 2147775600, 'TIOCPKT_DATA': 0, 'TIOCPKT_DOSTOP': 32, 'TIOCPKT_FLUSHREAD': 1, 'TIOCPKT_FLUSHWRITE': 2, 'TIOCPKT_NOSTOP': 16, 'TIOCPKT_START': 8, 'TIOCPKT_STOP': 4, 'TIOCSCTTY': 536900705, 'TIOCSETD': 2147775515, 'TIOCSPGRP': 2147775606, 'TIOCSSIZE': 2148037735, 'TIOCSTI': 2147578994, 'TIOCSWINSZ': 2148037735}

def generate_TIOC_commands(cw):
    codeval = {}
    for name in TIOC_cmd_linux:
        set_value(codeval, name, TIOC_cmd_linux[name], linux_idx)
    for name in TIOC_cmd_darwin:
        set_value(codeval, name, TIOC_cmd_darwin[name], darwin_idx)
    codeval = OrderedDict(sorted(codeval.items()))
    generate_codes(cw, codeval, 'public', hex, unix_only=True)


# python3 -c 'import signal;print(dict(sorted((s, int(getattr(signal, s))) for s in dir(signal) if s.startswith("SIG") and not s.startswith("SIG_"))))'
# Python 3.12.3 [GCC 13.3.0] on linux 6.8.0
SIG_codes_linux = {'SIGABRT': 6, 'SIGALRM': 14, 'SIGBUS': 7, 'SIGCHLD': 17, 'SIGCLD': 17, 'SIGCONT': 18, 'SIGFPE': 8, 'SIGHUP': 1, 'SIGILL': 4, 'SIGINT': 2, 'SIGIO': 29, 'SIGIOT': 6, 'SIGKILL': 9, 'SIGPIPE': 13, 'SIGPOLL': 29, 'SIGPROF': 27, 'SIGPWR': 30, 'SIGQUIT': 3, 'SIGRTMAX': 64, 'SIGRTMIN': 34, 'SIGSEGV': 11, 'SIGSTKFLT': 16, 'SIGSTOP': 19, 'SIGSYS': 31, 'SIGTERM': 15, 'SIGTRAP': 5, 'SIGTSTP': 20, 'SIGTTIN': 21, 'SIGTTOU': 22, 'SIGURG': 23, 'SIGUSR1': 10, 'SIGUSR2': 12, 'SIGVTALRM': 26, 'SIGWINCH': 28, 'SIGXCPU': 24, 'SIGXFSZ': 25}
# Python 3.9.6 [Clang 17.0.0] on darwin 24.3.0
SIG_codes_darwin = {'SIGABRT': 6, 'SIGALRM': 14, 'SIGBUS': 10, 'SIGCHLD': 20, 'SIGCONT': 19, 'SIGEMT': 7, 'SIGFPE': 8, 'SIGHUP': 1, 'SIGILL': 4, 'SIGINFO': 29, 'SIGINT': 2, 'SIGIO': 23, 'SIGIOT': 6, 'SIGKILL': 9, 'SIGPIPE': 13, 'SIGPROF': 27, 'SIGQUIT': 3, 'SIGSEGV': 11, 'SIGSTOP': 17, 'SIGSYS': 12, 'SIGTERM': 15, 'SIGTRAP': 5, 'SIGTSTP': 18, 'SIGTTIN': 21, 'SIGTTOU': 22, 'SIGURG': 16, 'SIGUSR1': 30, 'SIGUSR2': 31, 'SIGVTALRM': 26, 'SIGWINCH': 28, 'SIGXCPU': 24, 'SIGXFSZ': 25}
# Python 3.6.8 [MSC v.1916 64 bit (AMD64)] on win32
SIG_codes_windows = {'SIGABRT': 22, 'SIGBREAK': 21, 'SIGFPE': 8, 'SIGILL': 4, 'SIGINT': 2, 'SIGSEGV': 11, 'SIGTERM': 15}

def generate_signal_codes(cw):
    codeval = {}
    for idx, SIG_codes in [(linux_idx, SIG_codes_linux), (darwin_idx, SIG_codes_darwin), (windows_idx, SIG_codes_windows)]:
        for name in SIG_codes:
            set_value(codeval, name, SIG_codes[name], idx)
    codeval = OrderedDict(sorted(codeval.items()))
    generate_codes(cw, codeval, 'public', str)


def generate_supported_signals(cw):
    for system, SIG_codes in [("Linux", SIG_codes_linux), ("MacOS", SIG_codes_darwin), ("Windows", SIG_codes_windows)]:
        cw.writeline(f'[SupportedOSPlatform("{system.lower()}")]')
        cw.writeline(f'private static readonly int[] _PySupportedSignals_{system} = [')
        cw.indent()
        cw.writeline(', '.join(sorted(SIG_codes)))
        cw.dedent()
        cw.writeline('];')
        cw.writeline()


def main():
    return generate(
        ("Errno Codes", generate_errno_codes),
        ("Common Errno Codes", generate_common_errno_codes),
        ("Errno Names", generate_errno_names),
        ("O_Flags", generate_all_O_flags),
        ("Common O_Flags", generate_common_O_flags),
        ("FD Commands", generate_FD_commands),
        ("Directory Notify Flags", generate_DN_flags),
        ("LOCK Flags", generate_LOCK_flags),
        ("TIOC Commands", generate_TIOC_commands),
        ("Signal Codes", generate_signal_codes),
        ("Supported Signals", generate_supported_signals),
    )

if __name__ == "__main__":
    main()
