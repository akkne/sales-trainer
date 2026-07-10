import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PrecallPanel } from "@/features/companies/components/precall-panel";
import type { CompanyPersona } from "@/features/companies/hooks/use-company-personas";

const PERSONA: CompanyPersona = {
    id: "persona-1",
    companyId: "c1",
    name: "Мария Соколова",
    position: "Руководитель закупок",
    personality: "Прагматична.",
    difficulty: "Hard",
    createdAt: "",
};

describe("PrecallPanel", () => {
    const onCall = vi.fn();
    const onChat = vi.fn();
    const onGeneratePersona = vi.fn();
    const onSavePersona = vi.fn();

    beforeEach(() => {
        onCall.mockReset();
        onChat.mockReset();
        onGeneratePersona.mockReset();
        onSavePersona.mockReset();
    });

    function renderPanel(overrides: Partial<React.ComponentProps<typeof PrecallPanel>> = {}) {
        return render(
            <PrecallPanel
                hasDescription
                recentGoals={[]}
                onCall={onCall}
                onChat={onChat}
                personas={[]}
                onGeneratePersona={onGeneratePersona}
                onSavePersona={onSavePersona}
                {...overrides}
            />
        );
    }

    it("calls onCall with null persona when 'Без персоны' is selected (default)", () => {
        renderPanel();
        fireEvent.click(screen.getByText("Позвонить"));
        expect(onCall).toHaveBeenCalledWith("", null);
    });

    it("renders persona chips and selects a persona for the call", () => {
        renderPanel({ personas: [PERSONA] });

        fireEvent.click(screen.getByText("Мария Соколова"));
        fireEvent.click(screen.getByText("Позвонить"));

        expect(onCall).toHaveBeenCalledWith("", {
            name: "Мария Соколова",
            position: "Руководитель закупок",
            personality: "Прагматична.",
            difficulty: "Hard",
        });
    });

    it("reverts to no persona when 'Без персоны' is clicked again", () => {
        renderPanel({ personas: [PERSONA] });

        fireEvent.click(screen.getByText("Мария Соколова"));
        fireEvent.click(screen.getByText("Без персоны"));
        fireEvent.click(screen.getByText("Чат"));

        expect(onChat).toHaveBeenCalledWith("", null);
    });

    it("generates a persona draft and saves it", async () => {
        onGeneratePersona.mockResolvedValue({ name: "Новый", position: "Позиция", personality: "Характер" });
        renderPanel();

        fireEvent.click(screen.getByText("Сгенерировать собеседника"));
        fireEvent.click(screen.getByText("Сгенерировать"));

        await waitFor(() => expect(screen.getByText("Новый")).toBeTruthy());
        expect(onGeneratePersona).toHaveBeenCalledWith({
            contactName: undefined,
            contactPosition: undefined,
            difficulty: "Medium",
        });

        fireEvent.click(screen.getByText("Сохранить собеседника"));
        expect(onSavePersona).toHaveBeenCalledWith({
            name: "Новый",
            position: "Позиция",
            personality: "Характер",
            difficulty: "Medium",
        });
    });

    it("shows an error message when generation fails", async () => {
        onGeneratePersona.mockRejectedValue(new Error("AI unavailable"));
        renderPanel();

        fireEvent.click(screen.getByText("Сгенерировать собеседника"));
        fireEvent.click(screen.getByText("Сгенерировать"));

        await waitFor(() => expect(screen.getByText("AI unavailable")).toBeTruthy());
    });
});
