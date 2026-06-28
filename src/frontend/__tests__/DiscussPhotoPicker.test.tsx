import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { PhotoPicker } from "@/features/discuss/components/photo-picker";

vi.mock("@/shared/components/button", () => ({
    Button: ({ children, disabled, onClick, iconLeft: _iconLeft, variant: _variant, size: _size, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement> & { iconLeft?: string; variant?: string; size?: string }) => (
        <button disabled={disabled} onClick={onClick} {...rest}>{children}</button>
    ),
}));

vi.mock("@/shared/components/icon", () => ({
    Icon: () => null,
}));

let objectUrlCounter = 0;
URL.createObjectURL = vi.fn(() => `blob:mock-${++objectUrlCounter}`);
URL.revokeObjectURL = vi.fn();

const makePngFile = (name = "photo.png"): File => {
    const pngHeader = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
    return new File([pngHeader], name, { type: "image/png" });
};

describe("PhotoPicker", () => {
    it("calls onChange with the selected file when a valid image is picked", () => {
        const onChange = vi.fn();
        render(<PhotoPicker files={[]} onChange={onChange} />);

        const input = document.querySelector('input[type="file"]') as HTMLInputElement;
        const file = makePngFile();

        const fileList = Object.assign([file], { item: (i: number) => file ?? null });
        Object.defineProperty(input, "files", { value: fileList, configurable: true });
        fireEvent.change(input);

        expect(onChange).toHaveBeenCalledOnce();
        const received: File[] = onChange.mock.calls[0][0];
        expect(received).toHaveLength(1);
        expect(received[0].name).toBe("photo.png");
    });

    it("disables the add button when ten photos are already attached", () => {
        const files = Array.from({ length: 10 }, (_, index) => makePngFile(`photo-${index}.png`));
        render(<PhotoPicker files={files} onChange={vi.fn()} />);

        const addButton = screen.getByText("Add photo").closest("button");
        expect(addButton?.disabled).toBe(true);
    });
});
