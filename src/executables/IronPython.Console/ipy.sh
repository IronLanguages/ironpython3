#!/bin/sh
BASEDIR=$(dirname -- "$0")
ABS_PATH=$(cd -- "$BASEDIR"; pwd)
exec dotnet "$ABS_PATH/ipy.dll" "$@"
