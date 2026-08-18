/**
 * Derives the whole brand ramp from three choices, and renders it as a CSS override block.
 *
 * The panel only ever lets you set the brand fill, the ink (brand text on a light surface) and
 * on-primary (whatever sits on top of a brand fill) — the same three-token contract documented in
 * docs/BRAND_PALETTE.md. Everything else in the ramp is a function of those, so the tints, borders,
 * gradients and glows can never drift out of sync with the colour you picked.
 */

import { atLightness, contrastRatio, hexToHsl, mix, normalizeHex, rgba, shiftLightness } from "./color";

export interface InkPreset {
    id: string;
    label: string;
    /** Brand text on a light surface. */
    light: string;
    /** Brand text on a dark surface — must stay legible when the page inverts. */
    dark: string;
}

/**
 * `--primary-ink` candidates. The first is the default: it drops the green from text entirely, so
 * the lime only ever appears as a fill and never as olive-looking type.
 */
export const INK_PRESETS: InkPreset[] = [
    { id: "graphite", label: "Графит", light: "#22261C", dark: "#E6EBDF" },
    { id: "deep-lime", label: "Тёмный лайм", light: "#3B6600", dark: "#B7F76B" },
    { id: "olive", label: "Оливковый", light: "#4A7C00", dark: "#B0F95C" },
    { id: "emerald", label: "Изумруд", light: "#14603F", dark: "#5FE0AC" },
    { id: "black", label: "Чёрный", light: "#101010", dark: "#F2F2F2" },
];

export interface OnPrimaryPreset {
    id: string;
    label: string;
    value: string;
}

/** `--on-primary` candidates. Always dark — the brand lime is far too light to carry white type. */
export const ON_PRIMARY_PRESETS: OnPrimaryPreset[] = [
    { id: "near-black", label: "Почти чёрный", value: "#0F0F0F" },
    { id: "black", label: "Чёрный", value: "#000000" },
    { id: "graphite", label: "Графит", value: "#1A1D16" },
    { id: "dark-green", label: "Тёмно-зелёный", value: "#142A00" },
];

export interface ThemeDraft {
    primary: string;
    inkPresetId: string;
    onPrimary: string;
}

export const DEFAULT_THEME_DRAFT: ThemeDraft = {
    primary: "#96F500",
    inkPresetId: "graphite",
    onPrimary: "#0F0F0F",
};

export function resolveInkPreset(inkPresetId: string): InkPreset {
    return INK_PRESETS.find((preset) => preset.id === inkPresetId) ?? INK_PRESETS[0];
}

export interface DerivedRamp {
    light: Record<string, string>;
    dark: Record<string, string>;
}

/**
 * Expands a draft into every brand token, for both themes.
 *
 * Light tints are made by mixing toward white so they stay opaque over the card surfaces; dark
 * tints are alpha washes of the brand so they pick up whatever surface sits behind them.
 */
