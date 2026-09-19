# Contract: Rendered Markup

**Feature**: 007-material-design-ui

What each component guarantees to emit after the restyle. It is a contract because two parties depend on it: the existing 99 tests, which assert on it, and the theme, which styles it. It exists so a restyle cannot silently change something a test binds to.

## Invariants — must not change

Load-bearing for SC-001 and SC-002. A change to any of these is a spec violation, not an implementation detail.

| Invariant | Why |
|-----------|-----|
| The sync control renders a `<button>` carrying class `sync` | `Find("button.sync")` and `Find("button")`. `<MudButton Class="sync">` satisfies this — Mud appends `Class` to the rendered root |
| The chart legend still reads Fitness, Fatigue, Form | FR-002 wording, and FR-013a — with the dash patterns gone this is now the *only* way to identify a series |
| The date range and "Not enough data to show trends (30+ days required)" stay, word for word | Our markup, outside the chart component; FR-002 |
| Every text string, number and date format is byte-identical | FR-002. All formatting stays in `Display` and `MetricsChart`; neither is touched |
| The page keeps exactly **one** `<h1>` | `Routes.razor` uses `<FocusOnNavigate Selector="h1" />` (R10) |
| The button's disabled condition stays `Status.IsRunning` | FR-001 |
| `Display.Missing` renders as `—` wherever a value is absent | FR-002, US2 scenario 1 |

## Per-component

### `MainLayout.razor`

```
<MudThemeProvider Theme="TrainingLoadTheme.Instance" @bind-IsDarkMode="isDarkMode" />
<MudLayout>
  <MudAppBar>
    <MudText Typo="Typo.h6" HtmlTag="span">Training Load</MudText>   span, NOT a heading (R10)
  <MudMainContent>
    <CascadingValue Value="isDarkMode" Name="IsDarkMode">
      @Body
```

`IsDarkMode` is cascaded because `MetricsChartView` needs it to pick the chart palette, and this is the one component that knows it (R5). Nothing else consumes it. `#blazor-error-ui` keeps its id, its text and its `.reload` / `.dismiss` children; only its colours move onto theme values so it works in dark (FR-015).

Only `MudThemeProvider` is added — not the popover, dialog or snackbar providers. Nothing in this application uses those features, and adding them speculatively is what Principle III forbids.

### `Dashboard.razor`

```
<MudContainer MaxWidth="MaxWidth.Large">
  <h1>Training Load</h1>              kept as a real h1, styled via Typo (R10)
  <SyncPanel/>
  <MudGrid>                            three metric cards, xs=12 sm=4 (FR-016)
    <MudItem><MetricTile/></MudItem>   ×3
  <WeeklyLoadPanel/> <MetricsChartView/> <RecentActivityList/>   each in a MudPaper
```

Empty, loading and unavailable states become `MudPaper` surfaces with their existing heading and text unchanged (FR-014).

### `MetricTile.razor`

```
<MudPaper>
  <MudText Typo="Typo.caption">        the label
  <MudText Typo="Typo.h3">             the value
  <MudChip>still settling</MudChip>    TEXT INSIDE THE CHIP, never replaced by it
```

The qualifier chips must keep rendering `still settling`, `estimated` and `partly estimated` as readable text. The chip is a container around that text. **This is the single most likely regression in the feature** (US2 scenario 2, FR-012) — a `MudChip` used with an icon or a colour instead of its label silently removes information a screen reader depends on.

### `SyncPanel.razor`

```
<MudPaper>
  <MudButton Class="sync" Disabled="@Status.IsRunning">     renders <button class="… sync">
  <MudText>                                                 the sync message
  <MudButton Href="/connect" Class="connect">               FR-010 — prominent, not a bare link
  <MudText Typo="Typo.caption">                             the last-checked timestamp
```

### `WeeklyLoadPanel.razor`

All four values kept. The weekly total takes `Typo.h3`; the judgement becomes a `MudChip` with its exact wording preserved.

### `RecentActivityList.razor`

```
<MudPaper>
  <h2>Recent activities</h2>                  kept
  <MudList Readonly>                          replaces <ul>/<li> (R15)
    <MudListItem>
      day / type / duration / load            all kept
      <MudChip>measured|estimated</MudChip>   word kept INSIDE (FR-012, FR-022)
  "Nothing recorded yet."                     kept for the empty state
```

`MudList` renders `<div>`s, so the existing `Assert.Empty(FindAll("li"))` would pass vacuously. It is **rewritten** to assert the empty-state message instead — the behaviour it was actually reaching for (Amendment 1, R15). Check in review that a row does not look clickable when nothing happens.

### `MetricsChartView.razor`

```
<MudPaper>
  <MudChart ChartType="ChartType.Line"
            ChartSeries="@(Fitness|Fatigue|Form)"     names drive the legend — FR-013a
            ChartOptions="@(palette from IsDarkMode)" Wong trio, R5
  <p class="chart-span">  2026-03-22 — 2026-09-18     kept, word for word
  "Not enough data to show trends (30+ days required)" kept, word for word
```

Series render as `<path>`, not `<polyline>`; geometry belongs to the component. `MetricsChart.cs` and its 4 tests are deleted, and the 3 `polyline` assertions go with them — each recorded in the completion review, as amended SC-002 requires.

**No `stroke-dasharray` override.** It is technically possible via `.mud-chart-line path.mud-chart-serie:nth-of-type(n)` and was deliberately declined (R5). Do not add one without reopening Amendment 1.

### `Error.razor`, `NotFound.razor`

Wrapped in `MudContainer` + `MudPaper`. `Error.razor`'s Bootstrap-era `class="text-danger"` is replaced by `Color="Color.Error"`, since Bootstrap is not present and the class is currently inert.

## Test-side changes

Arrange-step only. No assertion changes (SC-002, R13).

| Change | Where |
|--------|-------|
| `Services.AddMudServices()` | `SeedAndRegister` and any other bUnit context rendering Mud markup |
| `JSInterop.Mode = JSRuntimeMode.Loose` | Same — MudBlazor calls JS on render |
| **Delete** `MetricsChartTests.cs` (4 tests) | Pins `MetricsChart.Plot`, which no longer exists |
| **Delete** 3 `polyline` assertions | The element type is no longer rendered |
| **Rewrite** the `FindAll("li")` assertion | Would otherwise pass vacuously against `MudList`'s `<div>`s |

Every deletion above is named in the completion review. Amendment 1 permits removal; it does not permit doing it quietly.

## Package

| Package | Version | Project |
|---------|---------|---------|
| `MudBlazor` | 9.10.0 | `TrainingLoadAnalyzer.Web` |

Plus, in `App.razor`: `_content/MudBlazor/MudBlazor.min.css` and `_content/MudBlazor/MudBlazor.min.js`. In `Program.cs`: `builder.Services.AddMudServices()`. The JS is not optional — dark mode depends on it (R3).
