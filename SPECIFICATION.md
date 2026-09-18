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
- Shell Scripting: Bash, Zsh
- Configuration Management: YAML or JSON for storing environment configurations
- Version Control: Git
- Testing Framework: TUnit
- CLI Framework: System.CommandLine
- Package Management: NuGet for managing C# dependencies

### CLI Input  Output

- Use [Spectre.Console](https://spectreconsole.net/) for rendering CLI output.
- Use [Spectre.Console](https://spectreconsole.net/) for handling CLI input.
- Use progress bars or progress indicators for long-running operations.
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
| ----------------------------- | ----------------------------------------------------------------------------- |
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

Inside each branch, the environment configuration is stored as a single YAML file called `environment.yaml`.

## Command Line Interface (CLI)

The EnvManager project will provide a command-line interface (CLI) for managing user environment configurations. The CLI will support the following commands:

The CLI structure follows the following typical command-subcommand pattern:

```yaml
envmanager:
  init:   # Initializes a new environment configuration.
  variable:
    set:    # Sets a (new) environment variable or configuration.
    remove: # Removes an existing environment variable or configuration.
    list:   # Lists all environment variables and configurations.
    update: # Updates an existing environment variable or configuration.
  tools:
    add:    # Adds a new tool or configuration.
    remove: # Removes an existing tool or configuration.
    list:   # Lists all tools and configurations.
    update: # Updates an existing tool or configuration.
  file:
    add:    # Adds a new file or configuration.
    remove: # Removes an existing file or configuration.
    list:   # Lists all files and configurations.
    update: # Updates an existing file or configuration.
  push:     # Pushes the current environment configuration to the remote repository.
  pull:     # Pulls the latest environment configuration from the remote repository.
  switch:   # Switches to a different environment configuration.
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
  clone       Clones the environment configuration.
  variable    Manage environment variables and configurations.
  tools       Manage tools and configurations.
  push        Push the current environment configuration to the remote repository.
  pull        Pull the latest environment configuration from the remote repository.
  switch      Switch to a different environment configuration.
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

#### Push Command

```bash
Description:
  Pushes the current environment configuration to the remote repository.

Syntax:
  EnvManager push
```

#### Pull Command

```bash
Description:
  Pulls the latest environment configuration from the remote repository.

Syntax:
  EnvManager pull
```

#### Switch Command

```bash
Description:
  Switches to a different environment configuration.

Syntax:
  EnvManager switch [environment_name]

Arguments:
  environment_name    The name of the environment configuration to switch to.
```

## Features

### Shells

The user shall be able to configure a list of supported shells for the environment.

Possible shells include:

| Shell      | Operating System      |
| ---------- | --------------------- |
| bash       | Linux, macOS          |
| zsh        | Linux, macOS          |
| fish       | Linux, macOS          |
| powershell | Linux, macOS, Windows |
| cmd        | Windows               |

### Tools

EnvManager allows you to manage tools for the environment. The tools can be added, removed, listed, and updated using a local package manager. It shall automatically detect and use the appropriate package manager for the operating system.

#### Supported Package Managers

EnvManager supports the following package managers:

| Package Manager | Operating System |
| --------------- | ---------------- |
| apt             | Linux            |
| dnf             | Linux            |
| yum             | Linux            |
| zypper          | Linux            |
| brew            | Linux, macOS     |
| scoop           | Windows          |
| choco           | Windows          |
| winget          | Windows          |

### Environment Variables

EnvManager allows you to manage environment variables efficiently. You can set, update, remove, and list environment variables using the respective commands.

- Environment variables shall be set on user scope.
- They shall be set for all configured shells.
