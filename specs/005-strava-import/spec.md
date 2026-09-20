# Feature Specification: Strava Import

**Feature Branch**: `005-strava-import`

**Created**: 2026-09-17

**Status**: Complete — implemented and signed off 2026-09-20

**Input**: User description: "Strava Import: the user connects their Strava account and the analyzer keeps a local copy of their running and cycling activities in sync. Covers Strava OAuth authorization and token handling (including refresh), retrieving activities from the Strava API with pagination and rate-limit handling, mapping Strava activity DTOs into the existing TrainingActivity domain model without letting Strava types leak into the domain, persisting activities locally (EF Core / SQLite), and incremental synchronization so a later sync fetches only new or changed activities rather than the full history. The domain layer must stay independent of Strava; the integration lives in a new TrainingLoadAnalyzer.Infrastructure project tested at its own boundary. Out of scope: editing or pushing activities back to Strava, other providers (Garmin, Intervals.icu, TrainingPeaks), multi-user SaaS, the dashboard UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Connect a Strava account (Priority: P1)

An athlete opens the analyzer for the first time and it has nothing to analyse. They say they want to
connect Strava, are sent to Strava's own consent page, approve read access to their activities, and
come back to an analyzer that now holds a working, durable authorization to read on their behalf.
They never type a Strava password into this application. Six hours later, when that authorization has
quietly expired, they do not have to do any of it again — the analyzer renews it on its own. If they
revoke access from Strava's settings, the analyzer says so plainly and asks to be reconnected rather
than failing with a stack trace or silently reporting that they have not trained.

**Why this priority**: Nothing else in this feature can happen without it, and it is the only part
the athlete performs by hand. It is also the part with the sharpest failure mode: a credential that
is mishandled, logged, or lost costs the athlete their trust, not just a sync.

**Independent Test**: Drive the authorization exchange and the renewal against a stand-in for Strava
and read the stored connection afterwards: a connection exists, survives a restart, renews itself
when expired, and reports a revoked authorization as needing reconnection. Fully testable with no
activities imported at all.

**Acceptance Scenarios**:

1. **Given** no connected account, **When** the athlete completes Strava's consent flow and the
   analyzer exchanges the returned authorization grant, **Then** a connection is stored that
   identifies the Strava athlete and carries credentials usable for reading activities.
2. **Given** a stored connection, **When** the analyzer is restarted, **Then** the connection is still
   present and usable without the athlete authorizing again.
3. **Given** a stored connection whose access credential has expired, **When** a sync is started,
   **Then** the credential is renewed automatically and the sync proceeds, with the renewed credential
   stored in place of the old one.
4. **Given** a stored connection whose renewal credential Strava now rejects, **When** a sync is
   started, **Then** the sync fails with a reconnection-required outcome, the stored activities are
   left untouched, and the failure is not reported as "no new activities".
5. **Given** a connected account, **When** the athlete disconnects, **Then** the stored credentials
   are discarded and no further sync is attempted until a new authorization is completed.
6. **Given** activities already imported for one Strava athlete, **When** an authorization completes
   for a different Strava athlete, **Then** the connection is refused and the refusal names the
   mismatch, rather than mixing two athletes' training into one history.
7. **Given** an athlete who approves the consent page but declines access to their private activities,
   **When** the analyzer exchanges the returned grant, **Then** the connection is refused, the refusal
   names the access that was withheld, and no partial connection is stored.

---

### User Story 2 - Bring in the training history (Priority: P2)

The athlete has just connected. They have four years of runs and rides on Strava, along with swims, a
few gym sessions, and a handful of activities they logged by hand. They ask the analyzer to import,
and it works through their history — page after page, oldest to newest — turning each run and each
ride into the analyzer's own training session and storing it locally. Swims and gym sessions are left
where they are. When it finishes, it tells them what it did: how many sessions came in, how many were
ignored and why. From that point on, every figure the analyzer already knows how to compute — daily
load, weekly totals, fitness, fatigue, form, trends — has real training behind it instead of test
data.

**Why this priority**: This is the point of the feature. It is the step that turns four completed
domain features into something the athlete can look at. It depends on User Story 1 and on nothing
else.

**Independent Test**: Run an import against a stand-in Strava holding a fixed, mixed history and read
the stored sessions afterwards: the right ones are present, mapped field for field, and the ignored
ones are accounted for by reason. Testable with no incremental logic and no rate limiting involved.

**Acceptance Scenarios**:

1. **Given** a Strava history of 40 runs, 60 rides, and 20 swims, **When** a first import runs,
   **Then** 100 training sessions are stored and the 20 swims are reported as ignored because their
   sport is outside the analyzer's scope.
2. **Given** a Strava history spanning more pages than one request returns, **When** a first import
   runs, **Then** every activity across every page is considered, and no activity is imported twice
   because of the page boundary.
