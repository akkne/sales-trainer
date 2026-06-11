import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type { DiscussTag, DiscussThreadSummary, PagedResult } from "./use-discuss";

export function useAdminDiscussThreads(page = 1, search = "") {
    const query = new URLSearchParams({ page: String(page), pageSize: "20" });
    if (search) query.set("search", search);
    return useQuery({
        queryKey: ["admin", "discuss", "threads", { page, search }],
        queryFn: () => apiClient.get<PagedResult<DiscussThreadSummary>>(`/admin/discuss/threads?${query}`),
    });
}

export function useAdminDiscussTags() {
    return useQuery({
        queryKey: ["admin", "discuss", "tags"],
        queryFn: () => apiClient.get<DiscussTag[]>("/admin/discuss/tags"),
    });
}

function useThreadInvalidation() {
    const queryClient = useQueryClient();
    return () => {
        queryClient.invalidateQueries({ queryKey: ["admin", "discuss", "threads"] });
        queryClient.invalidateQueries({ queryKey: ["discuss", "threads"] });
    };
}

export function useDeleteThread() {
    const invalidate = useThreadInvalidation();
    return useMutation({
        mutationFn: (threadId: string) => apiClient.delete(`/admin/discuss/threads/${threadId}`),
        onSuccess: invalidate,
    });
}

export function useSetThreadPin() {
    const invalidate = useThreadInvalidation();
    return useMutation({
        mutationFn: ({ threadId, isPinned }: { threadId: string; isPinned: boolean }) =>
            apiClient.post(`/admin/discuss/threads/${threadId}/pin`, { isPinned }),
        onSuccess: invalidate,
    });
}

export function useSetThreadHot() {
    const invalidate = useThreadInvalidation();
    return useMutation({
        mutationFn: ({ threadId, isHot }: { threadId: string; isHot: boolean }) =>
            apiClient.post(`/admin/discuss/threads/${threadId}/hot`, { isHot }),
        onSuccess: invalidate,
    });
}

function useTagInvalidation() {
    const queryClient = useQueryClient();
    return () => {
        queryClient.invalidateQueries({ queryKey: ["admin", "discuss", "tags"] });
        queryClient.invalidateQueries({ queryKey: ["discuss", "tags"] });
    };
}

export function useCreateTag() {
    const invalidate = useTagInvalidation();
    return useMutation({
        mutationFn: (request: { name: string; slug?: string }) =>
            apiClient.post<DiscussTag>("/admin/discuss/tags", request),
        onSuccess: invalidate,
    });
}

export function useUpdateTag() {
    const invalidate = useTagInvalidation();
    return useMutation({
        mutationFn: ({ tagId, request }: { tagId: string; request: { name?: string; slug?: string } }) =>
            apiClient.put<DiscussTag>(`/admin/discuss/tags/${tagId}`, request),
        onSuccess: invalidate,
    });
}

export function useDeleteTag() {
    const invalidate = useTagInvalidation();
    return useMutation({
        mutationFn: (tagId: string) => apiClient.delete(`/admin/discuss/tags/${tagId}`),
        onSuccess: invalidate,
    });
}
