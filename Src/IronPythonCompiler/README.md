IronPython Compiler
=====

Usage: ipc.exe [options] file [file ...]

Options:

    /out:output_file                          Output file name (default is main_file.<extenstion>)
    
    /target:dll                               Compile only into dll.  Default
    
    /target:exe                               Generate CONSOLE executable stub for startup in addition to dll.
    
    /target:winexe                            Generate WINDOWS executable stub for startup in addition to dll.
    
    @<file>                                   Specifies a response file to be parsed for input files and command line options (one per line)
    
    /? /h                                     This message    
    

EXE/WinEXE specific options:

    /main:main_file.py                        Main file of the project (module to be executed first)
    
    /platform:x86                             Compile for x86 only
    
    /platform:x64                             Compile for x64 only
    
    /embed                                    Embeds the generated DLL as a resource into the executable which is loaded at runtime
    
    /standalone                               Embeds the IronPython assemblies into the stub executable.
    
    /mta                                      Set MTAThreadAttribute on Main instead of STAThreadAttribute, only valid for /target:winexe
    
    /errfmt:msg                               A string that will be used when showing an error occured, {{0}} will be replaced by the exception message
    
    /win32icon:file.ico                       Sets file.ico as the icon for the executable

Example:

    ipc.exe /main:Program.py Form.py /target:winexe