3. **Given** a Strava activity recorded in Helsinki at 07:30 local time, **When** it is imported,
   **Then** the stored session's start carries both that instant and the athlete's offset from UTC at
   the time, so it falls on the correct local day.
4. **Given** a Strava activity whose moving time is 52 minutes and whose elapsed time is 68 minutes,
   **When** it is imported, **Then** the stored session's moving time is 52 minutes.
5. **Given** a Strava activity with no moving time at all, **When** it is imported, **Then** it is
   skipped with a stated reason, counted in the import summary, and the import continues through the
   rest of the history rather than stopping.
6. **Given** an import that has completed, **When** the analyzer is restarted, **Then** every imported
   session is still present.
7. **Given** a completed import, **When** the same import is run again over the same history, **Then**
   the stored session count is unchanged and no session is duplicated.
8. **Given** a Strava run from last month that Strava reports as carrying heart-rate data, **When** it
   is imported, **Then** its heart-rate series is retrieved and stored with the session, so the session
   carries measured load rather than estimated load.
9. **Given** a Strava ride from three years ago that Strava reports as carrying heart-rate data,
   **When** it is imported, **Then** it is stored without a heart-rate series and no request is spent
   retrieving one, because it falls outside the measured window.
10. **Given** a Strava activity recorded as an electrically assisted ride, **When** the history is
    imported, **Then** it is not stored, and it is reported as ignored because its sport is outside the
    analyzer's scope.
11. **Given** Strava activities recorded as a trail run and as a gravel ride, **When** they are
    imported, **Then** they are stored as a running session and a cycling session respectively.
12. **Given** a Strava run inside the measured window whose heart-rate series opens with eight
    implausible samples before the strap reads reliably, **When** it is imported, **Then** those eight
    samples are discarded, the rest of the series is stored, the session carries measured load, and the
    sync summary reports that eight samples were discarded.
13. **Given** a Strava ride whose heart-rate series contains only implausible samples, **When** it is
    imported, **Then** the session is stored without a series and carries estimated load, because fewer
    than two samples survived.

---

### User Story 3 - Keep it up to date without re-reading four years (Priority: P3)

The athlete rides on Saturday and opens the analyzer on Sunday. They expect the new ride to appear in
a few seconds. They do not expect the analyzer to walk through 1,200 activities to find the one it
does not have. If they then notice they logged Saturday's ride under the wrong sport and fix it on
Strava, or delete a duplicate upload, the next sync should pick that up too — for recent training,
at least, which is the training the analyzer's figures are most sensitive to. The second and every later sync asks Strava only for what has happened since the last
one, stores it, and stops. An activity uploaded late — a Wednesday run that only reached Strava on
Friday — still gets picked up, because the sync deliberately looks back a little further than the
last thing it saw.

**Why this priority**: It is what makes the feature usable more than once, and it is the requirement
the MVP calls out by name. It depends on User Story 2 having something to be incremental against, and
a correct-but-slow full re-read would still deliver working figures, so it comes third rather than
second.

**Independent Test**: Import a fixed history, add one activity to the stand-in Strava, sync again, and
inspect both what was requested and what was stored: one new session, and a request that did not ask
for the whole history. Testable without any rate limiting or failure handling.

**Acceptance Scenarios**:

1. **Given** a completed import and one new ride on Strava since, **When** a sync runs, **Then** one
   new session is stored, the existing sessions are unchanged, and the request sent to Strava did not
   cover the whole history.
2. **Given** a completed import and nothing new on Strava, **When** a sync runs, **Then** no session
   is stored, the sync reports success with nothing imported, and this is not reported as a failure.
3. **Given** a completed import, **When** an activity dated three days before the last imported one is
   uploaded late and a sync runs, **Then** that activity is imported, because the sync looks back
   beyond the last activity it saw.
4. **Given** a sync that re-reads an activity already stored, **When** it stores it, **Then** the
   result is one session, not two, and the stored session matches the activity as Strava reports it
   now.
5. **Given** a stored history, **When** the athlete asks for a full resynchronization, **Then** the
   whole history is read again and the stored session count is unchanged except for activities that
   genuinely changed on Strava.
6. **Given** a stored session for an activity dated three days ago that the athlete has since deleted
   on Strava, **When** a routine sync completes, **Then** the stored session is removed and the removal
   is reported by identifier in the sync summary.
7. **Given** a stored session for an activity the athlete deleted on Strava two months ago, **When** a
   routine sync completes, **Then** the stored session remains, because a routine sync does not read
   that far back; a full resynchronization removes it.
8. **Given** a stored session whose activity has been deleted on Strava, **When** a sync stops at the
   request limit before reading its span to completion, **Then** nothing is removed.
