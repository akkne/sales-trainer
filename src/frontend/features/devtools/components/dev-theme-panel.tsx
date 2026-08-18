"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
    DEFAULT_THEME_DRAFT,
    INK_PRESETS,
    ON_PRIMARY_PRESETS,
    type ThemeDraft,
    deriveRamp,
    readContrast,
    renderThemeCss,
    renderThemeCssForSource,
    resolveInkPreset,
} from "../lib/theme-tokens";
import { normalizeHex } from "../lib/color";

const STORAGE_KEY = "sellevate:dev-theme";
const STYLE_ELEMENT_ID = "sellevate-dev-theme";

/**
 * The panel paints itself from hardcoded neutrals rather than design tokens on purpose: it edits
 * those tokens, so borrowing them would make the panel restyle itself mid-edit and get harder to
 * read exactly when you are experimenting with an unreadable colour.
 */
const CHROME = {
    surface: "#16181A",
    surfaceRaised: "#1F2225",
    border: "#2E3236",
    text: "#ECEEF0",
    textMuted: "#9AA0A6",
    accent: "#5B9CFF",
};

function readStoredDraft(): ThemeDraft | null {
    try {
        const raw = window.localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;

        const parsed = JSON.parse(raw) as Partial<ThemeDraft>;
        if (!parsed.primary || !parsed.inkPresetId || !parsed.onPrimary) return null;

        return {
            primary: normalizeHex(parsed.primary) ?? DEFAULT_THEME_DRAFT.primary,
            inkPresetId: parsed.inkPresetId,
            onPrimary: normalizeHex(parsed.onPrimary) ?? DEFAULT_THEME_DRAFT.onPrimary,
        };
    } catch {
        return null;
    }
}

function applyThemeCss(draft: ThemeDraft | null) {
    const existing = document.getElementById(STYLE_ELEMENT_ID);

    if (draft === null) {
        existing?.remove();
        return;
    }

    const style = existing ?? document.createElement("style");
    style.id = STYLE_ELEMENT_ID;
    style.textContent = renderThemeCss(draft);
    if (!existing) document.head.appendChild(style);
}

