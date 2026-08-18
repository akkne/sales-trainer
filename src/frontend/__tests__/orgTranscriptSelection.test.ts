import { describe, expect, it } from "vitest";
import {
    buildQuotedText,
    countSelectedMessages,
    describeQuotedRange,
    describeSelectedRange,
    isMessageSelected,
    toggleTranscriptSelection,
} from "@/features/org-dialogs/lib/transcript-selection";

/**
 * Block 40.20, screen O6. Everything here works on the server's message `index`, and the case that
 * proves it is a transcript where the same sentence was said twice — the reason the index is on
 * the wire at all (`AdminDialogTranscriptMessageDto`).
 */
const TRANSCRIPT = [
    { index: 4, content: "Ваша цена выше рынка на 20%." },
    { index: 5, content: "Хорошо, могу дать 15% скидки." },
    { index: 6, content: "Давайте закрепим цену сразу." },
    { index: 7, content: "Хорошо, могу дать 15% скидки." },
];

describe("selecting a fragment of a transcript", () => {
    it("selects one line on a plain click", () => {
        expect(toggleTranscriptSelection(null, 5, false)).toEqual({ fromIndex: 5, toIndex: 5 });
    });

    it("clears the selection when the only selected line is clicked again", () => {
        expect(toggleTranscriptSelection({ fromIndex: 5, toIndex: 5 }, 5, false)).toBeNull();
    });

    it("moves the selection to another line on a plain click", () => {
        expect(toggleTranscriptSelection({ fromIndex: 5, toIndex: 6 }, 9, false)).toEqual({
            fromIndex: 9,
            toIndex: 9,
        });
    });

    it("stretches the selection on shift+click, in either direction", () => {
        expect(toggleTranscriptSelection({ fromIndex: 5, toIndex: 5 }, 7, true)).toEqual({
            fromIndex: 5,
            toIndex: 7,
        });
        expect(toggleTranscriptSelection({ fromIndex: 5, toIndex: 5 }, 2, true)).toEqual({
            fromIndex: 2,
            toIndex: 5,
        });
    });

    it("treats shift+click with nothing selected as an ordinary click", () => {
        expect(toggleTranscriptSelection(null, 5, true)).toEqual({ fromIndex: 5, toIndex: 5 });
    });

    it("reports which lines are inside the range", () => {
        const selection = { fromIndex: 5, toIndex: 6 };
        expect(isMessageSelected(selection, 4)).toBe(false);
        expect(isMessageSelected(selection, 5)).toBe(true);
        expect(isMessageSelected(selection, 6)).toBe(true);
        expect(isMessageSelected(selection, 7)).toBe(false);
        expect(isMessageSelected(null, 5)).toBe(false);
        expect(countSelectedMessages(TRANSCRIPT, selection)).toBe(2);
        expect(countSelectedMessages(TRANSCRIPT, null)).toBe(0);
    });
});

describe("the quote a note carries", () => {
    it("copies the selected lines whole, in transcript order", () => {
        expect(buildQuotedText(TRANSCRIPT, { fromIndex: 5, toIndex: 6 })).toBe(
            "Хорошо, могу дать 15% скидки.\nДавайте закрепим цену сразу."
        );
    });

    it("quotes the line that was selected, not the first line that reads the same", () => {
        expect(buildQuotedText(TRANSCRIPT, { fromIndex: 7, toIndex: 7 })).toBe(
            "Хорошо, могу дать 15% скидки."
        );
        expect(describeSelectedRange({ fromIndex: 7, toIndex: 7 })).toBe("реплика 7");
    });

    it("is empty when nothing is selected, which is what disables the send button", () => {
        expect(buildQuotedText(TRANSCRIPT, null)).toBe("");
        expect(describeSelectedRange(null)).toBeNull();
    });

    it("names a range in the plural and a single line in the singular", () => {
        expect(describeSelectedRange({ fromIndex: 5, toIndex: 6 })).toBe("реплики 5–6");
        expect(describeSelectedRange({ fromIndex: 5, toIndex: 5 })).toBe("реплика 5");
    });
});

describe("the range of a note that already exists", () => {
    it("reads the two nullable indexes of the stored row", () => {
        expect(describeQuotedRange(5, 6)).toBe("реплики 5–6");
        expect(describeQuotedRange(5, null)).toBe("реплика 5");
    });

    it("says nothing for a dispute that quoted nothing — which the manager is allowed to file", () => {
        expect(describeQuotedRange(null, null)).toBeNull();
    });
});
