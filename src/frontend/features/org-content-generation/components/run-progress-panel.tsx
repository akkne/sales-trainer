"use client";

import { Card } from "@/shared/components/card";
import { describeJobProgress } from "@/features/org-content-generation/utils/job-state";

interface RunProgressPanelProps {
    status: string;
    /** Shown under the copy so a run left open overnight does not look like it just started. */
    startedAtLabel: string;
}

/**
 * O11 layout (а) — `structuring` and `generating`.
 *
 * The one thing this screen has to say is that leaving is safe. Both halves are minutes long and
 * run in a background worker, so a РОП who closes the tab loses nothing — and the alternative
 * reading, that the browser is holding the job open, is what makes people sit and watch a spinner
 * for two minutes.
 *
 * `role="status"` rather than `role="alert"`: it is progress, not a problem, and a screen reader
 * should hear it when it settles rather than be interrupted.
 */
export function RunProgressPanel({ status, startedAtLabel }: RunProgressPanelProps) {
    const progressCopy = describeJobProgress(status);

    return (
        <Card padding={24}>
            <div className="flex items-start gap-4" role="status" aria-live="polite">
                {/*
                 * An indeterminate ring, not a percentage bar. Nothing in the contract reports how
                 * far along an LLM call is, and a bar that fills on a timer is a number we made up.
                 */}
                <span
                    aria-hidden="true"
                    className="shrink-0 mt-0.5 w-8 h-8 rounded-full animate-spin"
                    style={{
                        border: "3px solid var(--line)",
                        borderTopColor: "var(--indigo-ink)",
                    }}
                />
                <div className="min-w-0">
                    <h2 className="text-base font-bold text-ink">{progressCopy.title}</h2>
                    <p className="mt-1 text-sm text-ink-3">{progressCopy.description}</p>
                    <p className="mt-3 text-xs text-ink-3">Прогон начат {startedAtLabel}</p>
                </div>
            </div>
        </Card>
    );
}
