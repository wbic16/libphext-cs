#!/usr/bin/env bash
# libphext-cs publish step: pack the NuGet package, then (only when
# PUBLISH_CONFIRM=1) push it to nuget.org.
# Prerequisites: .NET SDK 8.0+; NUGET_API_KEY set in the environment
# (never embed an API key in this script or in the repo).
set -euo pipefail
cd "$(dirname "$0")/.."
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: 'dotnet' not found. Install the .NET SDK 8.0+ first (the library targets net8.0)." >&2
  exit 1
fi
VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' src/Phext/Phext.csproj)"
echo "== libphext-cs publish: version ${VERSION} =="
echo "-- step 1/3: tests"
"$(dirname "$0")/test.sh"
echo "-- step 2/3: pack"
dotnet pack -c Release
PKG="$(find . -name "libphext-cs.${VERSION}.nupkg" | head -n 1)"
echo "-- step 3/3: push"
echo "Will publish: ${PKG} -> https://api.nuget.org/v3/index.json (nuget.org, package id libphext-cs, version ${VERSION})"
if [ "${PUBLISH_CONFIRM:-}" != "1" ]; then
  echo "STOP: dry run only. Re-run with PUBLISH_CONFIRM=1 (and NUGET_API_KEY set) to push."
  exit 0
fi
: "${NUGET_API_KEY:?ERROR: set NUGET_API_KEY in the environment before publishing}"
dotnet nuget push "${PKG}" --api-key "${NUGET_API_KEY}" --source "https://api.nuget.org/v3/index.json"
echo "Published ${PKG}."
