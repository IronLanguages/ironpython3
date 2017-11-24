
class KSubMod(object):
    static_member = 1
    def __init__(self):
        self.member = 2

FROM_SUB_MOD = KSubMod()

from cp15803 import mod
FROM_MOD_IN_SUBMOD = mod.KMod()
