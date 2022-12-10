#!/bin/bash
for FILE in ./$1/*.pdf; do
    echo ${FILE}
    pdfcrop "${FILE}" "./$1/crop/$(basename $FILE)"
done