9. **Given** a stored cycling session whose activity the athlete has since corrected on Strava to an
   electrically assisted ride, and whose date falls within the look-back window, **When** a routine sync
   completes, **Then** the stored session is removed.

---

### User Story 4 - Survive Strava's limits and a dropped connection (Priority: P4)

Strava caps how often it will answer. A four-year first import can reach that cap partway through,
and the athlete's network can drop at any point. Neither should cost them the work already done or
leave the analyzer holding a history it believes is complete when it is not. A sync that cannot
continue stops cleanly, keeps everything it has already stored, says why it stopped and when it can
be tried again, and the next sync picks up where it left off instead of starting over.

**Why this priority**: It only matters once the first three stories work, but without it the first
import of a real multi-year history is unreliable, and — worse — a sync that stopped halfway could be
mistaken for one that finished, making every downstream figure quietly wrong.

**Independent Test**: Make the stand-in Strava refuse with a rate-limit response, and separately fail
mid-page, after a known number of activities; read the stored sessions, the sync outcome, and the
resume point. Testable without any real network.

**Acceptance Scenarios**:

1. **Given** an import in progress, **When** Strava reports that the request limit is reached,
   **Then** the sync stops, every session stored so far is kept, and the outcome states that the limit
   was reached and when it can be retried.
2. **Given** a sync that stopped at the request limit, **When** a sync is run again afterwards,
   **Then** it resumes from where the previous one stopped and does not re-read the pages it had
   already stored.
3. **Given** an import in progress, **When** the connection fails partway through a page, **Then** no
   partially mapped session is stored, the sessions from completed pages are kept, and the outcome
   states that the sync was interrupted.
4. **Given** a sync that was interrupted, **When** a sync is run again, **Then** the activities from
   the incomplete page are read again and stored exactly once.
5. **Given** any sync, **When** it ends for any reason, **Then** it reports how many sessions were
   imported, how many were updated, how many were skipped and for what reasons, and whether it
   finished or stopped early.
6. **Given** a sync already running, **When** another sync is started, **Then** the second one is
   refused rather than running alongside the first.

---

### Edge Cases

- **An activity Strava reports with zero or missing moving time.** Skipped with a reason and counted,
  never stored and never allowed to abort the sync (FR-011). The domain refuses such a session, and
  that refusal is a fact about one activity, not about the import.
- **An activity with no heart-rate data at all.** Imported normally, stored without a series, and its
  load estimated from moving time by the rules Feature 1 already set (FR-017). No request is spent
  looking for a series Strava has already said does not exist.
- **An activity with heart-rate data that falls outside the measured window.** Imported without its
  series, carrying estimated load (FR-017, FR-017a). Nothing about it is skipped or flagged as a
  problem; the basis on every downstream figure already states that the load was estimated.
- **A heart-rate series containing dropouts.** The implausible samples are discarded, the rest of the
  series is kept, and the count is reported (FR-017f, FR-017g). Only a series with fewer than two
  surviving samples is unusable.
- **A heart-rate series that fails to arrive.** The session is stored anyway with estimated load, the
  outstanding series is recorded with its reason, and the resume point stays behind the activity so the
  next sync retries it (FR-017d, FR-017e).
- **An activity that ages out of the measured window after its series was already stored.** Keeps its
  series and its measured load forever (FR-017b). The window bounds retrieval, not retention.
- **A manually entered activity.** Imported like any other, provided it carries a sport in scope and a
  positive moving time. A session the athlete typed in by hand is still training.
- **A private activity.** Imported. Load is load regardless of who else can see it (FR-012).
- **An activity uploaded long after it happened.** Caught by the look-back window when it falls inside
  it, and by a full resynchronization when it does not (FR-027, FR-028, FR-032).
- **An activity deleted on Strava after it was imported.** Its session is removed when a sync reads to
  completion the span that activity falls in — routinely if it is recent, on a full resynchronization
  otherwise (FR-031, FR-031a, FR-031b). The removal is reported, never silent (FR-031d).
- **An activity edited on Strava after it was imported** — a trimmed moving time, a corrected sport
  type. Updated in place on the same terms (FR-030, FR-031); if the correction takes it outside the
  sport types in scope, its session is removed instead (FR-031e).
- **A sync that stops early while an activity is missing from Strava.** Removes nothing. Absence from a
  truncated read is not absence from Strava (FR-031c).
- **The first sync on an account with no activities at all.** Succeeds, imports nothing, and stores a
  sync state so the next sync is incremental rather than treating the account as never synced.
- **Two Strava activities that map to the same instant and duration.** Both stored: identity is the
  provider's activity identifier, never a combination of the session's fields (FR-023).
