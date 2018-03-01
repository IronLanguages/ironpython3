#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the License.html file at the root of this distribution. If 
# you cannot locate the  Apache License, Version 2.0, please send an email to 
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

__all__ = ["ClrClass", "ClrInterface", "accepts", "returns", "attribute", "propagate_attributes"]

import clr
clr.AddReference("Microsoft.Dynamic")
clr.AddReference("Microsoft.Scripting")
clr.AddReference("IronPython")

if clr.IsNetCoreApp:
    clr.AddReference("System.Reflection.Emit")

import System
from System import Char, Void, Boolean, Array, Type, AppDomain
from System.Reflection import FieldAttributes, MethodAttributes, PropertyAttributes, ParameterAttributes
from System.Reflection import CallingConventions, TypeAttributes, AssemblyName
from System.Reflection.Emit import OpCodes, CustomAttributeBuilder, AssemblyBuilder, AssemblyBuilderAccess
from System.Runtime.InteropServices import DllImportAttribute, CallingConvention, CharSet
from Microsoft.Scripting.Generation import Snippets
from Microsoft.Scripting.Runtime import DynamicOperations
from Microsoft.Scripting.Utils import ReflectionUtils
from IronPython.Runtime import NameType, PythonContext
from IronPython.Runtime.Types import PythonType, ReflectedField, ReflectedProperty

def validate_clr_types(signature_types, var_signature = False):
    if not isinstance(signature_types, tuple):
        signature_types = (signature_types,)
    for t in signature_types:
        if type(t) is type(System.IComparable): # type overloaded on generic arity, eg IComparable and IComparable[T]
            t = t[()] # select non-generic version
        clr_type = clr.GetClrType(t)
        if t == Void: 
            raise TypeError("Void cannot be used in signature")
        is_typed = clr.GetPythonType(clr_type) == t
        # is_typed needs to be weakened until the generated type
        # gets explicitly published as the underlying CLR type
        is_typed = is_typed or (hasattr(t, "__metaclass__") and t.__metaclass__ in [ClrInterface, ClrClass])
        if not is_typed:
            raise Exception, "Invalid CLR type %s" % str(t)
        if not var_signature:
            if clr_type.IsByRef:
                raise TypeError("Byref can only be used as arguments and locals")
            # ArgIterator is not present in Silverlight
            if hasattr(System, "ArgIterator") and t == System.ArgIterator:
                raise TypeError("Stack-referencing types can only be used as arguments and locals")

class TypedFunction(object):
    """
    A strongly-typed function can get wrapped up as a staticmethod, a property, etc.
    This class represents the raw function, but with the type information
    it is decorated with. 
    Other information is stored as attributes on the function. See propagate_attributes
    """
    def __init__(self, function, is_static = False, prop_name_if_prop_get = None, prop_name_if_prop_set = None):
        self.function = function
        self.is_static = is_static
        self.prop_name_if_prop_get = prop_name_if_prop_get
        self.prop_name_if_prop_set = prop_name_if_prop_set

