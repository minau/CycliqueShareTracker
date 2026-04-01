#!/bin/bash
set -e

echo "[Codex Setup] Starting setup..."

if ! command -v curl >/dev/null 2>&1; then
  echo "[Codex Setup] Installing curl and CA certificates..."
  apt-get update
  apt-get install -y --no-install-recommends curl ca-certificates
else
  echo "[Codex Setup] curl already available."
fi

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | awk '{print $1}' | grep -q '^8\.'; then
  echo "[Codex Setup] Installing .NET SDK 8.0..."

  INSTALL_SCRIPT="/tmp/dotnet-install.sh"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
  chmod +x "$INSTALL_SCRIPT"
  "$INSTALL_SCRIPT" --channel 8.0 --install-dir /usr/share/dotnet
else
  echo "[Codex Setup] .NET SDK 8.x already installed."
fi

export DOTNET_ROOT=/usr/share/dotnet
export PATH="$DOTNET_ROOT:$PATH"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[Codex Setup] ERROR: dotnet is not available in PATH after installation."
  exit 1
fi

echo "[Codex Setup] dotnet version: $(dotnet --version)"

echo "[Codex Setup] Detecting solution file..."
SLN_PATH=$(find . -maxdepth 4 -type f -name '*.sln' | sort | head -n 1)

if [ -z "$SLN_PATH" ]; then
  echo "[Codex Setup] ERROR: No .sln file found in repository."
  exit 1
fi

echo "[Codex Setup] Using solution: $SLN_PATH"

echo "[Codex Setup] Running dotnet restore..."
dotnet restore "$SLN_PATH"

echo "[Codex Setup] Running dotnet build..."
dotnet build "$SLN_PATH" --no-restore

echo "[Codex Setup] Running dotnet test..."
if ! dotnet test "$SLN_PATH" --no-build; then
  echo "[Codex Setup] WARNING: dotnet test reported failures (ignored for setup)."
fi

echo "[Codex Setup] Setup completed."
