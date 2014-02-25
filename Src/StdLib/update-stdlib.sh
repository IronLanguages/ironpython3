#!/bin/sh

if [ -z "$1" ]; then echo "Must specify CPython repo." && exit 1; fi

git diff-index --quiet HEAD
if [ $? -ne 0 ]
then
    echo "There are uncomitted changes. Commit or stash before proceeding."
    exit 1
fi

curbranch=$(git rev-parse --abbrev-ref HEAD)
if [ "$curbranch" != "python-stdlib" ]
then
    echo "This should only be run on the python-stdlib branch (on $curbranch)."
    exit 1
fi

pyrepo=$1
hgrev=$(hg -R "$pyrepo" id -i)
stdlibdir=$(dirname $0)

rsync -qcr --delete --exclude="plat-*" ~/repos/cpython/Lib "$stdlibdir"

git update-index --refresh -q > /dev/null
git add -A "$stdlibdir/Lib"
git diff-index --quiet HEAD

if [ $? -eq 0 ]
then
    echo "No changes found."
else
    git commit -am "Import python stdlib @ $hgrev"
fi