export function DevThemePanel() {
    const [isOpen, setIsOpen] = useState(false);
    // Until the user touches something this stays null and we inject nothing at all, so dev renders
    // byte-identical to production instead of quietly running on a re-derived ramp.
    // Safe to read storage in the initializer: the panel is mounted with ssr: false.
    const [draft, setDraft] = useState<ThemeDraft | null>(() => readStoredDraft());
    const [copyState, setCopyState] = useState<"idle" | "copied" | "failed">("idle");

    // The stylesheet is the external system this component syncs to — mirroring `draft` into it is
    // exactly what an effect is for, and it covers the restored-from-storage case on mount too.
    useEffect(() => {
        applyThemeCss(draft);
    }, [draft]);

    const activeDraft = draft ?? DEFAULT_THEME_DRAFT;

    const update = useCallback((patch: Partial<ThemeDraft>) => {
        setCopyState("idle");
        setDraft((current) => {
            const next = { ...(current ?? DEFAULT_THEME_DRAFT), ...patch };
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
            return next;
        });
    }, []);

    const reset = useCallback(() => {
        setCopyState("idle");
        setDraft(null);
        window.localStorage.removeItem(STORAGE_KEY);
    }, []);

    const copyCss = useCallback(async () => {
        try {
            await navigator.clipboard.writeText(renderThemeCssForSource(activeDraft));
            setCopyState("copied");
        } catch {
            setCopyState("failed");
        }
    }, [activeDraft]);

    const ramp = useMemo(() => deriveRamp(activeDraft), [activeDraft]);
    const contrast = useMemo(() => readContrast(activeDraft), [activeDraft]);
    const ink = resolveInkPreset(activeDraft.inkPresetId);

    if (!isOpen) {
        return (
            <button
                type="button"
                onClick={() => setIsOpen(true)}
                title="Палитра (dev)"
                style={{
                    position: "fixed",
                    right: 16,
                    bottom: 16,
                    zIndex: 2147483000,
                    width: 40,
                    height: 40,
                    borderRadius: 12,
                    border: `1px solid ${CHROME.border}`,
                    background: CHROME.surface,
                    color: CHROME.text,
                    display: "grid",
                    placeItems: "center",
                    cursor: "pointer",
                    boxShadow: "0 6px 20px rgba(0,0,0,.35)",
                }}
            >
                <span
                    style={{
                        width: 16,
                        height: 16,
                        borderRadius: 5,
                        background: activeDraft.primary,
                        border: draft ? `2px solid ${CHROME.accent}` : "none",
                    }}
                />
            </button>
        );
    }

    return (
        <div
            style={{
                position: "fixed",
                right: 16,
                bottom: 16,
                zIndex: 2147483000,
                width: 320,
                maxHeight: "min(78vh, 720px)",
                overflowY: "auto",
                background: CHROME.surface,
                border: `1px solid ${CHROME.border}`,
                borderRadius: 14,
                boxShadow: "0 18px 50px rgba(0,0,0,.5)",
                color: CHROME.text,
                font: "500 12px/1.45 ui-sans-serif, system-ui, sans-serif",
            }}
        >
            <header
                style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    padding: "12px 14px",
                    borderBottom: `1px solid ${CHROME.border}`,
                    position: "sticky",
                    top: 0,
                    background: CHROME.surface,
                }}
            >
                <strong style={{ fontSize: 12.5, letterSpacing: "-0.01em" }}>Палитра</strong>
                <span style={{ color: CHROME.textMuted, fontSize: 11 }}>
                    {draft ? "изменено" : "по умолчанию"}
                </span>
                <button
                    type="button"
                    onClick={() => setIsOpen(false)}
                    aria-label="Закрыть"
                    style={{
                        marginLeft: "auto",
                        background: "none",
                        border: "none",
                        color: CHROME.textMuted,
                        cursor: "pointer",
                        fontSize: 15,
                        lineHeight: 1,
                    }}
                >
                    ✕
                </button>
            </header>

            <div style={{ padding: 14, display: "flex", flexDirection: "column", gap: 18 }}>
                <Section
                    title="Основной цвет"
                    hint="Только заливка — кнопки, прогресс, градиенты."
                >
                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                        <input
                            type="color"
                            value={activeDraft.primary}
                            onChange={(event) => update({ primary: event.target.value.toUpperCase() })}
                            style={{
                                width: 42,
                                height: 32,
                                padding: 0,
                                border: `1px solid ${CHROME.border}`,
                                borderRadius: 8,
                                background: "none",
                                cursor: "pointer",
                            }}
                        />
                        <input
                            type="text"
                            value={activeDraft.primary}
                            onChange={(event) => {
                                const normalized = normalizeHex(event.target.value);
                                if (normalized) update({ primary: normalized });
                            }}
                            spellCheck={false}
                            style={{
                                flex: 1,
                                minWidth: 0,
                                height: 32,
                                padding: "0 10px",
                                borderRadius: 8,
                                border: `1px solid ${CHROME.border}`,
                                background: CHROME.surfaceRaised,
                                color: CHROME.text,
                                font: "500 12px ui-monospace, SFMono-Regular, Menlo, monospace",
                            }}
                        />
                    </div>
                </Section>

                <Section
                    title="Текст брендом"
                    hint="--primary-ink: цвет ссылок и иконок на светлом фоне."
                >
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                        {INK_PRESETS.map((preset) => (
                            <Swatch
                                key={preset.id}
                                color={preset.light}
                                label={preset.label}
                                selected={preset.id === activeDraft.inkPresetId}
                                onSelect={() => update({ inkPresetId: preset.id })}
                            />
                        ))}
                    </div>
                    <PreviewLine
                        background="#FFFFFF"
                        color={ink.light}
                        text="Смотреть все →"
                    />
                    <PreviewLine
                        background={ramp.light["--primary-soft"]}
                        color={ink.light}
                        text="Активный пункт"
                    />
                </Section>

                <Section
                    title="Поверх заливки"
                    hint="--on-primary: текст кнопок и инициалы на брендовом фоне."
                >
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                        {ON_PRIMARY_PRESETS.map((preset) => (
                            <Swatch
                                key={preset.id}
                                color={preset.value}
                                label={preset.label}
                                selected={preset.value === activeDraft.onPrimary}
                                onSelect={() => update({ onPrimary: preset.value })}
                            />
                        ))}
                    </div>
                    <PreviewLine
                        background={activeDraft.primary}
                        color={activeDraft.onPrimary}
                        text="Начать разговор"
                    />
                </Section>

                <Section title="Контраст" hint="WCAG AA для обычного текста — от 4.5:1.">
                    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        {contrast.map((entry) => {
                            const passes = entry.ratio >= entry.threshold;
                            return (
                                <div
                                    key={entry.label}
                                    style={{ display: "flex", alignItems: "center", gap: 8 }}
                                >
                                    <span style={{ color: CHROME.textMuted, flex: 1 }}>
                                        {entry.label}
                                    </span>
                                    <span
                                        style={{
                                            font: "500 11.5px ui-monospace, SFMono-Regular, Menlo, monospace",
                                            color: passes ? "#5FD08A" : "#FF7A6B",
                                        }}
                                    >
                                        {entry.ratio.toFixed(2)}:1 {passes ? "✓" : "✕"}
                                    </span>
                                </div>
                            );
                        })}
                    </div>
                </Section>

                <Section title="Производные" hint="Считаются от основного цвета автоматически.">
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 5 }}>
                        {[
                            "--primary-strong",
                            "--primary-soft",
                            "--primary-softer",
                            "--primary-tint-border",
                            "--primary-tint-border-3",
                            "--primary-tint-deep",
                        ].map((token) => (
                            <span
                                key={token}
                                title={`${token}: ${ramp.light[token]}`}
                                style={{
                                    width: 30,
                                    height: 22,
                                    borderRadius: 6,
                                    background: ramp.light[token],
                                    border: `1px solid ${CHROME.border}`,
                                }}
                            />
                        ))}
                        <span
                            title="--grad-bar"
                            style={{
                                width: 66,
                                height: 22,
                                borderRadius: 6,
                                background: ramp.light["--grad-bar"],
                                border: `1px solid ${CHROME.border}`,
                            }}
                        />
                    </div>
                </Section>

                <div style={{ display: "flex", gap: 8 }}>
                    <button
                        type="button"
                        onClick={copyCss}
                        style={{
                            flex: 1,
                            height: 32,
                            borderRadius: 8,
                            border: "none",
                            background: CHROME.accent,
                            color: "#08121F",
                            fontWeight: 700,
                            cursor: "pointer",
                        }}
                    >
                        {copyState === "copied"
                            ? "Скопировано"
                            : copyState === "failed"
                              ? "Не вышло"
                              : "Скопировать CSS"}
                    </button>
                    <button
                        type="button"
                        onClick={reset}
                        disabled={draft === null}
                        style={{
                            height: 32,
                            padding: "0 12px",
                            borderRadius: 8,
                            border: `1px solid ${CHROME.border}`,
                            background: CHROME.surfaceRaised,
                            color: draft === null ? CHROME.textMuted : CHROME.text,
                            cursor: draft === null ? "default" : "pointer",
                        }}
                    >
                        Сброс
                    </button>
                </div>

                <p style={{ margin: 0, color: CHROME.textMuted, fontSize: 11, lineHeight: 1.5 }}>
                    Правки живут только в этом браузере. Чтобы закрепить — «Скопировать CSS» и
                    вставить блок в <code>app/globals.css</code>.
                </p>
            </div>
        </div>
    );
}

