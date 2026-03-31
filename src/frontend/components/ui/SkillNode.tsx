"use client";

import Link from "next/link";
import type { SkillTreeNode } from "@/lib/hooks/useSkillTree";

interface SkillNodeProps {
    skillNode: SkillTreeNode;
    positionIndex: number;
}

const HORIZONTAL_OFFSETS = ["ml-0", "ml-12", "ml-20", "ml-12", "ml-0"];

export function SkillNode({ skillNode, positionIndex }: SkillNodeProps) {
    const horizontalOffsetClass =
        HORIZONTAL_OFFSETS[positionIndex % HORIZONTAL_OFFSETS.length];

    const progressPercent =
        skillNode.totalLessonCount > 0
            ? Math.round(
                  (skillNode.completedLessonCount / skillNode.totalLessonCount) * 100
              )
            : 0;

    if (skillNode.isLocked) {
        return (
            <div className={`flex flex-col items-center gap-2 ${horizontalOffsetClass}`}>
                <div className="w-[70px] h-[70px] rounded-full bg-gray-200 flex items-center justify-center shadow-[0_4px_0_#D1D5DB]">
                    <span className="text-2xl grayscale opacity-50">🔒</span>
                </div>
                <span className="text-sm font-semibold text-gray-400 text-center max-w-[90px]">
                    {skillNode.title}
                </span>
            </div>
        );
    }

    return (
        <Link
            href={`/skill/${skillNode.slug}`}
            className={`flex flex-col items-center gap-2 ${horizontalOffsetClass} group`}
        >
            <div
                className={`relative w-[70px] h-[70px] rounded-full flex items-center justify-center transition-transform active:translate-y-1 ${
                    skillNode.status === "completed"
                        ? "bg-[#58CC02] shadow-[0_4px_0_#4CAD00]"
                        : "bg-white border-4 border-[#58CC02] shadow-[0_4px_0_#4CAD00]"
                }`}
            >
                <span className="text-2xl">{skillNode.iconName || "📚"}</span>

                {skillNode.status === "completed" && (
                    <div className="absolute -top-1 -right-1 w-5 h-5 rounded-full bg-[#FFC800] flex items-center justify-center">
                        <span className="text-xs">✓</span>
                    </div>
                )}

                {skillNode.status === "in_progress" && skillNode.totalLessonCount > 0 && (
                    <svg
                        className="absolute inset-0 w-full h-full -rotate-90"
                        viewBox="0 0 70 70"
                    >
                        <circle
                            cx="35"
                            cy="35"
                            r="31"
                            fill="none"
                            stroke="#E5E5E5"
                            strokeWidth="4"
                        />
                        <circle
                            cx="35"
                            cy="35"
                            r="31"
                            fill="none"
                            stroke="#58CC02"
                            strokeWidth="4"
                            strokeDasharray={`${(progressPercent / 100) * 195} 195`}
                            strokeLinecap="round"
                        />
                    </svg>
                )}
            </div>

            <span className="text-sm font-semibold text-gray-700 text-center max-w-[90px] group-hover:text-[#58CC02] transition-colors">
                {skillNode.title}
            </span>
        </Link>
    );
}
