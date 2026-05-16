#!/usr/bin/env bash
# Builds the Pots.Client Blazor WebAssembly project for Vercel deploy.
# Vercel's build container has Node but no .NET SDK, so we install it inline
# into the build sandbox. The output goes to publish-client/wwwroot, which
# vercel.json's outputDirectory points at.
#
# Required env var (set in Vercel project settings):
#   API_BASE_URL  — public URL of the Render backend, e.g.
#                   https://pots-tracker-api.onrender.com
#
# The script rewrites src/Pots.Client/wwwroot/appsettings.json so the deployed
# client knows where the API lives without baking it into source control.

set -euo pipefail

if [[ -z "${API_BASE_URL:-}" ]]; then
  echo "❌ API_BASE_URL is not set. Configure it in Vercel → Project → Settings → Environment Variables." >&2
  exit 1
fi

echo "==> Pointing client at API: $API_BASE_URL"
cat > src/Pots.Client/wwwroot/appsettings.json <<EOF
{
  "ApiBaseUrl": "$API_BASE_URL"
}
EOF

if ! command -v dotnet >/dev/null 2>&1; then
  echo "==> Installing .NET 10 SDK into ./dotnet (Vercel build sandbox has no .NET)..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh -Channel 10.0 -InstallDir ./dotnet
  export PATH="$PWD/dotnet:$PATH"
fi

echo "==> dotnet version: $(dotnet --version)"

echo "==> Publishing Pots.Client (Release)..."
dotnet publish src/Pots.Client/Pots.Client.csproj \
  -c Release \
  -o publish-client \
  --nologo

echo "==> Static output ready at publish-client/wwwroot"
ls -la publish-client/wwwroot | head -20