class ClrType(type):
    """
    Base metaclass for creating strongly-typed CLR types
    """

    def is_typed_method(self, function):
        if hasattr(function, "arg_types") != hasattr(function, "return_type"):
            raise TypeError("One of @accepts and @returns is missing for %s" % function.func_name)

        return hasattr(function, "arg_types")

    def get_typed_properties(self):
        for item_name, item in self.__dict__.items():
            if isinstance(item, property):
                if item.fget:
                    if not self.is_typed_method(item.fget): continue
                    prop_type = item.fget.return_type
                else:
                    if not self.is_typed_method(item.fset): continue
                    prop_type = item.fset.arg_types[0]
                validate_clr_types(prop_type)
                clr_prop_type = clr.GetClrType(prop_type)
                yield item, item_name, clr_prop_type

    def emit_properties(self, typebld):
        for prop, prop_name, clr_prop_type in self.get_typed_properties():
            self.emit_property(typebld, prop, prop_name, clr_prop_type)

    def emit_property(self, typebld, prop, name, clrtype):	
        prpbld = typebld.DefineProperty(name, PropertyAttributes.None, clrtype, None)
        if prop.fget:
            getter = self.emitted_methods[(prop.fget.func_name, prop.fget.arg_types)]
            prpbld.SetGetMethod(getter)
        if prop.fset:
            setter = self.emitted_methods[(prop.fset.func_name, prop.fset.arg_types)]
            prpbld.SetSetMethod(setter)

    def dummy_function(self): raise RuntimeError("this should not get called")
    
    def get_typed_methods(self):
        """
        Get all the methods with @accepts (and @returns) decorators
        Functions are assumed to be instance methods, unless decorated with @staticmethod
        """
        
        # We avoid using the "types" library as it is not a builtin
        FunctionType = type(ClrType.__dict__["dummy_function"])

        for item_name, item in self.__dict__.items():
            function = None
            is_static = False
            if isinstance(item, FunctionType):
                function, is_static = item, False
            elif isinstance(item, staticmethod):
                function, is_static = getattr(self, item_name), True
            elif isinstance(item, property):
                if item.fget and self.is_typed_method(item.fget):
                    if item.fget.func_name == item_name:
                        # The property hides the getter. So yield the getter
                        yield TypedFunction(item.fget, False, item_name, None)
                if item.fset and self.is_typed_method(item.fset):
                    if item.fset.func_name == item_name:
                        # The property hides the setter. So yield the setter
                        yield TypedFunction(item.fset, False, None, item_name)
                continue
            else:
                continue
            if self.is_typed_method(function):
                yield TypedFunction(function, is_static)

    def emit_methods(self, typebld):
        # We need to track the generated methods so that we can emit properties
        # referring these methods. 
        # Also, the hash is indexed by name *and signature*. Even though Python does 
        # not have method overloading, property getter and setter functions can have 
        # the same func_name attribute
        self.emitted_methods = {}
        for function_info in self.get_typed_methods():
            method_builder = self.emit_method(typebld, function_info)
            function = function_info.function
            if self.emitted_methods.has_key((function.func_name, function.arg_types)):
                raise TypeError("methods with clashing names")
            self.emitted_methods[(function.func_name, function.arg_types)] = method_builder

    def emit_classattribs(self, typebld):
        if hasattr(self, '_clrclassattribs'):
            for attrib_info in self._clrclassattribs:
                if isinstance(attrib_info, type):
                    ci = clr.GetClrType(attrib_info).GetConstructor(())
                    cab = CustomAttributeBuilder(ci, ())
                elif isinstance(attrib_info, CustomAttributeDecorator):
                    cab = attrib_info.GetBuilder()
                else:
                    make_decorator = attrib_info()
                    cab = make_decorator.GetBuilder()
                typebld.SetCustomAttribute(cab)

    def get_clr_type_name(self):
        if hasattr(self, "_clrnamespace"):
            return self._clrnamespace + "." + self.__name__
        else:
            return self.__name__

    def create_type(self, typebld):
        self.emit_members(typebld)	
        new_type = typebld.CreateType()        
        self.map_members(new_type)        
        return new_type

