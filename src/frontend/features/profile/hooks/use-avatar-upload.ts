import { useState, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

interface AvatarUploadResult {
    avatarUrl: string;
}

export interface UseAvatarUploadReturn {
    version: number;
    uploading: boolean;
    uploadError: string | null;
    fileInputRef: React.RefObject<HTMLInputElement | null>;
    openFilePicker: () => void;
    handleFileChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
}

export function useAvatarUpload(): UseAvatarUploadReturn {
    const [version, setVersion] = useState(0);
    const [uploading, setUploading] = useState(false);
    const [uploadError, setUploadError] = useState<string | null>(null);
    const fileInputRef = useRef<HTMLInputElement | null>(null);
    const queryClient = useQueryClient();

    function openFilePicker() {
        fileInputRef.current?.click();
    }

    async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        if (!file) return;

        // Reset so the same file can be re-selected if needed
        e.target.value = "";

        setUploadError(null);
        setUploading(true);

        try {
            const formData = new FormData();
            formData.append("file", file);
            await apiClient.postFile<AvatarUploadResult>("/avatars", formData);
            setVersion((v) => v + 1);
            await queryClient.invalidateQueries({ queryKey: ["profile"] });
        } catch (err) {
            setUploadError(
                err instanceof Error ? err.message : "Failed to upload photo"
            );
        } finally {
            setUploading(false);
        }
    }

    return { version, uploading, uploadError, fileInputRef, openFilePicker, handleFileChange };
}
