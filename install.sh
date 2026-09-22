#!/bin/bash
#
# Installs the EnvManager CLI.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
#
# Environment variables:
#   ENVMANAGER_VERSION   Release tag to install (default: latest).
#   ENVMANAGER_INSTALL_DIR  Directory the executable is installed into
#                           (default: $HOME/.local/bin).

set -euo pipefail

REPO="WolfBublitz/EnvManager"
BINARY_NAME="EnvManager"
VERSION="${ENVMANAGER_VERSION:-latest}"
INSTALL_DIR="${ENVMANAGER_INSTALL_DIR:-$HOME/.local/bin}"

RED="\033[0;31m"
GREEN="\033[0;32m"
YELLOW="\033[0;33m"
RESET="\033[0m"

info() { printf "%b\n" "${GREEN}==>${RESET} $1"; }
warn() { printf "%b\n" "${YELLOW}warning:${RESET} $1" >&2; }
error() { printf "%b\n" "${RED}error:${RESET} $1" >&2; exit 1; }

# ┌────────────────────────────────────────────────────────────┐
# │ Detect operating system and CPU architecture                │
# └────────────────────────────────────────────────────────────┘

os="$(uname -s)"
arch="$(uname -m)"

case "$os" in
  Linux)
    case "$arch" in
      x86_64) rid="linux-x64" ;;
      aarch64 | arm64) rid="linux-arm64" ;;
      *) error "unsupported architecture: $arch" ;;
    esac
    ;;
  Darwin)
    case "$arch" in
      x86_64 | arm64) rid="osx-x64" ;;
      *) error "unsupported architecture: $arch" ;;
    esac
    ;;
  *)
    error "unsupported operating system: $os (use install.ps1 on Windows)"
    ;;
esac

# ┌────────────────────────────────────────────────────────────┐
# │ Download the executable                                     │
# └────────────────────────────────────────────────────────────┘

asset_name="${BINARY_NAME}-${rid}"

if [ "$VERSION" = "latest" ]; then
  download_url="https://github.com/${REPO}/releases/latest/download/${asset_name}"
else
  download_url="https://github.com/${REPO}/releases/download/${VERSION}/${asset_name}"
fi

info "Downloading ${asset_name} (${VERSION})..."

mkdir -p "$INSTALL_DIR"
tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

if ! curl -fsSL "$download_url" -o "$tmp_file"; then
  error "failed to download EnvManager from $download_url"
fi

install -m 755 "$tmp_file" "$INSTALL_DIR/$BINARY_NAME"

info "Installed ${BINARY_NAME} to ${INSTALL_DIR}/${BINARY_NAME}"

# ┌────────────────────────────────────────────────────────────┐
# │ Verify the install directory is on PATH                     │
# └────────────────────────────────────────────────────────────┘

case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    warn "$INSTALL_DIR is not on your PATH."
    shell_rc="$HOME/.profile"
    case "${SHELL:-}" in
      */zsh) shell_rc="$HOME/.zshrc" ;;
      */bash) shell_rc="$HOME/.bashrc" ;;
    esac
    printf "%b\n" "  Add it by appending this line to ${shell_rc}:"
    printf "%b\n" "    export PATH=\"${INSTALL_DIR}:\$PATH\""
    ;;
esac

info "Run '${BINARY_NAME} --help' to get started."
