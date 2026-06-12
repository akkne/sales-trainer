"use client";

import { Icon } from "@/shared/components/icon";
import { resolveDiscussPhotoUrl } from "../utils/resolve-discuss-photo-url";
import type { DiscussPhoto } from "../hooks/use-discuss";

interface PhotoGalleryProps {
    photos: DiscussPhoto[];
    canDelete?: boolean;
    onDelete?: (photoId: string) => void;
    deleteDisabled?: boolean;
}

export function PhotoGallery({ photos, canDelete = false, onDelete, deleteDisabled = false }: PhotoGalleryProps) {
    if (photos.length === 0) return null;

    const orderedPhotos = [...photos].sort((first, second) => first.orderIndex - second.orderIndex);

    return (
        <div
            style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))",
                gap: 8,
                marginTop: 12,
            }}
        >
            {orderedPhotos.map((photo) => {
                const absoluteUrl = resolveDiscussPhotoUrl(photo.url);
                return (
                    <div
                        key={photo.id}
                        style={{
                            position: "relative",
                            aspectRatio: "1 / 1",
                            borderRadius: 10,
                            overflow: "hidden",
                            border: "1px solid var(--line-2)",
                        }}
                    >
                        <a href={absoluteUrl} target="_blank" rel="noopener noreferrer">
                            <img
                                src={absoluteUrl}
                                alt=""
                                loading="lazy"
                                style={{ width: "100%", height: "100%", objectFit: "cover", display: "block" }}
                            />
                        </a>
                        {canDelete && onDelete && (
                            <button
                                type="button"
                                aria-label="Удалить фото"
                                disabled={deleteDisabled}
                                onClick={() => onDelete(photo.id)}
                                style={{
                                    position: "absolute",
                                    top: 4,
                                    right: 4,
                                    width: 24,
                                    height: 24,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    borderRadius: "50%",
                                    border: "none",
                                    background: "rgba(0,0,0,0.6)",
                                    color: "#fff",
                                    cursor: deleteDisabled ? "not-allowed" : "pointer",
                                }}
                            >
                                <Icon name="close" size={14} />
                            </button>
                        )}
                    </div>
                );
            })}
        </div>
    );
}
