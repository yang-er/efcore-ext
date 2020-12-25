#!/bin/bash
NUGET_KEY=$1
dotnet pack -c Release -o packages
cd packages
dotnet nuget push --skip-duplicate -s https://nuget.xylab.fun/v3/index.json -k $NUGET_KEY "*.nupkg"
