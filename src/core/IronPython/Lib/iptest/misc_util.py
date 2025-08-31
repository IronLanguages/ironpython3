# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.


#------------------------------------------------------------------------------
ip_supported_encodings = [  'cp1252','ascii', 'utf-8', 'utf-16', 'latin-1', 
                            'iso-8859-1', 'utf-16-le', 'utf-16-be', 'unicode-escape', 
                            'raw-unicode-escape', 'utf-7', 'utf-8-sig']
#make sure all encoding names are lowercase
ip_supported_encodings = [ x.lower() for x in ip_supported_encodings]
#now make them uppercase as well
ip_supported_encodings += [x.upper() for x in ip_supported_encodings]
#add in a few mixed-case encodings
ip_supported_encodings += ['Cp1252', 'asciI', 'uTF-8']
#add in a few encodings with '-'s removed
ip_supported_encodings += ['latin1', 'utf-16le', 'utf8']
#replace '-'s with whitespace
ip_supported_encodings += ['unicode escape', 'utf 16 be']
#add a few 'interesting' cases
ip_supported_encodings += ['uTf!!!8', 'utf_8']

#------------------------------------------------------------------------------
