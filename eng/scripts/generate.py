# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import functools
import re
import sys
import os


def get_root_dir():
    script = os.path.realpath(__file__)
    return  os.path.normpath(os.path.join(os.path.dirname(script), "../.."))

root_dir = get_root_dir()

source_directories = [
    os.path.join(root_dir, "src"),
    os.path.join(root_dir, "src", "dlr", "Src"),
]

exclude_directories = [
    os.path.join(root_dir, "src", "dlr"),
    os.path.join(root_dir, "src", "core", "IronPython.StdLib"),
]

START_COMMON = "#region Generated"
START = START_COMMON + " %s"
END =   "#endregion"
PREFIX = r"^([ \t]*)"

class ConditionWriter:
    def __init__(self, cw):
        self.cw = cw
        self.first = True

    def condition(self, text=None, **kw):
        if self.first:
            self.first = False
            self.cw.enter_block(text, **kw)
        else:
            self.cw.else_block(text, **kw)

    def close(self):
        if not self.first:
            self.cw.exit_block()

class CodeWriter:
    def __init__(self, indent=0):
        self.lines = []
        self.__indent = indent
        self.kws = {}

    def begin_generated(self, generator):
        self.writeline()
        self.writeline("// *** BEGIN GENERATED CODE ***")
        filename = os.path.basename(generator.__code__.co_filename)
        self.writeline("// generated by function: " + generator.__name__ + " from: " + filename)

        self.writeline()

    def end_generated(self):
        self.writeline()
        self.writeline("// *** END GENERATED CODE ***")
        self.writeline()

    def indent(self): self.__indent += 1
    def dedent(self): self.__indent -= 1

    def writeline(self, text=None):
        if text is None or text.strip() == "":
            self.lines.append("")
        else:
            self.lines.append("    "*self.__indent + text)

    def write(self, template, **kw):
        if kw or self.kws:
            kw1 = self.kws.copy()
            kw1.update(kw)
            #print kw
            template = template % kw1
        for l in template.split('\n'):
            self.writeline(l)

    def enter_block(self, text=None, **kw):
        if text is not None:
            self.write(text + " {", **kw)
        self.indent()

    def else_block(self, text=None, **kw):
        self.dedent()
        if text:
            self.writeline("} else " + (text % kw) + " {")
        else:
            self.writeline("} else {")
        self.indent()

    def case_block(self, text=None, **kw):
        self.enter_block(text, **kw)
        self.indent()

    def case_label(self, text=None, **kw):
        self.write(text, **kw)
        self.indent()

    def exit_case_block(self):
        self.exit_block()
        self.dedent()

    def catch_block(self, text=None, **kw):
        self.dedent()
        if text:
            self.writeline("} catch " + (text % kw) + " {")
        else:
            self.writeline("} catch {")
        self.indent()

    def finally_block(self):
        self.dedent()
        self.writeline("} finally {")
        self.indent()

    def exit_block(self, text=None, **kw):
        self.dedent()
        if text:
            self.writeline("} " + text, **kw)
        else:
            self.writeline('}')

    def text(self):
        return '\n'.join(self.lines)

    def conditions(self):
        return ConditionWriter(self)

@functools.lru_cache()
def find_candidates(dirname):
    def listdir(dirname):
        if dirname in exclude_directories:
            return

        for file in os.listdir(dirname):
            if file == "obj": continue # obj folders are not interesting...
            filename = os.path.join(dirname, file)
            if os.path.isdir(filename):
                yield from listdir(filename)
            elif filename.endswith(".cs") and not file == "StandardTestStrings.cs": # TODO: fix encoding of StandardTestStrings.cs
                yield filename

    res = []
    for file in listdir(dirname):
        with open(file, encoding='latin-1') as f:
            if START_COMMON in f.read():
                res.append(file)
    return res

class CodeGenerator:
    def __init__(self, name, generator):
        self.generator = generator
        self.generators = []
        self.replacer = BlockReplacer(name)

    def do_file(self, filename):
        g = FileGenerator(filename, self.generator, self.replacer)
        if g.has_match:
            self.generators.append(g)

    def do_generate(self):
        if not self.generators:
            raise Exception("didn't find a match for %s" % self.replacer.name)

        result = []
        for g in self.generators:
            result.append(g.generate())
        return result

    def doit(self):
        for src_dir in source_directories:
            for file in find_candidates(src_dir):
                self.do_file(file)
        for g in self.generators:
            g.collect_info()
        return self.do_generate()