function Section({
    title,
    hint,
    children,
}: {
    title: string;
    hint: string;
    children: React.ReactNode;
}) {
    return (
        <section style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <div>
                <div style={{ fontWeight: 700, fontSize: 11.5 }}>{title}</div>
                <div style={{ color: CHROME.textMuted, fontSize: 11, marginTop: 1 }}>{hint}</div>
            </div>
            {children}
        </section>
    );
}

function Swatch({
    color,
    label,
    selected,
    onSelect,
}: {
    color: string;
    label: string;
    selected: boolean;
    onSelect: () => void;
}) {
    return (
        <button
            type="button"
            onClick={onSelect}
            title={`${label} — ${color}`}
            aria-pressed={selected}
            style={{
                width: 34,
                height: 28,
                borderRadius: 8,
                background: color,
                border: selected ? `2px solid ${CHROME.accent}` : `1px solid ${CHROME.border}`,
                cursor: "pointer",
                padding: 0,
            }}
        />
    );
}

function PreviewLine({
    background,
    color,
    text,
}: {
    background: string;
    color: string;
    text: string;
}) {
    return (
        <div
            style={{
                background,
                color,
                borderRadius: 8,
                padding: "7px 10px",
                fontWeight: 700,
                fontSize: 12.5,
                border: `1px solid ${CHROME.border}`,
            }}
        >
            {text}
        </div>
    );
}
