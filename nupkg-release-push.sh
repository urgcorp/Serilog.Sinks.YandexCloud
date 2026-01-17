#!/bin/bash

PROJECT_PATH="./src/Serilog.Sinks.YandexCloud/Serilog.Sinks.YandexCloud.csproj"
SOURCE_URL="http://localhost:7665/v3/index.json"

echo "--- Build latest NuGet Release ---"

rm -rf src/Serilog.Sinks.YandexCloud/bin/Release/*.nupkg
dotnet clean "$PROJECT_PATH" -c Release
dotnet build "$PROJECT_PATH" -c Release --no-incremental
dotnet pack "$PROJECT_PATH" -c Release --no-build
dotnet nuget push "src/Serilog.Sinks.YandexCloud/bin/Release/*.nupkg" -s "$SOURCE_URL" --allow-insecure-connections

echo "--- Ready ---"
