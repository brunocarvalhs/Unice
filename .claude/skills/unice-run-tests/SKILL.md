---
name: unice-run-tests
description: >-
  How to compile Unice and run its Edit Mode tests from the command line (no Rider/MCP needed). Use after any change to
  Assets/Unice/Scripts, before committing, or when the user asks to run tests, check the build, or see if something broke.
---

# Running Unice's tests

## Run all Edit Mode tests (real Unity compile + tests)

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity   # macOS; on Windows/Linux use the Hub's path
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults /tmp/unice-results.xml -logFile /tmp/unice-unity.log
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' /tmp/unice-results.xml | head -1
```

- Takes a few minutes. Run it in the foreground with a long timeout (≈10 min) — do not poll.
- `-runTests` quits by itself; do not add `-quit`.
- **The project must not be open in another Unity Editor** (the batch run can't lock it). An Editor open on a *different*
  project is fine.
- Exit code 0 means every test passed. Any other code: read the XML (failed tests) and the log (compile errors) below —
  a missing or empty XML means the run never reached the tests.

## Read the results

```bash
# compile errors (the run stops before tests if any)
grep -E "error CS" /tmp/unice-unity.log | head
# failed tests with their messages
grep -B2 -A8 'result="Failed"' /tmp/unice-results.xml | grep -E "test-case|message|CDATA" | head -40
# count of tests in one fixture
grep -c 'classname="Unice.MatchHostTests"' /tmp/unice-results.xml
```

## Where the tests are

- `Assets/Unice/Scripts/Domain/Tests` — rules, bots, snapshot, host election, maps (assembly `Unice.Domain.Tests`).
- `Assets/Unice/Scripts/Core/Tests` — profile (`Unice.Core.Tests`).
- CI runs the same suites on every pull request (`.github/workflows/pulls.yml`) and publishes a coverage report.

## When to report what

- Report the exact numbers (`total / passed / failed`) and paste failing test names with their message.
- Passing tests do not cover UI, audio, rendering or networking — say so, and list the manual check from the
  `unice-test-design` skill when your change touched those.
