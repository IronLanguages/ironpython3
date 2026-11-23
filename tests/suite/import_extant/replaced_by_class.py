import sys

class ReplacedByClass: pass

sys.modules[__name__] = ReplacedByClass
