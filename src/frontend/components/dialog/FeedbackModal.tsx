"use client";

import { DialogFeedback } from "@/lib/hooks/useDialog";

interface FeedbackModalProps {
    feedback: DialogFeedback;
    onClose: () => void;
}

export function FeedbackModal({ feedback, onClose }: FeedbackModalProps) {
    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full max-h-[80vh] flex flex-col">
                <div className="p-6 border-b border-gray-100">
                    <h2 className="text-xl font-bold text-gray-800">
                        Обратная связь
                    </h2>
                    {feedback.xpEarned > 0 && (
                        <div className="mt-2 inline-flex items-center gap-2 px-3 py-1 bg-[#58CC02]/10 rounded-full">
                            <span className="text-[#58CC02] font-bold">+{feedback.xpEarned} XP</span>
                            <span className="text-gray-500 text-sm">заработано</span>
                        </div>
                    )}
                </div>

                <div className="p-6 overflow-y-auto flex-1">
                    <div className="prose prose-sm max-w-none">
                        <p className="whitespace-pre-wrap text-gray-700">
                            {feedback.content}
                        </p>
                    </div>
                </div>

                <div className="p-6 border-t border-gray-100">
                    <button
                        onClick={onClose}
                        className="w-full py-3 bg-[#58CC02] text-white font-bold rounded-2xl hover:bg-[#4CAD02] transition-colors"
                    >
                        Новый диалог
                    </button>
                </div>
            </div>
        </div>
    );
}
