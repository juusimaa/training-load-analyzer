# Parity reference (feature 009)

The shared, purpose-built fixtures and the golden outputs that turn 009 SC-001–SC-003 into
assertions ([contracts/parity.md](../specs/009-python-react-stack/contracts/parity.md), research
R13). Everything here is test data. None of it comes from a real Strava account, and every token
in it is obviously fake (`test-access-…`, `test-refresh-…`).

## Layout

```text
parity/
├── fixtures/     # inputs, written by hand
│   ├── histories/<name>.json       # activities + today + max HR (SC-001, SC-002)
│   ├── sync/<name>/scenario.json   # clock, store, recorded Strava exchange (SC-003)
│   │   └── responses/*.json
│   └── probes/display.json         # formatting probe values (research R2)
├── golden/       # outputs of the .NET reference, committed
│   ├── histories/<name>.json
│   ├── sync/<name>.json
│   └── probes/{display,surfaces}.json
└── generator/    # the .NET console tool that writes golden/
```

## Regenerate

```bash
dotnet run --project parity/generator
git diff parity/golden
```

The tool deletes and rewrites `golden/` and nothing else. It exits non-zero if any fixture fails
to load, if a sync scenario issues a request its recording does not expect (or leaves one unused),
or if a token value would reach a golden.

**Never hand-edit a golden.** A golden that looks wrong means the reference or the fixture is
wrong. Raise it, and regenerate only deliberately, after a reviewed change to the reference or a
fixture.

## How the goldens are made

- **Histories.** Each fixture's activities are built through the reference's own public
  constructors. The golden records:
  - the loads, daily and weekly totals, metrics and weekly trends, over `range`;
  - `DashboardViewBuilder.Build`, projected to exactly
    [http-api.md §2](../specs/009-python-react-stack/contracts/http-api.md#2-get-apidashboard);
  - `MetricsChart` geometry for the 180-, 90- and 30-day windows;
  - rendered text. The components are rendered with ASP.NET Core's `HtmlRenderer`, and
    `Pages/Dashboard.razor` with bUnit (the page declares an interactive render mode, which
    `HtmlRenderer` refuses; the reference's own tests use bUnit for it too). The page reads a real
    in-memory SQLite store through the real `DashboardReader`.
- **Sync.** Each scenario seeds an in-memory SQLite store migrated with the reference's own
  migrations, and runs `StravaActivitySync.SyncAsync` over a handler that replays the recorded
  exchanges strictly in order.
- **Probes.** Every `Display` helper on the probe values, `SyncMessage.For` on every row of the
  message table, and the surfaces that depend on no history (loading, the sync panel per row,
  not-found, error).

**Rendered text** is whitespace-normalised: comments and tags removed, entities decoded, every
run of whitespace collapsed to one space, the ends trimmed. That is the DOM's `textContent`
reduced the same way, which is what the React tests compare.

The **loading** surface's rail shows the machine's own date, because the view it would read the
date from has not arrived. The generator replaces that date and its ISO week with `{asOf}` and
`{isoWeek}`, and the React test fills them in from the browser's clock.

## Count

31 goldens: 16 histories, 13 sync scenarios, 2 probe files.
