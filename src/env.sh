#!/bin/bash

set -euo pipefail

SCRIPT_NAME="$(basename "$0")"

# ┌────────────────────────────────────────────────────────────┐
# │ Colors                                                     │
# └────────────────────────────────────────────────────────────┘

# Regular colors
RED="\033[0;31m"
GREEN="\033[0;32m"
YELLOW="\033[0;33m"
BLUE="\033[0;34m"
MAGENTA="\033[0;35m"
CYAN="\033[0;36m"
WHITE="\033[0;37m"

# Bold
BOLD="\033[1m"
BRED="\033[1;31m"
BGREEN="\033[1;32m"
BYELLOW="\033[1;33m"
BBLUE="\033[1;34m"
BMAGENTA="\033[1;35m"
BCYAN="\033[1;36m"
BWHITE="\033[1;37m"

# Reset
RESET="\033[0m"

# ┌────────────────────────────────────────────────────────────┐
# │ Emojis                                                    │
# └────────────────────────────────────────────────────────────┘

EMOJI_SUCCESS="${GREEN}✔${RESET}"
EMOJI_FAILED="${RED}✘${RESET}"

CONFIG_FILE_DIR="$HOME/.EnvManager"
CONFIG_FILE_PATH="$CONFIG_FILE_DIR/config.json"

usage() {
  cat <<EOF
Usage:
  $SCRIPT_NAME <command> [subcommand] [options]

Commands:
  init      initialize EnvManager

Run:
  $SCRIPT_NAME help <command>
for more details.
EOF
}

usageInit() {
    cat <<EOF
Usage:
  $SCRIPT_NAME init

Description:
  Initializes the EnvManager environment by creating necessary directories and configuration files.
EOF
}

# ┌────────────────────────────────────────────────────────────┐
# │ Utils.                                                     │
# └────────────────────────────────────────────────────────────┘

gump_exists() {
    if command -v gum >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# ┌────────────────────────────────────────────────────────────┐
# │ Logging                                                    │
# └────────────────────────────────────────────────────────────┘

log_info() {
    local message="$1"
    local sender="${2:-}"

    log "info" "$message" "$sender"
}

log_warn() {
    local message="$1"
    local sender="${2:-}"

    log "warn" "$message" "$sender"
}

log_error() {
    local message="$1"
    local sender="${2:-}"

    log "error" "$message" "$sender"
}

log_success() {
    local message="$1"
    local sender="${2:-}"

    log "success" "$message" "$sender"
}

log() {
    local level="$1"
    local message="$2"
    local sender="${3:-}"

    if [ -n "$sender" ]; then
            sender=" ${BLUE}[$sender]${RESET} "
    fi

    case "$level" in
        info) echo -e "${GREEN}[INFO]${RESET}$sender$message" ;;
        warn) echo -e "${YELLOW}[WARN]${RESET}$sender$message" ;;
        error) echo -e "${RED}[ERROR]${RESET}$sender$message" ;;
        success) echo -e "${GREEN}[SUCC]${RESET}$sender$message ${EMOJI_SUCCESS}" ;;
        *) echo -e "${WHITE}[LOG]${RESET} $sender$message" ;;
    esac
}

# ┌────────────────────────────────────────────────────────────┐
# │ Header                                                     │
# └────────────────────────────────────────────────────────────┘

print_app_header() {
    if gump_exists; then
        gum style --padding "0 10" --border normal --foreground "#00F" --bold "EvnManager"
    else
        echo -e "${BOLD}${BLUE}EnvManager${RESET}"
    fi
}
    

print_header() {
    local message="$1"
    gum style --padding "0 1" --border normal --foreground "#00F" --bold "$message"
}

cprintf() {
    local color="$1"
    shift
    printf "%b" "${color}"
    printf "$@"
    printf "%b" "${RESET}"
}

printHeader() {
    cprintf ${BOLD} "┌────────────────────────────────────────────────────────────┐\n"
    cprintf ${BOLD} "│ EnvManager - A tool to manage your development environment │\n"
    cprintf ${BOLD} "└────────────────────────────────────────────────────────────┘\n\n"
}

