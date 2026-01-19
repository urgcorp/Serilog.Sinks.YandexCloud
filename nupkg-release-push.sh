#!/bin/bash

PROJECT_PATH="./src/Serilog.Sinks.YandexCloud/Serilog.Sinks.YandexCloud.csproj"
PACKAGE_SOURCE_URL="http://localhost:7665/v3/index.json"

for i in "$@"; do
  [[ $next == 1 ]] && psurl=$i && next=0
  [[ $i == "-psurl" ]] && next=1 && found=1
done
if [[ $found == 1 && ! $psurl =~ ^http ]]; then
  if [[ ! $psurl =~ ^http ]]; then
    echo "Error: \"psurl\" must be URL starting with http"
    exit 1
  fi
  PACKAGE_SOURCE_URL=$psurl
fi

echo "--- Build latest NuGet Release ---"

rm -rf src/Serilog.Sinks.YandexCloud/bin/Release/*.nupkg
dotnet clean "$PROJECT_PATH" -c Release
dotnet build "$PROJECT_PATH" -c Release --no-incremental
dotnet pack "$PROJECT_PATH" -c Release --no-build
dotnet nuget push "src/Serilog.Sinks.YandexCloud/bin/Release/*.nupkg" -s "$PACKAGE_SOURCE_URL" --allow-insecure-connections --skip-duplicate

echo "--- Ready ---"
