# Session: Telemetry Security Configuration

**Date:** 2026-04-20T21:17:20Z

Fixed unsecured Aspire OTLP telemetry endpoint warning by adding `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` to development launch configuration. Acknowledged and documented security implications with production guidance.

**Decision:** `.squad/decisions/inbox/copper-telemetry-security.md`