print_begin_task() {
    local message="$1"
    cprintf ${BGREEN} "[[ "
    cprintf ${BBLUE} "${message}"
    cprintf ${BGREEN} " ]]\n"
}

print_end_task() {
    cprintf ${BGREEN} "Task completed\n\n"
}

printSubStep() {
    local message="$1"
    cprintf ${BYELLOW} " → "
    cprintf ${BWHITE} "${message}\n"
}

printSuccess() {
    local message="$1"
    cprintf ${BGREEN} " → "
    cprintf ${BWHITE} "${message}"
    cprintf ${BGREEN} " ✔\n"
}

action_start() {
    local message="$1"
    cprintf ${BYELLOW} " → "
    cprintf ${BWHITE} "${message}"
}

action_finished() {
    local success=$1
    local message=${2:-}
    if [ $success -eq 0 ]; then
      cprintf ${BGREEN} " ✔\n"
    else
      if [ -n "$message" ]; then
        cprintf ${BWHITE} " ($message)"
      fi
      cprintf ${BRED} " ✘\n"
    fi
}

createDirIfNotExists() {
    local path="$1"
    mkdir -p $path
}

config_add_tools() {
    local tools=("$@")

    local temp_file
    temp_file=$(mktemp)

    # create a JSON array from the tools
    local json_array="["
    for tool in "${tools[@]}"; do
        json_array+="\"$tool\","
    done
    json_array="${json_array%,}]"

    # add the tools to the configuration file using jq
    jq --argjson t "$json_array" '.tools += $t' "$CONFIG_FILE_PATH" > "$temp_file"
    mv "$temp_file" "$CONFIG_FILE_PATH"
}

get_distro_id() {
   [ -e /etc/os-release ] && source /etc/os-release && echo "${ID:-Unknown}" && return
   [ -e /etc/lsb-release ] && source /etc/lsb-release && echo "${DISTRIB_ID:-Unknown}" && return
   [ "$(uname)" == "Darwin" ] && echo "mac" && return
}

get_distro_version() {
    [ -e /etc/os-release ] && source /etc/os-release && echo "${VERSION_ID:-Unknown}" && return
    [ -e /etc/lsb-release ] && source /etc/lsb-release && echo "${DISTRIB_RELEASE:-Unknown}" && return
    [ "$(uname)" == "Darwin" ] && echo "" && return
}

package_is_installed() {
   local package_name=$1
   local distro=$(get_distro_id)

   if [ "$distro" == "debian" ] || [ "$distro" == "ubuntu" ]; then
      dpkg -s $package_name >/dev/null 2>&1
      return $?
   elif [ "$distro" == "manjaro" ] || [ "$distro" == "arch" ]; then
      pacman -Qs $package_name >/dev/null 2>&1
      return $?
   elif [ "$distro" == "mac" ]; then
      grep -Fxq -- "$package_name" < <(brew list --formula)
      return $?
   elif [ "$distro" == "fedora" ]; then
      rpm -q $package_name >/dev/null 2>&1
      return $?
   fi

   return 1
}

package_install() {
   local package_name=$1

   if package_is_installed "$package_name"; then
      log_warn "Package '$package_name' is already installed" "PACK"
      return
   fi

   local distro=$(get_distro_id)

   if [ "$distro" == "debian" ] || [ "$distro" == "ubuntu" ]; then
      apt-get install $package_name -y>/dev/null 2>&1
   elif [ "$distro" == "manjaro" ] || [ "$distro" == "arch" ]; then
      pacman -S $package_name>/dev/null 2>&1
   elif [ "$distro" == "mac" ]; then
      brew install $package_name>/dev/null 2>&1
   elif [ "$distro" == "fedora" ]; then
      dnf install $package_name -y>/dev/null 2>&1
   fi

   if [ $? -eq 0 ]; then
      log_success "Package '$package_name' installed successfully" "PACK"
   else
      log_error "Failed to install package '$package_name'" "PACK"
   fi
}

