"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

const CONTENT_SOURCE_STALE_TIME_MILLISECONDS = 300_000;

const PUBLISHED_LESSON_VERSION_STATUS = "published";

export interface LessonChoice {
    id: string;
    topicId: string;
    topicIconicName: string;
    topicTitle: string;
    title: string;
    orderInTopic: number;
}

export interface LessonVersionChoice {
    id: string;
    lessonId: string;
    versionNumber: number;
    status: string;
    contentHash: string;
    baseVersionId: string | null;
    isBreaking: boolean;
    createdBy: string | null;
    createdAt: string;
    publishedAt: string | null;
}

export interface DialogBundleChoice {
    id: string;
    title: string;
    skillTitle: string;
    isActive: boolean;
}

export interface DialogModeChoice {
    id: string;
    bundleId: string;
    key: string;
    title: string;
    description: string;
    isActive: boolean;
}

export interface ReferenceMaterialChoice {
    materialId: string;
    title: string;
    category: string | null;
    skillSlug: string;
}

/**
 * `GET /admin/lessons` — the lessons this organization may hand out, already narrowed server-side to
 * its own overrides plus the global library (40.18).
 */
export function useAssignableLessons() {
    return useQuery<LessonChoice[]>({
        queryKey: ["org", "assignment-sources", "lessons"],
        queryFn: () => apiClient.get<LessonChoice[]>("/admin/lessons"),
        staleTime: CONTENT_SOURCE_STALE_TIME_MILLISECONDS,
    });
}

/**
 * The versions of one lesson. An assignment references a frozen `LessonVersions.Id` and never a
 * lesson id — a lesson id silently re-points at whatever the lesson has since become, which is the
 * defect 40.15 froze versions to remove.
 */
export function useLessonVersions(lessonId: string | null) {
    return useQuery<LessonVersionChoice[]>({
        queryKey: ["org", "assignment-sources", "lesson-versions", lessonId],
        queryFn: () => apiClient.get<LessonVersionChoice[]>(`/admin/lessons/${lessonId}/versions`),
        enabled: !!lessonId,
        staleTime: CONTENT_SOURCE_STALE_TIME_MILLISECONDS,
    });
}

/** The newest published version, or null for a lesson that has never been published. */
export function findLatestPublishedVersion(
    versions: LessonVersionChoice[] | undefined
): LessonVersionChoice | null {
    if (!versions) return null;

    const publishedVersions = versions.filter(
        (version) => version.status === PUBLISHED_LESSON_VERSION_STATUS
    );
    if (publishedVersions.length === 0) return null;

    return publishedVersions.reduce((newest, candidate) =>
        candidate.versionNumber > newest.versionNumber ? candidate : newest
    );
}

export function useDialogBundles() {
    return useQuery<DialogBundleChoice[]>({
        queryKey: ["org", "assignment-sources", "dialog-bundles"],
        queryFn: () => apiClient.get<DialogBundleChoice[]>("/dialog/bundles"),
        staleTime: CONTENT_SOURCE_STALE_TIME_MILLISECONDS,
    });
}

/**
 * The modes of one bundle. An assignment references `DialogModeDto.key`, not the mode's row id:
 * `AssignmentContentItemKinds.ReferenceIsIdentifier` says so, and the key is what ai-service resolves.
 */
export function useDialogModes(bundleId: string | null) {
    return useQuery<DialogModeChoice[]>({
        queryKey: ["org", "assignment-sources", "dialog-modes", bundleId],
        queryFn: () => apiClient.get<DialogModeChoice[]>(`/dialog/bundles/${bundleId}/modes`),
        enabled: !!bundleId,
        staleTime: CONTENT_SOURCE_STALE_TIME_MILLISECONDS,
    });
}

export function useReferenceMaterialSearch(searchTerm: string) {
    const trimmedSearchTerm = searchTerm.trim();
    const queryPath =
        trimmedSearchTerm.length === 0
            ? "/reference"
            : `/reference?search=${encodeURIComponent(trimmedSearchTerm)}`;

    return useQuery<ReferenceMaterialChoice[]>({
        queryKey: ["org", "assignment-sources", "reference", trimmedSearchTerm],
        queryFn: () => apiClient.get<ReferenceMaterialChoice[]>(queryPath),
        staleTime: CONTENT_SOURCE_STALE_TIME_MILLISECONDS,
    });
}
