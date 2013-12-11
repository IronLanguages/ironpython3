import sys

class warning(object):
    pass

class warning_trapper(object):
    def __init__(self):
        self.messages = []
        self.is_hooked = False
        self.hook()
    
    def __del__(self):
        self.unhook()
    
    def warn(self, message, category=None, stacklevel=1):
        msg = warning()
        msg.message = str(message)
        msg.category = category
        self.messages.append(msg)
    
    def warn_explicit(self, message, category, filename, lineno, module=None, registry=None, module_globals=None):
        self.warn(message, category)
    
    def get_value(self, dict, key, default=None):
        if not dict.has_key(key):
            return default
        return dict[key]
    
    def __enter__(self, *args):
        self.hook()
        return self
        
    def __exit__(self, *args):
        self.unhook()
    
    def hook(self):
        if self.is_hooked:
            return
        
        try:
            import warnings
            self.had_module = True
            self.old_warn = self.get_value(warnings.__dict__, 'warn')
            self.old_warn_explicit = self.get_value(warnings.__dict__, 'warn_explicit')
            try:
                warnings.resetwarnings()
            except:
                pass
        except:
            self.had_module = False
            warnings = type(sys)('warnings')
            sys.modules['warnings'] = warnings
        
        warnings.warn = self.warn
        warnings.warn_explicit = self.warn_explicit
        self.is_hooked = True
    
    def unhook(self):
        if not self.is_hooked:
            return
        if not self.had_module:
            del sys.modules['warnings']
        else:
            if self.old_warn:
              sys.modules['warnings'].warn = self.old_warn
            if self.old_warn_explicit:
              sys.modules['warnings'].warn_explicit = self.old_warn_explicit
        self.is_hooked = False
    
    def finish(self):
        self.unhook()
        return self.messages
