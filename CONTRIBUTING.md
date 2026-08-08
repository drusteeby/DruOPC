# Contributing to DruOPC

Thanks for wanting to help! DruOPC welcomes bug reports, documentation fixes and
pull requests. This page tells you everything you need to get a change from idea
to merged.

## Quick orientation

| Path | What lives there |
|---|---|
| `src/` | **DruOPC Simulator** — the OPC UA server (ASP.NET Core generic host) |
| `browser/` | **DruOPC Browser** — the web client (Blazor Server, project `DruOpc.csproj`) |
| `tests/` | Integration tests — each fixture boots a real server and talks to it with a real OPC UA client |
| `docs/` | [Getting started](docs/getting-started.md) and the [user manual](docs/user-manual.md) |

Solution file: `druopc.slnx`. Prerequisite: [.NET SDK](https://dotnet.microsoft.com/download) 10+.

```console
dotnet build druopc.slnx     # build everything
dotnet test tests            # run the full integration suite (~3 minutes)
dotnet run --project src     # run the simulator  (opc.tcp://localhost:50000)
dotnet run --project browser # run the browser    (http://localhost:5080)
```

## Reporting bugs

Open a [GitHub issue](https://github.com/drusteeby/DruOPC/issues) with:

- What you did, what you expected, what happened instead
- For browser issues: the server you connected to (vendor/model if a real PLC)
  and any error toast text
- For simulator issues: your `appsettings.json` changes and the log output

## Pull requests

1. Fork, branch from `main`, make your change.
2. **Add or update tests** when you change behavior. The suite runs real
   client/server round-trips — see `tests/PlcSimulatorFixture.cs` for how a
   fixture boots the simulator with configuration overrides, and
   `tests/AlarmAckTests.cs` for a typical end-to-end test.
3. Run `dotnet test tests` locally — all tests must pass.
4. Open the PR. CI (`build-and-test`) runs the Release build plus the full
   suite; **a green check is required before merge** (branch protection).
5. Keep PRs focused — one topic per PR merges much faster than a grab-bag.

### Claude Code integration

Two GitHub Actions bring [Claude Code](https://claude.com/claude-code) into
the workflow:

- **Automatic PR review** (`claude-code-review.yml`): every opened or updated
  PR gets a code review from Claude. Treat its comments like any reviewer's —
  address what's right, push back on what isn't. It doesn't block merges;
  only the `build-and-test` check is required.
- **@claude mentions** (`claude.yml`): mention `@claude` in an issue or PR
  comment to ask a question or request a change — e.g.
  `@claude why does this test fail?` or `@claude implement the fix described
  above`. Claude answers in a comment or pushes commits to a branch.

Both run on the maintainer's Claude subscription, so use mentions
purposefully.

### Code style

- Match the surrounding code — the repo favors explicit, readable C# with
  file-scoped namespaces and nullable enabled in the browser project.
- Warnings are errors (`common.props`); a clean build is part of a green CI.
- UI text in the browser should be understandable by someone new to OPC UA —
  when in doubt, use the terminology from the [glossary](docs/user-manual.md#glossary).

### Documentation

If your change is user-visible, update the matching doc: README for
configuration keys, `docs/user-manual.md` for browser features,
`docs/getting-started.md` only if the beginner path changes. The docs are held
to a "a beginner can follow this unaided" standard.

## Dependencies

Dependabot watches NuGet and GitHub Actions weekly. One deliberate pin:
**FluentAssertions stays at 7.x** — version 8 moved to a paid commercial
license. Don't bump it.

## Releasing (maintainers)

The version lives in `version.json` and must match the release tag; the
release workflow fails if they differ. To cut a release:

1. Bump `"version"` in `version.json` (e.g. to `1.0.3`) and commit.
2. Tag that commit `v1.0.3` and push the tag.

The tag push builds and publishes everything: GitHub release binaries,
GHCR containers, the NuGet package, and the snap — all versioned `1.0.3`.

## Licensing

DruOPC is MIT-licensed (see [LICENSE.md](LICENSE.md)) and builds on
[Azure-Samples/iot-edge-opc-plc](https://github.com/Azure-Samples/iot-edge-opc-plc)
and the [OPC Foundation UA .NET Standard stack](https://github.com/OPCFoundation/UA-.NETStandard)
(see [NOTICE.txt](NOTICE.txt)). By contributing you agree that your
contributions are licensed under the same MIT terms.
