# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from errno import errorcode
from generate import generate

num_systems = 3
linux_idx, macosx_idx, windows_idx = range(num_systems)

def set_value(errorval, name, value, index):
    if name not in errorval:
        errorval[name] = [None] * num_systems
    errorval[name][index] = value

def collect_errornames():
    # python3 -c 'import errno;print(dict(sorted(errno.errorcode.items())))'
    errorcode_linux   = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EAGAIN', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 15: 'ENOTBLK', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 26: 'ETXTBSY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 35: 'EDEADLOCK', 36: 'ENAMETOOLONG', 37: 'ENOLCK', 38: 'ENOSYS', 39: 'ENOTEMPTY', 40: 'ELOOP', 42: 'ENOMSG', 43: 'EIDRM', 44: 'ECHRNG', 45: 'EL2NSYNC', 46: 'EL3HLT', 47: 'EL3RST', 48: 'ELNRNG', 49: 'EUNATCH', 50: 'ENOCSI', 51: 'EL2HLT', 52: 'EBADE', 53: 'EBADR', 54: 'EXFULL', 55: 'ENOANO', 56: 'EBADRQC', 57: 'EBADSLT', 59: 'EBFONT', 60: 'ENOSTR', 61: 'ENODATA', 62: 'ETIME', 63: 'ENOSR', 64: 'ENONET', 65: 'ENOPKG', 66: 'EREMOTE', 67: 'ENOLINK', 68: 'EADV', 69: 'ESRMNT', 70: 'ECOMM', 71: 'EPROTO', 72: 'EMULTIHOP', 73: 'EDOTDOT', 74: 'EBADMSG', 75: 'EOVERFLOW', 76: 'ENOTUNIQ', 77: 'EBADFD', 78: 'EREMCHG', 79: 'ELIBACC', 80: 'ELIBBAD', 81: 'ELIBSCN', 82: 'ELIBMAX', 83: 'ELIBEXEC', 84: 'EILSEQ', 85: 'ERESTART', 86: 'ESTRPIPE', 87: 'EUSERS', 88: 'ENOTSOCK', 89: 'EDESTADDRREQ', 90: 'EMSGSIZE', 91: 'EPROTOTYPE', 92: 'ENOPROTOOPT', 93: 'EPROTONOSUPPORT', 94: 'ESOCKTNOSUPPORT', 95: 'ENOTSUP', 96: 'EPFNOSUPPORT', 97: 'EAFNOSUPPORT', 98: 'EADDRINUSE', 99: 'EADDRNOTAVAIL', 100: 'ENETDOWN', 101: 'ENETUNREACH', 102: 'ENETRESET', 103: 'ECONNABORTED', 104: 'ECONNRESET', 105: 'ENOBUFS', 106: 'EISCONN', 107: 'ENOTCONN', 108: 'ESHUTDOWN', 109: 'ETOOMANYREFS', 110: 'ETIMEDOUT', 111: 'ECONNREFUSED', 112: 'EHOSTDOWN', 113: 'EHOSTUNREACH', 114: 'EALREADY', 115: 'EINPROGRESS', 116: 'ESTALE', 117: 'EUCLEAN', 118: 'ENOTNAM', 119: 'ENAVAIL', 120: 'EISNAM', 121: 'EREMOTEIO', 122: 'EDQUOT', 123: 'ENOMEDIUM', 124: 'EMEDIUMTYPE', 125: 'ECANCELED', 126: 'ENOKEY', 127: 'EKEYEXPIRED', 128: 'EKEYREVOKED', 129: 'EKEYREJECTED', 130: 'EOWNERDEAD', 131: 'ENOTRECOVERABLE', 132: 'ERFKILL'}
    errorcode_macosx  = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EDEADLK', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 15: 'ENOTBLK', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 26: 'ETXTBSY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 35: 'EAGAIN', 36: 'EINPROGRESS', 37: 'EALREADY', 38: 'ENOTSOCK', 39: 'EDESTADDRREQ', 40: 'EMSGSIZE', 41: 'EPROTOTYPE', 42: 'ENOPROTOOPT', 43: 'EPROTONOSUPPORT', 44: 'ESOCKTNOSUPPORT', 45: 'ENOTSUP', 46: 'EPFNOSUPPORT', 47: 'EAFNOSUPPORT', 48: 'EADDRINUSE', 49: 'EADDRNOTAVAIL', 50: 'ENETDOWN', 51: 'ENETUNREACH', 52: 'ENETRESET', 53: 'ECONNABORTED', 54: 'ECONNRESET', 55: 'ENOBUFS', 56: 'EISCONN', 57: 'ENOTCONN', 58: 'ESHUTDOWN', 59: 'ETOOMANYREFS', 60: 'ETIMEDOUT', 61: 'ECONNREFUSED', 62: 'ELOOP', 63: 'ENAMETOOLONG', 64: 'EHOSTDOWN', 65: 'EHOSTUNREACH', 66: 'ENOTEMPTY', 67: 'EPROCLIM', 68: 'EUSERS', 69: 'EDQUOT', 70: 'ESTALE', 71: 'EREMOTE', 72: 'EBADRPC', 73: 'ERPCMISMATCH', 74: 'EPROGUNAVAIL', 75: 'EPROGMISMATCH', 76: 'EPROCUNAVAIL', 77: 'ENOLCK', 78: 'ENOSYS', 79: 'EFTYPE', 80: 'EAUTH', 81: 'ENEEDAUTH', 82: 'EPWROFF', 83: 'EDEVERR', 84: 'EOVERFLOW', 85: 'EBADEXEC', 86: 'EBADARCH', 87: 'ESHLIBVERS', 88: 'EBADMACHO', 89: 'ECANCELED', 90: 'EIDRM', 91: 'ENOMSG', 92: 'EILSEQ', 93: 'ENOATTR', 94: 'EBADMSG', 95: 'EMULTIHOP', 96: 'ENODATA', 97: 'ENOLINK', 98: 'ENOSR', 99: 'ENOSTR', 100: 'EPROTO', 101: 'ETIME', 102: 'EOPNOTSUPP', 103: 'ENOPOLICY', 104: 'ENOTRECOVERABLE', 105: 'EOWNERDEAD'}
    errorcode_windows = {1: 'EPERM', 2: 'ENOENT', 3: 'ESRCH', 4: 'EINTR', 5: 'EIO', 6: 'ENXIO', 7: 'E2BIG', 8: 'ENOEXEC', 9: 'EBADF', 10: 'ECHILD', 11: 'EAGAIN', 12: 'ENOMEM', 13: 'EACCES', 14: 'EFAULT', 16: 'EBUSY', 17: 'EEXIST', 18: 'EXDEV', 19: 'ENODEV', 20: 'ENOTDIR', 21: 'EISDIR', 22: 'EINVAL', 23: 'ENFILE', 24: 'EMFILE', 25: 'ENOTTY', 27: 'EFBIG', 28: 'ENOSPC', 29: 'ESPIPE', 30: 'EROFS', 31: 'EMLINK', 32: 'EPIPE', 33: 'EDOM', 34: 'ERANGE', 36: 'EDEADLOCK', 38: 'ENAMETOOLONG', 39: 'ENOLCK', 40: 'ENOSYS', 41: 'ENOTEMPTY', 42: 'EILSEQ', 104: 'EBADMSG', 105: 'ECANCELED', 111: 'EIDRM', 120: 'ENODATA', 121: 'ENOLINK', 122: 'ENOMSG', 124: 'ENOSR', 125: 'ENOSTR', 127: 'ENOTRECOVERABLE', 129: 'ENOTSUP', 132: 'EOVERFLOW', 133: 'EOWNERDEAD', 134: 'EPROTO', 137: 'ETIME', 139: 'ETXTBSY', 10000: 'WSABASEERR', 10004: 'WSAEINTR', 10009: 'WSAEBADF', 10013: 'WSAEACCES', 10014: 'WSAEFAULT', 10022: 'WSAEINVAL', 10024: 'WSAEMFILE', 10035: 'WSAEWOULDBLOCK', 10036: 'WSAEINPROGRESS', 10037: 'WSAEALREADY', 10038: 'WSAENOTSOCK', 10039: 'WSAEDESTADDRREQ', 10040: 'WSAEMSGSIZE', 10041: 'WSAEPROTOTYPE', 10042: 'WSAENOPROTOOPT', 10043: 'WSAEPROTONOSUPPORT', 10044: 'WSAESOCKTNOSUPPORT', 10045: 'WSAEOPNOTSUPP', 10046: 'WSAEPFNOSUPPORT', 10047: 'WSAEAFNOSUPPORT', 10048: 'WSAEADDRINUSE', 10049: 'WSAEADDRNOTAVAIL', 10050: 'WSAENETDOWN', 10051: 'WSAENETUNREACH', 10052: 'WSAENETRESET', 10053: 'WSAECONNABORTED', 10054: 'WSAECONNRESET', 10055: 'WSAENOBUFS', 10056: 'WSAEISCONN', 10057: 'WSAENOTCONN', 10058: 'WSAESHUTDOWN', 10059: 'WSAETOOMANYREFS', 10060: 'WSAETIMEDOUT', 10061: 'WSAECONNREFUSED', 10062: 'WSAELOOP', 10063: 'WSAENAMETOOLONG', 10064: 'WSAEHOSTDOWN', 10065: 'WSAEHOSTUNREACH', 10066: 'WSAENOTEMPTY', 10067: 'WSAEPROCLIM', 10068: 'WSAEUSERS', 10069: 'WSAEDQUOT', 10070: 'WSAESTALE', 10071: 'WSAEREMOTE', 10091: 'WSASYSNOTREADY', 10092: 'WSAVERNOTSUPPORTED', 10093: 'WSANOTINITIALISED', 10101: 'WSAEDISCON'}
    errorval = {}
    for code in errorcode_linux.keys():
        set_value(errorval, errorcode_linux[code], code,  linux_idx)
    for code in errorcode_macosx.keys():
        set_value(errorval, errorcode_macosx[code], code, macosx_idx)
    for code in errorcode_windows.keys():
        set_value(errorval, errorcode_windows[code], code, windows_idx)

    # WSA-codes are also available as E-codes if such code name is in use on Posix systems.
    for symbol in errorcode_windows.values():
        esymbol = symbol[3:]
        if symbol.startswith("WSAE") and esymbol in errorval.keys() and not errorval[esymbol][windows_idx]:
            errorval[esymbol][windows_idx] = errorval[symbol][windows_idx]

    return errorval

