# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import encodings

from generate import generate

def gen_aliases(cw):
    cw.writeline("// Based on encodings.aliases")

    codecs = sorted(set(encodings.aliases.aliases.values()))
    for codec in codecs:
        e = encodings.search_function(codec)
        if not e or not e._is_text_encoding:
            continue
        cw.writeline()
        aliases = sorted(alias for alias, aliased_codec in encodings.aliases.aliases.items() if aliased_codec == codec)
        for alias in aliases:
            qalias = '"{0}"'.format(alias)
            qcodec = '"{0}"'.format(codec)
            cw.writeline('{{ {0:24} , {1:24} }},'.format(qalias, qcodec))

def main():
    return generate(
        ("encoding aliases", gen_aliases)
    )

if __name__ == "__main__":
    main()