package_update() {
   local package_name=$1

   local distro=$(get_distro_id)

   if [ "$distro" == "debian" ] || [ "$distro" == "ubuntu" ]; then
      apt-get update $package_name -y>/dev/null 2>&1
   elif [ "$distro" == "manjaro" ] || [ "$distro" == "arch" ]; then
      pacman -Syu $package_name>/dev/null 2>&1
   elif [ "$distro" == "mac" ]; then
      brew upgrade $package_name>/dev/null 2>&1
   elif [ "$distro" == "fedora" ]; then
      dnf update $package_name -y>/dev/null 2>&1
   fi

   if [ $? -eq 0 ]; then
      log_success "Package '$package_name' updated successfully" "PACK"
   else
      log_error "Failed to update package '$package_name'" "PACK"
   fi
}

package_remove() {
   local package_name=$1

   local distro=$(get_distro_id)

   if [ "$distro" == "debian" ] || [ "$distro" == "ubuntu" ]; then
      apt-get remove $package_name -y>/dev/null 2>&1
   elif [ "$distro" == "manjaro" ] || [ "$distro" == "arch" ]; then
      pacman -R $package_name>/dev/null 2>&1
   elif [ "$distro" == "mac" ]; then
      brew uninstall $package_name>/dev/null 2>&1
   elif [ "$distro" == "fedora" ]; then
      dnf remove $package_name -y>/dev/null 2>&1
   fi

   if [ $? -eq 0 ]; then
      log_success "Package '$package_name' removed successfully" "PACK"
   else
      log_error "Failed to remove package '$package_name'" "PACK"
   fi
}

# ┌────────────────────────────────────────────────────────────┐
# │ Progress                                                   │
# └────────────────────────────────────────────────────────────┘
local_git() {
    git --git-dir=$CONFIG_FILE_DIR/repo --work-tree=$HOME "$@"
}

local_git_list_branches() {
    branches=()
    while IFS= read -r branch; do
        branches+=("$branch")
    done < <(local_git branch --format='%(refname:short)')
}

local_git_get_branch() {
    branch=$(local_git rev-parse --abbrev-ref HEAD)
}

local_git_list_modified_files() {
    modified_files=()
    while IFS= read -r file; do
        modified_files+=("$file")
    done < <(local_git diff --name-only)
}

local_git_list_staged_files() {
    staged_files=()

    local git_root
    git_root="$(local_git rev-parse --show-toplevel)"

    while IFS= read -r file; do
        staged_files+=("$git_root/$file")
    done < <(local_git diff --name-only --cached)
}

local_git_list_tracked_files() {
    tracked_files=()
    while IFS= read -r file; do
        tracked_files+=("$file")
    done < <(local_git ls-files)
}

# ┌────────────────────────────────────────────────────────────┐
# │ Progress                                                   │
# └────────────────────────────────────────────────────────────┘

progress() {
    local message="$1"
    shift

    local script_path
    script_path="$(realpath "${BASH_SOURCE[0]}")"

    # Run gum spin, passing the script path and all remaining arguments
    gum spin --spinner dot --title "$message" --show-stdout --show-stderr -- \
        bash -c '. "$1"; shift; "$@"' _ "$script_path" "$@"
}


# ┌────────────────────────────────────────────────────────────┐
# │ Config                                                     │
# └────────────────────────────────────────────────────────────┘

config_tool_add() {
    local tool_name="$1"

    # checking if the tool already exists in the configuration file
    if jq -e --arg t "$tool_name" '.tools | index($t)' "$CONFIG_FILE_PATH" > /dev/null; then
        log_warn "Tool '$tool_name' already exists in the configuration file" "CONF"
        return
    fi

    local temp_file
    temp_file=$(mktemp)
    
    # add the tool to the configuration file using jq
    jq --arg t "$tool_name" '.tools += [$t]' "$CONFIG_FILE_PATH" > "$temp_file"
    if [ $? -ne 0 ]; then
        log_error "Failed to add tool '$tool_name' to the configuration file" "CONF"
        rm -f "$temp_file"
        return
    fi

    # move the temp file back to the original configuration file path
    mv "$temp_file" "$CONFIG_FILE_PATH"
    
    log_success "Tool '$tool_name' added to the configuration file" "CONF"
}

