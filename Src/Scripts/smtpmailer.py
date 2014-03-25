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

import System

def send(to, subject, body, urgent=False, attachments=None):
    "send(to, subject, body, urgent=False)"
    mm = System.Net.Mail.MailMessage(System.Environment.UserName + "@microsoft.com", to)
    mm.Subject = subject
    mm.IsBodyHtml = True
    mm.Body = body
    if urgent:
        mm.Priority = System.Net.Mail.MailPriority.High
    if attachments != None:
        if(hasattr(attachments, '__len__')):
            for x in attachments:
                mm.Attachments.Add(x)
        else:
            mm.Attachments.Add(attachments)
    
    sc = System.Net.Mail.SmtpClient("smtphost")
    sc.UseDefaultCredentials = True
    sc.Send(mm)