errorvalues = collect_errornames()

def generate_errno_codes(cw):
    def priority(codes, idx):
        return codes[idx] + idx / 10 if codes[idx] is not None else None

    names = sorted(errorvalues, key = lambda x: priority(errorvalues[x], linux_idx) or priority(errorvalues[x], macosx_idx) or priority(errorvalues[x], windows_idx))
    for name in names:
        codes = errorvalues[name]
        hidden_on = []
        cw.writeline()
        if not codes[windows_idx]:
            hidden_on = ["PlatformsAttribute.PlatformFamily.Windows"]
        if not codes[linux_idx] and not codes[macosx_idx]:
            assert not hidden_on, "Cannot hide on both Unix and Windows"
            hidden_on = ["PlatformsAttribute.PlatformFamily.Unix"]
        else:
            if not codes[linux_idx]:
                hidden_on += ["PlatformID.Unix"]
            if not codes[macosx_idx]:
                hidden_on += ["PlatformID.MacOSX"]
        if hidden_on:
            cw.write(f"[PythonHidden({', '.join(hidden_on)})]")

        value = windows_code_expr(codes)
        cw.write(f"public static int {name} => {value};")

def windows_code_expr(codes):
    if codes[windows_idx]:
        if not any(codes[:windows_idx]) or all(map(lambda x: x == codes[windows_idx], codes[:windows_idx])):
            return str(codes[windows_idx])
        else:
            return f"RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? {codes[windows_idx]} : {macosx_code_expr(codes)}"
    else:
        return macosx_code_expr(codes)

