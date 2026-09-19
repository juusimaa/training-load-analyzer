# Quickstart: Validating the Material Design Refresh

**Feature**: 007-material-design-ui

How to prove this feature works. Everything below runs from the repository root on the `007-material-design-ui` branch.

## Prerequisites

- .NET 10 SDK (the solution targets `net10.0`)
- `Athlete:MaximumHeartRate` configured — the app refuses to start without it (`StartupTests`)
- `MudBlazor` 9.10.0 restored into `TrainingLoadAnalyzer.Web`. It is the **only** package this feature adds

## 1. The regression gate (SC-002)

```bash
dotnet test --project tests/TrainingLoadAnalyzer.Web.Tests/TrainingLoadAnalyzer.Web.Tests.csproj
```

**Expected before any change**: `total: 99, failed: 0`.

**Expected after the feature**: `failed: 0`. The count will be **lower than 99 before the new tests are added**, because Amendment 1 deletes `MetricsChartTests.cs` (4 tests) and 3 `polyline` assertions. Every one of those removals must be named in the completion review — amended SC-002 permits removal, not silence. This is the single most important check in the feature. If a pre-existing test fails, the fix is in the markup, not the test — SC-002 forbids weakening an assertion to accommodate the new presentation.

Their *arrange* step does change: `Services.AddMudServices()` and `JSInterop.Mode = JSRuntimeMode.Loose` are required before any MudBlazor component will render under bUnit. That is setup, not assertion, and is the one sanctioned edit to the existing test file ([research.md](research.md) R13).

**Do this first.** Adding the package and converting one component, then running this command, is the go/no-go spike for the whole approach.

```bash
dotnet test   # whole solution, before opening a PR
```

## 2. The automated visual-system checks

These are new with this feature and encode the criteria that would otherwise be opinions:

| Check | Proves |
|-------|--------|
| Contrast test | SC-005 — every declared pair meets its minimum, in **both** palettes |
| Colour-literal scan | FR-005, SC-009 — no colour outside `Theme/` |
| Palette slot-set equality | FR-025, FR-028 — light and dark populate the same slots |
| Text-content characterization | SC-001 — no string, figure or qualifier was lost |
| Rendered-output smoke test | Guards the `4c22009` class of bug: styles present on disk but absent from the browser |

All run inside the command in step 1. A failing contrast test names the pair and the measured ratio.

## 3. Run the application

```bash
dotnet run --project src/TrainingLoadAnalyzer.Web
```

Open the printed URL. With no data you land on the empty state; that is a valid surface to review, not a failure.

## 4. Manual checks that no test can make

Automation covers ratios and strings. It cannot tell you whether the result looks designed. These are the human-review items for the completion review.

### Both schemes (FR-025, FR-026, SC-010, SC-011)

macOS: System Settings → Appearance, toggle Light/Dark with the browser open.

- [ ] The scheme follows **without reloading the page** (`MudThemeProvider` observes this by default — no code of ours runs)
- [ ] On a **first load** with the OS in dark mode, note how obtrusive the light flash is before interop resolves. This is the known, accepted prerender trade-off (R14) — the check is whether it is tolerable, not whether it happens
- [ ] No surface keeps a colour from the other scheme — check the chart, the chips and `#blazor-error-ui`
- [ ] Content is identical in both: nothing vanishes, nothing becomes illegible
- [ ] Toggling mid-sync does not disturb the running sync

### Layout (FR-016 – FR-018, SC-004)

Browser devtools responsive mode.

- [ ] 320px: single column, no horizontal scrollbar, nothing clipped or overlapping
- [ ] 768px: reflow is sensible
- [ ] 2560px: content stops at the reading width instead of stretching
- [ ] 200% zoom: degrades to the narrow layout rather than clipping

### Accessibility (FR-020 – FR-023, SC-006, SC-007)

- [ ] Tab through the page: every interactive element shows a clearly visible focus ring
- [ ] Sync button and Connect link are at least 48×48
- [ ] Devtools → Rendering → emulate `achromatopsia`: every distinction outside the chart still readable — provenance by its word, qualifiers by their text
- [ ] In the **chart**, confirm the accepted regression is what you expect: the three series are no longer distinguishable by shape, only by legend order and hover labels (Amendment 1). Judge whether it is tolerable in practice
- [ ] Devtools → Rendering → emulate `prefers-reduced-motion`: no animation
- [ ] Heading order reads sensibly, and there is exactly one `<h1>`

### Information preservation, by eye (SC-001)

Alongside the automated check, confirm on a populated dashboard:

- [ ] `still settling` appears as text, not only as a coloured chip
- [ ] `estimated` / `measured` appear as words in every activity row
- [ ] `—` still marks every absent value
- [ ] The last-checked timestamp still reads `yyyy-MM-dd HH:mm`

## 5. States worth forcing

| State | How to reach it |
|-------|-----------------|
| Empty, unconnected | Fresh database, no Strava connection |
| Empty, connected | Connect, then do not sync |
| Insufficient chart history | Fewer than 30 days of activities |
| No previous week | Activities only in the current ISO week |
| Data unavailable | Point the connection string at an unreadable file |
| Error notice | Throw from a component to surface `#blazor-error-ui` |

## Definition of done

- [ ] `dotnet test` green; surviving pre-existing tests have unchanged assertions, and every deletion is listed in the completion review
- [ ] The rewritten empty-list test asserts the empty-state message, not the absence of `<li>`
- [ ] Every cell of the surface × state matrix in [data-model.md](data-model.md) reviewed in both schemes
- [ ] `MudBlazor` is the only package added; no popover/dialog/snackbar providers registered unless something came to need them
- [ ] No file under `src/TrainingLoadAnalyzer.Domain` touched
- [ ] Constitution compliance review recorded, as features 001–006 each did
