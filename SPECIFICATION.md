# Specification

This document outlines the specification for the EnvManager project. It includes the requirements, design considerations, and expected behavior of the system.

## Purpose

The purpose of the EnvManager project is to provide a robust and user-friendly tool for managing user environment consisting of:

- Environment variables
- Shell configuration files (e.g., .bashrc, .zshrc)
- Path management
- Aliases and functions
- Tools and utilities configuration (e.g., git, editor settings)
- Plugin and extension management for shells and editors

## Tech Stack

The EnvManager project will utilize the following technologies:

- Programming Language: C# / .NET11
- Shell Scripting: Bash, Zsh, Fish, PowerShell, and CMD
- Configuration Management: YAML for storing environment configurations
- Version Control: Git
- Testing Framework: TUnit
- CLI Framework: System.CommandLine
- Package Management: NuGet for managing C# dependencies

### CLI Input and Output

- Use [Spectre.Console](https://spectreconsole.net/) for rendering CLI output.
- Use explicit terminal confirmation for operations that execute shared custom commands or discard local changes; confirmations default to no.
- Use progress indicators for long-running operations where appropriate.
- Provided color-coded output for better readability:
  - green for successful operations
  - red for errors
  - yellow for warnings
  - blue for informational messages

## Project

### Structure

The project structure for EnvManager will be organized as follows:

```text
EnvManager/
├── src/
│   ├── EnvManager.csproj
├── tests/
│   ├── Directory.build.props
│   └── UnitTests/
├── docs/
├── .gitignore
├── Directory.build.props
├── Directory.packages.props
├── CHANGELOG.md
├── README.md
└── SPECIFICATION.md
```

| Item                          | Description                                                                   |
|-------------------------------|-------------------------------------------------------------------------------|
| `src`                         | Contains the main source code for the EnvManager project.                     |
| `tests`                       | Contains the test projects and test-related files for the EnvManager project. |
| `tests/Directory.build.props` | Contains the directory build properties for the test projects.                |
| `docs`                        | Contains documentation files for the EnvManager project.                      |
| `.gitignore`                  | Specifies files and directories to be ignored by Git.                         |
| `Directory.build.props`       | Centralized MSBuild properties for the solution.                              |
| `Directory.packages.props`    | Centralized NuGet package versions for the solution.                          |
| `CHANGELOG.md`                | Contains the changelog for the EnvManager project.                            |
| `README.md`                   | Contains the readme for the EnvManager project.                               |
| `SPECIFICATION.md`            | Contains the specification for the EnvManager project.                        |

### Settings

- Use native AOT
- Target multiple platforms (Windows, Linux, macOS)
- Use central package management
- enable nullable reference types

## Data Storage

The backend of the EnvManager uses a git repository to store and manage environment configurations.

- Each environment configuration is stored as a separate branch.
- The branch name corresponds to the environment name (e.g., `laptop`, `office`, `server`).

Inside each branch, the environment configuration is stored as `$HOME/.EnvManager/config.yml`. This is the canonical configuration file and is tracked in the bare repository's home-directory work tree. It shall be created if it does not exist. Other files are tracked at their paths relative to `$HOME`.

The first field in `config.yml` is `formatVersion`, initially `1.0`. Configuration files without this field are treated as format version `1.0`. On application startup, EnvManager checks the current configuration against the latest format supported by that app version, adds missing schema metadata, and commits a migration when needed. Configurations using an unsupported format version are not modified and produce an error. See the [configuration JSON Schema](schemas/config/config.schema.1.0.0.yaml) for the document schema and compatibility rules. Tagged releases publish each versioned schema file as a separate release asset.

### Git Setup

The Git repository for storing environment configurations is initialized as a bare repository in the user's home directory under `.EnvManager/repo`.

```bash
git clone --bare $HOME/.EnvManager/repo
git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME --local status.showUntrackedFiles no
git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME checkout main
```

All subsequent Git operations for managing environment configurations will be performed on this bare repository like:

```bash
git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME [command]
```

EnvManager shall move all files that might be overwritten by the environment configuration to a safe location before checking out a new branch.

### Errors

In case of errors during Git operations, EnvManager shall provide meaningful error messages to help the user understand and resolve the issue.

Always provide:

- The Git command that failed.
- The exit code of the Git command.
- The standard output and standard error of the Git command.

## Command Line Interface (CLI)

The EnvManager project will provide a command-line interface (CLI) for managing user environment configurations. The CLI will support the following commands:

The CLI structure follows the following typical command-subcommand pattern:

```yaml
envmanager:
  init:   # Initializes a new environment configuration see [Git Setup](#git-setup)
  variable:
    set:    # Sets a (new) environment variable or configuration.
    remove: # Removes an existing environment variable or configuration.
    list:   # Lists all environment variables and configurations.
    update: # Updates an existing environment variable or configuration.
  settings:
    edit:   # Selects a supported key and adds or overwrites its value.
    list:   # Lists all configured settings.
  tools:
    add:    # Adds a new tool or configuration.
    remove: # Removes an existing tool or configuration.
    list:   # Lists all tools and configurations.
    update: # Updates an existing tool or configuration.
  shells:
    add:    # Enables a shell and installs its profile loader.
    remove: # Disables a shell and restores its backed-up profile files.
    list:   # Lists shells configured for the current environment.
  files:
    add:    # Adds a new file or configuration (to the repository)
    remove: # Removes an existing file or configuration (from the repository)
    list:   # Lists all files and configurations (in the repository).
  push:     # Pushes the current environment configuration to the remote repository.
  pull:     # Pulls the latest environment configuration from the remote repository.
  switch:   # Switches to a different environment configuration.
  update:   # Checks for and installs a newer EnvManager release.
```

### Details

The `Details` section provides more in-depth explanations of the CLI commands and their usage.

#### Root Command

```bash
Description:
  The root command for the EnvManager CLI.

Syntax: 
  EnvManager [command]

Commands:
  init        Initializes a new local environment configuration.
  clone       Clones the environment configuration.
  variable    Manage environment variables and configurations.
  settings   Manage key-value settings stored in config.yml.
  tools      Manage tools and configurations.
  shells      Manage supported shells and their profile integration.
  push        Push the current environment configuration to the remote repository.
  pull        Pull the latest environment configuration from the remote repository.
  switch      Switch to a different environment configuration.
  update      Check for and install a newer EnvManager version.
```

#### Clone Command

```bash
Description:
  Clones the environment configuration.

Syntax:
  EnvManager clone [repository_url]
```

Arguments:
  repository_url    The URL of the git repository to clone.

#### Variable Command

```bash
Description:
  Manage environment variables

Syntax:
  EnvManager variable [subcommand]

Subcommands:
  set    Adds a new environment variable or configuration.
  remove Removes an existing environment variable or configuration.
  list   Lists all environment variables and configurations.
  update Updates an existing environment variable or configuration.
```

##### Set Subcommand

```bash
Description:
  Sets a new environment variable or configuration.

Syntax:
  EnvManager variable set [variable_name] [variable_value]

Arguments:
  variable_name    The name of the environment variable to add.
  variable_value   The value of the environment variable to add.
```

##### Remove Subcommand

```bash
Description:
  Removes an existing environment variable or configuration.

Syntax:
  EnvManager variable remove [variable_name]

Arguments:
  variable_name    The name of the environment variable to remove.
```

##### List Subcommand

```bash
Description:
  Lists all environment variables and configurations.

Syntax:
  EnvManager variable list
```

#### Settings Command

```bash
Description:
  Manage key-value settings stored in the current environment's config.yml.

Syntax:
  EnvManager settings [subcommand]

Subcommands:
  edit   Selects a supported key and adds or overwrites its value.
  list   Lists all configured settings.
```

Available setting keys:

```bash
editor # The editor used by EnvManager.
```

##### List Subcommand

```bash
Syntax:
  EnvManager settings list
```

##### Edit Subcommand

```bash
Syntax:
  EnvManager settings edit
  # Prompts for an available key and then its value.
```

#### Tools Command

```bash
Description:
  Manage tools and configurations.

Syntax:
  EnvManager tools [subcommand]

Subcommands:
  add    Adds a new tool or configuration.
  remove Removes an existing tool or configuration.
  list   Lists all tools and configurations.
  update Updates an existing tool or configuration.
```

##### Add Subcommand

```bash
Description:
  Adds a new tool or configuration.

Syntax:
  EnvManager tools add [tool_name]

Arguments:
  tool_name    The name of the tool to add.
```

##### Remove Subcommand

```bash
Description:
  Removes an existing tool or configuration.

Syntax:
  EnvManager tools remove [tool_name]

Arguments:
  tool_name    The name of the tool to remove.
```

##### List Subcommand

```bash
Description:
  Lists all tools and configurations.

Syntax:
  EnvManager tools list
```

### Files Command

```bash
Description:
  Manage files and configurations in the environment repository.

Syntax:
  EnvManager files [subcommand]

Subcommands:
  add    Adds a new file or configuration to the repository.
  remove Removes an existing file or configuration from the repository.
  edit   Selects a tracked file and opens it in the configured editor.
  list   Lists all files and configurations in the repository.
```

##### Add Subcommand

```bash
Description:
  Adds a new file or configuration to the repository.

Syntax:
  EnvManager files add [file_name]

Arguments:
  file_name    The name of the file to add.
```

Adding a file stages it without committing or pushing. Run `EnvManager push` to
commit and publish staged changes. A remote is not required to add a file.
Other commands that create commits, including configuration updates and `shells add`,
can include already staged file changes.

##### Remove Subcommand

```bash
Description:
  Removes an existing file or configuration from the repository.

Syntax:
  EnvManager files remove [file_name]

Arguments:
  file_name    The name of the file to remove.
```

Removing a file stages it for removal without committing or deleting the local file.
Run `EnvManager push` to commit and publish the removal.
When `file_name` is omitted, EnvManager prompts for a tracked file and asks for
confirmation before staging its removal. This selection requires an interactive terminal.

##### Edit Subcommand

```bash
Syntax:
  EnvManager files edit
```

Prompts for a tracked file and opens it using the executable configured in the
`editor` setting. This requires an interactive terminal and a configured editor.

##### List Subcommand

```bash
Description:
  Lists all files and configurations in the repository.

Syntax:
  EnvManager files list
```

#### Push Command

```bash
Description:
  Lists all pending changes to tracked files, asks for confirmation, commits them with
  an auto-generated commit message and pushes the current environment configuration to the remote repository.

Syntax:
  EnvManager push [--yes]

Options:
  -y, --yes    Commit and push without asking for confirmation.
```

#### Pull Command

```bash
Description:
  Pulls the latest environment configuration from the remote repository.

Syntax:
  EnvManager pull [--yes]

Options:
  -y, --yes    Apply the pull without asking for confirmation.
```

Before applying, pull shall display incoming file changes, uncommitted tracked changes,
local-only commits, and existing files or directories that may obstruct incoming paths.
It shall ask for confirmation, defaulting to no, unless `--yes` is supplied. Declining
leaves the local branch, index, and work tree unchanged. The confirmed pull applies the
exact fetched commit shown in the preview.

Pull shall fetch the current environment's branch from `origin` and replace the local
branch, index, and work tree with that remote version, even when their histories have
diverged. Local-only commits and staged or unstaged changes to tracked files are
discarded. Untracked files or directories obstructing remote tracked paths can also be
overwritten or removed; unrelated untracked files are left alone. Pull does not back up
these changes. If the fetch fails, the local branch, index, and work tree remain unchanged.

#### Switch Command

```bash
Description:
  Switches to a different environment configuration.

Syntax:
  EnvManager switch [environment_name]

Arguments:
  environment_name    The name of the environment configuration to switch to.
```

#### Update Command

```bash
Description:
  Checks for and installs a newer EnvManager release compatible with the current platform.

Syntax:
  EnvManager update [--version <version>]

Options:
  --version <version>    Install this available version without prompting.
```

The command shall compare GitHub release semantic versions with the running version
and consider only releases that contain the current platform's executable. If there is
one newer compatible release, it shall be selected automatically. If there are multiple,
an interactive terminal shall display a selection list; noninteractive invocation shall
require `--version`. The downloaded release asset shall be validated against its published
SHA-256 digest before replacing the current executable. On Windows, where the running
executable cannot be replaced in place, replacement shall be scheduled after the process
exits. Unsupported platforms, unavailable versions, download failures, and digest
mismatches shall be reported without replacing the existing executable.
The command shall require direct invocation of the installed EnvManager executable and
shall not attempt to replace a .NET host process.

## Features

### Shells

The user shall be able to configure a list of supported shells for the environment.

Possible shells include:

| Shell      | Operating System      |
|------------|-----------------------|
| bash       | Linux, macOS          |
| zsh        | Linux, macOS          |
| fish       | Linux, macOS          |
| powershell | Linux, macOS, Windows |
| cmd        | Windows               |

Use `shells add <shell>` to copy any existing profile contents into `$HOME/.EnvManager/<shell>/00_Profile` with the shell's corresponding script extension and replace the original profiles with EnvManager loaders. For bash and zsh use `.sh`, for fish `.fish`, for PowerShell `.ps1`, and for CMD `.cmd`. The migrated profile shall not be copied to the shell-profile backup directory. `shells remove <shell>` shall remove unmodified loader files while leaving the migrated profile in the shell directory. Installation shall stage profile replacements, the `01_EnvManager` PATH script, and the migrated profile, then roll back all changed files if any step fails. Shell loaders execute scripts from the environment repository; review and trust repository contents before enabling integration.

When a loader profile is not tracked in the current environment, `shells add` shall
commit it and push the current branch to `origin`. Existing tracked profiles shall not
be staged again by this operation, though any already staged changes are included in
the commit. A failed push shall be reported and leave the local commit available for a
later `push`; pushing also publishes any earlier local commits.

The installed shell profile shall only load scripts from the shell directory. EnvManager's executable directory shall be added to `PATH` by a separate `01_EnvManager` script in that directory. For each shell, the script shall use the extension listed above.

#### Bash

For bash the existing profile files (e.g., `~/.bashrc`, `~/.bash_profile`) shall be copied into `$HOME/.EnvManager/bash/00_Profile.sh` and replaced by:

```bash
for file in "$HOME/.EnvManager/bash/"*.sh; do
    [ -f "$file" ] && source "$file"
done
```

`$HOME/.EnvManager/bash/01_EnvManager.sh` shall contain:

```bash
export PATH="$HOME/.local/bin:$PATH"
```

#### Zsh

For zsh the existing profile files (e.g., `~/.zshrc`, `~/.zprofile`) shall be copied into `$HOME/.EnvManager/zsh/00_Profile.sh` and replaced by:

```bash
for file in "$HOME/.EnvManager/zsh/"*.sh; do
    [ -f "$file" ] && source "$file"
done
```

`$HOME/.EnvManager/zsh/01_EnvManager.sh` shall contain:

```bash
export PATH="$HOME/.local/bin:$PATH"
```

#### Fish

For fish the existing profile file (e.g., `~/.config/fish/config.fish`) shall be copied into `$HOME/.EnvManager/fish/00_Profile.fish` and replaced by:

```fish
if test -d $HOME/.EnvManager/fish
    for file in $HOME/.EnvManager/fish/*.fish
        source "$file"
    end
end
```

`$HOME/.EnvManager/fish/01_EnvManager.fish` shall contain:

```fish
set -gx PATH "$HOME/.local/bin" $PATH
```

#### PowerShell

For PowerShell the existing profile file (e.g., `$PROFILE`) shall be copied into `$HOME/.EnvManager/powershell/00_Profile.ps1` and replaced by:

```powershell
$path = "$HOME/.EnvManager/powershell"

if (Test-Path $path) {
    Get-ChildItem $path -Filter *.ps1 -File | Sort-Object Name | ForEach-Object { . $_.FullName }
}
```

`$HOME/.EnvManager/powershell/01_EnvManager.ps1` shall contain:

```powershell
$env:PATH = "$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH"
```

#### CMD

For CMD the existing profile file (e.g., `C:\Users\<User>\_cmdrc`) shall be copied into `%USERPROFILE%\.EnvManager\cmd\00_Profile.cmd` and replaced by:

```cmd
for %%f in ("%USERPROFILE%\.EnvManager\cmd\*") do (
    if exist "%%f" call "%%f"
)
```

`%USERPROFILE%\.EnvManager\cmd\01_EnvManager.cmd` shall contain:

```cmd
set PATH=%USERPROFILE%\.local\bin;%PATH%
```


### Tools

EnvManager allows you to manage tools for the environment. The tools can be added, removed, listed, and updated using a local package manager. It shall automatically detect and use the appropriate package manager for the operating system.

Alternatively, `tools add` accepts `--install-command` and optional `--update-command`
and `--remove-command` shell commands for a tool. These commands are stored with the
tool and run using `/bin/sh -c` on Unix or `cmd.exe /D /C` on Windows. An absent
update or remove command or a nonzero exit code causes the operation to fail; removal
then leaves the tool tracked. Custom commands cannot be combined with a package
manager or version option. Commands in shared configuration must be reviewed before
execution and must not contain secrets.

Before each custom command runs, EnvManager displays it and asks for confirmation;
declining (including end-of-input) does not execute the command. The prompt defaults
to no. `tools add`, `tools update`, and `tools remove` accept `--yes` to bypass the
prompt for a command the user has already reviewed.

#### Supported Package Managers

EnvManager supports the following package managers:

| Package Manager | Operating System |
|-----------------|------------------|
| apt             | Linux            |
| dnf             | Linux            |
| yum             | Linux            |
| zypper          | Linux            |
| brew            | Linux, macOS     |
| scoop           | Windows          |
| choco           | Windows          |
| winget          | Windows          |

On Linux, package operations for apt, dnf, yum, and zypper require administrator
privileges. When not already running as root, EnvManager invokes them via sudo,
which handles any authentication prompt. Availability checks do not require elevation.

### Environment Variables

EnvManager allows you to manage environment variables efficiently. You can set, update, remove, and list environment variables using the respective commands.

- Variable names must be valid portable environment-variable identifiers (ASCII letters, digits, and underscores; the first character is a letter or underscore).
- Variable values are stored in plaintext in `config.yml`; they may be committed and pushed. Secrets must not be stored there.
- On configuration load, update, environment switch, and pull, EnvManager generates shell-quoted exports for configured shells on the current operating system under `$HOME/.EnvManager/<shell>/`.
- Bash and Zsh exports use `.sh`, Fish uses `.fish`, PowerShell uses `.ps1`, and CMD uses `.cmd`; installed profile loaders source these files for newly started shells.
- Generated variable-export files are maintained by EnvManager and shall not be added separately with `files add`.
- A shell-startup change affects new shell processes, not already-running processes.
- CMD export values containing double quotes, percent signs, exclamation marks, or line breaks cannot be safely represented and shall fail with a clear error.
- Values from shared or cloned repositories shall be reviewed. Repositories and shell scripts are executable content and must be trusted before profile integration is enabled.

### Custom Tool Command Safety

- Before a custom install, update, or remove command runs, EnvManager shall display the command and request explicit approval, defaulting to no.
- The `tools add`, `tools update`, and `tools remove` commands may accept `--yes` to bypass the prompt for reviewed, trusted configuration.
- A declined command shall not run and shall not update tool tracking.
- Commands stored in a shared configuration must be reviewed before execution. Secrets must not be embedded in custom commands.
- `shells add` shall ask the user to trust the repository before installing a loader that will source its scripts; the prompt defaults to no. `shells add --yes` may bypass the prompt for a reviewed repository.

## Installation

The installation shall be possible via shell script (Linux/macOS) or PowerShell script (Windows) that can be downloaded and executed. The Linux shell installer shall use `curl` and standard Unix tools without requiring GitHub CLI (`gh`) and shall not verify release provenance. The macOS shell installer and Windows PowerShell installer shall require GitHub CLI (`gh`) with attestation-verification support. The installers shall:

- download the latest EnvManager executable for the respective operating system
- place it in a directory included in the system's PATH
- make the script executable

The release workflow shall run the TUnit test suite before publishing AOT binaries and shall
attest each release asset using GitHub build provenance. The macOS and Windows installers
shall verify the asset's attestation for this repository before installation.

### Download Url

```bash
https://github.com/WolfBublitz/EnvManager/releases/download/<version>/EnvManager-<os>-<arch>[.exe on Windows]
```

Replace:

- `<version>` with the desired version of EnvManager (e.g., `1.0.0-preview.1`)
- `<os>` with the target operating system (e.g., `linux`, `macos`, `windows`)
- `<arch>` with the target architecture (e.g., `x64`, `arm64`)

### Options

The installation script supports the following options:

| Option                      | Description                                                                                                                      |
|-----------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| `--version <version>`       | Specifies the version of EnvManager to install. Defaults to the latest version.                                                  |
| `--install-dir <directory>` | Specifies the directory where the EnvManager executable should be placed. Defaults to a directory included in the system's PATH. |
| `--help`                    | Displays help information about the installation script.                                                                         |

### Linux / MacOS

```bash
curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
```

The default `--install-dir` should be `$HOME/.local/bin`. It should be created if it does not exist.

### Windows

```pwsh
Invoke-RestMethod "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" | Invoke-Expression
```

The default `--install-dir` should be `$HOME/.local/bin`. It should be created if it does not exist.
