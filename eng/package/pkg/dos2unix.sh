#/bin/sh
perl -pi -e 's/\r\n|\n|\r/\n/g' "$1"