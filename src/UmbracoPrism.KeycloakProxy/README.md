# UmbracoPrism.KeycloakProxy

Lightweight HTTPS reverse proxy for local Keycloak development.

## Purpose

Provides real browser-usable HTTPS at `https://localhost:8443` for Keycloak authentication flows. Safari/WebKit requires HTTPS for secure cookies (`Secure; SameSite=None`) that Keycloak 26 uses for authentication sessions.

## How It Works

- Listens on `https://localhost:8443` with TLS using the .NET development certificate
- Forwards all requests to Keycloak's HTTP endpoint at `http://localhost:8080`
- Sets X-Forwarded headers so Keycloak knows the external origin is HTTPS

## Technology

- **YARP** (Yet Another Reverse Proxy) - Microsoft's reverse proxy library
- **Kestrel** - ASP.NET Core web server with TLS support
- **.NET dev certificate** - Already trusted on most dev machines

## Usage

Automatically orchestrated by `UmbracoPrism.AppHost` in local development. No manual setup required.

1. Ensure the .NET dev certificate is trusted: `dotnet dev-certs https --trust`
2. Start the Aspire AppHost
3. Use `https://localhost:8443` for all Keycloak interactions

## Configuration

All configuration is in `appsettings.json`:
- Proxy routes defined via YARP
- X-Forwarded headers set in route transforms
- Target backend is `http://localhost:8080`

## Why Not Use Keycloak Native HTTPS?

Keycloak native HTTPS requires pre-generating certificate files and mounting them into the container, which adds complexity for fresh clones. This proxy approach keeps everything repo-owned and automatic.
