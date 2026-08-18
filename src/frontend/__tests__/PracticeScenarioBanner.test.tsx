import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("next/link", () => ({
    default: ({ children, href }: { children: React.ReactNode; href: string }) => (
        <a href={href}>{children}</a>
    ),
}));

vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    useDialogBundles: () => ({
        data: [
            {
                id: "bundle-1",
                title: "Холодные звонки",
                description: "",
                sortOrder: 1,
                skillTitle: null,
            },
        ],
        isLoading: false,
        error: null,
        refetch: vi.fn(),
    }),
    useDialogSessions: () => ({ data: [] }),
    startDialogSession: vi.fn(),
}));

const refetchCustomScenarioMode = vi.fn();
let mockCustomScenario: {
    data: { bundleId: string; modeId: string } | undefined;
    isError: boolean;
    isFetching: boolean;
};
vi.mock("@/features/dialog/hooks/use-custom-scenario", () => ({
    SCENARIO_MIN_LENGTH: 20,
    SCENARIO_MAX_LENGTH: 1500,
    validateScenario: vi.fn(),
    useCustomScenarioMode: () => ({
        ...mockCustomScenario,
        refetch: refetchCustomScenarioMode,
    }),
}));

import DialogPage from "@/app/(main)/dialog/page";

function renderPage() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
        <QueryClientProvider client={queryClient}>
            <DialogPage />
        </QueryClientProvider>
    );
}

describe("Practice page — custom scenario banner", () => {
    beforeEach(() => {
        refetchCustomScenarioMode.mockReset();
        mockCustomScenario = { data: undefined, isError: false, isFetching: false };
    });

    it("opens the compose dialog once the hidden mode is resolved", () => {
        mockCustomScenario = {
            data: { bundleId: "bundle-x", modeId: "mode-x" },
            isError: false,
            isFetching: false,
        };
        renderPage();

        fireEvent.click(screen.getByRole("button", { name: "Описать сценарий" }));

        expect(screen.getByText("Свой сценарий")).toBeInTheDocument();
    });

    it("retries instead of sitting inert when the mode could not be loaded", () => {
        // GET /dialog/custom-scenario-mode failing used to leave a live-looking button that did
        // nothing at all — the whole feature was unreachable with no explanation on screen.
        mockCustomScenario = { data: undefined, isError: true, isFetching: false };
        renderPage();

        const button = screen.getByRole("button", { name: "Повторить" });
        expect(button).not.toBeDisabled();
        expect(screen.getByRole("alert")).toHaveTextContent("Режим сейчас недоступен");

        fireEvent.click(button);

        expect(refetchCustomScenarioMode).toHaveBeenCalled();
        expect(screen.queryByText("Свой сценарий")).not.toBeInTheDocument();
    });

    it("marks the button busy while the mode is being fetched", () => {
        mockCustomScenario = { data: undefined, isError: false, isFetching: true };
        renderPage();

        expect(screen.getByRole("button", { name: "Загружаем…" })).toBeDisabled();
        expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });
});