def macosx_code_expr(codes):
    if codes[macosx_idx]:
        if not any(codes[:macosx_idx]) or all(map(lambda x: x == codes[macosx_idx], codes[:macosx_idx])):
            return str(codes[macosx_idx])
        else:
            return f"RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? {codes[macosx_idx]} : {linux_code_expr(codes)}"
    else:
        return linux_code_expr(codes)

def linux_code_expr(codes):
    return str(codes[linux_idx])

def generate_errno_names(cw):
    cw.write("// names defined on all platforms")
    common_names = sorted(name for name in errorvalues if all(errorvalues[name]) and ("WSA" + name not in errorvalues or errorvalues["WSA" + name][windows_idx] != errorvalues[name][windows_idx]))
    for name in common_names:
        cw.write(f'errorcode[{name}] = "{name}";')

    cw.write("// names defined on Posix platforms")
    posix_names = sorted(name for name in errorvalues if errorvalues[name][linux_idx] and errorvalues[name][macosx_idx] and name not in common_names)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))")
    for name in posix_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on Linux")
    linux_names = sorted(name for name in errorvalues if errorvalues[name][linux_idx] and name not in common_names + posix_names)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))")
    for name in linux_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on macOS")
    macosx_names = sorted(name for name in errorvalues if errorvalues[name][macosx_idx] and name not in common_names + posix_names)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))")
    for name in macosx_names:
        cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

    cw.write("// names defined on Windows")
    windows_names = sorted(name for name in errorvalues if errorvalues[name][windows_idx] and name not in common_names)
    cw.enter_block("if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))")
    for name in windows_names:
        #if name.startswith("WSA") or "WSA" + name not in windows_names:
            cw.write(f'errorcode[{name}] = "{name}";')
    cw.exit_block()

def main():
    return generate(
        ("Errno Codes", generate_errno_codes),
        ("Errno Names", generate_errno_names),
    )

if __name__ == "__main__":
    main()