- **A Strava response carrying fields the analyzer does not recognise.** Ignored without failing the
  activity or the sync (FR-020).
- **The access credential expiring mid-sync.** Renewed and the sync continues; the athlete is not
  asked to do anything (FR-004).
- **Access granted more narrowly than requested.** Refused, naming what was withheld (FR-002a). The
  import would otherwise look complete while silently omitting every private activity the athlete has
  recorded.
- **Authorization revoked on Strava while a sync is running.** The sync stops with a
  reconnection-required outcome, keeping what it has already stored (FR-006).
- **A sync interrupted between storing activities and recording the resume point.** The next sync
  re-reads that ground and stores each activity exactly once (FR-026, FR-029, FR-030).

## Requirements *(mandatory)*

### Functional Requirements

#### Connecting a Strava account

- **FR-001**: The system MUST obtain access to the athlete's Strava data through Strava's own
  authorization flow, in which the athlete grants consent on a page hosted by Strava. The system MUST
  NOT ask for, transmit, or store the athlete's Strava password.
- **FR-002**: The system MUST request only read access to athlete profile and activity data, including
  activities the athlete has marked private, and MUST NOT request permission to write, modify, or
  upload anything to Strava.
- **FR-002a**: The system MUST verify that Strava granted the access that was requested, and MUST
  refuse to complete a connection when access covering the athlete's private activities was withheld,
  naming the access that was not granted. An athlete may approve some requested scopes and decline
  others on Strava's consent page, and the response reports what was actually granted rather than what
  was asked for. Accepting a narrower grant would exclude every private activity from every import,
  under-reporting the athlete's training in a way no figure downstream could detect — the same failure
  FR-006 exists to prevent, arriving through a different door. A refusal under this rule MUST be
  distinguishable from a rejected credential, and MUST tell the athlete what to approve when they
  reconnect.
- **FR-003**: The system MUST store the resulting credentials durably, so a connection survives a
  restart of the analyzer and the athlete authorizes once rather than once per session.
- **FR-004**: The system MUST renew an expired access credential automatically using the stored
  renewal credential, without athlete involvement, and MUST store the renewed credentials in place of
  the old ones — including a replacement renewal credential when Strava issues one.
- **FR-005**: The system MUST NOT write credentials to logs, to console output, to sync summaries, or
  to any file tracked in source control. Credentials MUST be held only in the analyzer's local store.
- **FR-006**: When Strava rejects the stored renewal credential — because the athlete revoked access,
  or the credential is otherwise invalid — the system MUST end the sync with a distinct
  reconnection-required outcome, MUST leave already-imported activities untouched, and MUST NOT report
  the condition as a successful sync that found nothing.
- **FR-007**: The system MUST allow the athlete to disconnect, which MUST discard the stored
  credentials and MUST prevent any further sync until a new authorization completes. Whether
  previously imported activities are also discarded MUST be the athlete's explicit choice, not a side
  effect of disconnecting.
- **FR-008**: The system MUST hold at most one connected Strava athlete at a time, and MUST refuse an
  authorization for a different Strava athlete while activities imported for another athlete are
  stored, naming the mismatch. Two athletes' training MUST NOT be combined into one history.

#### What is imported

- **FR-009**: The system MUST import only activities whose sport falls within the analyzer's scope of
  running and cycling, and MUST ignore every other activity rather than forcing it into one of the two
  types.
- **FR-010**: The system MUST map Strava's sport types onto the analyzer's two activity types by this
  fixed table, and MUST NOT extend, infer, or default beyond it:
  - **Running**: `Run`, `TrailRun`, `VirtualRun`.
  - **Cycling**: `Ride`, `GravelRide`, `MountainBikeRide`, `VirtualRide`.
  - **Ignored**: `EBikeRide` and `EMountainBikeRide`, both named here so their exclusion is visibly
    deliberate rather than incidental; `Handcycle` and `Velomobile`, which are cycling in a sense this
    analyzer's load model does not cover; and every other sport type Strava reports, including sport
    types Strava introduces after this specification is written.
  A sport type absent from this table MUST be ignored under FR-009 and reported as out of scope under
  FR-038. It MUST NOT be guessed at by name, by pattern, or by similarity to a listed type.
- **FR-010a**: The system MUST classify from the activity's sport type rather than from any older or
  coarser activity-type field Strava also returns, so that a gravel ride is recognised as a gravel ride
  rather than collapsed into a generic ride.
- **FR-010b**: Indoor training recorded under a listed sport type MUST be imported on the same terms as
  outdoor training. A treadmill run is a run and a turbo session is a ride; neither is a separate sport
  type on Strava, and neither is excluded here.
- **FR-011**: The system MUST skip, rather than store, any activity the analyzer's session model would
  refuse — a non-positive moving time or a missing start instant — MUST record the activity's
  identifier and the reason it was skipped, and MUST continue the sync. One unusable activity MUST NOT
  abort an import.
