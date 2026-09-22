# EnvManager

A cross-platform CLI tool for managing your user environment — environment variables and
the tools you rely on — across multiple machines. Environment configurations are stored
as YAML files versioned in a git repository, with one branch per environment (e.g.
`laptop`, `office`, `server`), so you can keep several machine profiles in sync and
review changes to your setup like any other code.

## Features

- **Environment variables**: set, update, remove, and list user-scoped environment variables.
- **Tools**: install, remove, list, and update tools using your system's package manager
  (apt, dnf, yum, zypper, brew, scoop, choco, or winget — auto-detected, or explicitly selected).
- **Files**: track arbitrary files from your home directory (e.g. dotfiles) in the
  environment repository with `add`, `remove`, and `list`.
- **Git-backed configuration**: every environment is a branch containing a single
  `environment.yaml` file, so changes are versioned and can be pushed/pulled between machines.
- **Multiple environments**: switch between environments (branches) at any time.
- Color-coded CLI output (green for success, red for errors, yellow for warnings, blue for info)
  using [Spectre.Console](https://spectreconsole.net/).

## Installation

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
```

Downloads the `EnvManager` executable for your platform, installs it to
`$HOME/.local/bin` (override with `ENVMANAGER_INSTALL_DIR`), and makes it executable.

### Windows

```pwsh
irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing | iex
```

Downloads `EnvManager.exe`, installs it to `%LOCALAPPDATA%\Programs\EnvManager`
(override with `-InstallDirectory`), and adds that directory to your user `PATH`.

### Build from source

Build from source (requires the [.NET 11 SDK](https://dotnet.microsoft.com/)):

```bash
dotnet build src/EnvManager.csproj -c Release
```

The resulting executable is at `src/bin/Release/net11.0/EnvManager[.dll|.exe]`. You can
also publish a native AOT, single-file binary for your platform:

```bash
dotnet publish src/EnvManager.csproj -c Release -r <RID> /p:PublishAot=true
```

## Getting Started

EnvManager stores its configuration history in a **bare** git repository at
`$HOME/.EnvManager/repo` (override with the `ENVMANAGER_HOME` environment variable,
which is treated as `$HOME`, primarily useful for testing). The work tree for this
repository is your home directory itself — the same trick used by many dotfiles
managers — so tracked files (`environment.yaml` and anything added via `files add`)
live directly where you'd expect them, without a dedicated checkout directory:

```bash
git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME [command]
```

### Starting fresh (no remote yet)

```bash
EnvManager init --environment laptop
```

Creates a brand-new, local-only bare repository with a single environment branch.
Attach a remote later (e.g.
`git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME remote add origin <url>`) and
run `EnvManager push` once you're ready to share it.

### Cloning an existing configuration repository

```bash
EnvManager clone https://github.com/you/env-config.git --environment laptop
```

Clones the repository and checks out the `laptop` branch, creating it (as an empty
environment) if it doesn't exist yet.

## Usage

```text
EnvManager [command]

Commands:
  clone <repository_url>     Clones the environment configuration repository.
  init                        Initializes a new, local-only environment configuration repository.
  variable                    Manage environment variables and configurations.
  tools                       Manage tools and configurations.
  files                       Manage files and configurations in the environment repository.
  push                        Pushes the current environment configuration to the remote repository.
  pull                        Pulls the latest environment configuration from the remote repository.
  switch <environment_name>   Switches to a different environment configuration.
```

### Variables

```bash
EnvManager variable set <variable_name> <variable_value>   # Add or overwrite a variable
EnvManager variable update <variable_name> <variable_value> # Update an existing variable
EnvManager variable remove <variable_name>                  # Remove a variable
EnvManager variable list                                    # List all variables
```

### Tools

```bash
EnvManager tools add <tool_name> [--package-manager <name>] [--version <version>]
EnvManager tools remove <tool_name>
EnvManager tools update [tool_name]   # Updates one tool, or every tracked tool when omitted
EnvManager tools list
```

The package manager is auto-detected from your operating system and installed tooling
unless `--package-manager` is specified explicitly. Supported package managers: `apt`,
`dnf`, `yum`, `zypper`, `brew`, `scoop`, `choco`, `winget`.

### Files

```bash
EnvManager files add <file_name>      # Track a file from your home directory
EnvManager files remove <file_name>   # Stop tracking a file (left in place on disk)
EnvManager files list                 # List all files tracked for the current environment
```

`file_name` is resolved relative to your home directory (or as an absolute path inside
it); paths outside the home directory are rejected.

### Synchronizing environments

```bash
EnvManager push    # Push the current environment's committed changes to the remote
EnvManager pull    # Pull the latest committed changes for the current environment
EnvManager switch <environment_name> [--create]   # Switch to a different environment
```

Use `--create` with `switch` to create a new, empty environment when it doesn't exist yet
locally or on the remote.

Before checking out an environment, EnvManager moves every existing file tracked by either
the current or destination environment into a unique backup directory under
`$HOME/.EnvManager/repo/overwritten-files`. This prevents a branch switch from overwriting
local files; the destination environment's version is then checked out normally.

## Configuration Storage

Each environment is stored as a single `environment.yaml` file at the root of its own git
branch (i.e. directly in your home directory when that branch is checked out):

```yaml
name: laptop
shells: []
variables:
  EDITOR: nvim
tools:
- name: git
  packageManager: brew
  version: null
```

Any files added with `files add` are tracked on the same branch, alongside
`environment.yaml`, at their original location relative to the home directory.

## Development

- Language: C# / .NET 11
- CLI framework: [System.CommandLine](https://github.com/dotnet/command-line-api)
- Console rendering: [Spectre.Console](https://spectreconsole.net/)
- YAML serialization: [YamlDotNet](https://github.com/aaubry/YamlDotNet)
- See [SPECIFICATION.md](SPECIFICATION.md) for the full project specification.

```bash
dotnet build src/EnvManager.csproj
```
