# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import clr
clr.AddReference("System.Windows.Forms")
from System.Windows.Forms import Form

form = Form(Text="(Compiled WinForms) Hello World")
form.ShowDialog()