class ClrInterface(ClrType):
    """
    Set __metaclass__ in a Python class declaration to declare a
    CLR interface type. 
    You need to specify object as the base-type if you do not specify any other
    interfaces as the base interfaces
    """
    
    def __init__(self, *args):
        return super(ClrInterface, self).__init__(*args)

    def emit_method(self, typebld, function_info):
        assert(not function_info.is_static)
        function = function_info.function
        attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract
        method_builder = typebld.DefineMethod(
            function.func_name,
            attributes,
            function.return_type,
            function.arg_types)
        
        instance_offset = 0 if function_info.is_static else 1
        arg_names = function.func_code.co_varnames
        for i in xrange(len(function.arg_types)):
            # TODO - set non-trivial ParameterAttributes, default value and custom attributes
            p = method_builder.DefineParameter(i + 1, ParameterAttributes.None, arg_names[i + instance_offset])

        if hasattr(function, "CustomAttributeBuilders"):
            for cab in function.CustomAttributeBuilders:
                method_builder.SetCustomAttribute(cab)
        
        return method_builder
	            
    def emit_members(self, typebld):
        self.emit_methods(typebld)
        self.emit_properties(typebld)
        self.emit_classattribs(typebld)

    def map_members(self, new_type): pass
    
    interface_module_builder = None

    @staticmethod
    def define_interface(typename, bases):
        for b in bases:
            validate_clr_types(b)
        if not ClrInterface.interface_module_builder:
            name = AssemblyName("interfaces")
            access = AssemblyBuilderAccess.Run
            assembly_builder = ReflectionUtils.DefineDynamicAssembly(name, access)
            ClrInterface.interface_module_builder = assembly_builder.DefineDynamicModule("interfaces")
        attrs = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract
        return ClrInterface.interface_module_builder.DefineType(typename, attrs, None, bases)

    def map_clr_type(self, clr_type):
        """
        TODO - Currently "t = clr.GetPythonType(clr.GetClrType(C)); t == C" will be False
        for C where C.__metaclass__ is ClrInterface, even though both t and C 
        represent the same CLR type. This can be fixed by publishing a mapping
        between t and C in the IronPython runtime.
        """
        pass

    def __clrtype__(self):
        # CFoo below will use ClrInterface as its metaclass, but the user will not expect CFoo
        # to be an interface in this case:
        #
        #   class IFoo(object):
        #     __metaclass__ = ClrInterface
        #   class CFoo(IFoo): pass
        if not "__metaclass__" in self.__dict__:
            return super(ClrInterface, self).__clrtype__()

        bases = list(self.__bases__)
        bases.remove(object)
        bases = tuple(bases)
        if False: # Snippets currently does not support creating interfaces
            typegen = Snippets.Shared.DefineType(self.get_clr_type_name(), bases, True, False)
            typebld = typegen.TypeBuilder
        else:
            typebld = ClrInterface.define_interface(self.get_clr_type_name(), bases)
        clr_type = self.create_type(typebld)
        self.map_clr_type(clr_type)
        return clr_type

