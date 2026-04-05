import { DialogMode } from "@/lib/hooks/useDialog";
import Link from "next/link";

interface ModeCardProps {
    bundleId: string;
    mode: DialogMode;
}

export function ModeCard({ bundleId, mode }: ModeCardProps) {
    return (
        <Link
            href={`/dialog/${bundleId}/${mode.id}`}
            className="block p-4 border-2 border-gray-200 rounded-2xl hover:border-[#58CC02] transition-colors bg-white"
        >
            <h3 className="font-bold text-lg text-gray-800">
                {mode.title}
            </h3>
            <p className="text-sm text-gray-500 mt-1 line-clamp-2">
                {mode.description}
            </p>
        </Link>
    );
}
