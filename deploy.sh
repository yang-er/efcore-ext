#!/bin/bash
NUGET_KEY=$1
cd packages
dotnet nuget push -s https://api.nuget.org/v3/index.json -k $NUGET_KEY "*.nupkg"