- **FR-012**: The system MUST import activities the athlete has marked private on the same terms as
  public ones. Visibility on Strava is not a statement about whether the training happened.
- **FR-013**: The system MUST import manually entered activities on the same terms as recorded ones,
  provided they satisfy FR-009 and FR-011.

#### Mapping Strava data onto the analyzer's model

- **FR-014**: The system MUST record Strava's activity identifier verbatim as the session's external
  identifier, without parsing, trimming, reformatting, or prefixing it, and MUST record separately
  which provider that identifier belongs to.
- **FR-015**: The system MUST map the activity's start so that the stored session carries both the
  instant the activity began and the athlete's offset from UTC at that moment, so that the session
  falls on the local day the athlete trained on regardless of where they were.
- **FR-016**: The system MUST map Strava's moving time — time spent actually moving — onto the
  session's moving time, and MUST NOT substitute elapsed time.
- **FR-017**: The system MUST retrieve an activity's full heart-rate series when, and only when, all
  three of the following hold: Strava's activity summary reports that the activity has heart-rate data;
  the activity started within the measured window of FR-017a; and the system does not already hold a
  heart-rate series for that activity. An activity meeting all three MUST be stored with its series, and
  will therefore carry measured load. Every other imported activity MUST be stored without one, and will
  carry load estimated from moving time by the rules Feature 1 already fixed.
- **FR-017a**: The measured window MUST be the **180 days** ending on the day the sync runs. It is
  bounded because retrieving a series costs one Strava request per activity, and a multi-year history
  would exceed the request allowance of FR-034 several times over, while the metrics that depend most
  on load — fitness, fatigue, and form — are governed by recent training rather than by training from
  years ago.
- **FR-017b**: The system MUST NOT discard, strip, or replace a heart-rate series it already holds.
  Once a session has been stored with its series, every later sync — including a full
  resynchronization, and including syncs run long after the activity has aged out of the measured
  window — MUST leave that series in place. The measured window governs what is newly retrieved, never
  what is kept, so an athlete's measured history accumulates rather than rolling forward.
- **FR-017c**: The system MUST retrieve the series at full recorded resolution and MUST NOT accept a
  downsampled one. Time spent in each heart-rate zone is what the load calculation is built from, and
  downsampling would change it.
- **FR-017d**: When a series cannot be retrieved — the request fails, a request limit is reached, or
  Strava returns no usable heart-rate data despite the summary reporting that it has some — the system
  MUST store the activity without a series, MUST record that its series is outstanding together with
  the reason, and MUST NOT skip the activity or fail the sync. A session carrying estimated load is
  better than no session at all.
- **FR-017e**: An activity stored with an outstanding series MUST NOT advance the resume point past
  itself (FR-029), so that the next sync re-reads it and retries the retrieval without any separate
  queue of outstanding work being kept. Once the activity has aged out of the measured window it no
  longer qualifies under FR-017, and it MUST NOT hold the resume point back any further.
- **FR-017f**: The system MUST discard heart-rate samples whose value falls outside the plausible range
  the session model accepts, MUST retain every other sample with the time it was recorded at, and MUST
  treat the series as unusable under FR-017d only when fewer than two samples survive. Two is the
  fewest from which any load can be computed, so no threshold beyond it is invented. Recorded
  heart-rate data routinely contains dropouts — most often in the opening seconds, before a chest strap
  reads reliably — and treating a whole session's evidence as worthless because of them would send
  nearly every real session to estimated load, defeating the measured window of FR-017a.
- **FR-017g**: The system MUST report, for every activity whose series had samples discarded, how many
  were discarded (FR-038). Discarding a sample changes the resulting load figure, because the sample
  before it then covers the gap, so the discarding MUST be visible rather than silent.
- **FR-018**: The mapping MUST be one-directional and MUST NOT introduce Strava concepts, field names,
  identifier formats, response shapes, or enumerations into the analyzer's domain. Strava's data
  shapes MUST remain confined to the integration.
- **FR-019**: The mapping MUST be deterministic: the same Strava activity data MUST always produce the
  same stored session, independent of the current time, of what else is being imported, and of the
  order in which activities arrive. Whether a heart-rate series is fetched for that activity is a
  decision of the sync, taken under FR-017 before the mapping runs, and is the only part of an imported
  session that depends on when the sync happened.
- **FR-020**: The system MUST ignore fields in Strava's responses that it does not use, including ones
  Strava adds later, without failing the activity or the sync.
- **FR-021**: The system MUST NOT compute, store, or cache a training-load value at import time. Load,
  daily and weekly totals, fitness, fatigue, form, and trends are derived from stored sessions by the
  existing domain features when they are asked for.

