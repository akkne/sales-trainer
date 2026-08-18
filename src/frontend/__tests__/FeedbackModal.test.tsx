import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { FeedbackModal } from "@/features/dialog/components/feedback-modal";

const FEEDBACK = {
    summary: "<p>Хорошее начало разговора</p>",
    content: "<p>Развёрнутый разбор</p>",
    score: 7,
    generatedAt: "2026-08-14T00:00:00Z",
    xpEarned: 25,
};

describe("FeedbackModal", () => {
    it("shows the score and the analysis", () => {
        render(<FeedbackModal feedback={FEEDBACK} onClose={vi.fn()} />);

        expect(screen.getByText("7")).toBeTruthy();
        expect(screen.getByText("Хорошее начало разговора")).toBeTruthy();
    });

    it("never shows XP for a call, even when the backend reports some", () => {
        // Calls award no experience — the analysis is the reward. `xpEarned` stays on the
        // contract because the backend still scores the session.
        render(<FeedbackModal feedback={FEEDBACK} onClose={vi.fn()} />);

        expect(screen.queryByText(/XP/)).toBeNull();
        expect(screen.queryByText(/\+25/)).toBeNull();
    });
});
