// GOV.UK Frontend v5 ships its JS as an ES module (see the dist file's own README), not a
// UMD/global-attaching bundle, so it must be loaded with type="module" and initAll imported
// explicitly. window.GOVUKFrontend is assigned here purely so existing call sites (and this
// behaviour's own regression test, govuk-frontend-bootstrap.spec.ts) can keep checking it,
// matching the shape the previous classic-script version exposed.
import { initAll } from '/js/govuk-frontend.min.js';
window.GOVUKFrontend = { initAll };
initAll();
