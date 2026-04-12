## Brewster — Keycloak localhost HTTPS exposure

**Context**

The AppHost advertised Keycloak at `https://localhost:8443` by calling `WithHttpsEndpoint(port: 8443, targetPort: 8080)` while the pinned Keycloak `start-dev` container only served HTTP on port 8080.

Runtime verification showed:

- `curl https://localhost:8443/...` failed the TLS handshake.
- `curl http://localhost:8443/...` succeeded.
- The discovery document from port 8443 reported `http://localhost:8443/...` issuer metadata.

**Decision**

Do not advertise an Aspire `WithHttpsEndpoint(...)` as a browser-usable TLS route unless the backing resource actually speaks HTTPS or a real TLS reverse proxy is in place.

For the current local Keycloak setup, use the real HTTP endpoint (`http://localhost:8080`) as `KEYCLOAK_URL` and document the limitation explicitly.

**Why**

- Prevents the TestSite from seeding a broken HTTPS authority.
- Avoids misdiagnosing the problem as Safari certificate trust when the route is not TLS at all.
- Keeps the repo honest until a real cert-backed localhost proxy or native Keycloak HTTPS is added.

**Standing Effect**

- Future local HTTPS work for Keycloak must include real TLS termination, not just an Aspire endpoint scheme label.
- Validate advertised browser URLs with an actual TLS probe before documenting them.
