"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { Button } from "@/shared/components/button";
import { Icon } from "@/shared/components/icon";

const MAXIMUM_PHOTO_COUNT = 10;
const MAXIMUM_FILE_SIZE_BYTES = 5 * 1024 * 1024;
const ALLOWED_MIME_TYPES = ["image/png", "image/jpeg", "image/webp"];

interface PhotoPickerProps {
    files: File[];
    onChange: (files: File[]) => void;
    disabled?: boolean;
}

export function PhotoPicker({ files, onChange, disabled = false }: PhotoPickerProps) {
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [rejectionMessage, setRejectionMessage] = useState<string | null>(null);

    const previewUrls = useMemo(() => files.map((file) => URL.createObjectURL(file)), [files]);

    useEffect(() => {
        return () => {
            for (const previewUrl of previewUrls) {
                URL.revokeObjectURL(previewUrl);
            }
        };
    }, [previewUrls]);

    const isAtMaximum = files.length >= MAXIMUM_PHOTO_COUNT;

    const handleSelection = (selectedList: FileList | null) => {
        if (!selectedList) return;
        const incoming = Array.from(selectedList);
        const accepted: File[] = [];
        let rejectedType = false;
        let rejectedSize = false;
        let rejectedLimit = false;

        for (const file of incoming) {
            if (!ALLOWED_MIME_TYPES.includes(file.type)) {
                rejectedType = true;
                continue;
            }
            if (file.size > MAXIMUM_FILE_SIZE_BYTES) {
                rejectedSize = true;
                continue;
            }
            if (files.length + accepted.length >= MAXIMUM_PHOTO_COUNT) {
                rejectedLimit = true;
                continue;
            }
            accepted.push(file);
        }

        if (accepted.length > 0) {
            onChange([...files, ...accepted]);
        }

        if (rejectedLimit) {
            setRejectionMessage(`You can attach up to ${MAXIMUM_PHOTO_COUNT} photos`);
        } else if (rejectedSize) {
            setRejectionMessage("Each photo must be 5 MB or less");
        } else if (rejectedType) {
            setRejectionMessage("Only PNG, JPG, or WEBP files are allowed");
        } else {
            setRejectionMessage(null);
        }
    };

    const removeFile = (indexToRemove: number) => {
        onChange(files.filter((_, index) => index !== indexToRemove));
        setRejectionMessage(null);
    };

    return (
        <div className="col" style={{ gap: 8 }}>
            <div className="row gap-2 wrap" style={{ alignItems: "center" }}>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    iconLeft="plus"
                    disabled={disabled || isAtMaximum}
                    onClick={() => fileInputRef.current?.click()}
                >
                    Add photo
                </Button>
                <span className="text-xs" style={{ color: "var(--ink-3)" }}>
                    {files.length}/{MAXIMUM_PHOTO_COUNT}
                </span>
            </div>

            {files.length > 0 && (
                <div className="row gap-2 wrap">
                    {previewUrls.map((previewUrl, index) => (
                        <div
                            key={previewUrl}
                            style={{
                                position: "relative",
                                width: 72,
                                height: 72,
                                borderRadius: 10,
                                overflow: "hidden",
                                border: "1px solid var(--line-2)",
                            }}
                        >
                            <img
                                src={previewUrl}
                                alt=""
                                style={{ width: "100%", height: "100%", objectFit: "cover" }}
                            />
                            {!disabled && (
                                <button
                                    type="button"
                                    aria-label="Delete photo"
                                    onClick={() => removeFile(index)}
                                    style={{
                                        position: "absolute",
                                        top: 2,
                                        right: 2,
                                        width: 20,
                                        height: 20,
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        borderRadius: "50%",
                                        border: "none",
                                        background: "rgba(0,0,0,0.6)",
                                        color: "#fff",
                                        cursor: "pointer",
                                    }}
                                >
                                    <Icon name="close" size={12} />
                                </button>
                            )}
                        </div>
                    ))}
                </div>
            )}

            {isAtMaximum && (
                <span className="text-xs" style={{ color: "var(--ink-3)" }}>
                    Photo limit reached
                </span>
            )}
            {rejectionMessage && <span className="text-xs text-bad">{rejectionMessage}</span>}

            <input
                ref={fileInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                multiple
                style={{ display: "none" }}
                onChange={(event) => {
                    handleSelection(event.target.files);
                    event.target.value = "";
                }}
            />
        </div>
    );
}
