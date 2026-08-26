#!/usr/bin/env bash
set -euo pipefail

echo "Cleaning C# project artifacts..."

# Remove bin and obj directories recursively
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + -print

# Remove common temporary/IDE directories
rm -rf .vs .idea .ionide

# Optional: Run dotnet clean for installed SDK targets
dotnet clean --verbosity quiet

echo "Repository fully cleaned!"
