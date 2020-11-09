#!/bin/sh
BASEDIR=$(dirname $0)
ABS_PATH=`cd "$BASEDIR"; pwd`
mono $ABS_PATH/ipy.exe "$@"