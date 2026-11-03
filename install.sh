#!/usr/bin/env bash
#
# Installs the EnvManager CLI.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
#   ./install.sh [--version <version>] [--install-dir <directory>]

set -euo pipefail

readonly REPOSITORY="WolfBublitz/EnvManager"
readonly BINARY_NAME="EnvManager"

version="${ENVMANAGER_VERSION:-latest}"
install_dir="${ENVMANAGER_INSTALL_DIR:-$HOME/.local/bin}"

print_usage() {
  cat <<'EOF'
Usage: install.sh [options]

Install EnvManager for the current operating system and architecture.

Options:
  --version <version>       Version to install. Defaults to the latest release.
  --install-dir <directory> Directory to install EnvManager into.
                            Defaults to ~/.local/bin.
  --help                    Display this help information.

Environment variables:
  ENVMANAGER_VERSION        Equivalent to --version.
  ENVMANAGER_INSTALL_DIR    Equivalent to --install-dir.
EOF
}

fail() {
  printf 'error: %s\n' "$1" >&2
  exit 1
}

info() {
  printf '==> %s\n' "$1"
}

warn() {
  printf 'warning: %s\n' "$1" >&2
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --version)
      [ "$#" -ge 2 ] || fail "--version requires a value"
      version="$2"
      shift 2
      ;;
    --install-dir)
      [ "$#" -ge 2 ] || fail "--install-dir requires a value"
      install_dir="$2"
      shift 2
      ;;
    --help)
      print_usage
      exit 0
      ;;
    *)
      fail "unknown option: $1 (use --help for usage)"
      ;;
  esac
done

case "$(uname -s)" in
  Linux) os="linux" ;;
  Darwin) os="macos" ;;
  *) fail "unsupported operating system: $(uname -s) (use install.ps1 on Windows)" ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch="x64" ;;
  aarch64 | arm64) arch="arm64" ;;
  *) fail "unsupported architecture: $(uname -m)" ;;
esac

require_command curl
require_command mktemp
require_command chmod

asset_name="${BINARY_NAME}-${os}-${arch}"
if [ "$version" = "latest" ]; then
  download_url="https://github.com/${REPOSITORY}/releases/latest/download/${asset_name}"
else
  download_url="https://github.com/${REPOSITORY}/releases/download/${version}/${asset_name}"
fi

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

info "Downloading ${asset_name} (${version})..."
if ! curl --fail --location --silent --show-error "$download_url" --output "$tmp_file"; then
  fail "failed to download EnvManager from $download_url"
fi

mkdir -p "$install_dir"
destination_path="${install_dir}/${BINARY_NAME}"
mv "$tmp_file" "$destination_path"
chmod 755 "$destination_path"

info "Installed ${BINARY_NAME} to ${destination_path}"

case ":${PATH}:" in
  *":${install_dir}:"*) ;;
  *)
    shell_rc="$HOME/.profile"
    case "${SHELL:-}" in
      */zsh) shell_rc="$HOME/.zshrc" ;;
      */bash) shell_rc="$HOME/.bashrc" ;;
    esac

    path_export="export PATH=\"${install_dir}:\$PATH\""
    if [ -f "$shell_rc" ] && grep -Fqx "$path_export" "$shell_rc"; then
      warn "$install_dir is not on the PATH for this shell. Restart your terminal to load it."
    else
      printf '\n%s\n' "$path_export" >> "$shell_rc"
      info "Added ${install_dir} to PATH in ${shell_rc}. Restart your terminal to load it."
    fi
    ;;
esac

info "Run '${BINARY_NAME} --help' to get started."
