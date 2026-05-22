# Decision: Clear the core-tests CI warning pair with API-aligned backend fixes

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Blathers  
**Status:** Implemented  

## Decision

1. Replace `ForwardedHeadersOptions.KnownNetworks` with `KnownIPNetworks` in `PrismComposer`.
2. Keep the forwarded-header trust behaviour unchanged by still clearing the trusted proxy collections in the same place.
3. Rewrite bearer token header parsing in `PrismAuthExtensions` to use a single local authorization header value before slicing the token.

## Why

- ASP.NET now marks `KnownNetworks` obsolete in favour of `KnownIPNetworks`, so the composer should follow the supported API instead of carrying a known deprecation into CI.
- The auth diagnostics path only needed a null-safe header read; a small local-variable rewrite removes the nullable warning without changing runtime behaviour.
- This keeps the fix plain and low-risk inside backend infrastructure code already owned by Blathers.

## Validation

- `dotnet build UmbracoPrism.sln -c Release --nologo`
- `dotnet test UmbracoPrism.sln -c Release --filter "FullyQualifiedName~PrismAuthExtensionsSecurityTests|FullyQualifiedName~LocalhostGenericOidcRegressionTests|FullyQualifiedName~BackchannelRewriteTests|FullyQualifiedName~BackchannelSecurityTests" --nologo`
- `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests --nologo`
