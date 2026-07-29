# Tackle Defense Resolution Implementation Plan

**Goal:** A successful Q defense resolves one target's full slide-tackle contact while preserving re-checks after a dodge evasion.

**Architecture:** Combat distinguishes applied damage, a successful block, and an evasion. The slide-tackle contact ledger records applied and blocked contacts; DefenseController exposes a tackle-specific block hook that currently uses the existing directional block animation.

## Tasks

1. Extend the EditMode tackle defense test to execute two collision ticks and verify the defender remains unstunned.
2. Make CombatController retain the hit-resolution reason, record blocked tackle contacts, and route tackle hits through DefenseController's dedicated hook.
3. Run the focused regression tests and the full EditMode suite; inspect the scoped diff.
