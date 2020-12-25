#!/bin/bash
NUGET_KEY=$1
cd packages
dotnet nuget push --skip-duplicate -s https://nuget.xylab.fun/v3/index.json -k $NUGET_KEY "*.nupkg"
