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
- **Shell integration**: configure bash, zsh, fish, PowerShell, and CMD profile loaders with automatic backups of existing profile files.
- Color-coded CLI output (green for success, red for errors, yellow for warnings, blue for info)
  using [Spectre.Console](https://spectreconsole.net/).

## Installation

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
```

Downloads the latest `EnvManager` executable for the current Linux or macOS architecture,
installs it to `$HOME/.local/bin`, makes it executable, and adds that directory to your
shell PATH if needed.

### Windows

```pwsh
irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing | iex
```

Downloads `EnvManager.exe`, installs it to `%LOCALAPPDATA%\Programs\EnvManager`
(and adds that directory to your user `PATH`).

### Installer options

The shell installer accepts these options:

| Option | Description |
| --- | --- |
| `--version <version>` | Installs a specific release instead of the latest release. |
| `--install-dir <directory>` | Installs the executable into a specific directory. |
| `--help` | Displays installer help. |

For example, download the shell script to pass options:

```bash
curl -fsSLO https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh
bash install.sh --version 1.0.0 --install-dir "$HOME/bin"
```

The PowerShell installer exposes the equivalent `-Version` and `-InstallDir` parameters:

```pwsh
& ([scriptblock]::Create((irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing))) -Version 1.0.0 -InstallDir "$HOME\bin"
```

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

### Development container

With Docker and the VS Code Dev Containers extension installed, open this repository
in VS Code and select **Dev Containers: Reopen in Container**. The container includes
the .NET 11 SDK and Linux native AOT build dependencies, and restores both projects
when it is created.

From the container terminal, build and run the tests with:

```bash
dotnet build src/EnvManager.csproj
dotnet run --project tests/EnvManager.Tests/EnvManager.Tests.csproj
```

To publish a native binary, use the publish command above with the container's Linux
runtime identifier (for example, `linux-x64` or `linux-arm64`).

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
  shells                      Manage supported shells and their profile integration.
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
EnvManager tools add <tool_name> --install-command <command> [--update-command <command>] [--remove-command <command>]
EnvManager tools remove <tool_name>
EnvManager tools update [tool_name]   # Updates one tool, or every tracked tool when omitted
EnvManager tools list
```

The package manager is auto-detected from your operating system and installed tooling
unless `--package-manager` is specified explicitly. Supported package managers: `apt`,
`dnf`, `yum`, `zypper`, `brew`, `scoop`, `choco`, `winget`.
On Linux, `apt`, `dnf`, `yum`, and `zypper` operations run through `sudo` when
EnvManager is not already root. `sudo` may prompt in the terminal for the
password required by its policy; EnvManager does not collect or store it.
Unattended use requires an appropriate non-interactive sudo configuration.

For tools without a supported package manager, provide shell commands instead:

```bash
EnvManager tools add mytool --install-command 'sh ./install-mytool.sh' \
  --update-command 'sh ./update-mytool.sh' --remove-command 'sh ./remove-mytool.sh'
```

Commands run through `/bin/sh -c` on Linux and macOS or `cmd.exe /D /C` on Windows,
with the current working directory, environment, and terminal. The install command
cannot be combined with `--package-manager` or `--version`; include any version in
the command itself. Update and remove commands are optional, but the corresponding
operation fails without one and leaves the tool tracked. They can be added or changed
in `.EnvManager/config.yml`. A nonzero command exit code fails the operation.

These commands are stored in the Git-backed environment configuration and update or
remove executes them with your user privileges. Review commands from shared or cloned
configurations before running them, and do not put secrets in them.

### Shells

```bash
EnvManager shells add <shell_name>    # Enable a shell and install its profile loader
EnvManager shells remove <shell_name> # Disable a shell and restore its original profile
EnvManager shells list                # List shells configured for this environment
```

Supported shells are `bash`, `zsh`, `fish`, `powershell`, and `cmd`, subject to the
operating-system support described in [SPECIFICATION.md](SPECIFICATION.md#shells).
Before replacing an existing profile, EnvManager backs it up under
`$HOME/.EnvManager/backups/shell-profiles`. Newly tracked loader profiles are committed
and pushed to `origin` by `shells add`; this also pushes any earlier local commits.
If no remote is configured or the push fails, the local commit remains; configure or
repair the remote and run `EnvManager push` to retry. Already tracked profile files
are not staged again, but any changes already staged are included in the commit.
Shell-specific files loaded by the profile
shims belong in `$HOME/.EnvManager/<shell>/` and can be versioned with `files add`: use
`.sh` files for bash/zsh, `.fish` for fish, and `.ps1` for PowerShell.

### Files

```bash
EnvManager files add <file_name>      # Track a file from your home directory
EnvManager files remove <file_name>   # Stop tracking a file (left in place on disk)
EnvManager files list                 # List all files tracked for the current environment
```

`file_name` is resolved relative to your home directory (or as an absolute path inside
it); paths outside the home directory are rejected.

`files add` commits the file locally without pushing or requiring a remote. Run
`EnvManager push` separately when you are ready to publish the commit.

### Synchronizing environments

```bash
EnvManager push    # List pending changes, confirm, commit (auto-generated message) and push (-y skips the prompt)
EnvManager pull    # Preview and confirm the destructive pull (-y skips the prompt)
EnvManager switch <environment_name> [--create]   # Switch to a different environment
```

Pull fetches and previews incoming changes, local changes and local-only commits before
asking for confirmation. It replaces the local branch and tracked work tree without a
backup; files or directories obstructing incoming paths may also be removed. Use
`EnvManager pull --yes` to skip confirmation.


**Pull is destructive:** it replaces the current environment with the `origin` branch,
discarding local-only commits and staged or unstaged changes to tracked files. Untracked
files or directories that obstruct remote tracked paths can also be overwritten or
removed. Unrelated untracked files are left alone, and pull creates no backup. A failed
fetch leaves the local branch, index, and work tree unchanged.

Use `--create` with `switch` to create a new, empty environment when it doesn't exist yet
locally or on the remote.

Before checking out an environment, EnvManager moves every existing file tracked by either
the current or destination environment into a unique backup directory under
`$HOME/.EnvManager/repo/overwritten-files`. This prevents a branch switch from overwriting
local files; the destination environment's version is then checked out normally. When
cloning, files from the remote's default branch are also backed up if present, since Git
may otherwise refuse to switch to the requested environment. Git failures report the failed
command, exit code, standard output, and standard error.

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
