// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: UpdateRelease.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

using Semver;

namespace EnvManager.Updates;

internal sealed record UpdateRelease(SemVersion Version, string Name, Uri DownloadUri, string Sha256);
