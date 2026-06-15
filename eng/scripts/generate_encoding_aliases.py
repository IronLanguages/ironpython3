# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import encodings
import itertools
import sys

from generate import generate

def gen_aliases(cw):
    cw.writeline("// Based on encodings.aliases")
    cw.enter_block("var d = new Dictionary<string, string>")

    aliases = encodings.aliases.aliases
    aliases_36 = {
        "kz_1048": "kz1048",
        "rk1048": "kz1048",
        "strk1048_2002": "kz1048",
    }
    codecs_36 = {v: "PYTHON_36_OR_GREATER" for v in aliases_36.values()}
    if sys.version_info >= (3, 6):
        for k, v in aliases_36.items():
            assert aliases[k] == v
    aliases = {**aliases, **aliases_36}

    sorted_aliases = sorted(aliases.items(), key=lambda kv: (kv[1], kv[0]))
    for codec, group in itertools.groupby(sorted_aliases, key=lambda kv: kv[1]):
        condition = codecs_36.get(codec)

        e = encodings.search_function(codec)
        if not condition and (e is None or not e._is_text_encoding or e.name == "mbcs"):
            continue

        cw.writeline()
        if condition:
            cw.writeline("#if {0}".format(condition))
        for alias, codec in group:
            qalias = '"{0}"'.format(alias)
            qcodec = '"{0}"'.format(codec)
            cw.writeline('{{ {0:24} , {1:24} }},'.format(qalias, qcodec))
        if condition:
            cw.writeline("#endif")

    cw.exit_block(";")

def main():
    return generate(
        ("encoding aliases", gen_aliases)
    )

if __name__ == "__main__":
    main()
