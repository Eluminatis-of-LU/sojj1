#!/bin/sh

# /usr/bin/sandbox -http-addr 0.0.0.0:5050 -release -dir ~/sandbox/ &
/usr/bin/sandbox -http-addr 0.0.0.0:5050 -dir ~/sandbox/
dotnet /app/Sojj.dll