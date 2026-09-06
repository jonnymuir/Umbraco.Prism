# Service Blueprint package design docs (archived)

This design-doc set described the service-blueprint engine, forms rendering, and validation
architecture while that code still lived in this repo. It has since moved out entirely — see
this repo's own `CLAUDE.md` — to [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder)
and [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco).
`UmbracoPrism.Core` carries no service-blueprint opinion of its own any more;
`UmbracoPrism.TestSite` consumes `Wayfinder.Umbraco` directly, the same way any other host would.

The original documents are kept for history in [`docs/archive/`](../archive/), but describe code
that no longer exists in this repo — don't treat them as current. For the live architecture, see:

- [`jonnymuir/Wayfinder`'s own `docs/guides/`](https://github.com/jonnymuir/Wayfinder/tree/main/docs/guides) — the engine, calculation language, and reference contract.
- [`jonnymuir/Wayfinder.Umbraco`'s own README](https://github.com/jonnymuir/Wayfinder.Umbraco#readme) — the Umbraco-hosted store, blocks, and authoring UI.
- [Service Request Hub and conditional fields](./service-request-hub-and-conditional-fields.md) — the one doc in this set that's still current, since it describes this repo's own `serviceRequestHub` page pattern rather than the engine itself.
