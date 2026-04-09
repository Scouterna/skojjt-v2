# Sensus e-tjänst API

Reverse-engineered API documentation for [Sensus e-tjänst](https://e-tjanst.sensus.se), a Swedish study circle (studiecirkel) attendance management platform. This API is used by the Skojjt → Sensus attendance sync feature.

> **Note:** This is unofficial documentation based on reverse-engineering the Sensus SPA bundle (`/assets/index-Ba6CCY6j.js`, client version 2026.01.00). The API has no public documentation.

## Base URL

```
https://e-tjanst.sensus.se
```

## Authentication

### Login

```
POST /api/account/login
Content-Type: application/json
```

**Request body:**

```json
{
  "username": "197001011234",
  "password": "secretpassword"
}
```

- `username` — Personnummer, samordningsnummer, or dossiernummer (LMA-nummer). **Digits only** (no hyphens). The SPA enforces `/^[0-9]{1,12}$/`.
- `password` — Plain text password.

**Response (200 OK, always):**

```json
{
  "username": "197001011234",
  "password": "5b102bb",
  "rememberme": false,
  "errormessage": null,
  "resetPasswordType": 0,
  "user": "{\"id\":12345,...}",
  "urlkontakt": ""
}
```

| Field | Type | Description |
|---|---|---|
| `errormessage` | `string?` | `null` on success. Error message string on failure. May contain `[support]` marker that the SPA strips and uses to toggle a support link. |
| `user` | `string?` | JSON-encoded string containing the user object. `null` on failure. Must be parsed with `JSON.parse()`. |
| `password` | `string` | Echoed back as a truncated/hashed value (not the original). |
| `urlkontakt` | `string` | URL to contact page, populated on certain errors. |

**Session:** On successful login, session cookies are returned via `Set-Cookie` headers. These must be sent with all subsequent API requests.

### Password reset

```
POST /api/open/reset
Content-Type: application/json
```

Request body same format as login. Returns a message about a new password being sent.

---

## Arrangemang (Arrangements)

### List arrangemang

```
GET /api/arrangemangs?size=100&page=1&view=4&verksar=0&listtype=0&arrtypfilters=0&narvarofilter=1&sorttype=9&getProgress=true
```

**Query parameters:**

| Parameter | Value | Description |
|---|---|---|
| `size` | `100` | Page size |
| `page` | `1` | Page number (1-based) |
| `view` | `4` | View type: `4` = Attendance registration ("Att registrera närvaro") |
| `verksar` | `0` | Activity year filter: `0` = all years |
| `listtype` | `0` | List type: `0` = user's arrangemang |
| `arrtypfilters` | `0` | Arrangement type filter: `0` = all types |
| `narvarofilter` | `1` | Attendance filter: `1` = "Alla närvarolistor" |
| `sorttype` | `9` | Sort: `9` = default (startdatum ascending) |
| `getProgress` | `true` | Include progress info |

**Response:**

```json
{
  "result": [
    {
      "id": 12345,
      "namn": "Bäverscouter VT2026",
      "name": "Bäverscouter VT2026",
      "antalSchema": 15
    }
  ],
  "totalCount": 3,
  "totalPages": 1
}
```

| Field | Type | Description |
|---|---|---|
| `id` | `int` | Arrangemang ID |
| `namn` | `string?` | Swedish name (primary) |
| `name` | `string?` | Alternative name field |
| `antalSchema` | `int` | Number of schemas (sammankomster/meetings) |

---

## Deltagare (Participants)

### List participants for an arrangemang

```
GET /api/arrangemangs/{arrangemangId}/arrdeltagares/&roll=0&dolda=false
```

> **Quirk:** The path uses `&` instead of `?` for query parameters. This is how the Sensus SPA constructs the URL. The `/arrdeltagares/&roll=0&dolda=false` path is treated as-is by the server.

**Query parameters (path-embedded):**

| Parameter | Value | Description |
|---|---|---|
| `roll` | `0` | Role filter: `0` = all roles |
| `dolda` | `false` | Include hidden: `false` = only visible |

**Response:**

```json
{
  "result": [
    {
      "id": 67890,
      "namn": "Anna Andersson",
      "person": {
        "id": 11111,
        "fornamn": "Anna",
        "efternamn": "Andersson"
      }
    }
  ],
  "totalCount": 25,
  "totalPages": 1
}
```

| Field | Type | Description |
|---|---|---|
| `id` | `int` | Deltagare record ID |
| `namn` | `string?` | Display name (fallback if `person` is null) |
| `person` | `object?` | Person details (may be null) |
| `person.id` | `int` | Person ID (used in narvaros list) |
| `person.fornamn` | `string?` | First name |
| `person.efternamn` | `string?` | Last name |

---

## Schema (Meeting Sessions / Sammankomster)

### List schemas for an arrangemang

```
GET /api/arrangemangs/{arrangemangId}/schema
```

**Response:**

The response format varies — it can be a plain JSON array or wrapped in `{ "result": [...] }` or `{ "items": [...] }`.

```json
[
  {
    "id": 99999,
    "datum": "2026-01-15",
    "signerad": false,
    "redigerbar": true,
    "narvaros": [11111, 22222, 33333],
    "signeratAntalStudieTimmar": 1.0
  }
]
```

| Field | Type | Description |
|---|---|---|
| `id` | `int` | Schema ID |
| `datum` | `string` | Date string (typically ISO format `yyyy-MM-dd`, but may vary) |
| `signerad` | `bool` | Whether the schema is signed/locked |
| `redigerbar` | `bool?` | Whether the schema can be edited. `false` = read-only |
| `narvaros` | `int[]` | Array of person IDs who were present |
| `signeratAntalStudieTimmar` | `decimal` | Study hours (45-minute blocks, not 60-min hours). Returned as JSON decimal (e.g., `1.0`) even though values are logically integers |

### Update a schema (sync attendance)

```
PUT /api/arrangemangs/{arrangemangId}/schemas/{schemaId}
Content-Type: multipart/form-data
```

**Request body:**

`multipart/form-data` with a single field `data` containing a JSON string of the updated schema object.

```
------WebKitFormBoundary
Content-Disposition: form-data; name="data"

{"id":99999,"datum":"2026-01-15","signerad":false,"redigerbar":true,"narvaros":[11111,22222],"signeratAntalStudieTimmar":1}
------WebKitFormBoundary--
```

> **Important:** The request is NOT regular JSON. The Sensus SPA wraps the JSON payload in a `FormData` object with a single `data` field. This is consistent across all mutation endpoints in the SPA (the axios instance uses a request interceptor that wraps objects in FormData for non-login endpoints).

**Response:** 200 OK on success.

**Constraints:**
- Cannot update schemas where `signerad` is `true` (already signed).
- Cannot update schemas where `redigerbar` is `false`.

---

## Response Patterns

### Paged responses

Most list endpoints return a paged response:

```json
{
  "result": [...],
  "totalCount": 100,
  "totalPages": 5
}
```

### Array responses

Some endpoints (notably `/schema`) return a plain array or use `items` as the key:

```json
[...]
```

or

```json
{
  "items": [...]
}
```

Client code should handle all three patterns.

---

## SPA Architecture Notes

- **Framework:** React SPA built with Vite.
- **HTTP client:** Axios instance (minified as `yt` in the bundle). Created via `axios.create(defaults)`.
- **Login endpoint** sends raw JSON (axios default for objects).
- **All other mutation endpoints** wrap payloads in `FormData` with a `data` field containing the JSON-stringified object (via an axios request interceptor).
- **Session management:** Cookie-based. The SPA uses `credentials: 'same-origin'` for all requests.
- **Username validation:** Client-side regex restricts username input to digits only: `/^[0-9]{1,12}$/`.
- **Error handling:** The SPA checks `errormessage` field in login responses. The `[support]` marker is stripped from the displayed message and used to toggle a support contact link.

## URL Routing

The Sensus SPA uses client-side routing. Relevant paths:

| Path | Description |
|---|---|
| `/` | Login page |
| `/logout/expired` | Session expired redirect |
| `/registrera-narvaro-signera/{arrangemangId}` | Attendance registration page for a specific arrangemang |

---

## Known Limitations

- **No public API documentation** — all endpoints are reverse-engineered from the SPA JavaScript bundle.
- **Session cookies** — the API uses cookie-based authentication; there are no API keys or OAuth tokens.
- **Rate limiting** — unknown; no rate limit headers observed.
- **Versioning** — the API has no version prefix. Breaking changes may occur without notice.
- **The `&`-prefixed query string** on the deltagare endpoint is a quirk of the SPA's URL construction, not a standard REST pattern.
