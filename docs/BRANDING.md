# Branding — logo & app icons

Single source of truth for the Sellevate mark: **`src/frontend/public/logo.svg`**
(1080×1080, lime background with the dark «S» glyph).

## Where it is used

| File | Purpose |
|------|---------|
| `src/frontend/public/logo.svg` | The mark served at `/logo.svg`; rendered by the `Wordmark` component |
| `src/frontend/app/icon.svg` | Next.js `icon` file convention → browser tab favicon (`<link rel="icon">`, `sizes="any"`) |
| `src/frontend/app/apple-icon.png` | Next.js `apple-icon` file convention → 180×180 iOS home-screen icon (`<link rel="apple-touch-icon">`) |

`app/favicon.ico` and the Next.js starter assets (`next.svg`, `vercel.svg`,
`globe.svg`, `window.svg`, `file.svg`) were removed — the SVG icon plus the Apple
PNG cover every browser we support, and `favicon.ico` would otherwise win over
`icon.svg` for the `/favicon.ico` request.

## Wordmark component

`src/frontend/shared/components/wordmark.tsx` renders `/logo.svg` as the mark
(rounded at 32 % of its size) next to the «Sellevate.» text. Props:

- `size` — text size in px; the mark is `size * 1.2`
- `variant` — `"full"` (mark + text, default) or `"mark"` (mark only; the image
  carries the `Sellevate` alt text in that case)

Call sites: landing page, auth screens (login, register, verify-email),
onboarding top bar, mobile top bar.

## Replacing the logo

1. Overwrite `src/frontend/public/logo.svg`.
2. Copy it to `src/frontend/app/icon.svg`.
3. Regenerate the Apple icon from the same artwork at 180×180 into
   `src/frontend/app/apple-icon.png`.

## Manual check

- Browser tab of http://localhost:3000 shows the new mark (hard-reload — favicons
  are cached aggressively).
- `/login`, `/register`, `/onboarding` and the mobile top bar show the mark next
  to «Sellevate.».
- `curl -s http://localhost:3000 | grep -o '<link rel="[^"]*icon[^"]*"[^>]*>'`
  lists the generated `icon` and `apple-touch-icon` links.