config_tool_remove() {
    local tool_name="$1"

    # checking if the tool exists in the configuration file
    if ! jq -e --arg t "$tool_name" '.tools | index($t)' "$CONFIG_FILE_PATH" > /dev/null; then
        log_error "Tool '$tool_name' does not exist in the configuration file" "CONF"
        return
    fi

    local temp_file
    temp_file=$(mktemp)
    
    # remove the tool from the configuration file using jq
    jq --arg t "$tool_name" '.tools -= [$t]' "$CONFIG_FILE_PATH" > "$temp_file"
    if [ $? -ne 0 ]; then
        log_error "Failed to remove tool '$tool_name' from the configuration file" "CONF"
        rm -f "$temp_file"
        return
    fi

    # move the temp file back to the original configuration file path
    mv "$temp_file" "$CONFIG_FILE_PATH"
    
    log_success "Tool '$tool_name' removed from the configuration file" "CONF"
}

config_read_tools() {
    tools=()

    while IFS= read -r t; do
        tools+=("$t")
    done < <(jq -r '.tools[]' "$CONFIG_FILE_PATH")
}

# ┌────────────────────────────────────────────────────────────┐
# │ Commands                                                   │
# └────────────────────────────────────────────────────────────┘

cmd_init() {
    local url="${1:-}"
    local branch="${2:-master}"

    if [ -z "$url" ]; then
        url=$(gum input --placeholder "Enter the URL of the repository to clone")
    fi

    createDirIfNotExists "$CONFIG_FILE_DIR"
    touch "$CONFIG_FILE_PATH"

    package_install "jq"
    config_tool_add "jq"

    package_install "git"
    config_tool_add "git"

    config_tool_add "gum"
    package_install "gum"

    git clone --branch "$branch" --bare "$url" "$CONFIG_FILE_DIR/repo"

    local_git config --local status.showUntrackedFiles no

    local_git checkout --force
}

cmd_env_list() {
    local_git_list_branches

    for branch in "${branches[@]}"; do
        echo "$branch\n"
    done
}

cmd_env_create() {
    local env_name="${1:-}"

    if [ -z "$env_name" ]; then
        env_name=$(gum input --placeholder "Enter the name of the environment to create")
    fi

    local_git checkout -b "$env_name"

    if [ $? -eq 0 ]; then
        log_success "Environment '$env_name' created successfully" "ENV"
    else
        log_error "Failed to create environment '$env_name'" "ENV"
    fi
}

cmd_env_push() {
    local_git_get_branch
    progress "Pushing environment" "local_git" "push" "origin" "$branch"

    if [ $? -eq 0 ]; then
        log_success "Environment pushed successfully" "ENV"
    else
        log_error "Failed to push environment" "ENV"
    fi
}

cmd_env_switch() {
    local env_name="${1:-}"

    if [ -z "$env_name" ]; then
        local_git_list_branches
        env_name=$(gum choose "${branches[@]}")
    fi

    progress "Switching environment" "local_git" "checkout" "$env_name"

    if [ $? -eq 0 ]; then
        log_success "Switched to environment '$env_name' successfully" "ENV"
    else
        log_error "Failed to switch to environment '$env_name'" "ENV"
    fi
}

cmd_env_remove() {
    local env_name="${1:-}"

    if [ -z "$env_name" ]; then
        local_git_list_branches
        env_name=$(gum choose "${branches[@]}")
    fi

    progress "Removing environment" "local_git" "branch" "-D" "$env_name"

    if [ $? -eq 0 ]; then
        log_success "Environment '$env_name' removed successfully" "ENV"
    else
        log_error "Failed to remove environment '$env_name'" "ENV"
    fi
}

cmd_env_add_file() {
    local file_path="${1:-}"

    if [ -z "$file_path" ]; then
        file_path=$(gum file $HOME --all)
    fi

    if [ ! -f "$file_path" ]; then
        log_error "File '$file_path' does not exist" "ENV"
        return
    fi

    progress "Adding file to environment" "local_git" "add" "$file_path"

    if [ $? -eq 0 ]; then
        log_success "File '$file_path' added to the environment successfully" "ENV"
    else
        log_error "Failed to add file '$file_path' to the environment" "ENV"
    fi
}