export function deriveRamp(draft: ThemeDraft): DerivedRamp {
    const primary = normalizeHex(draft.primary) ?? DEFAULT_THEME_DRAFT.primary;
    const onPrimary = normalizeHex(draft.onPrimary) ?? DEFAULT_THEME_DRAFT.onPrimary;
    const ink = resolveInkPreset(draft.inkPresetId);

    const { s, l } = hexToHsl(primary);
    const strongLight = shiftLightness(primary, -9);
    const strongDark = shiftLightness(primary, 8);
    const gradientTo = atLightness(primary, Math.min(l + 15, 96), s);

    return {
        light: {
            "--primary": primary,
            "--primary-strong": strongLight,
            "--primary-ink": ink.light,
            "--primary-soft": mix(primary, "#FFFFFF", 0.82),
            "--primary-softer": mix(primary, "#FFFFFF", 0.93),
            "--primary-ring": rgba(strongLight, 0.28),
            "--primary-tint-border": mix(primary, "#FFFFFF", 0.62),
            "--primary-tint-border-2": mix(primary, "#FFFFFF", 0.85),
            "--primary-tint-border-3": mix(primary, "#FFFFFF", 0.45),
            "--primary-tint-surface": mix(primary, "#FFFFFF", 0.93),
            "--primary-tint-deep": atLightness(primary, 16, Math.max(s, 60)),
            "--grad-primary": `linear-gradient(135deg, ${strongLight}, ${gradientTo})`,
            "--grad-bar": `linear-gradient(90deg, ${strongLight}, ${gradientTo})`,
            "--on-primary": onPrimary,
            "--sh-2": `0 6px 20px ${rgba(strongLight, 0.22)}`,
            "--sh-primary": `0 6px 18px ${rgba(strongLight, 0.38)}`,
        },
        dark: {
            "--primary": primary,
            "--primary-strong": strongDark,
            "--primary-ink": ink.dark,
            "--primary-soft": rgba(primary, 0.16),
            "--primary-softer": rgba(primary, 0.09),
            "--primary-ring": rgba(primary, 0.3),
            "--primary-tint-border": rgba(primary, 0.32),
            "--primary-tint-border-2": rgba(primary, 0.2),
            "--primary-tint-border-3": rgba(primary, 0.45),
            "--primary-tint-surface": rgba(primary, 0.1),
            "--primary-tint-deep": atLightness(primary, 80, Math.min(s, 95)),
            "--grad-primary": `linear-gradient(135deg, ${strongLight}, ${gradientTo})`,
            "--grad-bar": `linear-gradient(90deg, ${strongLight}, ${gradientTo})`,
            "--on-primary": onPrimary,
            "--sh-2": `0 6px 20px ${rgba(primary, 0.16)}`,
            "--sh-primary": `0 6px 20px ${rgba(primary, 0.32)}`,
        },
    };
}

function renderBlock(selector: string, tokens: Record<string, string>): string {
    const declarations = Object.entries(tokens)
        .map(([name, value]) => `  ${name}: ${value};`)
        .join("\n");
    return `${selector} {\n${declarations}\n}`;
}

/**
 * Renders the override stylesheet.
 *
 * Selectors are doubled (`:root:root`) purely to out-specify globals.css without depending on which
 * order Next happens to inject the two stylesheets in.
 */
export function renderThemeCss(draft: ThemeDraft): string {
    const ramp = deriveRamp(draft);
    return [
        renderBlock(":root:root", ramp.light),
        renderBlock('html[data-theme="dark"]:root', ramp.dark),
    ].join("\n\n");
}

/** The same block, but with plain selectors — what you paste back into globals.css. */
export function renderThemeCssForSource(draft: ThemeDraft): string {
    const ramp = deriveRamp(draft);
    return [
        "/* Brand ramp — generated by the dev theme panel */",
        renderBlock(":root", ramp.light),
        renderBlock('html[data-theme="dark"]', ramp.dark),
    ].join("\n\n");
}

export interface ContrastReadout {
    label: string;
    ratio: number;
    /** Smallest ratio that still counts as legible for this pairing. */
    threshold: number;
}

/**
 * The two ratios that decide whether a brand choice is usable at all: brand text on white, and
 * whatever sits on top of a brand fill. Surfaced live so a bad pick is visible before it ships.
 */
export function readContrast(draft: ThemeDraft): ContrastReadout[] {
    const primary = normalizeHex(draft.primary) ?? DEFAULT_THEME_DRAFT.primary;
    const onPrimary = normalizeHex(draft.onPrimary) ?? DEFAULT_THEME_DRAFT.onPrimary;
    const ink = resolveInkPreset(draft.inkPresetId);

    return [
        { label: "Текст на белом", ratio: contrastRatio(ink.light, "#FFFFFF"), threshold: 4.5 },
        { label: "Текст на плашке", ratio: contrastRatio(ink.light, mix(primary, "#FFFFFF", 0.82)), threshold: 4.5 },
        { label: "Поверх заливки", ratio: contrastRatio(onPrimary, primary), threshold: 4.5 },
    ];
}
