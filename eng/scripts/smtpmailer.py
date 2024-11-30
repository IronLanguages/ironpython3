# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
