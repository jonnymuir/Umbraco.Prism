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
11. [Delivery sequence](#11-delivery-sequence)
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

- Latitude and longitude text inputs are the primary control. The page is complete and accessible
  with the map script absent or failing.
- A "Use my current location" button fills the inputs from the browser Geolocation API.
- The map is an enhancement layered over the inputs: click or tap to place a pin, drag the pin to
  adjust. Typing in the inputs moves the pin, and moving the pin updates the inputs. The map
  region carries a text alternative that states the current coordinates.
- When the stage loads with no value, the picker requests the current position and centres on it.
  If permission is denied or unavailable, it centres on a configured default and shows the manual
  inputs only.
- The check-your-answers row shows the coordinates as text.

### Map library: OpenLayers

OpenLayers is the chosen library.

- It is actively released (10.11.0 at the time of writing). Leaflet's last stable release is 1.9.4
  from May 2023, and its 2.0 line has been in alpha since May 2025.
- It reprojects natively (with proj4 for British National Grid, EPSG:27700) and reads WMS, WFS and
  GeoJSON layers. Iteration 3 overlays records on hedgerow and woodland data that is typically
  published in those forms, so the component does not need replacing later.
- A bundle tree-shaken to this component (tile layer, vector point layer, drag interaction) is
  about 97 KB gzipped. The full distribution is about 299 KB gzipped, so the build step is worth
  taking. The script loads only on stages that contain a `location-picker`.
- Its map canvas is not keyboard-operable at the marker level. That is why the text inputs are the
  primary control and the map never the only way to set a value.

MapLibre GL is not used: it needs vector tile infrastructure, and its canvas offers the weakest
accessibility. Proprietary SDKs that need API keys are not used.

### Packaging

- `Wayfinder.Rendering.GovUk` ships `wayfinder-location-picker.js` and the OpenLayers bundle under
  `wwwroot`, alongside `wayfinder-slider.js` and the vendored govuk-frontend.
- A small esbuild script in the repository builds the tree-shaken OpenLayers bundle into a single
  committed file, so consumers need no Node toolchain.
- The tile source is configuration supplied by the host. The default is the public OpenStreetMap
  tile server with its required attribution, suitable for demos only. A production host configures
  its own provider (for the UK, the Ordnance Survey Maps API). The host's Content Security Policy
  must allow the tile host under `img-src`.

### Tests

The behaviour is tested through its outputs: validation of out-of-range, non-numeric and empty
values, the rendered inputs with and without a value, the editor descriptor, and a Playwright test
(semantic selectors) that sets the value through the text inputs and through the current-location
button with the geolocation permission granted and denied.

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
  confirm stage and later analysis both need to tell a measured position from a placed pin. How
  the picker posts these alongside the point is an open question (section 12).

## 8. Identification through Umbraco Automate and Umbraco.AI

### Packages

`UmbracoPrism.TestSite` already references `Umbraco.Automate` 17.4.0. It adds the Umbraco 17 line
of the AI packages:

- `Umbraco.AI` (17.x)
- `Umbraco.AI.Agent` (17.x)
- `Umbraco.AI.Automate` (17.x), which adds the **Run AI Agent** and **Transcribe Audio** actions
  and the AI triggers to Automate's catalogue
- `Umbraco.AI.Google` (17.x), the Gemini provider

The 18.x line targets Umbraco 18 and is not used.

### Flow

1. The `identify` stage's support-system call is sent by the configured webhook client, which
   POSTs a signed invocation, photo reference included, to an Automate webhook trigger.
2. The automation runs **Run AI Agent** with the photo as an attachment (a media key, up to 10
   attachments and 20 MB, images passed directly to vision-capable models) and the location,
   time and notes in the prompt.
3. The agent returns structured output. Each schema field is exposed to later steps as a named
   binding.
4. A branch maps the result to an outcome, and a resolve step (the in-process custom action
   pattern already in TestSite) completes the invocation with the outputs.

The agent is created in the Umbraco.AI backoffice and scoped to the **Automations** surface, with
read-only tool permissions. Its output schema is `speciesGuess`, `commonName`, `confidence`,
`lifeStage`, `habitatNotes` and `isButterfly`. Its instructions require an honest "unclear" when
the photo does not support an identification, and forbid inventing locality claims.

An `AutomateButterflyIdentificationSeeder` builds and publishes the automation on every boot,
following `JugglingLicenceDecisionAutomationSeeder`: create or update, then publish, never throw
if seeding fails. The agent and profile are seeded by the host the same way where Umbraco.AI
exposes a service for it, and are otherwise a documented manual setup step.

### Provider

The demo profile uses the Gemini free tier through `Umbraco.AI.Google`. Changing provider is a
change to the Umbraco.AI profile and nothing else, which is the point of routing the call through
Umbraco.AI. The API key is held in the host's secret store (a user secret locally, the existing
secret mechanism in deployment) and never in committed configuration. See section 10 for what the
free tier means for real data.

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

