# EnvManager

A cross-platform CLI tool for managing your user environment — environment variables and
the tools you rely on — across multiple machines. Environment configurations are stored
as YAML files versioned in a git repository, with one branch per environment (e.g.
`laptop`, `office`, `server`), so you can keep several machine profiles in sync and
review changes to your setup like any other code.

## Features

- **Environment variables**: set, update, remove, and list variables exported by configured
  shell profiles when new shells start.
- **Tools**: install, remove, list, and update tools using your system's package manager
  (apt, dnf, yum, zypper, brew, scoop, choco, or winget — auto-detected, or explicitly selected).
- **Files**: track arbitrary files from your home directory (e.g. dotfiles) in the
  environment repository with `add`, `remove`, and `list`.
- **Git-backed configuration**: each environment branch stores its configuration in
  `$HOME/.EnvManager/config.yml`, so changes can be reviewed and synchronized between machines.
- **Multiple environments**: switch between environments (branches) at any time.
- **Self-update**: check for compatible GitHub releases and replace the installed executable.
- **Shell integration**: install loaders in shell profiles (such as `.bashrc`) and preserve existing contents as addon files in the managed shell directory.
- **Executable discovery**: addon files add `$HOME/.local/bin` to `PATH` when loaded by a shell profile.
- **Shell alias**: configured shells provide `em` as a shortcut for `EnvManager`; startup migration adds it to existing shell integrations when missing.
- Color-coded CLI output (green for success, red for errors, yellow for warnings, blue for info)
  using [Spectre.Console](https://spectreconsole.net/).

## Installation

### Linux / macOS

The install script detects your operating system and CPU architecture, downloads the
latest release, and installs `EnvManager` to `$HOME/.local/bin`. It requires `bash` and
`curl`; on macOS, it also requires the [GitHub CLI](https://cli.github.com/) to verify
the release's build provenance.

```bash
curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
```

The installer makes the executable runnable and adds `$HOME/.local/bin` to your shell's
PATH if needed. On Linux, the installer does not verify the release's GitHub
build-provenance attestation.

To download an executable manually, open the
[latest release](https://github.com/WolfBublitz/EnvManager/releases/latest) and select the
asset for your platform:

| Platform | Release asset |
| --- | --- |
| Linux x64 | `EnvManager-linux-x64` |
| Linux ARM64 | `EnvManager-linux-arm64` |
| macOS Apple silicon (ARM64) | `EnvManager-macos-arm64` |

For example, to download the Linux x64 executable and install it to `$HOME/.local/bin`:

```bash
mkdir -p "$HOME/.local/bin"
curl -fL https://github.com/WolfBublitz/EnvManager/releases/latest/download/EnvManager-linux-x64 \
  -o "$HOME/.local/bin/EnvManager"
chmod 755 "$HOME/.local/bin/EnvManager"
```

Replace `EnvManager-linux-x64` with the release asset for your platform. If
`$HOME/.local/bin` is not already on your PATH, add it to the Bash startup file used by
your terminal. For Linux Bash, use `~/.bashrc`:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

For a macOS Bash login shell, use `~/.bash_profile` instead:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bash_profile
source ~/.bash_profile
```

The install script handles PATH setup automatically.
On macOS, prefer the install script because it verifies the release's build provenance;
manual downloads do not.

### Windows

In PowerShell, install the latest release with:

```pwsh
irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing | iex
```

Downloads `EnvManager.exe`, installs it to `$HOME\.local\bin` (creating the
directory if needed), and adds that directory to your user `PATH`. The installer requires
the GitHub CLI (`gh`) and verifies the release attestation before installing.

### Enable Bash integration

After installing EnvManager, initialize a local environment configuration or clone an
existing one, then enable Bash:

```bash
# Start a new, local-only environment configuration:
EnvManager init --environment laptop

# Or, instead, clone your existing configuration:
# EnvManager clone https://github.com/you/env-config.git --environment laptop

EnvManager shells add bash
```

`shells add bash` asks you to trust the configuration because its addon files will run when
Bash starts. It preserves existing shell profile contents under
`$HOME/.EnvManager/bash/00_Profile.sh` and installs shell profile loaders; open a new terminal
to load the integration. If the configuration has no Git remote, the changes are
committed locally and can be pushed after you configure one. See [Shells](#shells) for
details about shell profiles and addon files.

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

Run `EnvManager setup` in an interactive terminal for guided creation or cloning,
remote setup, and optional shell integration. Repository scripts must be reviewed and
trusted before enabling a shell.

EnvManager stores its configuration history in a **bare** git repository at
`$HOME/.EnvManager/repo` (override with the `ENVMANAGER_HOME` environment variable,
which is treated as `$HOME`, primarily useful for testing). The work tree for this
repository is your home directory itself — the same trick used by many dotfiles
managers — so tracked files (the configuration at `.EnvManager/config.yml` and anything
added via `files add`)
live directly where you'd expect them, without a dedicated checkout directory:

```bash
git --git-dir=$HOME/.EnvManager/repo --work-tree=$HOME [command]
```

### Starting fresh (no remote yet)

```bash
EnvManager init --environment laptop
```

Creates a brand-new, local-only bare repository with a single environment branch.
Attach a remote later with `EnvManager remote set <url>` and run `EnvManager push`
once you're ready to share it.

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
  setup                       Guides you through environment and shell setup.
  variable                    Manage environment variables and configurations.
  settings                    Manage key-value settings stored in config.yml.
  tools                       Manage tools and configurations.
  files                       Manage files and configurations in the environment repository.
  shells                      Manage supported shells and their profile integration.
  push                        Pushes the current environment configuration to the remote repository.
  pull                        Pulls the latest environment configuration from the remote repository.
  switch <environment_name>   Switches to a different environment configuration.
  environment list           Lists available environments.
  remote set <url>           Adds or changes the synchronization remote.
  status                     Shows the current environment and synchronization state.
  update [--version <version>] Checks for and installs a newer EnvManager version.
  downgrade [--version <version>] Selects and installs an earlier EnvManager version.
```

`EnvManager update` checks for newer releases with an asset for the current platform.
If several versions are available, an interactive terminal presents a selection list.
For scripts or redirected input, specify one explicitly, for example
`EnvManager update --version 1.2.0`. The downloaded executable is checked against the
release's published SHA-256 digest before replacing the current binary. On Windows,
replacement is scheduled for when the current process exits. Run the installed
`EnvManager` executable directly; self-update cannot replace a `dotnet` host when
invoked as `dotnet run` or `dotnet EnvManager.dll`.

`EnvManager downgrade` selects from earlier releases with an asset for the current
platform. If several versions are available, an interactive terminal presents a
selection list. For scripts or redirected input, specify one explicitly, for example
`EnvManager downgrade --version 1.2.0`. The downloaded executable is checked against
the release's published SHA-256 digest before replacing the current binary. On Windows,
replacement is scheduled for when the current process exits.

### Variables

```bash
EnvManager variable set <variable_name> <variable_value>   # Add or overwrite a variable
EnvManager variable update <variable_name> <variable_value> # Update an existing variable
EnvManager variable remove <variable_name>                  # Remove a variable
EnvManager variable list                                    # List all variables
```

### Settings

```bash
EnvManager settings edit                              # Select an available key and edit its value
EnvManager settings list                              # List all configured settings
```

Available setting keys are documented in the [specification](./SPECIFICATION.md#settings-command); currently, only `editor` is supported.

### Tools

```bash
EnvManager tools add <tool_name> [--package-manager <name>] [--version <version>]
EnvManager tools add <tool_name> --install-command <command> [--update-command <command>] [--remove-command <command>]
EnvManager tools remove <tool_name>
EnvManager tools update [tool_name]   # Updates one tool, or every tracked tool when omitted
EnvManager tools list
EnvManager tools apply [--preview]   # Review and selectively install recorded tools
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

These commands are stored in the Git-backed environment configuration and execute with
your user privileges. EnvManager displays each custom command and requires confirmation;
the prompt defaults to no. `tools add`, `tools update`, and `tools remove` accept `--yes`
to skip confirmation after you have reviewed the command. Treat a cloned configuration
repository and its shell scripts as executable code: review it before trusting it. Do not
put secrets in commands or variable values; configuration is stored in plaintext and can
be committed or pushed.

On another machine, `tools apply --preview` lists recorded installation actions without
running them. `tools apply` lets you select tools and confirms each installation; custom
commands are displayed and require separate trust. Installed state is not detected, so
review the list before choosing tools.

### Shells

```bash
EnvManager shells add <shell_name> [--yes] # Enable a shell after trusting repository scripts
EnvManager shells remove <shell_name> # Disable a shell, restore its shell profiles, and remove addon files
EnvManager shells list                # List shells configured for this environment
```

Supported shells are `bash`, `zsh`, `fish`, `powershell`, and `cmd`, subject to the
operating-system support described in [SPECIFICATION.md](SPECIFICATION.md#shells).
When adding a shell, its shell profile (the file loaded directly by the shell, such as
`.bashrc`) is replaced with a loader. Existing shell profile contents are preserved in an
addon file named `00_Profile` with the shell's script extension (`.sh`, `.fish`,
`.ps1`, or `.cmd`) under `$HOME/.EnvManager/<shell>/`. A `01_EnvManager` addon file
adds `$HOME/.local/bin` to `PATH`. The shell profiles and addon files are tracked with
the environment configuration. Removing the shell restores backed-up shell profiles or
removes unmodified loaders, removes the EnvManager-created addon file, and leaves the
`00_Profile` addon file in the shell directory. Newly tracked files are committed
locally by `shells add`; run `EnvManager push` when you want to publish them.
Already tracked shell profile and addon files
are not staged again, but any changes already staged are included in the commit.
Additional shell-specific addon files belong in `$HOME/.EnvManager/<shell>/` and are staged automatically with configuration
changes. Other files under `$HOME/.EnvManager/` can be versioned with `files add`; files under
`$HOME/.EnvManager/repo/` are EnvManager's internal Git database and cannot be added or removed.
Use
`.sh` files for bash/zsh, `.fish` for fish, and `.ps1` for PowerShell.
Shell profile installation stages replacement profiles and addon files, and rolls back
changes if an installation step fails. Configured variable exports are generated as addon files in the corresponding
shell directory and loaded on the next shell start; changes do not modify already-running
processes. Generated export addon files are maintained by EnvManager and staged with other shell
files; they can also be explicitly staged with `files add`. Enabling a shell prompts you to trust the repository because
its addon files will run at shell startup; `shells add --yes` bypasses this confirmation only
after you have reviewed the repository.

### Files

```bash
EnvManager files add <file_name>...                   # Track one or more files
EnvManager files remove [file_name]                   # Stop tracking a file (left in place)
EnvManager files edit [file_name]                     # Open a tracked file in the configured editor
EnvManager files list                                 # List tracked files
EnvManager files copy-to-environment [file] [environment] [--publish]
```

For `files add`, `remove`, `edit`, and explicit `copy-to-environment` paths, `file_name`
is resolved relative to the current working directory (or as an absolute path inside
your home directory); paths outside the home directory are rejected. Adding common
credential filenames produces a warning; review all files for secrets before publishing.
When `file_name` is omitted, EnvManager prompts you to select a tracked file and asks
for confirmation before staging its removal. This interactive mode requires a terminal.
`files edit` uses the `editor` setting as the editor executable (for example, `vim` or
`code`); omit the path to select a tracked file in a terminal.
`files copy-to-environment` prompts for omitted arguments in a terminal and commits the
file's current contents on the target branch without switching environments. Add
`--publish` to push the target branch directly, including any earlier local commits.

`files add` and `files remove` stage their changes without committing or requiring a
remote; removal leaves the local file in place. Run `EnvManager push` when you are
ready to commit and publish the staged changes.
Other commands that commit changes, including configuration updates and `shells add`,
can also include already staged files in their commits.

### Synchronizing environments

```bash
EnvManager push    # List pending changes, confirm, commit (auto-generated message) and push (-y skips the prompt)
EnvManager pull    # Preview and fast-forward safely (-y skips the prompt)
EnvManager switch [environment_name] [--create]   # Switch or select an environment
```

Pull fetches and previews incoming changes, local changes and local-only commits before
asking for confirmation. It only fast-forwards and refuses divergent histories or
conflicting local files. Use `EnvManager pull --yes` to skip confirmation; this does not
bypass those safeguards. A failed fetch leaves the local branch, index, and work tree
unchanged.

Use `--create` with `switch` to create a new, empty environment when it doesn't exist yet
locally or on the remote. Omit the name to select an existing environment interactively.

Before checking out an environment, EnvManager moves every existing file tracked by either
the current or destination environment into a unique backup directory under
`$HOME/.EnvManager/repo/overwritten-files`. This prevents a branch switch from overwriting
local files; the destination environment's version is then checked out normally. When
cloning, files from the remote's default branch are also backed up if present, since Git
may otherwise refuse to switch to the requested environment. Git failures report the failed
command, exit code, standard output, and standard error.

Run `EnvManager backups list` to see these backups. Recover an individual file with
`EnvManager backups restore <backup-id> <file> --to <destination>`; the destination must
be inside your home directory and must not already exist. Omit `--to` to restore to the
original location when it is free. Backups are retained after recovery.
These backups contain preserved working-tree files, not a complete snapshot of the Git
index or branch history. Restore the internally managed `config.yml` to an alternate
path for inspection.

## Configuration Storage

Each environment stores its configuration in `$HOME/.EnvManager/config.yml` in its
environment branch. Other files added with `files add` are tracked in the bare repository
with paths relative to the home directory:

```yaml
formatVersion: 1.0
name: laptop
shells:
  - bash
variables:
  EDITOR: nvim
settings: {}
tools:
  - name: git
    packageManager: brew
    version: null
```

On startup, EnvManager checks the current environment's configuration against the format
version supported by the installed app. It adds missing version or schema metadata and
commits those changes automatically. Configurations using an unsupported format version
are left unchanged and reported as an error. Files without `formatVersion` are treated
as version `1.0`.

See the [configuration JSON Schema](schemas/config/config.schema.1.0.0.yaml) for field and
compatibility details. Tagged releases publish versioned schema files as separate assets;
the current schema is available from the
[latest GitHub release](https://github.com/WolfBublitz/EnvManager/releases/latest/download/config.schema.1.0.0.yaml).
The YAML file is part of the checked-out home work tree at `.EnvManager/config.yml`.

## Wiki

See the [EnvManager wiki home](docs/wiki/Home.md) for a concise overview and the
[user manual](docs/wiki/Manual.md) for installation, setup, and common workflows.

## Development

- Language: C# / .NET 11
- CLI framework: [System.CommandLine](https://github.com/dotnet/command-line-api)
- Console rendering: [Spectre.Console](https://spectreconsole.net/)
- YAML serialization: [YamlDotNet](https://github.com/aaubry/YamlDotNet)
- See [SPECIFICATION.md](SPECIFICATION.md) for the full project specification.

```bash
dotnet build src/EnvManager.csproj
```
