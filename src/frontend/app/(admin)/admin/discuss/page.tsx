"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminDiscussTags,
    useAdminDiscussThreads,
    useCreateTag,
    useDeleteTag,
    useDeleteThread,
    useSetThreadHot,
    useSetThreadPin,
    useUpdateTag,
} from "@/features/discuss/hooks/use-admin-discuss";

export default function AdminDiscussPage() {
    const [page, setPage] = useState(1);
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");

    const { data: threads, isLoading, error } = useAdminDiscussThreads(page, search);
    const { data: tags } = useAdminDiscussTags();

    const deleteThread = useDeleteThread();
    const setPin = useSetThreadPin();
    const setHot = useSetThreadHot();
    const createTag = useCreateTag();
    const updateTag = useUpdateTag();
    const deleteTag = useDeleteTag();

    const [deletingThreadId, setDeletingThreadId] = useState<string | null>(null);
    const [newTagName, setNewTagName] = useState("");
    const [tagError, setTagError] = useState<string | null>(null);
    const [editingTagId, setEditingTagId] = useState<string | null>(null);
    const [editingTagName, setEditingTagName] = useState("");

    const handleCreateTag = async () => {
        if (!newTagName.trim()) return;
        setTagError(null);
        try {
            await createTag.mutateAsync({ name: newTagName.trim() });
            setNewTagName("");
        } catch (createError) {
            setTagError(createError instanceof Error ? createError.message : "Failed to create tag");
        }
    };

    const handleSaveTag = async (tagId: string) => {
        if (!editingTagName.trim()) return;
        await updateTag.mutateAsync({ tagId, request: { name: editingTagName.trim() } });
        setEditingTagId(null);
        setEditingTagName("");
    };

    return (
        <div className="p-6 space-y-10">
            {/* Threads moderation */}
            <section>
                <div className="flex justify-between items-center mb-6">
                    <h1 className="text-xl font-bold text-ink">Discuss — Threads</h1>
                    <form
                        className="flex gap-2"
                        onSubmit={(event) => {
                            event.preventDefault();
                            setPage(1);
                            setSearch(searchInput.trim());
                        }}
                    >
                        <input
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                            placeholder="Search threads…"
                            className="px-3 py-2 border border-line rounded-xl bg-surface text-ink text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                        <button className="px-4 py-2 bg-ink text-bg rounded-lg hover:opacity-90 text-sm">Search</button>
                    </form>
                </div>

                {isLoading && <p className="text-ink-3">Loading...</p>}
                {error && <p className="text-bad">Error: {error.message}</p>}

                {threads && (
                    <div className="bg-surface rounded-2xl overflow-hidden">
                        <table className="w-full">
                            <thead className="bg-surface">
                                <tr>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Title</th>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Author</th>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Votes</th>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Replies</th>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Flags</th>
                                    <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Actions</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-line">
                                {threads.items.map((thread) => (
                                    <tr key={thread.id} className="hover:bg-bg-2">
                                        <td className="px-4 py-3">
                                            <Link href={`/discuss/${thread.id}`} className="text-indigo hover:underline font-medium">
                                                {thread.title}
                                            </Link>
                                        </td>
                                        <td className="px-4 py-3 text-ink-3 text-sm">{thread.authorName}</td>
                                        <td className="px-4 py-3 text-ink-3">{thread.upvoteCount}</td>
                                        <td className="px-4 py-3 text-ink-3">{thread.replyCount}</td>
                                        <td className="px-4 py-3 text-xs text-ink-3 space-x-1">
                                            {thread.isPinned && <span className="px-2 py-1 rounded-full bg-indigo-soft text-indigo">Pinned</span>}
                                            {thread.isHot && <span className="px-2 py-1 rounded-full bg-bg-2">Hot</span>}
                                            {thread.isSolved && <span className="px-2 py-1 rounded-full bg-bg-2">Solved</span>}
                                        </td>
                                        <td className="px-4 py-3">
                                            <div className="flex gap-3 text-sm">
                                                <button
                                                    onClick={() => setPin.mutate({ threadId: thread.id, isPinned: !thread.isPinned })}
                                                    className="text-indigo hover:underline"
                                                >
                                                    {thread.isPinned ? "Unpin" : "Pin"}
                                                </button>
                                                <button
                                                    onClick={() => setHot.mutate({ threadId: thread.id, isHot: !thread.isHot })}
                                                    className="text-indigo hover:underline"
                                                >
                                                    {thread.isHot ? "Unhot" : "Hot"}
                                                </button>
                                                <button
                                                    onClick={() => setDeletingThreadId(thread.id)}
                                                    className="text-bad hover:underline"
                                                >
                                                    Delete
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                                {threads.items.length === 0 && (
                                    <tr>
                                        <td colSpan={6} className="px-4 py-8 text-center text-ink-3">No threads yet.</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                )}

                {threads && threads.totalCount > threads.pageSize && (
                    <div className="flex gap-2 mt-4 items-center text-sm">
                        <button
                            disabled={page <= 1}
                            onClick={() => setPage((current) => current - 1)}
                            className="px-3 py-1.5 bg-bg-2 rounded-lg disabled:opacity-40"
                        >
                            Prev
                        </button>
                        <span className="text-ink-3">Page {threads.page}</span>
                        <button
                            disabled={page * threads.pageSize >= threads.totalCount}
                            onClick={() => setPage((current) => current + 1)}
                            className="px-3 py-1.5 bg-bg-2 rounded-lg disabled:opacity-40"
                        >
                            Next
                        </button>
                    </div>
                )}
            </section>

            {/* Tag catalog */}
            <section>
                <h2 className="text-lg font-bold text-ink mb-4">Tag Catalog</h2>
                <div className="flex gap-2 mb-4">
                    <input
                        value={newTagName}
                        onChange={(event) => setNewTagName(event.target.value)}
                        placeholder="New curated tag name…"
                        className="px-3 py-2 border border-line rounded-xl bg-surface text-ink text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        onKeyDown={(event) => event.key === "Enter" && handleCreateTag()}
                    />
                    <button
                        onClick={handleCreateTag}
                        disabled={createTag.isPending}
                        className="px-4 py-2 bg-ink text-bg rounded-lg hover:opacity-90 disabled:opacity-40 text-sm"
                    >
                        + Add Tag
                    </button>
                </div>
                {tagError && <p className="text-bad text-sm mb-3">{tagError}</p>}

                <div className="bg-surface rounded-2xl overflow-hidden">
                    <table className="w-full">
                        <thead className="bg-surface">
                            <tr>
                                <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Name</th>
                                <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Slug</th>
                                <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Type</th>
                                <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-line">
                            {tags?.map((tag) => (
                                <tr key={tag.id} className="hover:bg-bg-2">
                                    <td className="px-4 py-3">
                                        {editingTagId === tag.id ? (
                                            <input
                                                value={editingTagName}
                                                onChange={(event) => setEditingTagName(event.target.value)}
                                                className="px-2 py-1 border border-line rounded-lg bg-surface text-ink text-sm"
                                            />
                                        ) : (
                                            <span className="text-ink font-medium">{tag.name}</span>
                                        )}
                                    </td>
                                    <td className="px-4 py-3 text-ink-3 text-sm font-mono">{tag.slug}</td>
                                    <td className="px-4 py-3 text-xs">
                                        <span className={`px-2 py-1 rounded-full ${tag.isCurated ? "bg-indigo-soft text-indigo" : "bg-bg-2 text-ink-3"}`}>
                                            {tag.isCurated ? "Curated" : "User"}
                                        </span>
                                    </td>
                                    <td className="px-4 py-3">
                                        <div className="flex gap-3 text-sm">
                                            {editingTagId === tag.id ? (
                                                <>
                                                    <button onClick={() => handleSaveTag(tag.id)} className="text-indigo hover:underline">Save</button>
                                                    <button onClick={() => setEditingTagId(null)} className="text-ink-3 hover:underline">Cancel</button>
                                                </>
                                            ) : (
                                                <>
                                                    <button
                                                        onClick={() => {
                                                            setEditingTagId(tag.id);
                                                            setEditingTagName(tag.name);
                                                        }}
                                                        className="text-indigo hover:underline"
                                                    >
                                                        Edit
                                                    </button>
                                                    <button onClick={() => deleteTag.mutate(tag.id)} className="text-bad hover:underline">Delete</button>
                                                </>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                            {(!tags || tags.length === 0) && (
                                <tr>
                                    <td colSpan={4} className="px-4 py-8 text-center text-ink-3">No tags yet.</td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </section>

            {deletingThreadId && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
                    <div className="bg-surface rounded-2xl p-6 max-w-sm">
                        <h3 className="text-lg font-semibold mb-2">Delete Thread?</h3>
                        <p className="text-ink-3 mb-4">This deletes the thread, its replies, tags links and votes. Cannot be undone.</p>
                        <div className="flex gap-2">
                            <button
                                onClick={async () => {
                                    await deleteThread.mutateAsync(deletingThreadId);
                                    setDeletingThreadId(null);
                                }}
                                disabled={deleteThread.isPending}
                                className="px-4 py-2 bg-bad text-white rounded-lg hover:opacity-90 disabled:opacity-40"
                            >
                                Delete
                            </button>
                            <button onClick={() => setDeletingThreadId(null)} className="px-4 py-2 bg-bg-2 text-ink-3 rounded-lg">Cancel</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
