# ADR 0003 — .NET 10 LTS target, Apache-2.0 license

- Status: Accepted
- Date: 2026-11-14
- Related: [integration.md](../architecture/integration.md#packaging)

## Context

Two low-cost decisions that are expensive to reverse once packages are published:
the target framework and the license.

## Decision

### Runtime

- Target **`net10.0`** for all packages. Single target framework, no multi-targeting
  before 1.0.
- Language version: `latest` with nullable reference types enabled and warnings as
  errors in CI.
- Avalonia **12.x**, pinned as a minimum with an explicit upper bound.

### License

- **Apache-2.0** for the whole repository (framework, workbench, samples).

## Consequences

### Positive

- .NET 10 is an LTS release: three years of support, so host applications can adopt the
  component without chasing runtime upgrades.
- The workspace has SDK 10.0.403 available, so the target builds locally without any
  extra installation.
- Apache-2.0 includes an explicit patent grant and a trademark clause, which matters for
  a framework that other companies embed in products. MIT is silent on patents.
- Apache-2.0 is compatible with the usual corporate open-source policies, unlike
  copyleft licenses, which would rule out embedding.

### Negative / costs

- A single target framework means hosts on `net8.0`/`net9.0` cannot reference the
  packages directly. This is acceptable while there are no users; revisit at 1.0 if
  demand appears (multi-targeting is additive then).
- Apache-2.0 requires a `NOTICE` file and preserving license headers, which is slightly
  more ceremony than MIT.

### Explicitly not allowed

- No dependency with a copyleft license (GPL/LGPL/AGPL) — it would poison the embedding
  use case.
- No dependency that is source-available-only or requires a commercial license for
  redistribution.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| `net9.0` / `net8.0` | Older LTS offers broader reach but forces older language features; the component has no users to protect yet. |
| Multi-targeting `net8.0;net10.0` | Doubles CI and hides API-availability problems; premature. |
| MIT | Fine, but no patent grant — weaker for corporate embedding. |
| GPL | Incompatible with the component-in-a-product goal. |
