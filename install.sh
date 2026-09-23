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

# Extracts the value of a top-level JSON string field named "$1" from the JSON
# document read on stdin, e.g. `extract_json_field tag_name <release.json`.
extract_json_field() {
  sed -n -E "s/^[[:space:]]*\"$1\": *\"([^\"]*)\",?\$/\\1/p" | head -n1
}

# Finds the "browser_download_url" of the asset named "$1" within the release
# JSON document read on stdin.
find_asset_download_url() {
  local asset="$1"
  awk -v asset="\"name\": \"${asset}\"" '
    index($0, asset) { found = 1 }
    found && /"browser_download_url"/ { print; exit }
  ' | extract_json_field browser_download_url
}

asset_name="${BINARY_NAME}-${os}-${arch}"

if [ "$version" = "latest" ]; then
  release_api_url="https://api.github.com/repos/${REPOSITORY}/releases/latest"
else
  release_api_url="https://api.github.com/repos/${REPOSITORY}/releases/tags/${version}"
fi

info "Looking up release information for ${version}..."
curl_headers=(--header "Accept: application/vnd.github+json")
if [ -n "${GITHUB_TOKEN:-}" ]; then
  curl_headers+=(--header "Authorization: Bearer ${GITHUB_TOKEN}")
fi

http_response="$(curl --location --silent --show-error --write-out '\n%{http_code}' "${curl_headers[@]}" "$release_api_url")" ||
  fail "failed to fetch release information from $release_api_url"

http_status="${http_response##*$'\n'}"
release_json="${http_response%$'\n'*}"

if [ "$http_status" = "404" ]; then
  fail "release not found: $version"
elif [ "$http_status" != "200" ]; then
  fail "failed to fetch release information from $release_api_url (HTTP $http_status)"
fi

resolved_version="$(printf '%s' "$release_json" | extract_json_field tag_name)"
[ -n "$resolved_version" ] || fail "could not determine release version from $release_api_url"

download_url="$(printf '%s' "$release_json" | find_asset_download_url "$asset_name")"
[ -n "$download_url" ] || fail "no asset named '${asset_name}' found in release ${resolved_version}"

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

info "Downloading ${asset_name} (${resolved_version})..."
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