# Note that ClrClass inherits from ClrInterface to satisfy Python requirements of metaclasses.
# A metaclass of a subtype has to be subtype of the metaclass of a base type. As a result,
# if you define a type hierarchy as shown below, it requires ClrClass to be a subtype
# of ClrInterface:
#
#   class IFoo(object):
#     __metaclass__ = ClrInterface
#   class CFoo(IFoo):
#     __metaclass__ = ClrClass
class ClrClass(ClrInterface):
    """
    Set __metaclass__ in a Python class declaration to specify strong-type
    information for the class or its attributes. The Python class
    retains its Python attributes, like being able to add or remove methods.    
    """

    # Holds the FieldInfo for a static CLR field which points to a 
    # Microsoft.Scripting.Runtime.DynamicOperations corresponding to the current ScriptEngine
    dynamic_operations_field = None

    def emit_fields(self, typebld):
        if hasattr(self, "_clrfields"):
            for fldname in self._clrfields:
                field_type = self._clrfields[fldname]
                validate_clr_types(field_type)
                typebld.DefineField(
                    fldname, 
                    clr.GetClrType(field_type), 
                    FieldAttributes.Public)

    def map_fields(self, new_type):
        if hasattr(self, "_clrfields"):
            for fldname in self._clrfields: 
                fldinfo = new_type.GetField(fldname)
                setattr(self, fldname, ReflectedField(fldinfo))
            
    @staticmethod
    def get_dynamic_operations_field():
        if ClrClass.dynamic_operations_field: 
            return ClrClass.dynamic_operations_field
        python_context = clr.GetCurrentRuntime().GetLanguage(PythonContext)
        dynamic_operations = DynamicOperations(python_context)
        
        typegen = Snippets.Shared.DefineType(
            "DynamicOperationsHolder" + str(hash(python_context)), 
            object, 
            True, 
            False)
        typebld = typegen.TypeBuilder
        typebld.DefineField(
            "DynamicOperations",
            DynamicOperations,
            FieldAttributes.Public | FieldAttributes.Static)
        new_type = typebld.CreateType()
        ClrClass.dynamic_operations_field = new_type.GetField("DynamicOperations")
        
        ClrClass.dynamic_operations_field.SetValue(None, dynamic_operations)
        
        return ClrClass.dynamic_operations_field
        
    def emit_typed_stub_to_python_method(self, typebld, function_info):
        function = function_info.function
        """
        Generate a stub method that repushes all the arguments and 
        dispatches to DynamicOperations.InvokeMember
        """
        invoke_member = clr.GetClrType(DynamicOperations).GetMethod(
            "InvokeMember", 
            Array[Type]((object, str, Array[object])))

        # Type.GetMethod raises an AmbiguousMatchException if there is a generic and a non-generic method 
        # (like DynamicOperations.GetMember) with the same name and signature. So we have to do things
        # the hard way
        get_member_search = [m for m in clr.GetClrType(DynamicOperations).GetMethods() if m.Name == "GetMember" and not m.IsGenericMethod and m.GetParameters().Length == 2]
        assert(len(get_member_search) == 1)
        get_member = get_member_search[0]

        set_member_search = [m for m in clr.GetClrType(DynamicOperations).GetMethods() if m.Name == "SetMember" and not m.IsGenericMethod and m.GetParameters().Length == 3]
        assert(len(set_member_search) == 1)
        set_member = set_member_search[0]

        convert_to = clr.GetClrType(DynamicOperations).GetMethod(
            "ConvertTo",
            Array[Type]((object, Type)))
        get_type_from_handle = clr.GetClrType(Type).GetMethod("GetTypeFromHandle")

        attributes = MethodAttributes.Public
        if function_info.is_static: attributes |= MethodAttributes.Static
        if function.func_name == "__new__":
            if function_info.is_static: raise TypeError
            method_builder = typebld.DefineConstructor(
                attributes,
                CallingConventions.HasThis,
                function.arg_types)
            raise NotImplementedError("Need to call self.baseType ctor passing in self.get_python_type_field()")
        else:
            method_builder = typebld.DefineMethod(
                function.func_name,
                attributes,
                function.return_type,
                function.arg_types)

        instance_offset = 0 if function_info.is_static else 1
        arg_names = function.func_code.co_varnames
        for i in xrange(len(function.arg_types)):
            # TODO - set non-trivial ParameterAttributes, default value and custom attributes
            p = method_builder.DefineParameter(i + 1, ParameterAttributes.None, arg_names[i + instance_offset])

        ilgen = method_builder.GetILGenerator()
        
        args_array = ilgen.DeclareLocal(Array[object])
        args_count = len(function.arg_types)
        ilgen.Emit(OpCodes.Ldc_I4, args_count)
        ilgen.Emit(OpCodes.Newarr, object)
        ilgen.Emit(OpCodes.Stloc, args_array)            
        for i in xrange(args_count):
            arg_type = function.arg_types[i]
            if clr.GetClrType(arg_type).IsByRef:
                raise NotImplementedError("byref params not supported")
            ilgen.Emit(OpCodes.Ldloc, args_array)
            ilgen.Emit(OpCodes.Ldc_I4, i)
            ilgen.Emit(OpCodes.Ldarg, i + int(not function_info.is_static))
            ilgen.Emit(OpCodes.Box, arg_type)
            ilgen.Emit(OpCodes.Stelem_Ref)
        
        has_return_value = True
        if function_info.prop_name_if_prop_get:
            ilgen.Emit(OpCodes.Ldsfld, ClrClass.get_dynamic_operations_field())
            ilgen.Emit(OpCodes.Ldarg, 0)
            ilgen.Emit(OpCodes.Ldstr, function_info.prop_name_if_prop_get)
            ilgen.Emit(OpCodes.Callvirt, get_member)
        elif function_info.prop_name_if_prop_set:
            ilgen.Emit(OpCodes.Ldsfld, ClrClass.get_dynamic_operations_field())
            ilgen.Emit(OpCodes.Ldarg, 0)
            ilgen.Emit(OpCodes.Ldstr, function_info.prop_name_if_prop_set)
            ilgen.Emit(OpCodes.Ldarg, 1)
            ilgen.Emit(OpCodes.Callvirt, set_member)
            has_return_value = False
        else:
            ilgen.Emit(OpCodes.Ldsfld, ClrClass.get_dynamic_operations_field())
            if function_info.is_static:
                raise NotImplementedError("need to load Python class object from a CLR static field")
                # ilgen.Emit(OpCodes.Ldsfld, class_object)
            else:
                ilgen.Emit(OpCodes.Ldarg, 0)
            
            ilgen.Emit(OpCodes.Ldstr, function.func_name)
            ilgen.Emit(OpCodes.Ldloc, args_array)
            ilgen.Emit(OpCodes.Callvirt, invoke_member)

        if has_return_value:
            if function.return_type == Void:
                ilgen.Emit(OpCodes.Pop)
            else:
                ret_val = ilgen.DeclareLocal(object)
                ilgen.Emit(OpCodes.Stloc, ret_val)
                ilgen.Emit(OpCodes.Ldsfld, ClrClass.get_dynamic_operations_field())
                ilgen.Emit(OpCodes.Ldloc, ret_val)
                ilgen.Emit(OpCodes.Ldtoken, clr.GetClrType(function.return_type))
                ilgen.Emit(OpCodes.Call, get_type_from_handle)
                ilgen.Emit(OpCodes.Callvirt, convert_to)
                ilgen.Emit(OpCodes.Unbox_Any, function.return_type)
        ilgen.Emit(OpCodes.Ret)
        return method_builder

    def emit_method(self, typebld, function_info):
        function = function_info.function
        if hasattr(function, "DllImportAttributeDecorator"):
            dllImportAttributeDecorator = function.DllImportAttributeDecorator
            name = function.func_name
            dllName = dllImportAttributeDecorator.args[0]
            entryName = function.func_name
            attributes = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl
            callingConvention = CallingConventions.Standard
            returnType = function.return_type
            returnTypeRequiredCustomModifiers = ()
            returnTypeOptionalCustomModifiers = ()
            parameterTypes = function.arg_types
            parameterTypeRequiredCustomModifiers = None
            parameterTypeOptionalCustomModifiers = None
            nativeCallConv = CallingConvention.Winapi
            nativeCharSet = CharSet.Auto
            method_builder = typebld.DefinePInvokeMethod(
                name,
                dllName,
                entryName,
                attributes,
                callingConvention,
                returnType,
                returnTypeRequiredCustomModifiers,
                returnTypeOptionalCustomModifiers,
                parameterTypes,
                parameterTypeRequiredCustomModifiers,
                parameterTypeOptionalCustomModifiers,
                nativeCallConv,
                nativeCharSet)
        else:
            method_builder = self.emit_typed_stub_to_python_method(typebld, function_info)

        if hasattr(function, "CustomAttributeBuilders"):
            for cab in function.CustomAttributeBuilders:
                method_builder.SetCustomAttribute(cab)

        return method_builder
	            
    def map_pinvoke_methods(self, new_type):
        pythonType = clr.GetPythonType(new_type)
        for function_info in self.get_typed_methods():
            function = function_info.function
            if hasattr(function, "DllImportAttributeDecorator"):
                # Overwrite the Python function with the pinvoke_method
                pinvoke_method = getattr(pythonType, function.func_name)
                setattr(self, function.func_name, pinvoke_method)
	  
    def emit_python_type_field(self, typebld):
        return typebld.DefineField(
            "PythonType",
            PythonType,
            FieldAttributes.Public | FieldAttributes.Static)

    def set_python_type_field(self, new_type):
        self.PythonType = new_type.GetField("PythonType")        
        self.PythonType.SetValue(None, self)

    def add_wrapper_ctors(self, baseType, typebld):
        python_type_field = self.emit_python_type_field(typebld)
        for ctor in baseType.GetConstructors(): 
            ctorparams = ctor.GetParameters()

            # leave out the PythonType argument
            assert(ctorparams[0].ParameterType == clr.GetClrType(PythonType))
            ctorparams = ctorparams[1:]

            ctorbld = typebld.DefineConstructor(
                        ctor.Attributes,
                        ctor.CallingConvention,
                        tuple([p.ParameterType for p in ctorparams]))
            ilgen = ctorbld.GetILGenerator()
            ilgen.Emit(OpCodes.Ldarg, 0)
            ilgen.Emit(OpCodes.Ldsfld, python_type_field)
            for index in xrange(len(ctorparams)):
                ilgen.Emit(OpCodes.Ldarg, index + 1)
            ilgen.Emit(OpCodes.Call, ctor)
            ilgen.Emit(OpCodes.Ret)
    
    def emit_members(self, typebld):
        self.emit_fields(typebld)
        self.add_wrapper_ctors(self.baseType, typebld)
        super(ClrClass, self).emit_members(typebld)
        
    def map_members(self, new_type):
        self.map_fields(new_type)
        self.map_pinvoke_methods(new_type)
        self.set_python_type_field(new_type)
        super(ClrClass, self).map_members(new_type)

    def __clrtype__(self):        
        # CDerived below will use ClrClass as its metaclass, but the user may not expect CDerived
        # to be a typed .NET class in this case:
        #
        #   class CBase(object):
        #     __metaclass__ = ClrClass
        #   class CDerived(CBase): pass
        if not "__metaclass__" in self.__dict__:
            return super(ClrClass, self).__clrtype__()

        # Create a simple Python type first. 
        self.baseType = super(ClrType, self).__clrtype__()
        # We will now subtype it to create a customized class with the 
        # CLR attributes as defined by the user
        typegen = Snippets.Shared.DefineType(self.get_clr_type_name(), self.baseType, True, False)
        typebld = typegen.TypeBuilder
        return self.create_type(typebld)

