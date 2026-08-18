"use client";

import { Card } from "@/shared/components/card";

interface SourceMaterialDisclosureProps {
    sourceMaterial: string;
    /** Non-null when the dashboard composed the material instead of a person pasting it. */
    gapSourceRef: string | null;
}

/**
 * «Откуда это взялось» — the second question a reviewer asks at the checkpoint, and the reason the
 * run keeps its material verbatim instead of discarding it after structuring.
 *
 * It matters most for a run the dashboard started (40.31): there was no textarea behind that
 * button, the material was composed by the server out of a measured failure and the organization
 * profile, and a reviewer looking at a structure they never uploaded anything for deserves to be
 * able to read what it was built from.
 *
 * Collapsed by default — it is a fifty-page deck often enough that opening it by default would bury
 * the checkpoint below the fold, which is where the one decision on this screen lives.
 */
export function SourceMaterialDisclosure({
    sourceMaterial,
    gapSourceRef,
}: SourceMaterialDisclosureProps) {
    if (sourceMaterial.trim().length === 0) return null;

    return (
        <Card padding={16}>
            <details>
                <summary className="cursor-pointer text-sm font-medium text-ink">
                    Исходный материал{" "}
                    <span className="mono text-xs font-normal text-ink-3">
                        ({sourceMaterial.length.toLocaleString("ru-RU")} символов)
                    </span>
                </summary>

                {gapSourceRef && (
                    <p className="mt-2 text-xs text-ink-3">
                        Этот текст собран автоматически по провалу на дашборде — вы его не вводили.
                    </p>
                )}

                <pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-words text-xs text-ink-2">
                    {sourceMaterial}
                </pre>
            </details>
        </Card>
    );
}
