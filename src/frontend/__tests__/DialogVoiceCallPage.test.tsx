import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const mockPush = vi.fn();
let mockSessionParam: string | null = null;
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: mockPush }),
    useParams: () => ({ bundleId: "bundle-1", modeId: "mode-1" }),
    useSearchParams: () => ({ get: (key: string) => (key === "session" ? mockSessionParam : null) }),
}));

let mockBundles: { id: string; title: string }[] | undefined;
const completeDialogSession = vi.fn(async () => null);
const fetchDialogSession = vi.fn(async () => ({ id: "sess-7", status: "active" }));
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    useDialogBundles: () => ({ data: mockBundles }),
    useDialogModes: () => ({
        data: [{ id: "mode-1", title: "Свой сценарий", description: "", voiceEnabled: true }],
    }),
    completeDialogSession: (...args: unknown[]) => completeDialogSession(...args),
    fetchDialogSession: (...args: unknown[]) => fetchDialogSession(...args),
}));

vi.mock("@/features/voice/hooks/use-voice-usage", () => ({
    useVoiceUsage: () => ({ data: undefined, refetch: vi.fn() }),
}));

let capturedVoiceOptions: Record<string, unknown> | null = null;
// Mirrors the real hook: the session is reported ready whether it was just created or handed in.
const startVoiceMock = vi.fn(async () => {
    const sessionId = (capturedVoiceOptions?.sessionId as string | null) ?? "sess-new";
    (capturedVoiceOptions?.onSessionReady as ((id: string) => void) | undefined)?.(sessionId);
});
const stopVoiceMock = vi.fn();
const endSessionMock = vi.fn();
vi.mock("@/features/voice/hooks/use-voice", () => ({
    useVoice: (options: Record<string, unknown>) => {
        capturedVoiceOptions = options;
        return {
            state: "idle",
            currentTranscript: "",
            isVoiceAvailable: true,
            startVoice: startVoiceMock,
            stopVoice: stopVoiceMock,
            endSession: endSessionMock,
        };
    },
}));

import VoiceCallPage from "@/app/dialog/[bundleId]/[modeId]/voice/page";

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
        <QueryClientProvider client={queryClient}>
            <VoiceCallPage />
        </QueryClientProvider>
    );
}

describe("VoiceCallPage — pre-started sessions", () => {
    beforeEach(() => {
        mockPush.mockReset();
        completeDialogSession.mockReset();
        completeDialogSession.mockResolvedValue(null);
        fetchDialogSession.mockReset();
        fetchDialogSession.mockResolvedValue({ id: "sess-7", status: "active" });
        startVoiceMock.mockClear();
        stopVoiceMock.mockClear();
        endSessionMock.mockClear();
        capturedVoiceOptions = null;
        mockSessionParam = null;
        mockBundles = [{ id: "bundle-1", title: "Холодные звонки" }];
    });

    it("hands a ?session= id straight to the voice pipeline", () => {
        // A custom scenario is created before this page loads; reusing the session is what keeps the
        // scenario attached. Creating a fresh one here would produce a context-free conversation.
        mockSessionParam = "sess-7";
        renderPage();

        expect(capturedVoiceOptions?.sessionId).toBe("sess-7");
    });

    it("starts with no session when the URL carries none", () => {
        renderPage();

        expect(capturedVoiceOptions?.sessionId).toBeNull();
    });

    it("connects the call when the pre-started session is reused", async () => {
        // Regression: reusing a session skipped the "session ready" callback, so the call stayed
        // on «Соединение…» for its whole duration and never started the timer.
        mockSessionParam = "sess-7";
        renderPage();

        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));

        // The header switches from «ЗВОНИМ» to the running call timer only once the call connects.
        expect(await screen.findByText("00:00")).toBeTruthy();
        expect(screen.queryByText("ЗВОНИМ")).toBeNull();
    });

    it("completes the pre-started session on hang-up instead of hanging on «Готовим разбор…»", async () => {
        mockSessionParam = "sess-7";
        renderPage();

        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));
        fireEvent.click(await screen.findByRole("button", { name: "Завершить звонок" }));

        await waitFor(() => expect(completeDialogSession).toHaveBeenCalledWith("sess-7"));
        await waitFor(() => expect(screen.queryByText("Готовим разбор…")).toBeNull());
    });

    it("refuses to call a scenario session that was already played out", async () => {
        // Regression: calling on a finished session produced a persona that never said a word —
        // the backend refuses the turn and the stream comes back empty.
        mockSessionParam = "sess-7";
        fetchDialogSession.mockResolvedValue({ id: "sess-7", status: "completed" });
        renderPage();

        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));

        expect(await screen.findByRole("alert")).toBeTruthy();
        expect(startVoiceMock).not.toHaveBeenCalled();
        fireEvent.click(await screen.findByRole("button", { name: "К сценариям" }));
        expect(mockPush).toHaveBeenCalledWith("/dialog/bundle-1");
    });

    it("sends the user back for a new scenario instead of redialling a spent one", async () => {
        mockSessionParam = "sess-7";
        renderPage();

        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));
        fireEvent.click(await screen.findByRole("button", { name: "Завершить звонок" }));

        await waitFor(() => expect(completeDialogSession).toHaveBeenCalledWith("sess-7"));
        expect(await screen.findByRole("button", { name: "К сценариям" })).toBeTruthy();
        expect(screen.queryByRole("button", { name: "Позвонить" })).toBeNull();
    });

    it("goes back to the bundle when the bundle is a visible one", () => {
        renderPage();

        fireEvent.click(screen.getByLabelText("Назад к сценариям"));

        expect(mockPush).toHaveBeenCalledWith("/dialog/bundle-1");
    });

    it("goes back to the practice page when the bundle is hidden", () => {
        // Hidden bundles (custom scenario, company call) are absent from GET /dialog/bundles, so
        // there is no mode list to return to.
        mockBundles = [{ id: "some-other-bundle", title: "Другое" }];
        renderPage();

        fireEvent.click(screen.getByLabelText("Назад к сценариям"));

        expect(mockPush).toHaveBeenCalledWith("/dialog");
    });

    it("keeps the old destination while the bundle list is still loading", () => {
        mockBundles = undefined;
        renderPage();

        fireEvent.click(screen.getByLabelText("Назад к сценариям"));

        expect(mockPush).toHaveBeenCalledWith("/dialog/bundle-1");
    });
});
