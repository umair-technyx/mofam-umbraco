# Mofam — Umbraco Headless API

A headless API built on **Umbraco CMS 17.8.2** (LTS). The Umbraco backoffice is used only as a content editor: there are no Razor views and no public content rendering. A Next.js frontend reads all content through the REST endpoints below.

## Tech Stack

- **.NET 10** / **Umbraco CMS 17.8.2** (versions pinned centrally in `src/Directory.Packages.props`)
- **Examine** — Umbraco's search index, used for page lookup and site search
- **SQL Server / Azure SQL** via `ConnectionStrings:umbracoDbDSN`
- **Azure Blob Storage** for media (`Umbraco.StorageProviders.AzureBlob`)
- **Azure Key Vault** for configuration outside Development
- **Microsoft Entra ID** single sign-on for the backoffice (App Roles → Umbraco user groups)
- **Google reCAPTCHA Enterprise** on the contact form
- **MailKit (SMTP)** or **SendGrid** for email
- **Serilog** (console, file, Azure Blob) with optional **Sentry**

## Project Structure

Clean-architecture style, split across four projects:

```
Mofam.Domain           Options, constants, DTOs, requests, exceptions
Mofam.Infrastructure   Middleware, rate limiting, API key check, health checks, logging
Mofam.Application      Services, mappers, cache, notification handlers
Mofam.CMS              Umbraco host — Program.cs, controllers, composers, Entra login, config
```

Interfaces live in an `Abstractions/` (or `IServices/`) folder in their layer.

## Getting Started

1. Clone the repo and open the solution in Visual Studio or Rider.
2. Create `src/Mofam.CMS/appsettings.Development.json` (gitignored) with your local values; see **Configuration**.
3. Point `ConnectionStrings:umbracoDbDSN` at your database.
4. Run `Mofam.CMS`. On first run Umbraco installs the database (unattended install is on).
5. Visit `/` for the status page and `/umbraco` for the backoffice.

In Development, Key Vault is not read; everything comes from the appsettings files. In every other environment the app loads Key Vault from `KeyVault:URL`.

## Configuration

Main `appsettings.json` sections (in Key Vault, use `--` between levels, e.g. `RateLimit--PermitLimit`; in environment variables use `__`):

| Section | Purpose |
|---|---|
| `Security:ApiKey` | Shared key required on every `/api` request (`X-Api-Key` header) |
| `RateLimit` | Fixed-window limits: `PermitLimit`/`WindowSeconds` (API, per visitor), `SignInPermitLimit`/`SignInWindowSeconds`, `FormPermitLimit`/`FormWindowSeconds` (per visitor, per form), `FormTotalPermitLimit` (all visitors, per form) |
| `Cache` | Cache on/off and durations for site root, pages, 404s, startup, filters and contact settings |
| `Cors:AllowedOrigins` | Allowed browser origins (empty = none) |
| `BackOfficeEntraId` | Entra ID login: tenant, client, secret, callback paths, `DenyLocalLogin`, `ExtraGroups` |
| `GoogleReCaptcha` | reCAPTCHA Enterprise legacy verification: `Enabled`, `SiteKey` (Enterprise site key), `SecretKey` (Enterprise legacy secret key, keep it in Key Vault or user secrets, never in the repo) and `MinimumScore` (default `0.5`) and `AllowedHostnames` (front end hosts a token may come from; empty = not checked here, so also restrict the allowed domains of the key in the Google console). The backend checks the token at `https://www.google.com/recaptcha/api/siteverify`. The front end still loads `enterprise.js?render=<SiteKey>` and calls `grecaptcha.enterprise.execute('<SiteKey>', { action: 'submit' })` |
| `EmailSettings` | Sender, SMTP or SendGrid |
| `LogStorage` | Azure Blob containers for application and Umbraco logs |
| `Sentry` | Optional error tracking (`Enabled` + `Dsn`) |
| `KeyVault` | Key Vault URL, plus a service principal if no managed identity is used |
| `AllowedHosts`, `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` | The public host of this backend |
| `Umbraco:CMS:Content:DisallowedUploadedFileExtensions` | File types that cannot be uploaded to Media (includes `html`, `xml`, `js`) |

## API Endpoints

Every `/api` request needs the `X-Api-Key` header. The frontend server also sends `X-Client-IP` (the visitor's address, from Cloudflare's `CF-Connecting-IP`); it is trusted only together with a valid key.

| Endpoint | Description |
|---|---|
| `GET /api/web/{culture}/startup` | Site settings: header, footer, navigation, labels |
| `GET /api/web/{culture}/{contentType}/{slug}` | One page by content type and slug |
| `GET /api/web/{culture}/filters?contentType=` | Filter groups for a listing |
| `POST /api/web/search` | Search with paging and filters (query max 150 characters) |
| `POST /api/web/form/{culture}/contact` | Contact form (reCAPTCHA + honeypot) |
| `GET /health` | Health check for load balancers (no key) |
| `GET /` | Status page (no Umbraco version shown) |

Responses use one shape: `ApiResponse<T>` with `StatusCode`, `Success`, `Message`, `Data`, `Errors` and `TraceId`.

## Rate Limits

- **API (`/api/web`)**: per visitor, `PermitLimit` per `WindowSeconds`. The frontend server's own calls (valid key, no `X-Client-IP`) are not limited.
- **Forms (`/api/web/form/...`)**: each form has its own counters: `FormPermitLimit` per visitor and `FormTotalPermitLimit` for all visitors, per `FormWindowSeconds`. Only requests with a valid key count toward the total.
- **Sign-in pages**: per visitor.
- A refused request gets `429` with a `Retry-After` header. IPv6 visitors are counted per `/64` block.

## Caching

Pages, startup and filters are cached in memory. A publish, a dictionary change or a media change in the CMS clears them at once.

## Request Pipeline

In order (see `Program.cs`):

1. Forwarded headers
2. Security headers (CSP, frame, cross-origin and referrer policies)
3. Response compression (Brotli + Gzip)
4. Global exception handler — a client mistake gets its status; anything else is a generic `500` with the `TraceId`
5. HSTS (outside Development) and HTTPS redirection
6. CORS
7. Rate limiting
8. For `/api` only: API key check → 404 normalization → request validation (JSON only, 1 MB body limit) → XSS check (not on forms)
9. Umbraco (backoffice, controllers, health check, status page)

## Health Check

`GET /health` checks that Umbraco is running and that the database answers `SELECT 1`. The result is cached for 10 seconds. It needs no key, so load balancers and uptime monitors can call it.

## Backoffice Login (Entra ID)

- Each Azure App Role value equals an Umbraco user group alias (`admin`, `editor`, `writer`, `translator`). Groups are synced on every login.
- A user with no matching role cannot sign in.
- A Microsoft login is never linked to an existing Umbraco user by email; delete the old local user first.
- With `DenyLocalLogin` on, only Microsoft sign-in is allowed.

## Security Notes

- The API key is compared in constant time.
- Rejected keys and rate-limit refusals are logged at Information level, so an attacker cannot flood Sentry.
- `SanitizeTinyMce` is on; HTML, XML and JS files cannot be uploaded to Media.
- Keep secrets (API key, connection strings, client secret, Sentry DSN, HMAC key) out of git: use Key Vault.

## Deployment

- The Docker image listens on port `8080`.
- App Service / Kubernetes needs only `ASPNETCORE_ENVIRONMENT` and `KeyVault__URL` (plus a managed identity with *Key Vault Secrets User*, or a service principal); everything else comes from Key Vault.
- For more than one instance, set `Umbraco:CMS:Global:MainDomLock` to `SqlMainDomLock`.