#### Keeping a local copy

- **FR-022**: The system MUST store imported sessions durably, so they are available after a restart
  and without contacting Strava.
- **FR-023**: A stored session's identity MUST be the pairing of its provider with that provider's
  activity identifier. Importing the same activity again MUST update the stored session in place and
  MUST NOT create a second one. Identity MUST NOT be derived from a session's start, duration, type,
  or any combination of its values.
- **FR-024**: A session read back from storage MUST be equal in every respect to the session that was
  mapped and stored, including its start offset and its heart-rate evidence, so that figures computed
  from stored data match figures computed from freshly imported data exactly.
- **FR-025**: The system MUST store the state a sync needs to be incremental — at minimum the point
  from which the next sync should read, when the last sync ran, and how it ended — and MUST keep that
  state durably alongside the activities.
- **FR-026**: A sync that fails partway MUST leave the store internally consistent: no partially
  mapped session, and no resume point that claims ground the store does not actually hold.

#### Synchronizing incrementally

- **FR-027**: Every sync after the first MUST request only activities from the stored resume point
  onward, and MUST NOT re-read the whole history as a matter of course.
- **FR-028**: The resume point MUST be set back by a **look-back window of 7 days** from the start of
  the most recent successfully stored activity, so that an activity uploaded to Strava days after it
  happened is still found. Re-reading a week of already-stored activities is harmless under FR-023 and
  is the price of not missing a late upload.
- **FR-029**: The system MUST advance the stored resume point only over activities it has durably
  stored. An activity read but not stored MUST NOT move the resume point past itself.
- **FR-030**: Re-reading an activity the system already holds MUST leave exactly one stored session
  for it, matching the activity as Strava reports it at the time of reading.
- **FR-031**: The system MUST reconcile its stored sessions against Strava's activities over any span
  of history it has read to completion, and MUST NOT reconcile over any other span. Reconciling means
  two things: an activity that changed updates its stored session in place (FR-030), and a stored
  session whose activity is absent from a complete read of the span that session falls in MUST be
  removed.
- **FR-031a**: A routine sync MUST reconcile over exactly the span its incremental request covered —
  from the resume point of FR-028 onward — and MUST NOT reconcile beyond it, because an activity's
  absence outside that span means only that it was never asked for. An edit or a deletion to an
  activity dated within the look-back window is therefore picked up by the next routine sync at no
  extra request cost, since that ground is re-read regardless.
- **FR-031b**: An edit or a deletion to an activity older than the span a routine sync reads MUST be
  picked up by a full resynchronization (FR-032), and MUST NOT be expected of a routine sync. This is a
  stated limit of the feature rather than a defect: Strava's activity listing is filtered by when an
  activity happened, not by when it was last changed, so a routine sync has no way to learn that a run
  from March was edited yesterday.
- **FR-031c**: The system MUST NOT remove any stored session on the strength of a span it did not read
  to completion. A sync stopped by a request limit, by an interruption, or by a rejected credential
  MUST remove nothing at all, because an activity missing from a truncated read is missing from the
  read, not from Strava. Deleting an athlete's training on the strength of a dropped connection is the
  one failure this feature must never produce.
- **FR-031d**: Every session removed by reconciliation MUST be reported in the sync summary by its
  identifier and as a removal (FR-038), so that training disappearing from the analyzer is never
  silent.
- **FR-031e**: An activity whose sport type changed on Strava to one outside the table in FR-010 MUST
  have its stored session removed when it is reconciled, on the same terms as a deletion. A ride the
  athlete has corrected to an electrically assisted ride is no longer training this analyzer counts.
- **FR-032**: The system MUST offer a full resynchronization that reads the entire history again
  regardless of the stored resume point. It MUST NOT duplicate any stored session, and it MUST
  reconcile over the whole history under FR-031 — but only when it read the whole history to
  completion, per FR-031c. A full resynchronization that stopped early is a partial read like any
  other.
- **FR-033**: A sync that finds nothing new MUST be reported as a successful sync that imported
  nothing, and MUST NOT be reported as a failure or as an empty history.

#### Limits, failures, and reporting

- **FR-034**: The system MUST stay within the request limits Strava imposes, both the short rolling
  window and the daily allowance, and MUST NOT continue issuing requests once a limit has been
  reported as reached.
- **FR-035**: On reaching a request limit, the system MUST stop the sync cleanly, keep every session
  already stored, keep the resume point consistent with what was stored, and report both that the
  limit was the reason and the earliest time the sync can usefully be retried.
- **FR-036**: A sync started after one that stopped early MUST resume from the stored resume point and
  MUST NOT restart the history from the beginning.
