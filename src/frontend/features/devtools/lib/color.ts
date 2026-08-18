/**
 * Small colour toolkit for the dev theme panel.
 *
 * Deliberately hex/HSL only: the panel needs to derive a plausible ramp and report WCAG contrast
 * live, not to be colour-managed. Everything here is pure so the derivation can be unit-tested.
 */

export interface Rgb {
    r: number;
    g: number;
    b: number;
}

export interface Hsl {
    h: number;
    s: number;
    l: number;
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

export function normalizeHex(value: string): string | null {
    const trimmed = value.trim().replace(/^#/, "");
    const expanded =
        trimmed.length === 3
            ? trimmed
                  .split("")
                  .map((character) => character + character)
                  .join("")
            : trimmed;

    return /^[0-9a-fA-F]{6}$/.test(expanded) ? `#${expanded.toUpperCase()}` : null;
}

export function hexToRgb(hex: string): Rgb {
    const normalized = normalizeHex(hex) ?? "#000000";
    return {
        r: parseInt(normalized.slice(1, 3), 16),
        g: parseInt(normalized.slice(3, 5), 16),
        b: parseInt(normalized.slice(5, 7), 16),
    };
}

export function rgbToHex({ r, g, b }: Rgb): string {
    const channel = (value: number) =>
        Math.round(clamp(value, 0, 255)).toString(16).padStart(2, "0");
    return `#${channel(r)}${channel(g)}${channel(b)}`.toUpperCase();
}

export function rgbToHsl({ r, g, b }: Rgb): Hsl {
    const red = r / 255;
    const green = g / 255;
    const blue = b / 255;

    const max = Math.max(red, green, blue);
    const min = Math.min(red, green, blue);
    const delta = max - min;
    const lightness = (max + min) / 2;

    if (delta === 0) {
        return { h: 0, s: 0, l: lightness * 100 };
    }

    const saturation = delta / (1 - Math.abs(2 * lightness - 1));

    let hue: number;
    if (max === red) {
        hue = ((green - blue) / delta) % 6;
    } else if (max === green) {
        hue = (blue - red) / delta + 2;
    } else {
        hue = (red - green) / delta + 4;
    }

    hue *= 60;
    if (hue < 0) hue += 360;

    return { h: hue, s: saturation * 100, l: lightness * 100 };
}

export function hslToRgb({ h, s, l }: Hsl): Rgb {
    const saturation = clamp(s, 0, 100) / 100;
    const lightness = clamp(l, 0, 100) / 100;

    const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation;
    const hueSegment = (((h % 360) + 360) % 360) / 60;
    const second = chroma * (1 - Math.abs((hueSegment % 2) - 1));
    const match = lightness - chroma / 2;

    const [red, green, blue] =
        hueSegment < 1
            ? [chroma, second, 0]
            : hueSegment < 2
              ? [second, chroma, 0]
              : hueSegment < 3
                ? [0, chroma, second]
                : hueSegment < 4
                  ? [0, second, chroma]
                  : hueSegment < 5
                    ? [second, 0, chroma]
                    : [chroma, 0, second];

    return {
        r: (red + match) * 255,
        g: (green + match) * 255,
        b: (blue + match) * 255,
    };
}

export function hexToHsl(hex: string): Hsl {
    return rgbToHsl(hexToRgb(hex));
}

export function hslToHex(hsl: Hsl): string {
    return rgbToHex(hslToRgb(hsl));
}

/** Returns `hex` with its HSL lightness moved by `delta` percentage points. */
export function shiftLightness(hex: string, delta: number): string {
    const hsl = hexToHsl(hex);
    return hslToHex({ ...hsl, l: clamp(hsl.l + delta, 0, 100) });
}

/** Returns `hex` forced to an absolute HSL lightness, keeping hue and saturation. */
export function atLightness(hex: string, lightness: number, saturation?: number): string {
    const hsl = hexToHsl(hex);
    return hslToHex({
        h: hsl.h,
        s: saturation ?? hsl.s,
        l: clamp(lightness, 0, 100),
    });
}

/** Linear blend in sRGB. `amount` is how much of `to` ends up in the result. */
export function mix(from: string, to: string, amount: number): string {
    const a = hexToRgb(from);
    const b = hexToRgb(to);
    const t = clamp(amount, 0, 1);

    return rgbToHex({
        r: a.r + (b.r - a.r) * t,
        g: a.g + (b.g - a.g) * t,
        b: a.b + (b.b - a.b) * t,
    });
}

export function rgba(hex: string, alpha: number): string {
    const { r, g, b } = hexToRgb(hex);
    return `rgba(${Math.round(r)}, ${Math.round(g)}, ${Math.round(b)}, ${alpha})`;
}

function channelLuminance(channel: number): number {
    const value = channel / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
}

export function relativeLuminance(hex: string): number {
    const { r, g, b } = hexToRgb(hex);
    return (
        0.2126 * channelLuminance(r) + 0.7152 * channelLuminance(g) + 0.0722 * channelLuminance(b)
    );
}

/** WCAG 2.1 contrast ratio between two opaque colours, 1–21. */
export function contrastRatio(foreground: string, background: string): number {
    const a = relativeLuminance(foreground);
    const b = relativeLuminance(background);
    const lighter = Math.max(a, b);
    const darker = Math.min(a, b);
    return (lighter + 0.05) / (darker + 0.05);
}
