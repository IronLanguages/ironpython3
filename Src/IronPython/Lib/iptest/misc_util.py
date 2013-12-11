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
