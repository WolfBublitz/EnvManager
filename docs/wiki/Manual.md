# EnvManager User Manual

EnvManager manages environment variables, tools, shell setup, and selected files.
Configuration is stored in a Git repository under `$HOME/.EnvManager/repo`, with one
branch per environment. Tracked files are checked out relative to your home directory.

## Install

On Linux or macOS:

```bash
curl -fsSL https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.sh | bash
```

On Windows, run in PowerShell:

```powershell
irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing | iex
```

The installers place EnvManager in `$HOME/.local/bin` and add it to your user PATH.
The macOS and Windows installers require GitHub CLI (`gh`) to verify the release
attestation. See the [README](../../README.md#installation) for installer options.

## Start or join an environment

Run `EnvManager setup` for guided creation or cloning, remote setup, and optional shell
integration in an interactive terminal.

Create a new local-only environment:

```bash
EnvManager init --environment laptop
```

Or clone an existing configuration repository:

```bash
EnvManager clone https://github.com/you/env-config.git --environment laptop
```

If you started locally, add a remote and publish your environment when ready:

```bash
EnvManager remote set <repository_url>
EnvManager push
```

Run `EnvManager status` to see the active environment and synchronization state, or
`EnvManager environment list` to discover available environments.

## Manage your environment

### Variables

Variables are exported by addon files loaded by enabled shell profiles when a new shell starts:

```bash
EnvManager variable set EDITOR nvim
EnvManager variable list
EnvManager variable update EDITOR vim
EnvManager variable remove EDITOR
```

Changes do not alter processes in already-running shells.

### Tools

Add a tool using a supported package manager, or list and update tracked tools:

```bash
EnvManager tools add git
EnvManager tools list
EnvManager tools update git
EnvManager tools remove git
EnvManager tools apply --preview
EnvManager tools apply
```

Package managers are detected automatically when possible. See
[supported managers and custom install commands](../../README.md#tools).
Custom commands are stored in the environment configuration and run with your user
privileges; EnvManager asks for confirmation before executing them.
`tools apply` lets you select recorded tools to install; it does not detect whether
they are already installed.

### Shells

Enable a shell integration, inspect configured shells, or disable an integration:

```bash
EnvManager shells add zsh
EnvManager shells list
EnvManager shells remove zsh
```

Adding a shell replaces its shell profile (the file loaded directly by the shell, such
as `.bashrc`) with a loader. Existing shell profile contents are preserved as addon files
under `$HOME/.EnvManager/<shell>/`, including the `00_Profile` addon file and a
`01_EnvManager` addon file that adds `$HOME/.local/bin` to `PATH` and defines the
`em` alias for `EnvManager`. Startup also adds this alias to existing configured shell
integrations when it is missing. Addon files run at shell startup, so review a
configuration repository before trusting it.

### Files

Track files located inside your home directory, such as dotfiles. File commands resolve
relative paths from the current working directory; absolute paths inside your home
directory are also accepted:

```bash
EnvManager files add .gitconfig
EnvManager files list
EnvManager files remove .gitconfig
EnvManager files edit .gitconfig
EnvManager files copy-to-environment .gitconfig office --publish
```

`files edit` opens a tracked file in the executable configured by the `editor` setting
(for example, `vim` or `code`); omit the path to select one interactively.
Removing a file stops tracking it but leaves the local copy in place. File add and remove
operations stage changes; run `EnvManager push` when you are ready to commit and publish them.
`files copy-to-environment` prompts for omitted arguments, then commits the file's current
contents on the target branch without switching the active environment. `--publish`
pushes that branch, including any earlier local commits.

### Settings

Edit or list supported configuration settings:

```bash
EnvManager settings edit
EnvManager settings list
```

The currently supported setting is `editor`.

## Sync and switch environments

```bash
EnvManager push
EnvManager pull
EnvManager switch office
EnvManager switch travel --create
```

`push` reviews pending changes before committing and pushing. `pull` previews changes
and only fast-forwards; conflicting local files or divergent history are not discarded.
`switch --create` creates a
new environment if it does not exist.

`EnvManager backups list` shows files preserved by clone and switch. Use
`EnvManager backups restore <backup-id> <file> --to <destination>` to recover a file
without overwriting an existing destination.

## Configuration and safety

Each environment stores its YAML configuration at
`$HOME/.EnvManager/config.yml`. The repository can also contain files you add with
`files add`. Configuration is plain text: do not store passwords, tokens, or other
secrets in it.

Before cloning or enabling shells from a repository you do not control, review its
configuration and scripts. Tracked custom tool commands can execute with your user
privileges. For schema details, see the
[configuration specification](../../SPECIFICATION.md#data-storage).
