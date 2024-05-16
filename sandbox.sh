#!/bin/bash

if [ -z "$PREFORK" ]; then
  PREFORK=1
fi

/usr/bin/sandbox -http-addr 0.0.0.0:5050 -dir ~/sandbox/ -release -file-timeout 30m -pre-fork=$PREFORK &