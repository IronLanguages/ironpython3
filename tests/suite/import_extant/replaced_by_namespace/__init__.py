import sys
import types

sys.modules[__name__] = types.SimpleNamespace(__name__=__name__, __path__=__path__, __spec__=__spec__)
