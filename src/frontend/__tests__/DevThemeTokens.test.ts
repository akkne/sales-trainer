import { describe, it, expect } from "vitest";
import {
    atLightness,
    contrastRatio,
    hexToHsl,
    hslToHex,
    mix,
    normalizeHex,
    rgba,
    shiftLightness,
} from "@/features/devtools/lib/color";
import {
    DEFAULT_THEME_DRAFT,
    INK_PRESETS,
    ON_PRIMARY_PRESETS,
    deriveRamp,
    readContrast,
    renderThemeCss,
    renderThemeCssForSource,
    resolveInkPreset,
} from "@/features/devtools/lib/theme-tokens";

const BRAND = "#96F500";

describe("color utilities", () => {
    it("normalizes hex input in the forms a user actually types", () => {
        expect(normalizeHex("96f500")).toBe("#96F500");
        expect(normalizeHex("#96f500")).toBe("#96F500");
        expect(normalizeHex(" #ABC ")).toBe("#AABBCC");
        expect(normalizeHex("nope")).toBeNull();
        expect(normalizeHex("#12345")).toBeNull();
    });

    it("round-trips through HSL", () => {
        for (const hex of [BRAND, "#22261C", "#FFFFFF", "#000000", "#2F6FE0"]) {
            expect(hslToHex(hexToHsl(hex))).toBe(hex);
        }
    });

    it("mixes toward a target colour", () => {
        expect(mix("#000000", "#FFFFFF", 0)).toBe("#000000");
        expect(mix("#000000", "#FFFFFF", 1)).toBe("#FFFFFF");
        expect(mix("#000000", "#FFFFFF", 0.5)).toBe("#808080");
    });

    it("moves and pins lightness while keeping hue", () => {
        const darker = shiftLightness(BRAND, -9);
        expect(hexToHsl(darker).l).toBeCloseTo(hexToHsl(BRAND).l - 9, 0);
        expect(hexToHsl(darker).h).toBeCloseTo(hexToHsl(BRAND).h, 0);

        expect(hexToHsl(atLightness(BRAND, 16)).l).toBeCloseTo(16, 0);
    });

    it("emits rgba from hex", () => {
        expect(rgba(BRAND, 0.3)).toBe("rgba(150, 245, 0, 0.3)");
    });

    it("computes WCAG contrast, symmetrically", () => {
        expect(contrastRatio("#000000", "#FFFFFF")).toBeCloseTo(21, 1);
        expect(contrastRatio("#FFFFFF", "#FFFFFF")).toBeCloseTo(1, 5);
        expect(contrastRatio("#22261C", "#FFFFFF")).toBeCloseTo(
            contrastRatio("#FFFFFF", "#22261C"),
            5
        );
    });

    it("confirms the constraint the whole palette is built around", () => {
        // The brand lime is far too light to carry white text — this is why --on-primary is dark
        // and --primary-ink exists at all. If this ever passes, the palette contract changed.
        expect(contrastRatio("#FFFFFF", BRAND)).toBeLessThan(2);
        expect(contrastRatio("#0F0F0F", BRAND)).toBeGreaterThan(4.5);
    });
});

describe("theme presets", () => {
    it("ships every ink preset legible on white", () => {
        for (const preset of INK_PRESETS) {
            expect(contrastRatio(preset.light, "#FFFFFF")).toBeGreaterThanOrEqual(4.5);
        }
    });

    it("ships every ink preset legible on a dark surface", () => {
        for (const preset of INK_PRESETS) {
            expect(contrastRatio(preset.dark, "#1C1C20")).toBeGreaterThanOrEqual(4.5);
        }
    });

    it("ships every on-primary preset legible on the default brand fill", () => {
        for (const preset of ON_PRIMARY_PRESETS) {
            expect(contrastRatio(preset.value, DEFAULT_THEME_DRAFT.primary)).toBeGreaterThanOrEqual(4.5);
        }
    });

    it("falls back to the first ink preset for an unknown id", () => {
        expect(resolveInkPreset("does-not-exist")).toBe(INK_PRESETS[0]);
        expect(resolveInkPreset("emerald").id).toBe("emerald");
    });
});

