import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
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
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    useDialogBundles: () => ({ data: mockBundles }),
    useDialogModes: () => ({
        data: [{ id: "mode-1", title: "Свой сценарий", description: "", voiceEnabled: true }],
    }),
    completeDialogSession: (...args: unknown[]) => completeDialogSession(...args),
}));

vi.mock("@/features/voice/hooks/use-voice-usage", () => ({
    useVoiceUsage: () => ({ data: undefined, refetch: vi.fn() }),
}));

let capturedVoiceOptions: Record<string, unknown> | null = null;
vi.mock("@/features/voice/hooks/use-voice", () => ({
    useVoice: (options: Record<string, unknown>) => {
        capturedVoiceOptions = options;
        return {
            state: "idle",
            currentTranscript: "",
            isVoiceAvailable: true,
            startVoice: vi.fn(),
            stopVoice: vi.fn(),
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
