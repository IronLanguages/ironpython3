# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""Helper to avoid tainting cmodule.py with clr methods"""
from System.Diagnostics import Process
from System.IO import File, Directory