describe("deriveRamp", () => {
    it("keeps the chosen brand colour as the fill in both themes", () => {
        const ramp = deriveRamp(DEFAULT_THEME_DRAFT);
        expect(ramp.light["--primary"]).toBe(BRAND);
        expect(ramp.dark["--primary"]).toBe(BRAND);
    });

    it("derives light tints as opaque hex and dark tints as alpha washes", () => {
        const ramp = deriveRamp(DEFAULT_THEME_DRAFT);
        expect(ramp.light["--primary-soft"]).toMatch(/^#[0-9A-F]{6}$/);
        expect(ramp.dark["--primary-soft"]).toMatch(/^rgba\(/);
    });

    it("orders the light tints from strongest to faintest", () => {
        const ramp = deriveRamp(DEFAULT_THEME_DRAFT);
        const lightnessOf = (token: string) => hexToHsl(ramp.light[token]).l;

        expect(lightnessOf("--primary-tint-border-3")).toBeLessThan(
            lightnessOf("--primary-tint-border")
        );
        expect(lightnessOf("--primary-tint-border")).toBeLessThan(
            lightnessOf("--primary-tint-border-2")
        );
        expect(lightnessOf("--primary-soft")).toBeLessThan(lightnessOf("--primary-softer"));
    });

    it("darkens the hover fill on light and brightens it on dark", () => {
        const ramp = deriveRamp(DEFAULT_THEME_DRAFT);
        const brandLightness = hexToHsl(BRAND).l;

        expect(hexToHsl(ramp.light["--primary-strong"]).l).toBeLessThan(brandLightness);
        expect(hexToHsl(ramp.dark["--primary-strong"]).l).toBeGreaterThan(brandLightness);
    });

    it("keeps --primary-tint-deep dark on light and light on dark, so hovers move the right way", () => {
        const ramp = deriveRamp(DEFAULT_THEME_DRAFT);
        expect(hexToHsl(ramp.light["--primary-tint-deep"]).l).toBeLessThan(30);
        expect(hexToHsl(ramp.dark["--primary-tint-deep"]).l).toBeGreaterThan(70);
    });

    it("tracks a different brand colour through the whole ramp", () => {
        const ramp = deriveRamp({ ...DEFAULT_THEME_DRAFT, primary: "#2F6FE0" });
        const brandHue = hexToHsl("#2F6FE0").h;

        expect(ramp.light["--primary"]).toBe("#2F6FE0");
        expect(hexToHsl(ramp.light["--primary-soft"]).h).toBeCloseTo(brandHue, 0);
        expect(ramp.light["--grad-bar"]).toContain("linear-gradient(90deg");
        expect(ramp.light["--sh-primary"]).toContain("rgba(");
    });

    it("falls back to the default brand colour for unusable input", () => {
        const ramp = deriveRamp({ ...DEFAULT_THEME_DRAFT, primary: "not-a-colour" });
        expect(ramp.light["--primary"]).toBe(DEFAULT_THEME_DRAFT.primary);
    });

    it("uses the ink preset's light value on light and its dark value on dark", () => {
        const ramp = deriveRamp({ ...DEFAULT_THEME_DRAFT, inkPresetId: "emerald" });
        const emerald = resolveInkPreset("emerald");

        expect(ramp.light["--primary-ink"]).toBe(emerald.light);
        expect(ramp.dark["--primary-ink"]).toBe(emerald.dark);
    });
});

describe("renderThemeCss", () => {
    it("out-specifies globals.css regardless of stylesheet order", () => {
        const css = renderThemeCss(DEFAULT_THEME_DRAFT);
        expect(css).toContain(":root:root {");
        expect(css).toContain('html[data-theme="dark"]:root {');
    });

    it("emits plain selectors for the copy-into-source variant", () => {
        const css = renderThemeCssForSource(DEFAULT_THEME_DRAFT);
        expect(css).toContain(":root {");
        expect(css).toContain('html[data-theme="dark"] {');
        expect(css).not.toContain(":root:root");
    });

    it("declares every brand token it derives", () => {
        const css = renderThemeCss(DEFAULT_THEME_DRAFT);
        for (const token of Object.keys(deriveRamp(DEFAULT_THEME_DRAFT).light)) {
            expect(css).toContain(`${token}:`);
        }
    });
});

describe("readContrast", () => {
    it("passes on the shipped defaults", () => {
        for (const entry of readContrast(DEFAULT_THEME_DRAFT)) {
            expect(entry.ratio).toBeGreaterThanOrEqual(entry.threshold);
        }
    });

    it("fails loudly on a combination that would be unreadable", () => {
        const readouts = readContrast({
            primary: "#FFFFFF",
            inkPresetId: "graphite",
            onPrimary: "#F5F5F5",
        });
        const onFill = readouts.find((entry) => entry.label === "Поверх заливки");

        expect(onFill!.ratio).toBeLessThan(onFill!.threshold);
    });
});
