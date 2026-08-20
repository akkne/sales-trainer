"use client";

import { useParams, useRouter } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { usePublicProfile } from "@/features/friends/hooks/use-friends";
import { useCreateConversation } from "@/features/friends/hooks/use-chat";
import { FriendshipButton } from "@/features/friends/components/friendship-button";
import { ApiError } from "@/shared/api/api-client";
import { ErrorState } from "@/shared/components/error-state";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Основатель",
    other: "Другое",
};

export default function PublicProfilePage() {
    const params = useParams<{ userId: string }>();
    const router = useRouter();
    const { data: profile, isLoading, error, refetch } = usePublicProfile(params.userId);
    const createConversationMutation = useCreateConversation();

    function handleChatClick() {
        createConversationMutation.mutate(params.userId, {
            onSuccess: (conversation) => {
                router.push(`/friends?conv=${conversation.conversationId}`);
            },
        });
    }

    if (isLoading) {
        return (
            <div className="frd-profile">
                <div className="frd-profile-cover" />
                <div className="frd-profile-body" style={{ paddingTop: 56 }}>
                    <div
                        className="frd-skeleton"
                        style={{ height: 22, width: 160, borderRadius: 8, marginBottom: 8 }}
                    />
                    <div
                        className="frd-skeleton"
                        style={{ height: 14, width: 100, borderRadius: 6 }}
                    />
                </div>
            </div>
        );
    }

    if (!profile) {
        // E-10: any failure of GET /friends/profile/{id} used to render "Пользователь не
        // найден" — a claim about the person, not about the request. Only a genuine 404 means
        // that; anything else gets the shared error state with a retry.
        const isNotFound = error instanceof ApiError && error.status === 404;
        if (isNotFound) {
            return (
                <div className="frd-profile">
                    <div className="frd-empty" style={{ paddingTop: 80 }}>
                        <div className="frd-empty-icon">
                            <Icon name="user" size={20} />
                        </div>
                        <p className="frd-empty-title">Пользователь не найден</p>
                    </div>
                </div>
            );
        }

        return (
            <div className="frd-profile">
                <div style={{ paddingTop: 80 }}>
                    <ErrorState
                        title="Не удалось загрузить профиль"
                        onRetry={() => refetch()}
                    />
                </div>
            </div>
        );
    }

    return (
        <div className="frd-profile">
            {/* Back button */}
            <div style={{ padding: "14px 28px 0" }}>
                <button
                    onClick={() => router.back()}
                    className="frd-rail-back"
                    style={{ width: "auto", padding: "0 12px", borderRadius: 9, gap: 6, display: "inline-flex", alignItems: "center", height: 32, fontSize: 13, fontWeight: 600, color: "var(--ink-3)" }}
                    aria-label="Назад"
                >
                    <Icon name="arrow-left" size={15} />
                    Назад
                </button>
            </div>

            {/* Cover band */}
            <div className="frd-profile-cover" style={{ margin: "14px 0 0" }} />

            {/* Identity row — pulled up over the cover */}
            <div className="frd-profile-identity">
                <div className="frd-profile-avatar-ring">
                    <GeoAvatar seed={profile.displayName} size={86} />
                </div>
                <div style={{ flex: 1, minWidth: 0, paddingTop: 48 }}>
                    <p className="frd-profile-name">{profile.displayName}</p>
                    {profile.persona && (
                        <p className="frd-profile-role">
                            {PERSONA_LABELS[profile.persona] ?? profile.persona}
                        </p>
                    )}
                </div>
            </div>

            {/* Body */}
            <div className="frd-profile-body">
                {/*
                  * No statistics block. `averageExerciseScore` on this DTO is a hard-coded zero:
                  * social-service owns friendships, not lesson scores, so the tile read "Средний
                  * балл 0%" for every person on the platform. Publishing another learner's real
                  * score is a product and privacy decision nobody has taken, so the tile is gone
                  * rather than wired up.
                  */}
                <p className="frd-profile-section">Действия</p>
                <div className="frd-profile-actions">
                    <FriendshipButton
                        userId={profile.userId}
                        friendshipStatus={profile.friendshipStatus}
                        friendshipId={profile.friendshipId ?? undefined}
                        fullWidth
                    />

                    {profile.friendshipStatus === "friends" && (
                        <button
                            onClick={handleChatClick}
                            disabled={createConversationMutation.isPending}
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                gap: 8,
                                height: 40,
                                background: "var(--surface-3)",
                                border: "1px solid var(--line)",
                                borderRadius: 10,
                                fontSize: 13,
                                fontWeight: 700,
                                color: "var(--ink-3)",
                                cursor: "pointer",
                                transition: "background var(--transition)",
                                width: "100%",
                            }}
                            aria-label="Сообщение"
                        >
                            <Icon name="message" size={16} />
                            {createConversationMutation.isPending ? "…" : "Написать"}
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
}
