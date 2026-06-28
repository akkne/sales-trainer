import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { VoteButton } from "@/features/discuss/components/vote-button";
import { formatTimeAgo, pluralizeRu } from "@/features/discuss/lib/format";

describe("VoteButton", () => {
    it("renders the vote count", () => {
        render(<VoteButton count={47} active={false} onToggle={vi.fn()} />);
        expect(screen.getByText("47")).toBeTruthy();
    });

    it("calls onToggle when clicked", () => {
        const onToggle = vi.fn();
        render(<VoteButton count={3} active={false} onToggle={onToggle} />);
        fireEvent.click(screen.getByRole("button"));
        expect(onToggle).toHaveBeenCalledOnce();
    });

    it("reflects active state via aria-pressed", () => {
        render(<VoteButton count={1} active onToggle={vi.fn()} />);
        expect(screen.getByRole("button").getAttribute("aria-pressed")).toBe("true");
    });

    it("does not toggle when disabled", () => {
        const onToggle = vi.fn();
        render(<VoteButton count={0} active={false} onToggle={onToggle} disabled />);
        fireEvent.click(screen.getByRole("button"));
        expect(onToggle).not.toHaveBeenCalled();
    });
});

describe("discuss format helpers", () => {
    it("formatTimeAgo returns hours for a few-hours-old date", () => {
        const threeHoursAgo = new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString();
        expect(formatTimeAgo(threeHoursAgo)).toBe("3 h");
    });

    it("formatTimeAgo returns 'just now' for now", () => {
        expect(formatTimeAgo(new Date().toISOString())).toBe("just now");
    });

    it("pluralizeRu picks the correct English plural form", () => {
        expect(pluralizeRu(1, "thread", "threads", "threads")).toBe("thread");
        expect(pluralizeRu(3, "thread", "threads", "threads")).toBe("threads");
        expect(pluralizeRu(11, "thread", "threads", "threads")).toBe("threads");
        expect(pluralizeRu(22, "thread", "threads", "threads")).toBe("threads");
        expect(pluralizeRu(25, "thread", "threads", "threads")).toBe("threads");
    });
});
