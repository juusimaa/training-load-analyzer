# Contract: HTTP interface between the backend and the browser

**Feature**: 009-python-react-stack | **Date**: 2026-09-23

This interface is internal to the application (spec Assumptions: "not a public API"). It exists
because the new stack separates the page from the server, where the reference used a Blazor
circuit. It serves one athlete on the local machine and has no authentication.

Conventions:
- **Encoding.** JSON, UTF-8. Field names are `camelCase`.
- **Strings vs numbers.** Every value the page *displays* is a **string**, formatted by the
  backend (research R2). Numbers appear only where the frontend computes geometry.
- **Secrets.** No response ever contains a token, client secret or any `connection` column other
  than the fact that one exists (005 FR-005, 009 FR-010).

## 1. Route table

| Method | Path | Purpose | Reference equivalent |
| --- | --- | --- | --- |
| `GET` | `/api/dashboard` | The whole dashboard view | `DashboardReader.ReadAsync` |
| `GET` | `/api/sync/status` | The process-held sync status | `SyncCoordinator.Status` |
| `POST` | `/api/sync` | Run one incremental sync and return its status | `SyncCoordinator.RunAsync` |
| `GET` | `/connect` | Set the state cookie and redirect to Strava's consent page | `StravaConnectEndpoints` `/connect` |
| `GET` | `/strava/callback` | Verify the state, exchange the code, redirect | `StravaConnectEndpoints` `/strava/callback` |
| `GET` | anything else | The SPA (`index.html`) in run mode; React renders the dashboard at `/` and **Not Found** everywhere else | Razor routes, `/not-found` |

## 2. `GET /api/dashboard`

**200**, always, including when the history cannot be read (`isUnavailable: true`). A 5xx is a
defect, and the frontend shows the FR-016 notice.

```jsonc
{
  "asOf": "2026-09-18",               // Display.Day
  "isoWeek": "2026-W38",              // Designation
  "maximumHeartRate": "190",          // rail renders "<value> bpm"
  "isStravaConnected": true,
  "isUnavailable": false,
  "hasActivities": true,              // Metrics.Count > 0
  "hasEnoughHistoryForChart": true,   // Metrics.Count >= 30 (over the 180-day series, never the window)

  "current": {                        // null when !hasActivities
    "fitness": { "value": "45.3", "qualifiers": ["still settling", "estimated"] },
    "fatigue": { "value": "12.1", "qualifiers": [] },
    "form":    { "value": "33.2", "qualifiers": ["partly estimated"] }
  },

  "week": {
    "points": "480.0",                // Display.Points(CurrentWeek?.Points), "—" when null
    "trend": null | {                 // null → the page shows "—"
      "change": "+120.0",             // (>=0 ? "+" : "") + Display.Points(AbsoluteChange)
      "percent": "+33%",              // Display.Percent(RelativeChange), "—" when absent
      "judgement": "Significant increase" // | "Significant decrease" | "Steady" | "Week in progress"
    }
  },

  "days": [                           // ≤180 entries, ascending, gap-free; aligned with loads
    {
      "day": "2026-03-23",            // Display.Day
      "fitness": 40.123456789,        // raw doubles: geometry only
      "fatigue": 38.1,
      "form": 2.023456789,
      "load": 120.0,                  // raw points as a number: geometry only
      "display": {                    // readout strings, through the same Display helpers
        "fitness": "40.1", "fatigue": "38.1", "form": "2.0", "load": "120.0"
      }
    }
  ],

  "recent": [                         // ≤7, newest first
    { "day": "2026-09-18", "type": "Cycling", "movingTime": "45m",
      "provenance": "measured",       // "measured" | "estimated"
      "load": "…" }
  ]
}
```

**Qualifier rules**, a port of `MetricRow.Qualifiers`:
- When the value is present and `!IsReliable`, add `"still settling"`.
- Then, by that figure's basis: add `"partly estimated"` for Mixed and `"estimated"` for
  Estimated.
- The array order is the rendering order.

## 3. `GET /api/sync/status` and `POST /api/sync`

Both return a `SyncStatusView`:

```jsonc
{
  "isRunning": false,
  "message": "3 activities imported.",   // SyncMessage.For(status); "" when nothing to say
  "needsConnection": false,              // Failure != null || Outcome == ReconnectionRequired
  "lastChecked": "2026-09-18 07:15"      // FinishedAt as "yyyy-MM-dd HH:mm", null while running or never
}
```

**`POST /api/sync`**
- The request **blocks until the sync ends** (research R9), then returns **200** with the final
  status.
- A concurrent `POST` returns **200** immediately with the current running status (`isRunning:
  true`, message "Syncing activities…"). This is the coordinator's refusal, not an error.
- No request body. The response carries no credential.

The **message table** is `SyncMessage.For`, verbatim, and pinned by a golden:

| Condition | `message` |
| --- | --- |
| running | `Syncing activities…` |
| not connected (failure) | `Strava connection required.` |
| never ran | `""` |
| Completed, imported > 0 | `{n} activities imported.` |
| Completed, imported = 0 | `Already up to date.` |
| RateLimited, retry known | `Rate limited by Strava. Available again at {HH:mm}.` (athlete's local time) |
| RateLimited, retry unknown | `Rate limited by Strava. Try again shortly.` |
| Interrupted | `Sync interrupted. Your stored history is unchanged — try again.` |
| ReconnectionRequired | `Strava connection required.` |
| Refused | `A sync is already running.` |
| any other outcome | `Sync finished with an outcome this page does not recognise.` |

## 4. `GET /connect` and `GET /strava/callback`

A port of `StravaConnectEndpoints`, with the same behaviour.

**`/connect`**
- Sets the `tla.oauth.state` cookie: 32 lowercase hex characters, `HttpOnly`, `SameSite=Lax`,
  `Secure` only over HTTPS, `Max-Age=600`.
- Answers **302** to Strava's `/oauth/authorize`, with `client_id`, `redirect_uri` (built from the
  request's scheme and `Host`), `response_type=code`, `scope=read,activity:read_all` and `state`.

**`/strava/callback?code&state&error`**, checked in this order:
1. The cookie is deleted.
2. If `error` is present → **302** to `/?connect=declined`.
3. If the state is missing or does not match, compared in constant time → **400** "This sign-in
   could not be verified. Start again from the dashboard." **No token exchange is attempted.**
4. If `code` is missing → **400** "Strava returned no authorization code."
5. The code is exchanged:
   - success → **302** `/`;
   - insufficient scope → **302** `/?connect=scope`;
   - different athlete → **302** `/?connect=mismatch`.

The `connect` query parameter is not read by the page. This is the recorded gap (research R3(3)).

## 5. Startup refusal

With `TLA_ATHLETE_MAXIMUM_HEART_RATE` missing, blank, non-integer or ≤ 0, the application factory
raises before the server binds. The process exits non-zero with one of these two messages
(006 FR-015):

- `'TLA_ATHLETE_MAXIMUM_HEART_RATE' is not configured. Every measured training load is computed
  from the athlete's maximum heart rate, and there is no sensible default for it. Set it in the
  environment or in alt-stack/backend/.env before starting.`
- `'TLA_ATHLETE_MAXIMUM_HEART_RATE' is '{value}', which is not a positive whole number of beats
  per minute. A training load computed from it would be meaningless.`