Run AI Agent takes attachments as media keys. TestSite's `IServiceRequestFileStorage`
implementation for the photo field therefore writes the file into the Umbraco media library and
returns the media key as the reference. The existing upload and download controllers keep working
because they depend only on the interface. Retention, deletion and access control of the media
items follow section 10.

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
- **Automate webhook.** HMAC-SHA256 signed with a key from configuration, as for the existing
  support systems. The signing key and the Gemini key are never committed or logged.
- **Endpoints.** Every new endpoint carries an explicit authorization policy. `AllowAnonymous`
  appears only with a written reason in the code.
- **Retention.** Photos in Media follow a stated retention rule for abandoned and rejected
  sightings. The rule is settled before iteration 1 ships.
- **Regression checks.** Behavioural tests go red if the location policy is bypassed, if an
  unverified record is published, or if the support-system callback accepts anything beyond the
  invocation id.

## 11. Delivery sequence

1. **This document** is reviewed.
2. **Wayfinder**, one branch and pull request carrying the three changes (capture mode, location
   picker, file inputs in the configured webhook support system), with docs and tests. Released through the existing lockstep release chain.
3. **Wayfinder.Umbraco** picks up the new Wayfinder versions.
4. **Prism `TestSite`** picks up the new versions and adds the Umbraco.AI packages, Media-backed
   storage, the support-system client, the seeder, the blueprint and the demo script. TestSite work
   can start against locally built Wayfinder packages while steps 2 and 3 are in review.
5. **Iteration 2**: the GeoJSON endpoint and a QGIS walkthrough.
6. **Iteration 3**: dataset research, then the overlay blueprint.

A spike comes first, before any component work: add the Umbraco 17 AI packages to TestSite, set up
the Gemini profile and an agent, and run a hand-built **Run AI Agent** automation against a test
photo, to confirm the 17.x stack boots and returns structured output.

## 12. Open questions and things to verify

1. **Umbraco 17 stack.** Confirm `Umbraco.AI`, `.Agent`, `.Automate` and `.Google` 17.x boot
   together with `Umbraco.Automate` 17.4.0 in TestSite and that Run AI Agent returns the schema
   bindings.
2. **Media into the automation.** Confirm `inputs.photo.storageKey` from the webhook trigger can
   be bound to Run AI Agent's attachment as a media key, and whether Automate ships an action to
   create a media item (only needed if a step, not the storage, has to create it).
3. **Provenance transport.** The picker posts one value, `lat,lng`. Accuracy and source need a
   route into the record. The leading option is optional companion field keys on the picker
   (`SourceFieldKey`, `AccuracyFieldKey`) that its script fills. To be settled in the Wayfinder
   pull request.
4. **Progressive upload script.** Confirm that the upload path TestSite uses honours the
   `capture` attribute, which should hold because it is the same input element.
5. **Capacitor permissions.** Confirm the generated bundle declares camera and location usage on
   both platforms.
6. **Photo retention rule** for abandoned and rejected sightings.
7. **Last-modified heuristic.** Confirm on real iOS and Android devices that a freshly taken photo
   reports a last-modified time close to now.