class BlockReplacer:
    def __init__(self, name):
        self.start = START % name
        self.end = END# % name
        #self.block_pat = re.compile(PREFIX+self.start+".*?"+self.end,
        #                            re.DOTALL|re.MULTILINE)
        self.name = name

    def match(self, text):
        #m = self.block_pat.search(text)
        #if m is None: return None
        #indent = m.group(1)
        #return indent

        startIndex = text.find(self.start)
        if startIndex != -1:
            origStart = startIndex
            # go to the beginning of the line on which self.start appears
            startIndex = text.rfind('\n', 0, startIndex) + 1

            # Some simple parsing logic that allows us to use regions within
            # our generated code
            if self.end == '#endregion':
                start = origStart + len(self.start)
                regionIndex = -1
                endregionIndex = -1
                count = 0 # number of '#region' encountered minus '#endregion'
                while True:
                    regionIndex = text.find('#region', start)
                    endregionIndex = text.find('#endregion', start)
                    if (endregionIndex >= 0 and
                        (endregionIndex < regionIndex or regionIndex == -1)):
                        if count == 0:
                            endIndex = endregionIndex
                            break
                        else:
                            count -= 1
                            start = endregionIndex + len("#endregion")
                            continue
                    if (regionIndex >= 0 and
                        (regionIndex < endregionIndex or endregionIndex == -1)):
                        count += 1
                        start = regionIndex + len("#region")
                        continue
                    if regionIndex == -1 and endregionIndex == -1:
                        # occurrences of '#region' outnumber #endregion"
                        endIndex = -1
                        break
            else:
                endIndex = text.find(self.end, startIndex)

            if endIndex != -1:
                indent = text[startIndex:origStart]
                return (indent, startIndex, endIndex+len(self.end))

        return None

    def replace(self, cw, text, indent):
        code = cw.lines
        code.insert(0, self.start)
        code.append(self.end)

        def should_indent(line):
            if not line: return False
            if line.startswith("#region"): return True
            if line.startswith("#endregion"): return True
            if line.startswith("#"): return False
            return True

       #code_text = '\n' + indent
        code_text = indent[0]
        delim = False
        for line in code:
            if delim:
                code_text += "\n"
                if should_indent(line):
                    code_text += indent[0]
            code_text += line
            delim = True

        #return self.block_pat.sub(code_text, text)
        #indicies = self.match(text)

        res = text[0:indent[1]] + code_text + text[indent[2]:len(text)]
        return res

def save_file(name, text):
    f = open(name, 'w', encoding='latin-1')
    f.write(text)
    f.close()

def texts_are_equivalent(texta, textb):
    """Compares two program texts by removing all identation and
    blank lines first."""

    def normalized_lines(text):
        for l in text.splitlines():
            l = l.strip()
            if l:
                yield l

    texta = "\n".join(normalized_lines(texta))
    textb = "\n".join(normalized_lines(textb))
    return texta == textb

class FileGenerator:
    def __init__(self, filename, generator, replacer):
        self.filename = filename
        self.generator = generator
        self.replacer = replacer

        with open(filename, encoding='latin-1') as thefile:
            self.text = thefile.read()
        self.indent = self.replacer.match(self.text)
        self.has_match = self.indent is not None

    def collect_info(self):
        pass

    def generate(self):
        print("generate", end=' ')
        if sys.argv.count('checkonly') > 0:
            print("(check-only)", end=' ')
        print(self.filename, "...", end=' ')

        cw = CodeWriter()
        cw.text = self.replacer.replace(CodeWriter(), self.text, self.indent)
        cw.begin_generated(self.generator)
        self.generator(cw)
        cw.end_generated()
        new_text = self.replacer.replace(cw, self.text, self.indent)
        if not texts_are_equivalent(self.text, new_text):
            if sys.argv.count('checkonly') > 0:
                print("different!")
                name = self.filename + ".diff"
                print("    generated file saved as: " + name)
                save_file(name, new_text)
                return False
            else:
                print("updated")
                save_file(self.filename, new_text)
        else:
            print("ok")

        return True

def generate(*g):
    result = []
    for name, func in g:
        run = CodeGenerator(name, func).doit()
        result.extend(run)
    return result
