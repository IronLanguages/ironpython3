#!/bin/sh
BASEDIR=$(dirname $(readlink -f "$0"))
ABS_PATH=$(cd "$BASEDIR"; pwd)
mono "$ABS_PATH/ipy.exe" "$@"
