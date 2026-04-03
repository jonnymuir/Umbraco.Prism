# Umbraco Prism Documentation

Complete guides for setup, mobile generation, authentication, and notifications.

---

## Setup & Installation

### [Umbraco Setup](umbraco-setup.md)
Install Prism, configure `Program.cs`, create document types, and set up your first tenant. Auto-seeding option for new sites.

### [Biometric Setup](biometric-setup.md)
Generate signing and encryption keys for mobile biometric authentication. Key Vault configuration for production.

### [Push Notification Setup](PUSH_SETUP.md)
Configure Firebase Cloud Messaging (Android) and Apple Push Notification service (iOS). Includes certificate setup and troubleshooting.

---

## Mobile & Notifications

### [Notifications Design Overview](notifications-design.md)
Push notification architecture: device registration, subscription management, content-triggered vs API-triggered notifications.

---

## Design Documents (Internal Reference)

These documents describe internal architecture and implementation details for contributors and maintainers.

### [Notifications Architecture](design/notifications-architecture.md)
**Internal design:** System layers, FCM integration, tenant isolation, and notification delivery pipeline.

### [Notifications Backend](design/notifications-backend.md)
**Internal design:** Backend API, service interfaces, subscription management, and database schema.

### [Notifications Mobile](design/notifications-mobile.md)
**Internal design:** Capacitor plugin integration, token lifecycle, permission flow, iOS/Android native setup.

### [Notifications Umbraco Integration](design/notifications-umbraco-demo.md)
**Internal design:** Umbraco content hooks, notification handlers, and Vinyl Vault demo site architecture.

---

## Need Help?

- [Main README](../README.md) — Quick start and feature overview
- [CHANGELOG](../CHANGELOG.md) — Version history and release notes
- [CONTRIBUTING](../CONTRIBUTING.md) — Contribution guidelines and development workflow
