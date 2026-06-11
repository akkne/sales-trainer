import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface TagRef {
    slug: string;
    name: string;
}

export interface DiscussThreadSummary {
    id: string;
    title: string;
    bodyPreview: string;
    authorId: string;
    authorName: string;
    upvoteCount: number;
    replyCount: number;
    viewCount: number;
    isPinned: boolean;
    isHot: boolean;
    isSolved: boolean;
    tags: TagRef[];
    createdAt: string;
    lastActivityAt: string;
    viewerHasUpvoted: boolean;
}

export interface DiscussReply {
    id: string;
    threadId: string;
    authorId: string;
    authorName: string;
    body: string;
    upvoteCount: number;
    isAccepted: boolean;
    createdAt: string;
    viewerHasUpvoted: boolean;
}

export interface DiscussThreadDetail {
    id: string;
    title: string;
    body: string;
    authorId: string;
    authorName: string;
    upvoteCount: number;
    replyCount: number;
    viewCount: number;
    isPinned: boolean;
    isHot: boolean;
    isSolved: boolean;
    acceptedReplyId: string | null;
    tags: TagRef[];
    createdAt: string;
    lastActivityAt: string;
    viewerHasUpvoted: boolean;
    replies: DiscussReply[];
}

export interface DiscussTag {
    id: string;
    slug: string;
    name: string;
    isCurated: boolean;
}

export interface PopularTag {
    slug: string;
    name: string;
    threadCount: number;
}

export interface TopAuthor {
    authorId: string;
    authorName: string;
    upvotesReceived: number;
}

export interface DiscussStats {
    totalThreads: number;
    totalReplies: number;
    topAuthorsOfWeek: TopAuthor[];
}

export interface VoteResult {
    upvoteCount: number;
    hasUpvoted: boolean;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

export type DiscussSort = "hot" | "new" | "unanswered";

export interface ThreadListParams {
    sort?: DiscussSort;
    search?: string;
    tag?: string;
    page?: number;
    pageSize?: number;
}

function buildThreadQuery(params: ThreadListParams): string {
    const search = new URLSearchParams();
    search.set("sort", params.sort ?? "hot");
    if (params.search) search.set("search", params.search);
    if (params.tag) search.set("tag", params.tag);
    search.set("page", String(params.page ?? 1));
    search.set("pageSize", String(params.pageSize ?? 20));
    return search.toString();
}

export function useDiscussThreads(params: ThreadListParams) {
    return useQuery({
        queryKey: ["discuss", "threads", params],
        queryFn: () =>
            apiClient.get<PagedResult<DiscussThreadSummary>>(`/discuss/threads?${buildThreadQuery(params)}`),
    });
}

export function useDiscussThread(threadId: string | null) {
    return useQuery({
        queryKey: ["discuss", "threads", threadId],
        queryFn: () => apiClient.get<DiscussThreadDetail>(`/discuss/threads/${threadId}`),
        enabled: !!threadId,
    });
}

export function useDiscussTags(curatedOnly = false) {
    return useQuery({
        queryKey: ["discuss", "tags", { curatedOnly }],
        queryFn: () => apiClient.get<DiscussTag[]>(`/discuss/tags?curatedOnly=${curatedOnly}`),
    });
}

export function usePopularTags(limit = 10) {
    return useQuery({
        queryKey: ["discuss", "tags", "popular", limit],
        queryFn: () => apiClient.get<PopularTag[]>(`/discuss/tags/popular?limit=${limit}`),
    });
}

export function useDiscussStats() {
    return useQuery({
        queryKey: ["discuss", "stats"],
        queryFn: () => apiClient.get<DiscussStats>("/discuss/stats"),
    });
}

export function useCreateThread() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (request: { title: string; body: string; tags: string[] }) =>
            apiClient.post<DiscussThreadDetail>("/discuss/threads", request),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads"] });
            queryClient.invalidateQueries({ queryKey: ["discuss", "stats"] });
            queryClient.invalidateQueries({ queryKey: ["discuss", "tags"] });
        },
    });
}

export function useAddReply(threadId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: string) =>
            apiClient.post<DiscussReply>(`/discuss/threads/${threadId}/replies`, { body }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads", threadId] });
            queryClient.invalidateQueries({ queryKey: ["discuss", "stats"] });
        },
    });
}

export function useThreadVote(threadId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (upvote: boolean) =>
            upvote
                ? apiClient.post<VoteResult>(`/discuss/threads/${threadId}/upvote`, {})
                : apiClient.delete<VoteResult>(`/discuss/threads/${threadId}/upvote`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads", threadId] });
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads"] });
        },
    });
}

export function useReplyVote(threadId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ replyId, upvote }: { replyId: string; upvote: boolean }) =>
            upvote
                ? apiClient.post<VoteResult>(`/discuss/replies/${replyId}/upvote`, {})
                : apiClient.delete<VoteResult>(`/discuss/replies/${replyId}/upvote`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads", threadId] });
        },
    });
}

export function useSetAcceptedReply(threadId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (replyId: string | null) =>
            replyId
                ? apiClient.post<DiscussThreadDetail>(`/discuss/threads/${threadId}/accepted-reply`, { replyId })
                : apiClient.delete<DiscussThreadDetail>(`/discuss/threads/${threadId}/accepted-reply`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads", threadId] });
            queryClient.invalidateQueries({ queryKey: ["discuss", "threads"] });
        },
    });
}
