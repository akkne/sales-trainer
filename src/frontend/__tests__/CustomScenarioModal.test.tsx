import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const push = vi.fn();
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push }),
}));

const validateScenario = vi.fn();
vi.mock("@/features/dialog/hooks/use-custom-scenario", async (importOriginal) => {
    const actual = await importOriginal<typeof import("@/features/dialog/hooks/use-custom-scenario")>();
    return { ...actual, validateScenario: (...args: unknown[]) => validateScenario(...args) };
});

const startDialogSession = vi.fn();
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    startDialogSession: (...args: unknown[]) => startDialogSession(...args),
}));

import { CustomScenarioModal } from "@/features/dialog/components/custom-scenario-modal";
import { SCENARIO_MIN_LENGTH } from "@/features/dialog/hooks/use-custom-scenario";

const BUNDLE_ID = "bundle-1";
const MODE_ID = "mode-1";
const VALID_SCENARIO =
    "Я продаю CRM небольшим агентствам. Звоню директору, который пользуется таблицами.";

function renderModal(onClose = vi.fn()) {
    render(<CustomScenarioModal bundleId={BUNDLE_ID} modeId={MODE_ID} onClose={onClose} />);
    const action = (label: string) => screen.getByText(label).closest("button") as HTMLButtonElement;
    return {
        onClose,
        textarea: () => document.querySelector(".scenario-input") as HTMLTextAreaElement,
        submit: () => action("Написать"),
        call: () => action("Позвонить"),
    };
}

describe("CustomScenarioModal", () => {
    beforeEach(() => {
        push.mockReset();
        validateScenario.mockReset();
        startDialogSession.mockReset();
    });

    it("keeps both actions disabled until the scenario is long enough", () => {
        const modal = renderModal();

        expect(modal.submit().disabled).toBe(true);
        expect(modal.call().disabled).toBe(true);

        fireEvent.change(modal.textarea(), { target: { value: "коротко" } });
        expect(modal.submit().disabled).toBe(true);

        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        expect(modal.submit().disabled).toBe(false);
        expect(modal.call().disabled).toBe(false);
    });

    it("tells the user how many characters are still missing", () => {
        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: "продажи" } });

        expect(screen.getByText(`ещё ${SCENARIO_MIN_LENGTH - 7} симв.`)).toBeTruthy();
    });

    it("starts a session with the scenario and opens the text conversation", async () => {
        validateScenario.mockResolvedValue({ isValid: true, rejectionReason: null });
        startDialogSession.mockResolvedValue({ id: "session-9" });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: `  ${VALID_SCENARIO}  ` } });
        fireEvent.click(modal.submit());

        await waitFor(() => expect(push).toHaveBeenCalled());

        expect(startDialogSession).toHaveBeenCalledWith(BUNDLE_ID, MODE_ID, undefined, VALID_SCENARIO);
        expect(push).toHaveBeenCalledWith(`/dialog/${BUNDLE_ID}/${MODE_ID}?session=session-9`);
    });

    it("sends the same session to the voice screen when the user calls", async () => {
        validateScenario.mockResolvedValue({ isValid: true, rejectionReason: null });
        startDialogSession.mockResolvedValue({ id: "session-9" });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.call());

        await waitFor(() => expect(push).toHaveBeenCalled());

        // One session, created here with the scenario — the voice screen resumes it by id and
        // never sees the text.
        expect(startDialogSession).toHaveBeenCalledTimes(1);
        expect(startDialogSession).toHaveBeenCalledWith(BUNDLE_ID, MODE_ID, undefined, VALID_SCENARIO);
        expect(push).toHaveBeenCalledWith(`/dialog/${BUNDLE_ID}/${MODE_ID}/voice?session=session-9`);
    });

    it("validates a voice call the same way as a text one", async () => {
        validateScenario.mockResolvedValue({
            isValid: false,
            rejectionReason: "Сценарий не связан с продажами.",
        });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.call());

        await screen.findByText("Сценарий не связан с продажами.");

        expect(startDialogSession).not.toHaveBeenCalled();
        expect(push).not.toHaveBeenCalled();
    });

    it("shows progress only on the action that was pressed", async () => {
        validateScenario.mockReturnValue(new Promise(() => {}));

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.call());

        await waitFor(() => expect(screen.getByText("Проверяем…")).toBeTruthy());
        expect(screen.getByText("Написать")).toBeTruthy();
        expect(modal.submit().disabled).toBe(true);
    });

    it("shows the rejection reason and starts nothing when the scenario is off-topic", async () => {
        validateScenario.mockResolvedValue({
            isValid: false,
            rejectionReason: "Сценарий не связан с продажами.",
        });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.submit());

        await screen.findByText("Сценарий не связан с продажами.");

        expect(startDialogSession).not.toHaveBeenCalled();
        expect(push).not.toHaveBeenCalled();
    });

    it("falls back to a readable message when a rejection carries no reason", async () => {
        validateScenario.mockResolvedValue({ isValid: false, rejectionReason: null });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.submit());

        await screen.findByText(/не связан с продажами/i);
    });

    it("surfaces a failure from the session call itself", async () => {
        validateScenario.mockResolvedValue({ isValid: true, rejectionReason: null });
        startDialogSession.mockRejectedValue(new Error("Не удалось проверить сценарий."));

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.submit());

        await screen.findByText("Не удалось проверить сценарий.");
        expect(push).not.toHaveBeenCalled();
    });

    it("clears a previous error as soon as the user edits the scenario", async () => {
        validateScenario.mockResolvedValue({
            isValid: false,
            rejectionReason: "Сценарий не связан с продажами.",
        });

        const modal = renderModal();
        fireEvent.change(modal.textarea(), { target: { value: VALID_SCENARIO } });
        fireEvent.click(modal.submit());
        await screen.findByText("Сценарий не связан с продажами.");

        fireEvent.change(modal.textarea(), { target: { value: `${VALID_SCENARIO} Ещё деталь.` } });

        expect(screen.queryByText("Сценарий не связан с продажами.")).toBeNull();
    });

    it("closes on Escape", () => {
        const onClose = vi.fn();
        renderModal(onClose);

        fireEvent.keyDown(document, { key: "Escape" });

        expect(onClose).toHaveBeenCalledTimes(1);
    });
});
