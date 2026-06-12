import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { UserAvatar } from "@/shared/components/user-avatar";

describe("UserAvatar", () => {
    it("renders an <img> with resolved src when avatarUrl is provided", () => {
        render(<UserAvatar avatarUrl="/uploads/avatar.jpg" seed="Alice" size={40} />);
        const img = screen.getByRole("img") as HTMLImageElement;
        expect(img).toBeTruthy();
        expect(img.src).toContain("avatar.jpg");
    });

    it("renders GeoAvatar fallback (<svg>) when avatarUrl is not provided", () => {
        const { container } = render(<UserAvatar seed="Bob" size={40} />);
        expect(container.querySelector("svg")).toBeTruthy();
        expect(container.querySelector("img")).toBeNull();
    });

    it("swaps to GeoAvatar fallback when img fires onError", () => {
        const { container } = render(
            <UserAvatar avatarUrl="/uploads/broken.jpg" seed="Carol" size={40} />
        );
        const img = container.querySelector("img") as HTMLImageElement;
        expect(img).toBeTruthy();
        fireEvent.error(img);
        expect(container.querySelector("img")).toBeNull();
        expect(container.querySelector("svg")).toBeTruthy();
    });
});
