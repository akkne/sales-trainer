"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/shared/components/icon";

/** Persona slug → human label, in dropdown order. Mirrors the backend allow-list. */
const PERSONA_OPTIONS: { value: string; label: string }[] = [
    { value: "sdr", label: "SDR" },
    { value: "account_executive", label: "Account Executive" },
    { value: "account_manager", label: "Account Manager" },
    { value: "founder", label: "Founder" },
    { value: "other", label: "Other" },
];

interface EditProfileModalProps {
    initialName: string;
    initialPersona: string | null;
    /** Current avatar image URL (already cache-busted), or null to show initials. */
    avatarSrc?: string | null;
    avatarInitials: string;
    uploading: boolean;
    uploadError: string | null;
    fileInputRef: React.RefObject<HTMLInputElement | null>;
    onPickPhoto: () => void;
    onFileChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
    isSaving: boolean;
    isError: boolean;
    onSave: (displayName: string, persona: string | null) => void;
    onClose: () => void;
}

export function EditProfileModal({
    initialName,
    initialPersona,
    avatarSrc,
    avatarInitials,
    uploading,
    uploadError,
    fileInputRef,
    onPickPhoto,
    onFileChange,
    isSaving,
    isError,
    onSave,
    onClose,
}: EditProfileModalProps) {
    const [name, setName] = useState(initialName);
    const [persona, setPersona] = useState(initialPersona ?? "");

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") onClose();
        };
        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    const trimmedName = name.trim();
    const canSave = trimmedName.length > 0 && trimmedName.length <= 100 && !isSaving;

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        if (!canSave) return;
        onSave(trimmedName, persona === "" ? null : persona);
    }

    return (
        <div
            className="modal-overlay"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-label="Edit profile"
        >
            <form
                className="modal fade-up"
                style={{ maxWidth: 460 }}
                onClick={(e) => e.stopPropagation()}
                onSubmit={handleSubmit}
            >
                <div className="modal-head">
                    <h3 className="h3" style={{ margin: 0 }}>Edit profile</h3>
                    <button
                        type="button"
                        className="icon-btn"
                        onClick={onClose}
                        aria-label="Close"
                    >
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: 18 }}>
                    {/* Photo */}
                    <div className="epm-photo-row">
                        <div className="pv2-avatar-ring epm-photo-avatar">
                            <div className="pv2-avatar-inner">
                                {avatarSrc ? (
                                    // eslint-disable-next-line @next/next/no-img-element
                                    <img src={avatarSrc} alt={initialName} />
                                ) : (
                                    avatarInitials
                                )}
                                {uploading && (
                                    <div className="pv2-avatar-overlay">
                                        <div className="pv2-spinner" />
                                    </div>
                                )}
                            </div>
                        </div>
                        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                            <button
                                type="button"
                                className="btn btn-outline btn-sm"
                                onClick={uploading ? undefined : onPickPhoto}
                                disabled={uploading}
                            >
                                {uploading ? "Uploading…" : "Change photo"}
                            </button>
                            <span style={{ fontSize: 11.5, color: "var(--ink-4)" }}>
                                PNG, JPG or WebP · up to 5 MB
                            </span>
                            {uploadError && (
                                <span style={{ fontSize: 11.5, color: "var(--heart)" }}>
                                    {uploadError}
                                </span>
                            )}
                        </div>
                        <input
                            ref={fileInputRef}
                            type="file"
                            accept="image/png,image/jpeg,image/webp"
                            style={{ display: "none" }}
                            onChange={onFileChange}
                        />
                    </div>

                    {/* Name */}
                    <label className="epm-field-label">
                        Name
                        <input
                            className="field"
                            type="text"
                            value={name}
                            maxLength={100}
                            placeholder="Your name"
                            onChange={(e) => setName(e.target.value)}
                            autoFocus
                        />
                    </label>

                    {/* Position / persona */}
                    <label className="epm-field-label">
                        Position
                        <select
                            className="field"
                            value={persona}
                            onChange={(e) => setPersona(e.target.value)}
                        >
                            <option value="">Not set</option>
                            {PERSONA_OPTIONS.map((option) => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>
                    </label>

                    {isError && (
                        <p style={{ fontSize: 12, color: "var(--heart)", margin: 0, textAlign: "center" }}>
                            Couldn&apos;t save. Please try again.
                        </p>
                    )}
                </div>

                <div className="modal-foot" style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
                    <button type="button" className="btn btn-ghost" onClick={onClose}>
                        Cancel
                    </button>
                    <button type="submit" className="btn btn-primary" disabled={!canSave}>
                        {isSaving ? "Saving…" : "Save"}
                    </button>
                </div>
            </form>
        </div>
    );
}