cmd_env_commit() {
    local message="${1:-}"

    local_git_list_staged_files

    selected_files=$(gum choose --no-limit "${staged_files[@]}")

    git_root="$(local_git rev-parse --show-toplevel)"

    while IFS= read -r file; do
        rel="${file#$git_root/}"

        progress "Adding file to environment" "local_git" "add" "$rel"
        progress "Committing changes" "local_git" "commit" "-m" "${message:-"Update $rel"}"
    done <<< "$selected_files"
}

cmd_env_delete() {
    local_git_list_tracked_files

    selected_files=$(gum choose --no-limit "${tracked_files[@]}")

    git_root="$(local_git rev-parse --show-toplevel)"

    while IFS= read -r file; do
        rel="${file#$git_root/}"

        progress "Removing file from environment" "local_git" "rm" "$rel"
        progress "Committing changes" "local_git" "commit" "-m" "Remove $rel"
    done <<< "$selected_files"
}

cmd_tool_add() {
    local tool_name="${1:-}"

    if [ -z "$tool_name" ]; then
        tool_name=$(gum input --placeholder "Enter the name of the tool to add")
    fi

    package_install "$tool_name"
    config_tool_add "$tool_name"
}

cmd_tool_update() {
    local tool_name="${1:-}"

    if [ -z "$tool_name" ]; then
        config_read_tools
        tools+=("all")
        tool_name=$(gum choose "${tools[@]}")
    fi
    
    if [ "$tool_name" == "all" ]; then
        for t in "${tools[@]}"; do
            if [ "$t" == "all" ]; then
                continue
            fi

            progress "Updating tool" "$t"
        done
    else
        progress "Updating tool" "$tool_name"
    fi
    
}

cmd_tool_remove() {
    local tool_name="${1:-}"

    if [ -z "$tool_name" ]; then
        tool_name=$(gum input --placeholder "Enter the name of the tool to remove")
    fi

    config_tool_remove "$tool_name"
}

# ┌────────────────────────────────────────────────────────────┐
# │ Menu                                                       │
# └────────────────────────────────────────────────────────────┘

menu_env_file() {
    local input="${1:-}" && shift || true

    if [ -z "$input" ]; then
        command=$(gum choose "add")
    else
        command=$input
    fi
    
    case "$command" in
        add) cmd_env_add_file "$@" ;;
        *) usage ;;
    esac
}

menu_env() {
    local input="${1:-}" && shift || true

    if [ -z "$input" ]; then
        command=$(gum choose "create" "list" "push" "switch" "remove", "file")
    else
        command=$input
    fi
    
    case "$command" in
        commit) cmd_env_commit "$@" ;;
        create) cmd_env_create "$@" ;;
        delete) cmd_env_delete "$@" ;;
        list) cmd_env_list "$@" ;;
        push) cmd_env_push "$@" ;;
        switch) cmd_env_switch "$@" ;;
        remove) cmd_env_remove "$@" ;;
        file) menu_env_file "$@" ;;
        *) usage ;;
    esac
}

menu_tool() {
    local input="${1:-}" && shift || true

    if [ -z "$input" ]; then
        command=$(gum choose "add" "update" "remove")
    else
        command=$input
    fi
    
    case "$command" in
        add) cmd_tool_add "$@" ;;
        update) cmd_tool_update "$@" ;;
        remove) cmd_tool_remove "$@" ;;
        *) usage ;;
    esac
}

menu_main() {
    local input="${1:-}" && shift || true

    if [ -z "$input" ]; then
        command=$(gum choose "init" "env" "tool")
    else
        command=$input
    fi
    
    case "$command" in
        init) cmd_init "$@" ;;
        env) menu_env "$@" ;;
        tool) menu_tool "$@" ;;
        *) usage ;;
    esac
}

main() {
    print_app_header

    menu_main "$@"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
fi
