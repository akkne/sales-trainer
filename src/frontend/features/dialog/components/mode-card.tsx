import { DialogMode } from "@/features/dialog/hooks/use-dialog";
import { trackEvent } from "@/shared/analytics/track";
import Link from "next/link";

interface ModeCardProps {
    bundleId: string;
    mode: DialogMode;
}

export function ModeCard({ bundleId, mode }: ModeCardProps) {
    return (
        <div className="p-4 border-2 border-gray-200 rounded-2xl bg-white">
            <div className="flex items-start justify-between mb-3">
                <div className="flex-1">
                    <h3 className="font-bold text-lg text-gray-800">
                        {mode.title}
                    </h3>
                    <p className="text-sm text-gray-500 mt-1 line-clamp-2">
                        {mode.description}
                    </p>
                </div>
            </div>
            <div className="flex gap-2">
                <Link
                    href={`/dialog/${bundleId}/${mode.id}?mode=text`}
                    onClick={() => trackEvent("start_dialog", "dialog")}
                    className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 bg-[#58CC02] text-white font-bold rounded-xl hover:bg-[#4CAD02] transition-colors text-sm"
                >
                    <span>💬</span>
                    <span>Текст</span>
                </Link>
                {mode.voiceEnabled && (
                    <Link
                        href={`/dialog/${bundleId}/${mode.id}/voice`}
                        onClick={() => trackEvent("start_dialog", "dialog")}
                        className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 bg-blue-500 text-white font-bold rounded-xl hover:bg-blue-600 transition-colors text-sm"
                    >
                        <span>🎤</span>
                        <span>Голос</span>
                    </Link>
                )}
            </div>
        </div>
    );
}