- **FR-037**: The system MUST retry a failure that is plausibly transient — a dropped connection, a
  server-side error from Strava — a bounded number of times before stopping the sync cleanly, and MUST
  NOT retry a failure that is not transient, such as a rejected credential or a malformed request.
- **FR-038**: Every sync MUST end with a summary stating how many sessions were imported, how many
  were updated, how many activities were skipped together with the reason for each, and whether the
  sync completed or stopped early and why. The summary MUST NOT contain credentials.
- **FR-039**: No error affecting what was imported MUST be silently swallowed. A sync that stopped
  early MUST be distinguishable from one that completed, by the athlete and by any later process
  reading the sync state.
- **FR-040**: The system MUST NOT run two syncs for the same connection at the same time; a sync
  started while another is running MUST be refused.

#### Independence and testability

- **FR-041**: This feature MUST NOT change the behaviour of the existing domain model, its load
  calculation, its aggregation, its fitness, fatigue and form metrics, or its trends. It supplies
  those features with real sessions; it does not alter what they do with them.
- **FR-042**: The integration MUST be exercisable end to end — authorization, renewal, paging, mapping,
  storage, incremental resumption, limit handling — without a network connection, a real Strava
  account, or real credentials.
- **FR-043**: Test data MUST be purpose-built and anonymized. Real personal Strava data MUST NOT be
  copied into fixtures, even when it was used to discover an edge case.

### Key Entities *(include if data involved)*

- **Strava Connection**: The analyzer's standing permission to read one athlete's Strava data — which
  Strava athlete it belongs to, the credentials that prove the permission, when the access credential
  expires, and whether the connection is currently usable or needs reauthorizing. At most one exists.
- **Access Credentials**: The short-lived credential used to read, and the longer-lived credential used
  to obtain a new one. Secret: stored locally, never logged, never reported, never committed.
- **Provider Activity**: One activity as Strava describes it. Lives only inside the integration; never
  crosses into the domain, never stored in its provider form.
- **Imported Session**: A stored training session, mapped from a provider activity onto the analyzer's
  own model, identified by its provider and that provider's activity identifier. What every existing
  domain feature reads.
- **Sync State**: What the next sync needs in order to be incremental — the resume point, when the last
  sync ran, and how it ended. One per connection, durable, advanced only over work that is durably
  stored.
- **Measured Window**: The 180 days ending on the day a sync runs, within which an activity's
  heart-rate series is worth the request it costs. A property of the sync, not of the session: it
  decides what is retrieved, never what is retained.
- **Sync Result**: What one sync did — imported, updated, removed, and skipped counts, the reason for
  each skip, the identifier of each removal, how many heart-rate samples were discarded and from which
  activities, whether it completed or stopped early, why it stopped,
  and when it may be retried. Reported to the athlete; carries no credentials.
- **Skipped Activity**: One activity the sync declined to store, with the identifier it was known by and
  the reason — out of scope by sport, or unusable to the session model. Counted and reported rather than
  discarded silently.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An athlete connects their Strava account once, through Strava's own consent page, and
  never authorizes again unless they revoke access themselves.
- **SC-002**: After a first import, every run and ride in the athlete's Strava history that the
  analyzer can use is present locally, and every one it could not use is accounted for by identifier
  and reason.
- **SC-003**: The figures the analyzer already produces — daily load, weekly totals, fitness, fatigue,
  form, trends — can be computed from the imported history with the analyzer offline and Strava
  unreachable.
- **SC-004**: A routine sync after a single new activity reads a small, bounded slice of the athlete's
  history rather than all of it, and the number of requests it makes does not grow as the history
  grows.
- **SC-005**: An activity uploaded to Strava up to a week after it happened is picked up by a routine
  sync, without the athlete asking for a full resynchronization.
- **SC-006**: Running any sync twice over an unchanged history leaves the stored session count
  identical, so no duplicate ever reaches the load figures.
- **SC-007**: A sync stopped by Strava's request limit or by a dropped connection keeps everything it
  had already stored, and the next sync completes the history without re-reading what it already
  holds.
- **SC-008**: A sync that stopped early can always be told apart from one that completed, both in what
  it reports to the athlete and in the state it leaves behind.
- **SC-009**: Revoked authorization is reported as needing reconnection and is never presented as an
  athlete who has not trained, and access granted more narrowly than requested is refused outright
  rather than producing a history quietly missing the athlete's private activities.
- **SC-010**: No credential appears in any log, console output, sync summary, or committed file.
- **SC-011**: Every session in the athlete's last 180 days whose activity Strava records heart-rate
  data for carries measured load, and a session that once carried measured load never silently reverts
  to estimated load later.
- **SC-012**: No sync ever removes a stored session on the strength of a span it did not read to
  completion, so no interruption, request limit, or dropped connection can cost the athlete training
  they actually did.
