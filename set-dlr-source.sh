#! /bin/sh

function usage {
    echo "set-dlr-source <path/to/dlr>"
    echo "set-dlr-source none"
    exit 1
}

function not_found {
    echo "$1 does not exist."
    exit 2
}

[ $# -eq 1 ] || usage

_root=$(cd "$(dirname "$0")"; pwd)
_dlr_root=$(cd $1; pwd)
_local_dlr_path_file=$_root/.local-dlr-path.props

cat > "$_local_dlr_path_file" <<EOF
<?xml version="1.0" encoding="utf-8"?> 
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"> 
  <PropertyGroup> 
    <DlrReferences>$_dlr_root/bin/\$(Configuration)</DlrReferences> 
  </PropertyGroup> 
</Project>
EOF
