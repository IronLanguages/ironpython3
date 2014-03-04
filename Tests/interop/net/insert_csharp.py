import nt, sys
import System

pattern = sys.argv[1]

for x in System.IO.Directory.GetFiles(".", pattern):
    f = file(x)
    lines = f.readlines()
    f.close()
    
    nl = []
    for l in lines:
        insert_file = ""
        if "clr.AddReference" in l:
            left = l.index('(')
            right = l.index(')')
            insert_file = nt.environ["DLR_ROOT"] + "\\Test\\ClrAssembly\\" + l[left+2:right-1] + ".cs"
            
        nl.append(l.rstrip()) # no matter what, print the current line
        
        if insert_file:
            nl.append("")
            f = file(insert_file)
            for l2 in f.readlines():
                if l2.strip():
                    nl.append("# " + l2.rstrip())
            f.close()
            nl.append("")
    
    f = file(x, "w")
    for l in nl:
        f.write(l + "\n")
    f.close()