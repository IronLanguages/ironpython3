// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime;


namespace IronPythonTest.interop.net.type.clrtype {
    ///--Helpers--
    public class Factory {
        public static T Get<T>() where T : new() {
            return new T();
        }
    }


    ///--Positive Scenarios--
    #region sanity derived
    public class SanityDerived : PythonExceptions.BaseException {
        public SanityDerived(PythonType pt) : base(pt) { }
    }
    #endregion

    #region sanity
    public class Sanity : IPythonObject {
        private PythonType _pythonType;
        private PythonDictionary _dict;


        public Sanity(PythonType param) {
            _pythonType = param;
        }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _pythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _pythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion

    #region sanity non-standard constructor
    public class SanityUniqueConstructor : IPythonObject {
        private PythonType _pythonType;
        private PythonDictionary _dict;

        public SanityUniqueConstructor(PythonType param, Int32 param2) { }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _pythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _pythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion

    #region sanity constructor overloads
    public class SanityConstructorOverloads : IPythonObject {
        private PythonType _pythonType;
        private PythonDictionary _dict;
        public Object MyField;


        public SanityConstructorOverloads(PythonType param) {
            _pythonType = param;
            param.Name = "first";
        }

        public SanityConstructorOverloads(PythonType param, Int32 param2) {
            _pythonType = param;
            param.Name = "second";
        }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _pythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _pythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion

    #region sanity generic
    public class SanityGeneric<T> : IPythonObject {
        private PythonType _pythonType;
        private PythonDictionary _dict;


        public SanityGeneric(PythonType param) {
            _pythonType = param;
        }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _pythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _pythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion

    #region sanity generic constructor
    public class SanityGenericConstructor<T> : IPythonObject {
        private PythonType _pythonType;
        private PythonDictionary _dict;


        public SanityGenericConstructor(T param) {
            _pythonType = (PythonType)(Object)param;
        }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _pythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _pythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion

    #region sanity constructor but no IPythonObject
    public class SanityNoIPythonObject {
        public SanityNoIPythonObject(PythonType pt) {
        }
    }
    #endregion

    #region sanity with parameterless constructor
    public class SanityParameterlessConstructor : IPythonObject {
        private static PythonType _PythonType;
        public static int WhichConstructor;
        private PythonDictionary _dict;


        public SanityParameterlessConstructor(PythonType param) {
            _PythonType = param;
            WhichConstructor = 1;
        }

        public SanityParameterlessConstructor() {
            WhichConstructor = 2;
        }

        #region IPythonObject Members

        PythonDictionary IPythonObject.Dict {
            get { return _dict; }
        }

        PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
            System.Threading.Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
            return _dict;
        }

        bool IPythonObject.ReplaceDict(PythonDictionary dict) {
            return false;
        }

        PythonType IPythonObject.PythonType {
            get { return _PythonType; }
        }

        void IPythonObject.SetPythonType(PythonType newType) {
            _PythonType = newType;
        }

        object[] IPythonObject.GetSlots() { return null; }
        object[] IPythonObject.GetSlotsCreate() { return null; }

        #endregion
    }
    #endregion


    ///--Negative Scenarios--

    #region negative empty
    public class NegativeEmpty { }
    #endregion

    #region negative no constructor, but implements IPythonOjbect
    public class NegativeNoConstructor : IPythonObject {
        #region IPythonObject Members

        public PythonDictionary Dict {
            get { throw new NotImplementedException(); }
        }

        public PythonDictionary SetDict(PythonDictionary dict) {
            throw new NotImplementedException();
        }

        public bool ReplaceDict(PythonDictionary dict) {
            throw new NotImplementedException();
        }

        public PythonType PythonType {
            get { return TypeCache.Int32; }
        }

        public void SetPythonType(PythonType newType) {
            throw new NotImplementedException();
        }

        public object[] GetSlots() {
            throw new NotImplementedException();
        }

        public object[] GetSlotsCreate() {
            throw new NotImplementedException();
        }

        #endregion
    }
    #endregion
}