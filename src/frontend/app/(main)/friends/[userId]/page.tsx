"use client";

import { useParams, useRouter } from "next/navigation";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { usePublicProfile } from "@/lib/hooks/useFriends";
import { useCreateConversation } from "@/lib/hooks/useChat";
import { FriendshipButton } from "@/components/friends/FriendshipButton";

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
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    if (!profile) {
        return (
            <div className="max-w-2xl mx-auto px-4 py-8 text-center">
                <p className="text-on-surface-variant">Пользователь не найден</p>
            </div>
        );
    }

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Back button */}
            <button
                onClick={() => router.back()}
                className="flex items-center gap-1 text-on-surface-variant hover:text-on-surface tonal-transition mb-6"
            >
                <Icon name="arrow_back" size="md" />
                <span className="text-sm font-medium">Назад</span>
            </button>

            {/* Header */}
            <div className="flex items-center justify-between mb-8">
                <div>
                    <h1 className="font-headline font-bold text-2xl text-on-surface">
                        {profile.displayName}
                    </h1>
                    {profile.persona && (
                        <span className="inline-flex items-center gap-1 mt-2 px-3 py-1 rounded-full bg-primary-container text-primary text-xs font-semibold">
                            <Icon name="badge" size="sm" />
                            {PERSONA_LABELS[profile.persona] ?? profile.persona}
                        </span>
                    )}
                </div>
                <div className="w-16 h-16 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-2xl ring-4 ring-primary-container">
                    {profile.displayName[0]?.toUpperCase()}
                </div>
            </div>

            {/* Stats grid */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-error-container flex items-center justify-center mb-2">
                        <Icon name="local_fire_department" size="md" className="text-error" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-on-surface">
                        {profile.currentStreakDayCount}
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">
                        Стрик
                    </span>
                </div>

                <div className="bg-primary-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-primary flex items-center justify-center mb-2">
                        <Icon name="bolt" size="md" className="text-on-primary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-primary">
                        {profile.totalXpAmount}
                    </span>
                    <span className="text-xs text-on-primary-container uppercase tracking-wider">
                        Всего XP
                    </span>
                </div>

                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center mb-2">
                        <Icon name="emoji_events" size="md" className="text-secondary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-on-surface">
                        {profile.achievementCount}
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">
                        Достижения
                    </span>
                </div>

                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-tertiary-container flex items-center justify-center mb-2">
                        <Icon name="target" size="md" className="text-tertiary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-tertiary">
                        {profile.averageExerciseScore}%
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">
                        Средний балл
                    </span>
                </div>
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
                        iconLeft="chat"
                        loading={createConversationMutation.isPending}
                        onClick={handleChatClick}
                    >
                        Написать
                    </Button>
                )}
            </div>
        </div>
    );
}
