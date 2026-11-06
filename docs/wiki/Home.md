# EnvManager Wiki

EnvManager keeps environment settings and selected home-directory files in a Git
repository, with one branch per environment. Use this wiki to get started and find
common commands.

## Pages

- [User manual](Manual.md) — installation, setup, and everyday use.
- [Project README](../../README.md) — features, platform support, and build instructions.
- [Configuration specification](../../SPECIFICATION.md) — configuration and design details.

## Quick start

Install EnvManager using the instructions in the [user manual](Manual.md), then start
a local environment:

```bash
EnvManager init --environment laptop
EnvManager variable set EDITOR nvim
EnvManager push
```

To use an existing configuration repository instead, run
`EnvManager clone <repository_url> --environment laptop`.
