# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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

common_errno_codes = ['ENOENT', 'EBADF', 'EACCES', 'EINVAL', 'EMFILE']

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

def generate_O_flags(cw, flagvalues, access):
    for name in flagvalues.keys():
        codes = flagvalues[name]
        hidden_on = []
        supported_on = set(systems)
        cw.writeline()
        if codes[windows_idx] is None:
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
        if hidden_on:
            cw.write(f"[PythonHidden({', '.join(hidden_on)})]")
        if len(supported_on) != len(systems):
            for s in sorted(supported_on):
                cw.write(f'[SupportedOSPlatform("{s}")]')

        value = windows_code_expr(codes, fmt=hex)
        cw.write(f"{access} static int {name} => {value};")

def generate_all_O_flags(cw):
    generate_O_flags(cw, O_flagvalues, 'public')

common_O_flags = ['O_RDONLY', 'O_WRONLY', 'O_RDWR', 'O_APPEND', 'O_CREAT', 'O_TRUNC', 'O_EXCL', 'O_CLOEXEC', 'O_BINARY', 'O_NOINHERIT']

def generate_common_O_flags(cw):
    generate_O_flags(cw, OrderedDict((f, O_flagvalues[f]) for f in common_O_flags), 'private')


def main():
    return generate(
        ("Errno Codes", generate_errno_codes),
        ("Common Errno Codes", generate_common_errno_codes),
        ("Errno Names", generate_errno_names),
        ("O_Flags", generate_all_O_flags),
        ("Common O_Flags", generate_common_O_flags),
    )

if __name__ == "__main__":
    main()
