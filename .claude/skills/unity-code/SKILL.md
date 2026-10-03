---
name: unity-code
description: >-
  Unice's conventions for writing C# in this Unity project. Use whenever writing, editing, refactoring or reviewing any
  .cs file under Assets/Unice/Scripts — new features, bug fixes, one-line edits — so the code keeps the project's naming,
  structure, comments and Unity 6 APIs. Also use when deciding where a new class belongs (Domain vs Core vs Net).
---

# Writing code in Unice

The full rules live in `SPEC.md` (Code Style, Project Structure, Boundaries). This skill is the checklist to apply them,
plus the Unity 6 API guidance in `resources/unity-modern-guidelines.md`.

## Before you edit

1. Read `SPEC.md` → **Code Style** and **Boundaries** if you have not in this session.
2. Read `resources/unity-modern-guidelines.md` before writing new Unity API calls (Find APIs, Awaitable, pooling, C# 9…).
3. Pick the right place for new code:
   - **Rules, game state, bots, maps, wire formats** → `Assets/Unice/Scripts/Domain/` (assembly `Unice.Domain`,
     `noEngineReferences: true`). No `UnityEngine` here — if you need it, the code belongs in Core.
   - **Anything Unity** (MonoBehaviour, UI Toolkit, sprites, audio, PlayerPrefs, Firebase) → `Core/<topic>/`.
   - **Netcode / Unity Services / Vivox** → `Net/`, behind the `INetBackend` / `IVoiceChat` ports in Domain.
   - Match the existing topic folders (`Match/`, `Match/Bots/`, `Maps/`, `Online/`, `Ui/Screens/`, `Ui/InGame/`, …).

## Rules

- **Descriptive English names** for everything, including locals and parameters (`timeLeft`, `hidingSpot`, `receiver`).
  Allowed short names: `x`/`y`, `dt`, `i`/`j` in loops, `id`, `me` (this device's character), `ax, ay, bx, by` for points A/B.
- **One responsibility per class.** If a class starts doing a second thing, split it (see how `Hud` composes `MatchCard`,
  `RoleCard`, `PlayerListPanel`). UI pieces derive from `UiPart`; build elements with `UiKit` (`AddLabel`, `AddButton`, …).
- **`/// <summary>`** on every class and on every member whose purpose isn't obvious from its name; say *why* and the units
  (seconds, house pixels).
- **No magic numbers**: named `const` with a comment when the unit or reason isn't obvious. **No state strings**: enums.
- Everything is in namespace `Unice` (one namespace for the whole project — do not add per-folder namespaces).
- `sealed` classes; `partial` only when a split can't be a separate class (`Game` / `Game.Snapshot`).
- Player-facing text goes through `Localization.Get("key")` with the key added to `Localization.Strings` in pt, en and es.
- Handled exceptions: `Metrics.Error(error, "where")`, not a bare `Debug.LogWarning`. New analytics events: follow
  `docs/ANALYTICS.md` (snake_case, no dots) and add them to its table.

## Never

- Rename `ArtManifest` fields (JsonUtility matches `manifest.json` by name), `PlayerPrefs` keys (`unice.name`, `unice.kit`,
  `unice.lang`) or `Localization.Strings` keys.
- Change the bytes of the snapshot (`Game.Snapshot.cs`) or presence (`PresenceCodec`) without bumping
  `Game.SnapshotVersion` / `PresenceCodec.Version`.
- Change a rule in `Game`, `MatchHost`, `Rules` or `Bots/` without saying so: the game is a 1:1 port of the web version,
  and the characterization tests will fail on purpose.

## `.meta` files

Unity creates `.meta` files when the Editor imports a new file. When you add `.cs` files or folders without the Editor open
(as an agent usually does), create the `.meta` yourself so the commit is complete and GUIDs are stable:

```bash
printf 'fileFormatVersion: 2\nguid: %s' "$(uuidgen | tr -d - | tr A-Z a-z)" > Path/To/NewFile.cs.meta
```

Folders need `folderAsset: yes` (copy an existing folder `.meta` and change the guid). Move files together with their
`.meta` (`git mv` both) so references survive.

## After editing

Compile and run the tests with the `unice-run-tests` skill. If you moved or renamed files, update `SPEC.md` → Project
Structure (and `docs/ARCHITECTURE_AUDIT.md` if a layer changed).
