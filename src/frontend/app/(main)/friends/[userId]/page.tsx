"use client";

import { useParams, useRouter } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { Chip } from "@/shared/components/chip";
import { StatTile } from "@/shared/components/stat-tile";
import { usePublicProfile } from "@/features/friends/hooks/use-friends";
import { useCreateConversation } from "@/features/friends/hooks/use-chat";
import { FriendshipButton } from "@/features/friends/components/friendship-button";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Founder",
    other: "Other",
};

export default function PublicProfilePage() {
    const params = useParams<{ userId: string }>();
    const router = useRouter();
    const { data: profile, isLoading } = usePublicProfile(params.userId);
    const createConversationMutation = useCreateConversation();

    function handleChatClick() {
        createConversationMutation.mutate(params.userId, {
            onSuccess: (conversation) => {
                router.push(`/friends?tab=chats&conv=${conversation.conversationId}`);
            },
        });
    }

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-[50vh]">
                <div
                    className="w-10 h-10 rounded-full border-4 border-t-transparent animate-spin"
                    style={{ borderColor: "var(--ink)", borderTopColor: "transparent" }}
                />
            </div>
        );
    }

    if (!profile) {
        return (
            <div className="max-w-2xl mx-auto px-4 py-8 text-center">
                <p className="text-ink-4">Пользователь не найден</p>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-bg pb-20">
            {/* Header band */}
            <div className="bg-surface border-b border-line px-6 py-5 md:px-8">
                <div className="max-w-2xl mx-auto">
                    <button
                        onClick={() => router.back()}
                        className="flex items-center gap-1 text-ink-4 hover:text-ink transition-colors mb-4"
                    >
                        <Icon name="arrow-left" size="md" />
                        <span className="text-sm font-medium">Назад</span>
                    </button>

                    <div className="flex items-center gap-2 mb-2">
                        <span className="text-[10px] font-mono tracking-[2px] uppercase text-indigo">
                            ПРОФИЛЬ
                        </span>
                    </div>

                    <div className="flex items-center gap-4">
                        <GeoAvatar seed={profile.displayName} size={64} />
                        <div className="min-w-0 flex-1">
                            <h1 className="text-2xl font-medium tracking-tight text-ink truncate">
                                {profile.displayName}
                            </h1>
                            {profile.persona && (
                                <div className="mt-2">
                                    <Chip tone="indigo" icon={<Icon name="star" size="xs" />}>
                                        {PERSONA_LABELS[profile.persona] ?? profile.persona}
                                    </Chip>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            <div className="max-w-2xl mx-auto px-4 md:px-6 py-6">
                {/* Stats grid */}
                <h3 className="text-xs font-mono tracking-[1px] uppercase text-ink-4 mb-3">
                    Показатели
                </h3>
                <div className="grid grid-cols-2 gap-3 mb-6">
                    <StatTile
                        label="Стрик"
                        value={profile.currentStreakDayCount}
                        unit="дн"
                        tone="rust"
                        icon={<Icon name="flame" size="xs" />}
                    />
                    <StatTile
                        label="Всего XP"
                        value={profile.totalXpAmount}
                        tone="indigo"
                        icon={<Icon name="bolt" size="xs" />}
                    />
                    <StatTile
                        label="Достижения"
                        value={profile.achievementCount}
                        tone="olive"
                        icon={<Icon name="trophy" size="xs" />}
                    />
                    <StatTile
                        label="Средний балл"
                        value={profile.averageExerciseScore}
                        unit="%"
                        tone="neutral"
                        icon={<Icon name="target" size="xs" />}
                    />
                </div>

                {/* Actions */}
                <div className="flex flex-col gap-3">
                    <FriendshipButton
                        userId={profile.userId}
                        friendshipStatus={profile.friendshipStatus}
                    />

                    {profile.friendshipStatus === "friends" && (
                        <Button
                            variant="secondary"
                            fullWidth
                            iconLeft="message"
                            loading={createConversationMutation.isPending}
                            onClick={handleChatClick}
                        >
                            Написать
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}
