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
- **Git-backed configuration**: every environment is a branch containing a single
  `environment.yaml` file, so changes are versioned and can be pushed/pulled between machines.
- **Multiple environments**: switch between environments (branches) at any time.
- Color-coded CLI output (green for success, red for errors, yellow for warnings, blue for info)
  using [Spectre.Console](https://spectreconsole.net/).

## Installation

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

EnvManager stores its local clone of your configuration repository under
`~/.envmanager/repository` (override with the `ENVMANAGER_HOME` environment variable).

### Starting fresh (no remote yet)

```bash
EnvManager init --environment laptop
```

Creates a brand-new, local-only repository with a single environment branch. Attach a
remote later (e.g. `git -C ~/.envmanager/repository remote add origin <url>`) and run
`EnvManager push` once you're ready to share it.

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

### Synchronizing environments

```bash
EnvManager push    # Push the current environment's committed changes to the remote
EnvManager pull    # Pull the latest committed changes for the current environment
EnvManager switch <environment_name> [--create]   # Switch to a different environment
```

Use `--create` with `switch` to create a new, empty environment when it doesn't exist yet
locally or on the remote.

## Configuration Storage

Each environment is stored as a single `environment.yaml` file at the root of its own git
branch:

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

## Development

- Language: C# / .NET 11
- CLI framework: [System.CommandLine](https://github.com/dotnet/command-line-api)
- Console rendering: [Spectre.Console](https://spectreconsole.net/)
- YAML serialization: [YamlDotNet](https://github.com/aaubry/YamlDotNet)
- See [SPECIFICATION.md](SPECIFICATION.md) for the full project specification.

```bash
dotnet build src/EnvManager.csproj
```
