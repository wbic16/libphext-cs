#!/usr/bin/env bash
# libphext-cs test step. Prerequisite: .NET SDK 8.0 or later on PATH.
set -euo pipefail
cd "$(dirname "$0")/.."
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: 'dotnet' not found. Install the .NET SDK 8.0+ first (the library targets net8.0)." >&2
  exit 1
fi
dotnet test
