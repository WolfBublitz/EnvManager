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
git --git-dir="$HOME/.EnvManager/repo" --work-tree="$HOME" remote add origin <repository_url>
EnvManager push
```

## Manage your environment

### Variables

Variables are exported by enabled shell profiles when a new shell starts:

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
```

Package managers are detected automatically when possible. See
[supported managers and custom install commands](../../README.md#tools).
Custom commands are stored in the environment configuration and run with your user
privileges; EnvManager asks for confirmation before executing them.

### Shells

Enable a shell integration, inspect configured shells, or disable an integration:

```bash
EnvManager shells add zsh
EnvManager shells list
EnvManager shells remove zsh
```

Adding a shell migrates the existing profile into `$HOME/.EnvManager/<shell>/` and
installs loader files. The repository's shell scripts run at shell startup, so review
a configuration repository before trusting it.

### Files

Track files located inside your home directory, such as dotfiles:

```bash
EnvManager files add .gitconfig
EnvManager files list
EnvManager files remove .gitconfig
```

Removing a file stops tracking it but leaves the local copy in place. These operations
stage changes; run `EnvManager push` when you are ready to commit and publish them.

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

`push` reviews pending changes before committing and pushing. `pull` replaces the
current environment with the remote branch and can discard local commits and changes
to tracked files; review its preview before confirming. `switch --create` creates a
new environment if it does not exist.

## Configuration and safety

Each environment stores its YAML configuration at
`$HOME/.EnvManager/config.yml`. The repository can also contain files you add with
`files add`. Configuration is plain text: do not store passwords, tokens, or other
secrets in it.

Before cloning or enabling shells from a repository you do not control, review its
configuration and scripts. Tracked custom tool commands can execute with your user
privileges. For schema details, see the
[configuration specification](../../SPECIFICATION.md#data-storage).
