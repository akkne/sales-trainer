# Brand Palette — V3 "Electric Lime"

**Status:** current. Supersedes the violet ramp documented in
[REDESIGN_V2/DESIGN_SPEC.md §1.1](REDESIGN_V2/DESIGN_SPEC.md) (that file stays as a
verbatim record of the V2 design canvas — do not read its hexes as the live palette).

Single source of truth in code: the `:root` / `html[data-theme="dark"]` blocks at the
top of `src/frontend/app/globals.css`. Everything below documents *why* the tokens are
shaped the way they are.

---

## 1. The brand color

**`#96F500`** — electric lime. Hue 83°, S 100%, L 48%.

The one thing to understand about it: its relative luminance is **~0.71**, roughly that
of a light pastel. Consequences, both non-negotiable:

| | Contrast | Verdict |
|---|---|---|
| white text on `#96F500` | **1.38 : 1** | unusable |
| near-black text on `#96F500` | **11.3 : 1** | correct |
| `#96F500` text on white | **1.38 : 1** | unusable |

So the brand color **can only be a fill**, and anything sitting on that fill must be
dark. A darker sibling carries the brand into text.

## 2. Token contract

Three brand tokens, three jobs. Using the wrong one is the single most likely way to
break this palette.

| Token | Light | Dark | Use for |
|---|---|---|---|
| `--primary` | `#96F500` | `#96F500` | **fills only** — button backgrounds, progress fills, switches, active dots, avatar gradients, focus outlines, borders |
| `--primary-ink` | `#22261C` | `#E6EBDF` | **text and icons** on a light/`--primary-soft` surface (14.6 : 1 on white) |
| `--on-primary` | `#0F0F0F` | `#0F0F0F` | **anything drawn on top of a `--primary` fill** — button labels, bubble text, avatar initials |

Rule of thumb: `color:` almost never takes `var(--primary)`. If you are writing
`color:`, you want `--primary-ink` (on a light surface) or `--on-primary` (on a lime
fill).

`--primary-ink` defaults to graphite rather than a dark lime: the lime family only reads
as olive once it is dark enough to be legible as type, so the brand stays a fill and text
stays neutral. Four other inks ship as presets in the dev theme panel below — swap the
default there and paste the result back if you prefer green type.

Supporting brand tokens:

| Token | Light | Dark | Use |
|---|---|---|---|
| `--primary-strong` | `#7CD100` | `#AAFF33` | hover state of a `--primary` fill; deep-lime strokes on thin SVG rings |
| `--primary-soft` | `#E7FCC6` | `rgba(150,245,0,.16)` | tinted pill / badge / chip fills; pairs with `--primary-ink` |
| `--primary-softer` | `#F4FEE7` | `rgba(150,245,0,.09)` | faintest wash — selected cards, quote boxes |
| `--primary-tint-surface` | `#F4FEE7` | `rgba(150,245,0,.10)` | soft-button background |
| `--primary-tint-border` / `-2` / `-3` | `#C6F58C` / `#E1FBBF` / `#A9EF56` | lime alphas at .32 / .20 / .45 | hover ring, soft-button border, selected-card border |
| `--primary-tint-deep` | `#2E5200` | `#D2FF8F` | text-link **hover** (deepens on light, brightens on dark), active skill-row label |
| `--primary-ring` | `rgba(124,209,0,.28)` | `rgba(150,245,0,.30)` | focus ring |
| `--grad-primary` | `linear-gradient(135deg,#7CD100,#B4FF4D)` | same | avatars, wordmark mark, hero badges |
| `--grad-bar` | `linear-gradient(90deg,#7CD100,#B4FF4D)` | same | in-progress progress bars |

The gradients run deep-lime → bright-lime, so `#96F500` sits at the middle of the ramp
and the brand reads as the gradient's center of gravity.

## 3. Supporting colors

Chosen to sit around the lime without competing with it.

