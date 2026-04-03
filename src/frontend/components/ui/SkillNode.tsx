"use client";

import Link from "next/link";
import type { SkillTreeNode } from "@/lib/hooks/useSkillTree";

interface SkillNodeProps {
    skillNode: SkillTreeNode;
    positionClass?: string; // "node-center" | "node-left" | "node-right"
}

export function SkillNode({ skillNode, positionClass = "node-center" }: SkillNodeProps) {
    const isLocked = skillNode.isLocked;
    const isCompleted = skillNode.status === "completed";
    const isActive = skillNode.status === "in_progress" || skillNode.status === "available";

    const progressPercent =
        skillNode.totalLessonCount > 0
            ? Math.round(
                  (skillNode.completedLessonCount / skillNode.totalLessonCount) * 100
              )
            : 0;

    if (isLocked) {
        return (
            <div className={`flex flex-col items-center gap-2 ${positionClass}`}>
                <div
                    className="w-[72px] h-[72px] rounded-full bg-[#F7F7F7] border-4 border-[#E5E5E5] flex items-center justify-center cursor-not-allowed"
                    style={{ boxShadow: "0 4px 0 #E5E5E5" }}
                >
                    <svg className="w-7 h-7 text-[#AFAFAF]" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z" />
                    </svg>
                </div>
                <span className="text-xs font-semibold text-[#AFAFAF] text-center max-w-[88px]">
                    {skillNode.title}
                </span>
            </div>
        );
    }

    const nodeContent = (
        <>
            {/* Outer ping ring for active */}
            {isActive && (
                <span className="absolute inset-0 rounded-full bg-[#58CC02] opacity-20 animate-ping" />
            )}

            <div
                className={`relative w-[72px] h-[72px] rounded-full flex items-center justify-center transition-transform active:translate-y-1 ${
                    isCompleted
                        ? "bg-[#FFC800]"
                        : "bg-[#58CC02]"
                }`}
                style={{
                    boxShadow: isCompleted
                        ? "0 4px 0 #E0A800"
                        : "0 4px 0 #58A700",
                }}
            >
                <span className="text-2xl select-none">{skillNode.iconName || "📚"}</span>

                {/* Gold medal badge (completed) */}
                {isCompleted && (
                    <div
                        className="absolute -top-1 -right-1 w-6 h-6 rounded-full bg-[#FFC800] border-2 border-white flex items-center justify-center"
                        style={{ boxShadow: "0 2px 0 #E0A800" }}
                    >
                        <span className="text-[10px] leading-none">🏅</span>
                    </div>
                )}

                {/* Progress ring (in_progress) */}
                {isActive && skillNode.totalLessonCount > 0 && (
                    <svg
                        className="absolute inset-0 w-full h-full -rotate-90"
                        viewBox="0 0 72 72"
                    >
                        <circle cx="36" cy="36" r="32" fill="none" stroke="rgba(255,255,255,0.3)" strokeWidth="4" />
                        <circle
                            cx="36" cy="36" r="32"
                            fill="none"
                            stroke="white"
                            strokeWidth="4"
                            strokeDasharray={`${(progressPercent / 100) * 201} 201`}
                            strokeLinecap="round"
                        />
                    </svg>
                )}
            </div>

            {/* Popover above active node */}
            {isActive && (
                <div className="absolute bottom-[calc(100%+12px)] left-1/2 -translate-x-1/2 w-48 bg-white rounded-2xl shadow-lg border border-[#E5E5E5] px-4 py-3 z-10">
                    <p className="font-bold text-sm text-gray-900 mb-1 truncate">{skillNode.title}</p>
                    <div className="h-1.5 bg-[#E5E5E5] rounded-full overflow-hidden mb-2">
                        <div
                            className="h-full bg-[#58CC02] rounded-full"
                            style={{ width: `${progressPercent}%` }}
                        />
                    </div>
                    <p className="text-xs text-[#AFAFAF] mb-2">
                        {skillNode.completedLessonCount}/{skillNode.totalLessonCount} уроков
                    </p>
                    {/* Triangle arrow pointing down */}
                    <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 w-4 h-2 overflow-hidden">
                        <div className="w-3 h-3 bg-white border border-[#E5E5E5] rotate-45 translate-y-[-6px] mx-auto" />
                    </div>
                </div>
            )}
        </>
    );

    return (
        <div className={`relative flex flex-col items-center gap-2 ${positionClass}`}>
            <Link
                href={`/skill/${skillNode.slug}`}
                className="relative flex items-center justify-center"
            >
                {nodeContent}
            </Link>
            <span className="text-xs font-semibold text-gray-700 text-center max-w-[88px]">
                {skillNode.title}
            </span>
        </div>
    );
}
