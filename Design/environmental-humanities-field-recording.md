# Environmental humanities: field recording reference services

**Status:** Draft, awaiting review before implementation begins
**Date:** 2026-10-05
**Requested by:** Jonny Muir

An internal design document. It sets out a family of reference services in which a practitioner in
the field records an observation (first: a butterfly sighting) from a phone, with a photo, a
position and a time. The record is identified with AI assistance, confirmed by the practitioner,
and published to a GIS layer that QGIS can read. Later services combine those records with other
datasets (ancient hedgerows, woodland) to ask questions of them.

## Contents

1. [Goals and non-goals](#1-goals-and-non-goals)
2. [Where each piece lives](#2-where-each-piece-lives)
3. [Scenarios and iterations](#3-scenarios-and-iterations)
4. [The service blueprint](#4-the-service-blueprint)
5. [Wayfinder change 1: capture mode on file upload](#5-wayfinder-change-1-capture-mode-on-file-upload)
6. [Wayfinder change 2: the location picker](#6-wayfinder-change-2-the-location-picker)
7. [Capture behaviour on the device](#7-capture-behaviour-on-the-device)
8. [Identification through Umbraco Automate and Umbraco.AI](#8-identification-through-umbraco-automate-and-umbraco-ai)
9. [The record and the GIS layer](#9-the-record-and-the-gis-layer)
10. [Privacy and security](#10-privacy-and-security)
11. [Delivery state](#11-delivery-state)
12. [Open questions and things to verify](#12-open-questions-and-things-to-verify)

---

## 1. Goals and non-goals

**Goals**

- Show a practitioner capturing a photo, position and time on a phone, through the Prism reference
  app (`UmbracoPrism.TestSite`, deployed as prismreference.com and wrapped by the Capacitor shell).
- Show Umbraco Automate and Umbraco.AI doing real work in a reference service, with the AI
  provider changeable by configuration alone.
- Keep the capture components general. Anything useful beyond butterflies lives in Wayfinder, so
  it reaches every layer above it.
- Know where the work is heading (a QGIS layer, comparison with other datasets) so each iteration
  moves towards it.

**Non-goals for the first iterations**

- A native app, or offline capture.
- Live QGIS integration. QGIS reads a published layer by URL.
- Multiple roles. One practitioner queue does everything. An analysis queue may be added later.
- Changes to Prism Core, Wayfinder.Umbraco, Umbraco.Automate or Umbraco.AI.

## 2. Where each piece lives

| Concern | Home | Reason |
|---|---|---|
| Capture mode on the existing file upload; the new location picker component, its script, validation and editor support; file inputs in the configured webhook support system | `Wayfinder` (`Wayfinder`, `Wayfinder.Engine`, `Wayfinder.Rendering.GovUk`, `Wayfinder.Editor`) | Not specific to butterflies or to Umbraco. A registered host-side component cannot receive its own configured properties in the renderer (see the extending-the-component-catalog guide), so a designer-configurable option has to be a Wayfinder property. File inputs help every flow that hands a document to Automate or another consumer. |
| Media-backed file storage, the Automate automation and its seeder, the AI agent and provider setup, the blueprints, the GeoJSON endpoint | `UmbracoPrism.TestSite` | A reference host. It already owns the demo queue, the file upload controllers and the Automate seeders. |
| Multi-tenant ownership of records, restricted precise locations per organisation | Host policy | The packages carry no tenancy or auth opinion. Restriction of precise coordinates enters through a host-registered policy. |
| Analysis against hedgerow and woodland layers | `UmbracoPrism.TestSite`, later | Same reason as above. |

## 3. Scenarios and iterations

The service blueprint model applies directly: the practitioner is the customer, the AI and GIS
publication are support processes, and a single queue carries the work.

| # | Scenario | Iteration |
|---|---|---|
| S1 | Record a sighting: photo, position, time, notes | 1 |
| S2 | AI first-pass identification through Automate | 1 |
| S3 | Practitioner confirms or corrects the suggestion | 1 |
| S4 | Publish confirmed records as a GIS layer | 2 |
| S5 | Compare records with other datasets and ask questions of them | 3 |

**Iteration 1** delivers S1 to S3 end to end, with the data shaped so S4 needs no remodelling.
**Iteration 2** serves confirmed records as GeoJSON from an endpoint. QGIS adds that URL as a
vector layer. A GeoPackage export and an OGC API Features endpoint follow if a live, filterable
layer is wanted.
**Iteration 3** adds a second blueprint that overlays records on an open hedgerow or woodland
dataset. The dataset is chosen when that iteration starts, after research into the environmental
humanities questions worth asking.

## 4. The service blueprint

One queue, `practitioner`, owns every stage. The practitioner starts the service and does all of
the work. There is no frontstage and backstage split in this scenario.

| Stage | What happens |
|---|---|
| `record-sighting` | Photo (file upload, capture mode as configured), location picker, date and time, optional notes. |
| `identify` | A `support-system-call` action invokes the identification capability. The practitioner sees a wait screen. |
| `confirm` | Shows the suggested species, confidence and habitat notes beside the photo and the pin. The practitioner accepts or corrects, and the confirmed values are the record. |
| `recorded` | Confirmation page with the record summary. |

The `identify` capability is declared in `appsettings.json` under `Wayfinder:SupportSystems`:

- **Inputs:** `photo` (the file reference), `location`, `capturedAt`, `notes`.
- **Outputs:** `speciesGuess`, `commonName`, `confidence`, `lifeStage`, `habitatNotes`.
- **Outcomes:** `identified`, `unclear`, `notButterfly`. The gateway after the stage routes
  `notButterfly` back to `record-sighting` with a message, and `unclear` to `confirm` with an empty
  suggestion.
- **Completion:** webhook, resolved by an in-process Automate step as in
  `ResolveWayfinderSupportSystemOutcomeAction`.

## 5. Wayfinder change 1: capture mode on file upload

`FileUploadComponent` gains an optional `CaptureMode` property chosen by the blueprint designer.

| Value | Rendering | Behaviour on a phone |
|---|---|---|
| unset or `choose` | `<input type="file" accept="image/*">` | The operating system offers both taking a photo and choosing an existing one. |
| `camera` | The same input with `capture="environment"` | Opens the rear camera directly. There is no library option. |

The property follows the path `AcceptedFileTypes` and `MaxSizeBytes` already take:

- the `FileUploadComponent` record and its `ComponentDescriptor` entry, as a closed set of options
  so the editor offers a select;
- the `StageRenderer` payload mapping and `FieldRenderPayload`;
- `GovUkFields.RenderFileUpload`;
- the generated editor TypeScript model and the step inspector;
- the reference service blueprint contract and the extending guides;
- tests: the rendered attribute for each mode, the editor property, and a blueprint round trip.

An unset value keeps today's rendering exactly, so existing blueprints are unaffected. The butterfly
service uses `choose`, because practitioners also upload photos taken earlier.

## 6. Wayfinder change 2: the location picker

A new input component, discriminator `location-picker`, that captures one geographic point.

### Value

The field value is a single string, `lat,lng`, in WGS84 decimal degrees, for example
`51.5074,-0.1278`. A scalar string works unchanged in calculations, the configured webhook support
system (scalar inputs only), CSV bulk data and GIS tooling. The server parses and validates it:
latitude from -90 to 90, longitude from -180 to 180, both finite, and a required picker rejects an
empty value.

### Control

- One labelled text input holding `latitude,longitude` is the primary control and the only posted
  field. The page is complete and accessible with the map script absent or failing, and no host
  needs to compose or whitelist extra keys.
- A "Use my current location" button fills the input from the browser Geolocation API and
  announces the result and its accuracy in a live region.
- The map is an enhancement layered over the input: click or tap to place a pin, drag the pin to
  adjust. Typing a valid point in the input moves the pin, and moving the pin rewrites the input
  in a canonical form (six decimal places, no spaces). The map region is focusable, pans with the
  arrow keys, and its label tells a keyboard user to type the location in the field instead.
- When the stage loads with an empty field, the picker requests the current position and fills
  it. An existing value is never overwritten. If permission is declined or unavailable, the field
  stays empty with a message saying how to proceed.
- Each change fires a bubbling `wayfinder:location-changed` event on the input, with the point,
  its `source` (`device`, `map` or `typed`) and, for a device fix, `accuracyMetres`.
- The check-your-answers row shows the coordinates as text.

### Map library: OpenLayers

OpenLayers is the chosen library.

- It is actively released (10.11.0 at the time of writing). Leaflet's last stable release is 1.9.4
  from May 2023, and its 2.0 line has been in alpha since May 2025.
- It reprojects natively (with proj4 for British National Grid, EPSG:27700) and reads WMS, WFS and
  GeoJSON layers. Iteration 3 overlays records on hedgerow and woodland data that is typically
  published in those forms, so the component does not need replacing later.
- The bundle, tree-shaken to this component (tile layer, vector point layer, drag interaction), is
  about 96 KB gzipped. The full distribution is about 299 KB gzipped, so the build step is worth
  taking. The script loads only on stages that contain a `location-picker`.
- Its map canvas is not keyboard-operable at the marker level. That is why the text input is the
  primary control and the map never the only way to set a value.

MapLibre GL is not used: it needs vector tile infrastructure, and its canvas offers the weakest
accessibility. Proprietary SDKs that need API keys are not used.

### Packaging

- `Wayfinder.Rendering.GovUk` ships one module, `wayfinder-location-picker.js`, with OpenLayers
  bundled in, plus its stylesheet and a third-party notice, as static web assets under
  `/_content/Wayfinder.Rendering.GovUk/location-picker/`. The script loads its own stylesheet, so a
  host adds a single `<script type="module">`.
- `npm run build` in `Wayfinder.Rendering.GovUk` (esbuild) builds the committed bundle from
  `src/`, so consumers need no Node toolchain. Continuous integration rebuilds it and fails on
  drift.
- Tile source, attribution and default centre are optional `<meta>` tags the host adds
  (`wayfinder-map-tile-url`, `wayfinder-map-attribution`, `wayfinder-map-default-centre`). The
  default is the public OpenStreetMap tile server with its required attribution, suitable for
  demos only. A production host configures its own provider (for the UK, the Ordnance Survey Maps
  API). The host's Content Security Policy must allow the tile host under `img-src`.

### Tests

The behaviour is tested through its outputs: validation of out-of-range, non-numeric and empty
values, the rendered input with and without a value, the blueprint JSON round trip, the point
rules the script shares with the server, and a real-browser test (semantic selectors, mocked
device location, local tiles) that covers auto-fill, the button, clicking the map, typing, a
declined permission, an existing value being kept, and the no-script fallback.

## 7. Capture behaviour on the device

- **Secure context.** Camera capture and geolocation need HTTPS. prismreference.com and the
  Capacitor shell both qualify.
- **Geolocation API, not Capacitor.** The plain browser API works in the desktop site and in the
  shell's WebView, with one code path. The generated Capacitor bundle must carry the iOS and
  Android camera and location permission declarations. This is checked in
  `UmbracoPrism.MobileBundleCli` output before iteration 1 is called done.
- **Position comes from the device at capture time, not from the photo.** Browsers and phone
  operating systems generally strip GPS metadata from camera captures and often from library
  picks, so EXIF cannot be the primary source.
- **Photos taken earlier.** After a file is chosen:
  1. If the file's last-modified time is within a couple of minutes of now, treat it as freshly
     taken and keep the device position.
  2. Otherwise, if the photo carries EXIF GPS and a timestamp, offer those.
  3. Otherwise, keep the picker on the current position with a prompt to move the pin to where the
     photo was taken.
  Step 2 needs a small EXIF reader and is a second drop of the picker. The first drop covers
  step 1 and the manual fallback.
- **Provenance.** The record keeps where the location came from (device, photo metadata or
  entered by hand), the reported accuracy in metres, and the source of the capture time. The
  confirm stage and later analysis both need to tell a measured position from a placed pin. The
  picker posts only the point. A small TestSite script listens for `wayfinder:location-changed` and
  copies `source` and `accuracyMetres` into companion fields declared in the blueprint (section 12).

## 8. Identification through Umbraco Automate and Umbraco.AI

### Packages

`UmbracoPrism.TestSite` references `Umbraco.Automate` 17.4.0 and adds the Umbraco 17 line of the AI
packages:

- `Umbraco.AI` 17.5.0 and `Umbraco.AI.Agent` 17.3.0
- `Umbraco.AI.Automate` 17.0.1, which adds the **Run AI Agent** and **Transcribe Audio** actions and
  the AI triggers to Automate's catalogue
- `Umbraco.AI.Google` 17.0.3, the Gemini provider
- `Google.GenAI` 1.24.0, pinned by the host. `Umbraco.AI.Google` accepts any 1.x and resolves
  1.10.0 by default, which rejects an inline image part's `displayName` with a Gemini Developer API
  key and so fails every photo sent to a free AI Studio key.

The 18.x line targets Umbraco 18 and is not used.

### Flow

1. The `identify` stage's support-system call is sent by the configured webhook client, which POSTs
   a signed invocation, photo reference included, to an Automate webhook trigger.
2. The automation runs **Run AI Agent** with the photo as its attachment
   (`${trigger.body.inputs.photo.storageKey}`, the media UDI) and the location, date, time and notes
   in the message. The notes are quoted as data, and the agent's instructions say they are never
   instructions.
3. The agent returns structured output. Each schema field is exposed to later steps as a named
   binding under the step's id.
4. A typed in-process action, `prism.resolveButterflyIdentification`, reads each binding as its own
   setting and completes the invocation. It builds the payload with `JsonObject` from individual
   fields, so model text is never spliced into JSON. The outcome is restricted to `identified`,
   `unclear` and `not-a-butterfly`, confidence to `low`, `medium` and `high`, control characters are
   replaced, and lengths are capped. No branch step is needed because the agent's `outcome` field is
   passed straight through.

### The Umbraco.AI setup

`FieldRecordingAiSetup` creates the connection (`field-recording-gemini`), the chat profile
(`field-recording-vision`) and the agent (`butterfly-identifier`, scoped to the Automations surface,
read-only tool permissions). `ButterflyIdentificationSeeder` then publishes the automation on every
boot, following `JugglingLicenceDecisionAutomationSeeder`: create or update, then publish, never throw.

- The connection's API key is the reference `$Umbraco:AI:Secrets:GoogleGeminiApiKey`, resolved from
  the host's configuration (a user secret locally). It is never committed or logged.
- The connection and profile are created only when missing, so moving to another provider is an edit
  to that profile and nothing else, and a later boot does not undo it. The agent's instructions and
  output schema are the contract the automation depends on, so they are refreshed on every boot while
  its profile is left alone.
- The output schema is `outcome` (required, an enum), `speciesGuess`, `commonName`, `confidence`,
  `lifeStage` and `habitatNotes`.
- The instructions require an honest `unclear` when the photo does not support an identification,
  say never to guess a species to be helpful, and limit habitat notes to what is visible.

### The model

The default model is `gemini-3.6-flash`, configurable with `Prism:FieldRecording:GeminiModel`. A model set there is
applied on every boot, including to a profile that already exists; with none set, the profile keeps the model it has
(for example one chosen in the backoffice).

- The agent runtime always declares tools alongside a JSON output schema. `gemini-2.5-flash` rejects
  that combination outright (HTTP 400, "Function calling with a response mime type: 'application/json'
  is unsupported"). The Gemini 3 family accepts it.
- Free-tier availability shifts quickly. A flash-lite model has already been withdrawn for new keys,
  and several models intermittently answer 503 "high demand". The default is the fastest model that
  answered a photo correctly when it was chosen (about 7 seconds).
- The free tier has a daily request quota per model per project
  (`GenerateRequestsPerDayPerProjectPerModel-FreeTier`), observed at 20 requests a day. Each sighting
  costs at least one request, and every retry costs another, so a busy day or a long test session can
  exhaust it (HTTP 429, with a retry delay of many hours). The quota is per model, so switching
  `Prism:FieldRecording:GeminiModel` to another Gemini 3 model gives a fresh allowance. Umbraco.AI
  reports every Google client error as "Couldn't reach the AI service", so the real cause is only in
  the site's log.

### When the AI does not answer

- The agent step retries once, five seconds apart, which rides out a transient 503. On a live run the
  first attempt was a 503 and the retry succeeded. It does not retry more, because a quota error (429) is
  not transient and every attempt spends the same exhausted allowance.
- If the step still fails, Automate skips past it and runs the next step with empty bindings. The
  typed action treats a missing or unrecognised outcome as `unclear` with the note "No suggestion was
  available. Enter what you saw yourself.", so the practitioner reaches the confirm stage and types
  the species instead of waiting.
- Wayfinder has no timeout for a support-system call that never answers. A failed AI step is covered
  as above, but an automation that never runs at all would leave the practitioner at the wait screen.

### Files through the configured webhook support system (Wayfinder change 3)

The configuration-only webhook support system sends scalar inputs and throws on an input that
resolves to an uploaded file. The photo is a file upload field, and any Automate flow that calls
AI is likely to want a file, so `WebhookSupportSystemClient` is extended to send one.

An input that resolves to an uploaded file is written into the envelope as the file reference
object, the same shape a file-typed support-system output already uses on the way back in:

```json
"inputs": {
  "photo": {
    "storageKey": "9f1c...",
    "originalFileName": "IMG_2041.jpg",
    "contentType": "image/jpeg",
    "sizeBytes": 2210456
  }
}
```

- **No bytes and no URL travel.** The consumer receives the opaque storage key and descriptive
  metadata only. The envelope still carries no callback or download URL, so a leaked signing key
  cannot turn the host into an HTTP client or hand out file access.
- **The key means something to the host.** With the Media-backed storage below, `storageKey` is
  the Umbraco media key, which is exactly what Run AI Agent accepts as an attachment. An
  automation on the same site binds `inputs.photo.storageKey` straight to the action.
- **Out-of-process consumers.** A consumer that does not share the host's storage receives a key
  it cannot resolve. Delivering the bytes to such a consumer (an opt-in inline encoding with a
  size cap, or a short-lived signed download URL) is a separate decision with its own security
  review and is not part of this change.
- **Change surface:** the client, the "Scalar inputs only" note in `docs/guides/support-systems.md`,
  and the test that currently asserts the throw, which becomes a test that the envelope carries
  the reference object and nothing else.

Nothing in existing configuration depends on the throw, so no behaviour is kept for it.

### Photos live in Umbraco Media

Run AI Agent takes attachments as media keys, UDIs or picker values. `MediaBackedSightingPhotoStorage`
decorates the host's `IServiceRequestFileStorage` and handles only the `sightingPhoto` field: it writes
the file to the media library under a "Butterfly sightings" folder and returns the media UDI as the
storage key. Every other upload falls through to the disk storage it wraps, so the juggling licence and
bulk contributions are unaffected, and the existing upload and download controllers keep working
because they depend only on the interface.

- Only JPEG, PNG and WebP are accepted, checked by file name and by file signature, because the engine
  adapter hands storage a stream with a generic content type.
- A stored reference can only be read back if it names a media item inside the sightings folder, so a
  storage key is never a way to open an unrelated media item.
- Retention, deletion and access control of the media items follow section 10.

## 9. The record and the GIS layer

Field names follow Darwin Core where a matching term exists, so the layer, exports and any later
sharing need no remapping.

| Record field | Darwin Core term | Source |
|---|---|---|
| Position | `decimalLatitude`, `decimalLongitude` | Location picker |
| Position accuracy | `coordinateUncertaintyInMeters` | Device-reported, or a stated default for a placed pin |
| Date and time | `eventDate` | Device clock or photo metadata, as recorded |
| Species | `scientificName`, `vernacularName` | Confirmed by the practitioner |
| Identification status | `identificationVerificationStatus` | `unverified` (AI suggestion only), `verified` (practitioner confirmed) |
| AI suggestion and confidence | `identificationRemarks` | Automate result |
| Notes and habitat | `occurrenceRemarks`, `habitat` | Practitioner and AI suggestion |
| Photo | associated media | Media item key |

The AI result is advisory. A record becomes `verified` only when the practitioner confirms it.
Only verified records are published.

**Iteration 2 publication.** A TestSite endpoint returns the verified records as a GeoJSON
FeatureCollection in WGS84 with the properties above. QGIS adds it as a vector layer by URL. The
endpoint carries an explicit authorization policy and applies the location restriction in
section 10.

## 10. Privacy and security

- **Gemini free tier.** The free tier permits Google to use submitted content to improve its
  products (the current terms must be re-checked before use). Photos with a location from real
  practitioners must not go through it. The demo uses the project's own photos, and the demo
  narration says so. Real use switches the Umbraco.AI profile to a paid or self-hosted provider
  with suitable terms.
- **Location sensitivity.** Precise locations of rare or protected species are conventionally
  generalised before sharing. The GeoJSON endpoint passes each record through a host-registered
  policy that can return a coarsened position. The package takes no opinion on which records are
  sensitive. The first version registers a policy that returns the position unchanged, with a test
  that proves a replacement policy is honoured.
- **Uploaded photos.** Size and type are enforced server-side against the field's declared limits
  before the file reaches storage. The existing upload controller's antiforgery token, upload
  nonce and ownership checks apply unchanged. Photo metadata is not trusted: coordinates and
  times in the stored record are the values the practitioner confirmed.
- **Photos are publicly servable.** Umbraco serves media by path. A sighting photo, which also shows
  where the practitioner was, is only as private as that path is unguessable, and is not access
  controlled. Real use needs private storage or an authorised download route.
- **Model output is untrusted.** The agent's answer, and the notes a practitioner types, reach a model
  that can be steered, so the answer is treated as untrusted text end to end: fixed outcome and
  confidence sets, values only ever set as JSON values, and a graceful result when nothing usable comes
  back.
- **Browser permissions.** The site's default `Permissions-Policy` forbids geolocation and its CSP
  blocks map tiles. The host widens exactly two things through Prism's existing options:
  `geolocation=(self)` for this origin, and the tile host under `img-src`. Camera stays off, because a
  file input's `capture` attribute does not use it.
- **Automate webhook.** HMAC-SHA256 signed with a key from configuration, as for the existing
  support systems. The signing key and the Gemini key are never committed or logged.
- **Endpoints.** Every new endpoint carries an explicit authorization policy. `AllowAnonymous`
  appears only with a written reason in the code.
- **Retention.** Photos in Media follow a stated retention rule for abandoned and rejected
  sightings. The rule is settled before iteration 1 ships.
- **Regression checks.** Behavioural tests go red if the location policy is bypassed, if an
  unverified record is published, or if the support-system callback accepts anything beyond the
  invocation id.

## 11. Delivery state

1. **Wayfinder** 0.16.0 carries the three changes (capture mode, location picker, file inputs in the
   configured webhook support system), with Editor 0.6.0 and Rendering.GovUk 0.6.0.
2. **Wayfinder.Umbraco** 2.2.0 applies the capture mode in its own progressive-upload markup through the
   public `GovUkFileUploadField.CaptureAttribute`, and loads the picker module on a stage that contains a
   `location-picker`.
3. **Prism `TestSite`** has the Umbraco.AI packages, the Media-backed photo storage, the
   `butterfly-identification` support system, the Umbraco.AI setup, the seeded automation, the typed
   hand-back action, the blueprint and its page. A phone-width headless-browser run over HTTPS goes from
   photo upload through device location, the signed webhook, Gemini and the confirm stage to the final
   record. An Environmental humanities page (a rich-text hub under Home) describes the services and links
   to each one; it is in the desktop nav and on the member dashboard. The mobile bottom bar stays at four
   tabs because its Block List data type allows at most four.
4. **Iteration 2**: the GeoJSON endpoint and a QGIS walkthrough.
5. **Iteration 3**: dataset research, then the overlay blueprint.

## 12. Open questions and things to verify

1. **Notes that try to steer the model.** The agent's instructions say to treat the notes as data. A
   photo with no butterfly has been confirmed against the live model (`gemini-3.6-flash` answered
   `not-a-butterfly` and the journey ended with nothing recorded). The hostile-notes case has not: every
   attempt hit Gemini 503 "high demand" and took the graceful-degradation path. Re-run it when capacity
   allows.
2. **Provenance transport.** The picker posts one value, `lat,lng`, and announces source and accuracy
   through `wayfinder:location-changed`. Confirm a small TestSite script can copy them into companion
   fields that the blueprint declares (for example hidden or read-only inputs), and that the
   validator's key whitelist accepts them as ordinary fields.
3. **Progressive upload script.** Confirm in a browser that the progressive-upload script keeps the
   `capture` attribute on the input it drives.
4. **A real phone.** Camera capture, the location permission prompts and the last-modified heuristic
   for a freshly taken photo have only been exercised in a desktop browser. Confirm on iOS and Android,
   including that the generated Capacitor bundle declares camera and location usage on both.
5. **Support-call timeout.** Wayfinder has no timeout for a support-system call that never answers. A
   small Wayfinder feature (an outcome the engine resolves itself after a declared time) would close
   the gap the typed action only covers for a failed AI step.
6. **Photo privacy and retention.** Media is publicly servable by path, and abandoned or rejected
   sightings leave their photo behind. Decide on private storage or an authorised download route, and
   a retention rule.
7. **Free-tier capacity.** The daily per-model quota and intermittent 503s make the free tier suitable
   for a demo only. Decide whether the demo needs a paid key or a second model to fall back on.
