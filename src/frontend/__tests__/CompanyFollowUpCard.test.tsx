import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CompanyFollowUpCard } from "@/features/companies/components/company-followup-card";

describe("CompanyFollowUpCard", () => {
    const onSave = vi.fn();

    beforeEach(() => {
        onSave.mockReset();
    });

    it("shows the empty state when no follow-up is scheduled", () => {
        render(<CompanyFollowUpCard nextActionAt={null} nextActionNote={null} onSave={onSave} />);
        expect(screen.getByText("Запланировать")).toBeTruthy();
    });

    it("shows the scheduled date and note when a follow-up exists", () => {
        render(
            <CompanyFollowUpCard
                nextActionAt="2026-08-15T00:00:00Z"
                nextActionNote="Обсудить условия"
                onSave={onSave}
            />
        );
        expect(screen.getByText("Обсудить условия")).toBeTruthy();
    });

    it("enters edit mode and saves a new date and note", () => {
        render(<CompanyFollowUpCard nextActionAt={null} nextActionNote={null} onSave={onSave} />);

        fireEvent.click(screen.getByText("Изменить"));
        const dateInput = document.getElementById("co-followup-date") as HTMLInputElement;
        fireEvent.change(dateInput, { target: { value: "2026-09-01" } });
        const noteInput = document.getElementById("co-followup-note") as HTMLTextAreaElement;
        fireEvent.change(noteInput, { target: { value: "Позвонить насчёт цен" } });
        fireEvent.click(screen.getByText("Сохранить"));

        expect(onSave).toHaveBeenCalledTimes(1);
        const [nextActionAt, nextActionNote] = onSave.mock.calls[0];
        expect(nextActionAt).toContain("2026-09-01");
        expect(nextActionNote).toBe("Позвонить насчёт цен");
    });

    it("disables saving while the date is empty", () => {
        render(<CompanyFollowUpCard nextActionAt={null} nextActionNote={null} onSave={onSave} />);

        fireEvent.click(screen.getByText("Изменить"));
        const saveButton = screen.getByText("Сохранить") as HTMLButtonElement;
        expect(saveButton.disabled).toBe(true);
    });

    it("clears the follow-up via the remove button", () => {
        render(
            <CompanyFollowUpCard nextActionAt="2026-08-15T00:00:00Z" nextActionNote="note" onSave={onSave} />
        );

        fireEvent.click(screen.getByText("Изменить"));
        fireEvent.click(screen.getByText("Убрать напоминание"));

        expect(onSave).toHaveBeenCalledWith(null, null);
    });

    it("cancels edits without calling onSave", () => {
        render(<CompanyFollowUpCard nextActionAt={null} nextActionNote={null} onSave={onSave} />);

        fireEvent.click(screen.getByText("Изменить"));
        fireEvent.click(screen.getByText("Отмена"));

        expect(onSave).not.toHaveBeenCalled();
        expect(screen.getByText("Запланировать")).toBeTruthy();
    });
});
