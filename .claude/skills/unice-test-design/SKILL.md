---
name: unice-test-design
description: >-
  How to design and write tests in Unice. Use whenever adding or changing tests, fixing a bug (write the failing
  reproduction test first), or changing game rules, bots, snapshot/presence formats or host election — anything in
  Assets/Unice/Scripts/Domain. Also use to decide what can be tested automatically and what needs a manual check.
---

# Designing tests in Unice

Adapted for this project from the test design method in
[nowsprinting/unity-coding-skills](https://github.com/nowsprinting/unity-coding-skills) (`test-designing-guide`, Unlicense).
The parts that assume Play Mode-only runtime tests, Test Helper packages or Rider tooling were left out, because Unice
deliberately keeps its rules in a Unity-free assembly tested in Edit Mode.

## Where tests go

| What is being tested | Where | Mode |
|---|---|---|
| Rules, bots, maps, snapshot/presence bytes, host election, text helpers (`Unice.Domain`) | `Assets/Unice/Scripts/Domain/Tests` (`Unice.Domain.Tests`) | Edit Mode — the code has no Unity dependency, so this is fast and exact |
| Core code that can run without a scene (e.g. `ProfileStore` with `PlayerPrefs`) | `Assets/Unice/Scripts/Core/Tests` (`Unice.Core.Tests`) | Edit Mode |
| UI Toolkit screens, rendering, audio, Netcode/Vivox | — | **Manual** for now (see "Manual checks") |

Prefer moving logic into Domain over writing a Play Mode test for it: if a rule is hard to test, it is usually in the wrong layer.

## Choosing test cases

- **Equivalence partitions:** one test method = one partition = one definite expected outcome. If the expected result changes
  with the input, those inputs are different partitions → different methods. Several inputs with the same outcome → one
  parameterized method (`[TestCase]`).
- **Boundaries** only where the spec treats the edge differently (e.g. `Rules.CatchRange`, cooldown reaching 0, last round).
- **State transitions** for `Phase` (Home → Lobby → Hide → Hunt → Result → Final) and bot modes: cover each direct transition.
- **Error guessing** typical of this game: a player leaving mid-round (becomes a bot), host leaving (`MatchHost.TakeOver`),
  stale or malformed network data (`PresenceCodec.TryRead`, `Game.ReadEpoch`), actions arriving late (latency slack),
  pressing an action during the wrong phase or while on cooldown.
- **Randomness** (`GameRandom`): assert invariants that hold for any draw (the pick is in the list, the shuffle keeps the
  items, the bot ends up hidden or fleeing), or run a loop and assert on the set of outcomes — never on one exact random value.
- **Bug fixes:** first a reproduction test that fails on the current code, then the fix, then regression tests for nearby behavior.
- **Refactors:** cover-and-modify — make sure behavior is pinned by tests *before* moving code.

### Characterization tests (`GameTests`, `GameAiTests`, `MatchHostTests`, `SnapshotTests`)

They pin the **current** behavior of the port of the web version. When one fails after a change, the rule changed: confirm with
the user whether that was intended before updating the expectation. Never "fix" a characterization test just to make it green.

## Writing the test

- Name: `<MethodName>_<Condition>_<Expected>` (e.g. `FindHost_SameEpoch_FirstToJoinWins`); `<Expected>` is a concrete
  outcome, never "Matches…"/"DependsOn…".
- `[TestFixture]` on the class; Arrange / Act / Assert separated by blank lines.
- One assertion per test, except when checking several properties of one resulting state — then give each assertion a message.
- **Assertion style:** the existing files use the classic model (`Assert.AreEqual`, `Assert.IsTrue`). Keep the style of the file
  you are editing; new test files may use the constraint model (`Assert.That(actual, Is.EqualTo(expected))`).
- Tests that read `Map` statics load the map in `[SetUp]` (`Map.Load(MapHouse.Build())`, see `MapTests`); tests that depend on
  a draw also call `GameRandom.Seed(...)` there (see `GameTests`). `PlayerPrefs` is restored in `[TearDown]` (`ProfileStoreTests`).

## Manual checks (not automated yet)

When a change touches UI, audio, rendering or networking, list the manual check in your summary instead of claiming it works:
- Solo match: menu → waiting room → rounds → final scoreboard.
- Online: two devices with the same build; create, join by code, play a round, close the host's app mid-round → the other takes over.

Run the tests with the `unice-run-tests` skill.
