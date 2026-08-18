import { describe, it, expect, vi } from "vitest";

import { CallHaptics } from "@/features/voice/services/call-haptics";

describe("CallHaptics", () => {
    it("vibrateOnConnect calls navigator.vibrate when available", () => {
        const vibrate = vi.fn();
        vi.stubGlobal("navigator", { vibrate });

        new CallHaptics().vibrateOnConnect();

        expect(vibrate).toHaveBeenCalledWith(80);
    });

    it("vibrateOnConnect is a no-op without vibration support", () => {
        vi.stubGlobal("navigator", {});

        expect(() => new CallHaptics().vibrateOnConnect()).not.toThrow();
    });
});
