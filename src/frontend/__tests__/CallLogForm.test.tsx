import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
        postFile: vi.fn(),
    },
}));

vi.mock("@/features/notifications/store/toast-store", () => ({
    toast: { error: vi.fn() },
}));

import { apiClient } from "@/shared/api/api-client";
import { CallLogForm } from "@/features/companies/components/call-log-form";
import type { CallLogEntry } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";

const mockPost = apiClient.post as ReturnType<typeof vi.fn>;
const mockPostFile = apiClient.postFile as ReturnType<typeof vi.fn>;

function renderWithClient(ui: React.ReactElement) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    return render(ui, { wrapper });
}

describe("CallLogForm", () => {
    const onSubmit = vi.fn();
    const onCancel = vi.fn();

    beforeEach(() => {
        onSubmit.mockReset();
        onCancel.mockReset();
        mockPost.mockReset();
    });

    it("disables save until the required 'С кем говорил' field has content", () => {
        renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
        const saveButton = screen.getByText("Сохранить запись");
        expect(saveButton).toBeDisabled();

        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "Иван" } });
        expect(saveButton).not.toBeDisabled();
    });

    it("submits trimmed field values with an ISO occurredAt date", () => {
        renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "  Иван  " } });
        fireEvent.change(screen.getByPlaceholderText("Кратко о ходе разговора"), { target: { value: "Обсудили цену" } });
        fireEvent.change(screen.getByPlaceholderText("Договорённости, следующий шаг"), { target: { value: "Пришлём КП" } });
        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({
                contactName: "Иван",
                subject: "Обсудили цену",
                outcome: "Пришлём КП",
                contactId: null,
            })
        );
        const payload = onSubmit.mock.calls[0][0];
        expect(new Date(payload.occurredAt).toString()).not.toBe("Invalid Date");
    });

    it("calls onCancel when the cancel button is clicked", () => {
        renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
        fireEvent.click(screen.getByText("Отмена"));
        expect(onCancel).toHaveBeenCalledOnce();
    });

    it("pre-fills fields when editing an existing entry", () => {
        const initial: CallLogEntry = {
            id: "l1", companyId: "c1", contactName: "Пётр", subject: "Тема", outcome: "Итог",
            occurredAt: "2026-07-01T00:00:00Z", createdAt: "", updatedAt: "", contactId: null,
        };
        renderWithClient(<CallLogForm companyId="c1" initial={initial} onSubmit={onSubmit} onCancel={onCancel} />);

        expect(screen.getByDisplayValue("Пётр")).toBeTruthy();
        expect(screen.getByDisplayValue("Тема")).toBeTruthy();
        expect(screen.getByDisplayValue("Итог")).toBeTruthy();
    });

    it("does not offer paste-notes mode while editing an existing entry", () => {
        const initial: CallLogEntry = {
            id: "l1", companyId: "c1", contactName: "Пётр", subject: "Тема", outcome: "Итог",
            occurredAt: "2026-07-01T00:00:00Z", createdAt: "", updatedAt: "", contactId: null,
        };
        renderWithClient(<CallLogForm companyId="c1" initial={initial} onSubmit={onSubmit} onCancel={onCancel} />);

        expect(screen.queryByText("Вставить заметки")).toBeNull();
    });

    const CONTACT: CompanyContact = {
        id: "contact-1", companyId: "c1", name: "Иван Петров", position: "Руководитель закупок",
        notes: "", createdAt: "", updatedAt: "",
    };

    it("picking a contact chip fills the name field and sets contactId on submit", () => {
        renderWithClient(<CallLogForm companyId="c1" contacts={[CONTACT]} onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.click(screen.getByText("Иван Петров"));
        expect(screen.getByDisplayValue("Иван Петров")).toBeTruthy();

        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({ contactName: "Иван Петров", contactId: "contact-1" })
        );
    });

    it("typing free text after picking a contact clears contactId on submit", () => {
        renderWithClient(<CallLogForm companyId="c1" contacts={[CONTACT]} onSubmit={onSubmit} onCancel={onCancel} />);

        fireEvent.click(screen.getByText("Иван Петров"));
        fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "Другой человек" } });
        fireEvent.click(screen.getByText("Сохранить запись"));

        expect(onSubmit).toHaveBeenCalledWith(
            expect.objectContaining({ contactName: "Другой человек", contactId: null })
        );
    });

    describe("paste-notes mode", () => {
        it("switches to paste mode and prefills fields from the AI response on success", async () => {
            mockPost.mockResolvedValue({
                contactName: "Иван Петров",
                subject: "Обсудили условия",
                outcome: "Взял паузу подумать",
                occurredAt: "2026-07-01T00:00:00Z",
            });

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);

            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.change(
                screen.getByPlaceholderText(/AI распознает/),
                { target: { value: "звонили Ивану, обсудили условия, взял паузу" } }
            );
            fireEvent.click(screen.getByText("Распознать"));

            await waitFor(() => expect(mockPost).toHaveBeenCalledWith(
                "/companies/c1/logs/parse",
                { rawText: "звонили Ивану, обсудили условия, взял паузу" }
            ));

            await waitFor(() => expect(screen.getByDisplayValue("Иван Петров")).toBeTruthy());
            expect(screen.getByDisplayValue("Обсудили условия")).toBeTruthy();
            expect(screen.getByDisplayValue("Взял паузу подумать")).toBeTruthy();
            // Back in manual mode for review before saving.
            expect(screen.getByText("Вставить заметки")).toBeTruthy();
        });

        it("keeps contact name untouched when the AI omits it", async () => {
            mockPost.mockResolvedValue({
                contactName: null,
                subject: "Звонок",
                outcome: "Договорились созвониться",
                occurredAt: null,
            });

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);

            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.change(screen.getByPlaceholderText(/AI распознает/), { target: { value: "текст без имени" } });
            fireEvent.click(screen.getByText("Распознать"));

            await waitFor(() => expect(screen.getByDisplayValue("Звонок")).toBeTruthy());
            expect((screen.getByPlaceholderText(/Имя и должность/) as HTMLInputElement).value).toBe("");
        });

        it("falls back gracefully to manual entry when parsing fails", async () => {
            mockPost.mockRejectedValue(new Error("AI service unavailable"));

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);

            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.change(screen.getByPlaceholderText(/AI распознает/), { target: { value: "сырые заметки" } });
            fireEvent.click(screen.getByText("Распознать"));

            await waitFor(() =>
                expect(screen.getByText(/Не удалось распознать заметки/)).toBeTruthy()
            );

            // Still in paste mode with a manual-entry escape hatch — the form stays usable.
            fireEvent.click(screen.getByText("Заполнить вручную"));
            fireEvent.change(screen.getByPlaceholderText(/Имя и должность/), { target: { value: "Иван" } });
            expect(screen.getByText("Сохранить запись")).not.toBeDisabled();
        });

        it("returns to manual mode via the 'Заполнить вручную' link without calling the API", () => {
            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);

            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.click(screen.getByText("Заполнить вручную"));

            expect(screen.getByText("Вставить заметки")).toBeTruthy();
            expect(mockPost).not.toHaveBeenCalled();
        });
    });

    describe("voice memo recording (39.15)", () => {
        // Minimal MediaRecorder stub: start() flips state, stop() synchronously fires
        // ondataavailable + onstop, mirroring how real recorders flush their last chunk on stop.
        class MockMediaRecorder {
            state: "inactive" | "recording" = "inactive";
            ondataavailable: ((event: { data: Blob }) => void) | null = null;
            onstop: (() => void) | null = null;
            constructor(public stream: MediaStream) {}
            start() {
                this.state = "recording";
            }
            stop() {
                this.state = "inactive";
                this.ondataavailable?.({ data: new Blob(["audio"], { type: "audio/webm" }) });
                this.onstop?.();
            }
        }

        function fakeStream(): MediaStream {
            const track = { stop: vi.fn() };
            return { getTracks: () => [track] } as unknown as MediaStream;
        }

        let getUserMedia: ReturnType<typeof vi.fn>;

        beforeEach(() => {
            mockPostFile.mockReset();
            getUserMedia = vi.fn().mockResolvedValue(fakeStream());
            (window as unknown as { MediaRecorder: typeof MockMediaRecorder }).MediaRecorder = MockMediaRecorder;
            Object.defineProperty(navigator, "mediaDevices", {
                value: { getUserMedia },
                writable: true,
                configurable: true,
            });
        });

        it("records, transcribes, and lands the transcript in the raw-notes textarea", async () => {
            mockPostFile.mockResolvedValue({ text: "Звонили Ивану по поводу поставки", language: "ru" });

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));

            fireEvent.click(screen.getByLabelText("Наговорить заметку"));
            await waitFor(() => expect(getUserMedia).toHaveBeenCalledWith({ audio: true }));
            await waitFor(() => expect(screen.getByLabelText("Остановить запись")).toBeTruthy());

            fireEvent.click(screen.getByLabelText("Остановить запись"));

            await waitFor(() => expect(mockPostFile).toHaveBeenCalledWith(
                "/transcription/transcribe",
                expect.any(FormData)
            ));
            await waitFor(() =>
                expect(screen.getByDisplayValue("Звонили Ивану по поводу поставки")).toBeTruthy()
            );
        });

        it("appends the transcript with a separator when raw notes already has text", async () => {
            mockPostFile.mockResolvedValue({ text: "и договорились созвониться", language: "ru" });

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.change(screen.getByPlaceholderText(/AI распознает/), { target: { value: "Обсудили условия поставки" } });

            fireEvent.click(screen.getByLabelText("Наговорить заметку"));
            await waitFor(() => expect(screen.getByLabelText("Остановить запись")).toBeTruthy());
            fireEvent.click(screen.getByLabelText("Остановить запись"));

            await waitFor(() => {
                const textarea = screen.getByPlaceholderText(/AI распознает/) as HTMLTextAreaElement;
                expect(textarea.value).toBe("Обсудили условия поставки\nи договорились созвониться");
            });
        });

        it("shows a graceful error and keeps the form usable when the microphone is denied", async () => {
            getUserMedia.mockRejectedValue(new Error("Permission denied"));

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));

            fireEvent.click(screen.getByLabelText("Наговорить заметку"));

            await waitFor(() => expect(screen.getByText(/Доступ к микрофону запрещён/)).toBeTruthy());

            // Raw-notes field stays editable — manual/paste entry still works.
            fireEvent.change(screen.getByPlaceholderText(/AI распознает/), { target: { value: "текст вручную" } });
            expect(screen.getByDisplayValue("текст вручную")).toBeTruthy();
        });

        it("shows a graceful error and keeps raw notes editable when transcription fails", async () => {
            mockPostFile.mockRejectedValue(new Error("ai-service unavailable"));

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));

            fireEvent.click(screen.getByLabelText("Наговорить заметку"));
            await waitFor(() => expect(screen.getByLabelText("Остановить запись")).toBeTruthy());
            fireEvent.click(screen.getByLabelText("Остановить запись"));

            await waitFor(() => expect(screen.getByText(/ai-service unavailable/)).toBeTruthy());

            fireEvent.change(screen.getByPlaceholderText(/AI распознает/), { target: { value: "заметка вручную после ошибки" } });
            expect(screen.getByDisplayValue("заметка вручную после ошибки")).toBeTruthy();
        });

        it("skips transcription and returns to idle for an empty recording", async () => {
            class EmptyMediaRecorder extends MockMediaRecorder {
                stop() {
                    this.state = "inactive";
                    // No dataavailable payload → the hook builds a 0-byte blob.
                    this.onstop?.();
                }
            }
            (window as unknown as { MediaRecorder: typeof MockMediaRecorder }).MediaRecorder =
                EmptyMediaRecorder;

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));

            fireEvent.click(screen.getByLabelText("Наговорить заметку"));
            await waitFor(() => expect(screen.getByLabelText("Остановить запись")).toBeTruthy());
            fireEvent.click(screen.getByLabelText("Остановить запись"));

            await waitFor(() => expect(screen.getByLabelText("Наговорить заметку")).toBeTruthy());
            expect(mockPostFile).not.toHaveBeenCalled();
        });

        it("releases the microphone if unmounted while the permission prompt is open", async () => {
            const track = { stop: vi.fn() };
            let resolveStream: (stream: MediaStream) => void = () => {};
            getUserMedia.mockReturnValue(
                new Promise<MediaStream>((resolve) => {
                    resolveStream = resolve;
                })
            );

            const { unmount } = renderWithClient(
                <CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />
            );
            fireEvent.click(screen.getByText("Вставить заметки"));
            fireEvent.click(screen.getByLabelText("Наговорить заметку"));
            await waitFor(() => expect(getUserMedia).toHaveBeenCalled());

            // Unmount before the mic permission resolves, then resolve it late.
            unmount();
            resolveStream({ getTracks: () => [track] } as unknown as MediaStream);

            await waitFor(() => expect(track.stop).toHaveBeenCalledTimes(1));
        });

        it("hides the mic button when MediaRecorder is unsupported", () => {
            (window as unknown as { MediaRecorder?: typeof MockMediaRecorder }).MediaRecorder = undefined;

            renderWithClient(<CallLogForm companyId="c1" onSubmit={onSubmit} onCancel={onCancel} />);
            fireEvent.click(screen.getByText("Вставить заметки"));

            expect(screen.queryByLabelText("Наговорить заметку")).toBeNull();
            // Paste/manual flow remains fully usable without the mic.
            expect(screen.getByPlaceholderText(/AI распознает/)).toBeTruthy();
        });
    });
});