def make_cab(attrib_type, *args, **kwds):
    clrtype = clr.GetClrType(attrib_type)
    argtypes = tuple(map(lambda x:clr.GetClrType(type(x)), args))
    ci = clrtype.GetConstructor(argtypes)

    props = ([],[])
    fields = ([],[])
    
    for kwd in kwds:
        pi = clrtype.GetProperty(kwd)
        if pi is not None:
            props[0].append(pi)
            props[1].append(kwds[kwd])
        else:
            fi = clrtype.GetField(kwd)
            if fi is not None:
                fields[0].append(fi)
                fields[1].append(kwds[kwd])
            else:
                raise TypeError("No %s Member found on %s" % (kwd, clrtype.Name))
    
    return CustomAttributeBuilder(ci, args, 
        tuple(props[0]), tuple(props[1]), 
        tuple(fields[0]), tuple(fields[1]))

def accepts(*args):
    """
    TODO - needs to be merged with clr.accepts
    """
    validate_clr_types(args, True)
    def decorator(function):
        function.arg_types = args
        return function
    return decorator

def returns(return_type = Void):
    """
    TODO - needs to be merged with clr.returns
    """
    if return_type != Void:
        validate_clr_types(return_type)
    def decorator(function):
        function.return_type = return_type
        return function
    return decorator

