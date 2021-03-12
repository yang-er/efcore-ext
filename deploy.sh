#!/bin/bash
NUGET_KEY=$1
cd packages
dotnet nuget push -k $NUGET_KEY "*.nupkg"