| Role | Light | Dark | Note |
|---|---|---|---|
| Success / done | `#0E9F6E` (bright `#16C48A`, soft `#E2F8F0`) | `#2ED8A7` / `#4BE8BC` / `rgba(46,216,167,.16)` | **Moved from green to emerald-teal.** The old `#1F9E5A` was too close to lime — "done" and "brand" have to be readable apart at badge size. Hue 164° vs the brand's 83° puts real distance between them. |
| Danger / hard | `#D9503E`, soft `#FDECEA` | `#E86555` | Unchanged. Red is near-complementary to lime and reads loudly against it. |
| Warning / medium | `#B5840F`, soft `#FFF8E6` | `#C79212` | Unchanged. Amber is far enough round the wheel. |
| Info | `#2F6FE0`, soft `#EAF2FF` | `#4C8DF6` | Unchanged. |
| Flame / streak | `#D9722E`, soft `#FFF1E8` | `#E8884A` | Unchanged. |
| **Violet (secondary accent)** | `#6C5BD9`, soft `#EFEAFE`, light `#9B8CF0` | `#9B8CF0` / `rgba(155,140,240,.18)` / `#C5B8F8` | **No longer the brand — now a categorical accent.** Kept deliberately: violet against lime is a strong, intentional pairing, and several categorical surfaces already depended on it (`.co-status--meeting`, `.pv2-stat-ic.violet`, `.pv2-quota-fill.violet`, the `match` exercise chip, the 7-pair avatar gradient palette). |

Neutrals (`--bg`, `--surface*`, `--line*`, `--ink*`) are unchanged from V2 — they were
already near-neutral greys and carry a lime brand as well as they carried a violet one.

Brand-tinted shadows follow the primary: `--sh-2` and `--sh-primary` are lime glows
(`rgba(124,209,0,…)` light, `rgba(150,245,0,…)` dark) at higher alpha than the violet
originals, because a light hue needs more opacity to register as a shadow.

## 4. Legacy aliases

Older components still reference pre-V2 names. They resolve as:

| Alias | Resolves to |
|---|---|
| `--indigo` / `--accent` | `var(--primary)` — a **fill** |
| `--indigo-soft` / `--accent-soft` | `var(--primary-soft)` |
| `--indigo-ink` / `--accent-ink` | `var(--primary-ink)` — the **text** variant |

Tailwind exposes these as `bg-indigo`, `bg-indigo-soft`, `text-indigo-ink`,
`bg-accent-soft`, `text-accent-ink`, plus `text-primary-ink` and `text-on-primary`.
`text-indigo` / `text-accent` are gone from the admin UI — they resolved to the fill
lime and were unreadable; `bg-indigo text-white` likewise became
`bg-indigo text-on-primary`.

## 5. Checklist when adding UI

1. Filling a shape with the brand? `background: var(--primary)`, and give its contents
   `color: var(--on-primary)`.
2. Writing brand-colored text on white or on `--primary-soft`? `var(--primary-ink)`.
3. Adding a text-link hover? `--primary-ink` → `--primary-tint-deep`, not
   `--primary-ink` → `--primary` (that reverses into invisibility on light).
4. Need a "done"/success color? `--success`, never the lime.
5. Need another categorical hue? `--violet`, `--info`, `--flame`, `--amber` — in that
   order of preference.

## 6. Editing the palette live (dev only)

`features/devtools/components/dev-theme-panel.tsx` mounts a floating swatch button in the
bottom-right corner under `next dev`. It is **not** a Next.js DevTools panel — Next 16.2's
overlay has no public API for registering custom panels — it is an in-app panel that is
tree-shaken out of production builds by the `NODE_ENV` guard in `app/providers.tsx`.

What it edits is exactly the three-token contract from §2, and nothing else:

- **Основной цвет** — a colour picker + hex field for `--primary`.
- **Текст брендом** — five presets for `--primary-ink`, each carrying both a light and a dark
  value so the choice survives a theme switch.
- **Поверх заливки** — four presets for `--on-primary`, all dark, because §1 leaves no choice.

Everything else in the ramp — `--primary-strong`, the tints, the borders, `--primary-tint-deep`,
the gradients, `--primary-ring` and the two brand shadows — is **derived** from those, so the
ramp can never drift out of sync with the colour you picked. Light tints are mixed toward white
so they stay opaque over cards; dark tints are alpha washes so they pick up the surface behind
them.

Alongside it the panel shows live WCAG ratios for the three pairings that decide whether a choice
is usable at all (ink on white, ink on the soft tint, on-primary on the fill), so a bad pick is
visible before it ships.

**How it applies:** the panel injects a `<style>` element with `:root:root` and
`html[data-theme="dark"]:root` blocks — doubled selectors purely to out-specify `globals.css`
without depending on stylesheet order. Until you touch a control it injects nothing at all, so
dev renders byte-identical to production. Choices persist in `localStorage`
(`sellevate:dev-theme`) and stay in that browser only.

**Скопировать CSS** puts the same ramp on the clipboard with plain `:root` selectors — that is
what you paste into `app/globals.css` to make a choice real. **Сброс** clears the override.

The derivation and the presets are pure functions in `features/devtools/lib/`, covered by
`__tests__/DevThemeTokens.test.ts` — including a check that every shipped preset clears 4.5:1 in
both themes.
