#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
(cd "$ROOT/api" && dotnet restore Jackstaff.sln && dotnet test Jackstaff.sln --no-restore)
(cd "$ROOT/web" && npm install && npm run build)
echo "JACKSTAFF v0.1 checks completed."
