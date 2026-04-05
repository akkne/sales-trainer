import { DialogBundle } from "@/lib/hooks/useDialog";
import Link from "next/link";

interface BundleCardProps {
    bundle: DialogBundle;
}

export function BundleCard({ bundle }: BundleCardProps) {
    return (
        <Link
            href={`/dialog/${bundle.id}`}
            className="block p-4 border-2 border-gray-200 rounded-2xl hover:border-[#58CC02] transition-colors bg-white"
        >
            <div className="flex items-start gap-3">
                <span className="text-3xl">{bundle.iconEmoji}</span>
                <div className="flex-1 min-w-0">
                    <h3 className="font-bold text-lg text-gray-800 truncate">
                        {bundle.title}
                    </h3>
                    <p className="text-sm text-gray-500 mt-1 line-clamp-2">
                        {bundle.description}
                    </p>
                </div>
            </div>
        </Link>
    );
}
