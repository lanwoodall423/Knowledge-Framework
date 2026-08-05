#!/usr/bin/env bash
set -euo pipefail

script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if ! command -v pwsh >/dev/null 2>&1; then
  printf '%s\n' 'build=BLOCKED PowerShell 7 (pwsh) is required for the cross-platform entry point.' >&2
  exit 2
fi
exec pwsh "$script_dir/Build-KnowledgeFramework.ps1" "$@"
