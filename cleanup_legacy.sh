#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
legacy_files=(
  "src/OrderCreation.Api/Infrastructure/Infrastructure.cs"
)

removed=0
for relative_path in "${legacy_files[@]}"; do
  full_path="$repo_root/$relative_path"
  if [[ -f "$full_path" ]]; then
    rm -f "$full_path"
    echo "Removed legacy file: $relative_path"
    removed=$((removed + 1))
  fi
done

if [[ "$removed" -eq 0 ]]; then
  echo "No legacy files found."
else
  echo "Removed $removed legacy file(s)."
fi

echo "Run: git add -A"
