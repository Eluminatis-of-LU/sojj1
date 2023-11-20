#!/bin/sh
/usr/bin/sandbox -http-addr 0.0.0.0:5050 -dir ~/sandbox/ -release &

dotnet /app/Sojj.dll