class CustomAttributeDecorator(object):
    """
    This represents information about a custom-attribute applied to a type or a method
    Note that we cannot use an instance of System.Attribute to capture this information
    as it is not possible to go from an instance of System.Attribute to an instance
    of System.Reflection.Emit.CustomAttributeBuilder as the latter needs to know
    how to represent information in metadata to later *recreate* a similar instance of 
    System.Attribute.
    
    Also note that once a CustomAttributeBuilder is created, it is not possible to
    query it. Hence, we need to store the arguments required to store the 
    CustomAttributeBuilder so that pseudo-custom-attributes can get to the information.
    """
    def __init__(self, attrib_type, *args, **kwargs):
        self.attrib_type = attrib_type
        self.args = args
        self.kwargs = kwargs

    def __call__(self, function):
        if self.attrib_type == DllImportAttribute:
            function.DllImportAttributeDecorator = self
        else:
            if not hasattr(function, "CustomAttributeBuilders"):
                function.CustomAttributeBuilders = []
            function.CustomAttributeBuilders.append(self.GetBuilder())
        return function

    def GetBuilder(self):
        assert not self.attrib_type in [DllImportAttribute]
        return make_cab(self.attrib_type, *self.args, **self.kwargs)

def attribute(attrib_type):
    """
    This decorator is used to specify a CustomAttribute for a type or method.
    """
    def make_decorator(*args, **kwargs):
        return CustomAttributeDecorator(attrib_type, *args, **kwargs)
    return make_decorator

def propagate_attributes(old_function, new_function):
    """
    Use this if you replace a function in a type with ClrInterface or ClrClass as the metaclass.
    This will typically be needed if you are defining a decorator which wraps functions with
    new functions, and want it to work in conjunction with clrtype
    """
    if hasattr(old_function, "return_type"):
        new_function.func_name = old_function.func_name
        new_function.return_type = old_function.return_type
        new_function.arg_types = old_function.arg_types
    if hasattr(old_function, "CustomAttributeBuilders"):
        new_function.CustomAttributeBuilders = old_function.CustomAttributeBuilders
    if hasattr(old_function, "CustomAttributeBuilders"):
        new_function.DllImportAttributeDecorator = old_function.DllImportAttributeDecorator
    
