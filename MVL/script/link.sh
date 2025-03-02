#!/bin/bash

. "$MSVCENV"
export LIB="${LIB//\\//}"
export LIB="${LIB//z:/}"
export LIBPATH="$LIB"

lld -flavor link "$@"