- **SC-013**: The whole integration — consent exchange, renewal, paging, mapping, storage, incremental
  resumption, and limit handling — can be tested with no network connection and no real Strava
  account.
- **SC-014**: No Strava-specific name, identifier format, or data shape appears anywhere in the
  analyzer's domain model.

## Assumptions

- **Single athlete, single provider.** The MVP serves one person analysing their own training, so one
  connection exists at a time (FR-008) and the stored data is theirs. Multi-user support, user
  accounts beyond Strava's own authorization, and additional providers are out of scope, per the
  project plan's non-goals.
- **Load is computed on read, not at import.** Feature 1 made a session's training load a pure function
  of the session (FR-021), so the import's only job is to make sessions available. The athlete's
  maximum heart rate, which measured load needs, is therefore supplied when load is calculated rather
  than captured during import — this feature neither reads it from Strava nor stores it.
- **The look-back window is 7 days and is not configurable.** FR-028 fixes it. It is the shortest
  window that covers the ordinary case of an activity synced from a watch a few days late, and
  re-reading a week costs at most one extra request per sync. A settable window would be speculative
  generalization; widening it later is a specification amendment.
- **The measured window is 180 days, and it is a budget decision before it is a training one.** A
  series costs one request per activity against a read allowance of roughly 100 requests per fifteen
  minutes and 1,000 per day, so a multi-year history cannot be fetched in full without the sync
  spanning days. Half a year comfortably covers the range over which fitness and fatigue are actually
  accumulated — a 42-day fitness constant is long settled by then — while fitting inside a single day's
  allowance for a typical athlete. Older training keeps estimated load, and every figure derived from
  it already states that basis, so nothing is silently degraded.
- **Electrically assisted rides are excluded deliberately.** Load for a session without a heart-rate
  series is estimated from moving time, and an assisted hour is not an unassisted hour. Including them
  would inflate an athlete's fitness with training they did not do. Strava has two such sport types,
  `EBikeRide` and `EMountainBikeRide`, and both are named in FR-010 so that neither is excluded merely
  by having been forgotten. Should a future feature make that
  distinction safely — measured load for every session, say — including them is a specification
  amendment, not a mapping tweak.
- **Edits and deletions are reconciled only over history actually re-read.** Strava's listing is
  filtered by when an activity happened, not by when it changed, so there is no cheap way to learn of an
  edit to an old activity. Reconciling over the look-back window catches the common case — correcting or
  deleting something from the last few days — at no extra request cost, and the full resynchronization
  covers everything else on demand. Webhook subscriptions, which would catch every change as it
  happens, are out of scope: they need a publicly reachable callback and a subscription lifecycle, which
  is exactly the Strava time-sink the project plan warns against.
- **Private activities are in scope.** Training the athlete chose not to publish still counts toward
  their load, so read access is requested at the level that includes it (FR-002). The data never leaves
  the athlete's own machine.
- **The store is local and single-process.** Persistence backs one person's analyzer on their own
  machine. Concurrent writers, replication, and migrations between machines are out of scope; FR-040
  forbidding overlapping syncs is what stands in for concurrency control.
- **The athlete triggers syncs; nothing schedules them.** A background scheduler, a daemon, or webhook
  subscriptions that push activity updates from Strava are out of scope for this feature. FR-031's
  answer must therefore work without them.
- **No user interface is specified here.** The consent redirect has to land somewhere, and the athlete
  has to be able to start a sync and read its summary, but what those look like belongs to Feature 6.
  This specification fixes what the sync does and reports, not how it is presented.
- **Nothing is ever sent to Strava.** Read access only (FR-002). Editing activities, uploading
  activities, and pushing any analyzer-derived value back are non-goals of the project.
- **Strava's own data is taken as correct, with one stated exception.** The import does not
  second-guess a moving time, re-derive a sport type, or reinterpret what Strava reports about an
  activity: it maps what Strava reports, or it declines the activity and says why (FR-011). The
  exception is implausible heart-rate samples, which FR-017f discards and FR-017g counts. A dropout is
  a recording artefact rather than a measurement, discarding one lets the preceding sample cover the
  gap — which is what the load calculation already does for every interval between samples — and the
  alternative is to throw away a whole session's measured evidence because of a few seconds of it.
- **Strava MCP remains a development-time aid only.** It may be used to discover real data shapes and
  edge cases while writing the plan and the tests; it is not the runtime integration, and anything
  learned from real data is reduced to anonymized, purpose-built fixtures (FR-043).
- **This feature adds the infrastructure boundary the constitution requires.** Isolating OAuth, token
  handling, API access, paging, and rate limits from domain logic, and testing that boundary
  separately, is Principle V. Where exactly those pieces live in the solution is a planning decision,
  not a specification one.
