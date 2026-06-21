import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface Friend {
    userId: string;
    displayName: string;
    persona: string | null;
    totalXpAmount: number;
    currentStreakDayCount: number;
    achievementCount: number;
    avatarUrl?: string | null;
}

export interface FriendRequest {
    friendshipId: string;
    userId: string;
    displayName: string;
    persona: string | null;
    direction: "incoming" | "outgoing";
    createdAt: string;
}

export interface UserSearchResult {
    userId: string;
    displayName: string;
    persona: string | null;
    friendshipStatus: "none" | "pending_outgoing" | "pending_incoming" | "friends";
}

export interface PublicProfile {
    userId: string;
    displayName: string;
    persona: string | null;
    totalXpAmount: number;
    currentStreakDayCount: number;
    achievementCount: number;
    averageExerciseScore: number;
    friendshipStatus: "none" | "pending_outgoing" | "pending_incoming" | "friends";
    avatarUrl?: string | null;
}

export interface FriendLeaderboardEntry {
    userId: string;
    displayName: string;
    totalXpAmount: number;
    rank: number;
    isCurrentUser: boolean;
    avatarUrl?: string | null;
}

export interface FriendActivity {
    userId: string;
    displayName: string;
    activityType: string;
    description: string;
    occurredAt: string;
}

export function useFriends() {
    return useQuery({
        queryKey: ["friends"],
        queryFn: () => apiClient.get<Friend[]>("/friends"),
    });
}

export function useFriendRequests() {
    return useQuery({
        queryKey: ["friendRequests"],
        queryFn: () => apiClient.get<FriendRequest[]>("/friends/requests"),
    });
}

export function useUserSearch(query: string) {
    return useQuery({
        queryKey: ["userSearch", query],
        queryFn: () => apiClient.get<UserSearchResult[]>(`/friends/search?query=${encodeURIComponent(query)}`),
        enabled: query.length >= 2,
    });
}

export function usePublicProfile(userId: string) {
    return useQuery({
        queryKey: ["publicProfile", userId],
        queryFn: () => apiClient.get<PublicProfile>(`/friends/profile/${userId}`),
        enabled: !!userId,
    });
}

export function useFriendLeaderboard() {
    return useQuery({
        queryKey: ["friendLeaderboard"],
        queryFn: () => apiClient.get<FriendLeaderboardEntry[]>("/friends/leaderboard"),
    });
}

export function useFriendActivity() {
    return useQuery({
        queryKey: ["friendActivity"],
        queryFn: () => apiClient.get<FriendActivity[]>("/friends/activity"),
    });
}

export function useSendFriendRequest() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (addresseeId: string) =>
            apiClient.post("/friends/requests", { addresseeId }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["friends"] });
            queryClient.invalidateQueries({ queryKey: ["friendRequests"] });
            queryClient.invalidateQueries({ queryKey: ["userSearch"] });
            queryClient.invalidateQueries({ queryKey: ["publicProfile"] });
        },
    });
}

export function useAcceptFriendRequest() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (friendshipId: string) =>
            apiClient.put(`/friends/requests/${friendshipId}/accept`, {}),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["friends"] });
            queryClient.invalidateQueries({ queryKey: ["friendRequests"] });
            queryClient.invalidateQueries({ queryKey: ["friendLeaderboard"] });
        },
    });
}

export function useDeclineFriendRequest() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (friendshipId: string) =>
            apiClient.put(`/friends/requests/${friendshipId}/decline`, {}),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["friendRequests"] });
        },
    });
}

export function useCancelFriendRequest() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (friendshipId: string) =>
            apiClient.delete(`/friends/requests/${friendshipId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["friendRequests"] });
            queryClient.invalidateQueries({ queryKey: ["userSearch"] });
            queryClient.invalidateQueries({ queryKey: ["publicProfile"] });
        },
    });
}

export function useRemoveFriend() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (friendUserId: string) =>
            apiClient.delete(`/friends/${friendUserId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["friends"] });
            queryClient.invalidateQueries({ queryKey: ["friendRequests"] });
            queryClient.invalidateQueries({ queryKey: ["friendLeaderboard"] });
        },
    });
}
