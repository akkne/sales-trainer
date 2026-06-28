import { DialogBundle } from "@/features/dialog/hooks/use-dialog";
import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";

interface BundleCardProps {
    bundle: DialogBundle;
}

const BUNDLE_ICONS: Record<string, IconName> = {
    "cold-calling": "phone",
    "negotiation": "users",
    "objection": "check",
    "discovery": "search",
    "closing": "target",
    "follow-up": "clock",
};

function getBundleIcon(bundle: DialogBundle): IconName {
    const slug = bundle.id.toLowerCase();
    for (const [key, icon] of Object.entries(BUNDLE_ICONS)) {
        if (slug.includes(key)) return icon;
    }
    return "message";
}

export function BundleCard({ bundle }: BundleCardProps) {
    const iconName = getBundleIcon(bundle);

    return (
        <Link
            href={`/dialog/${bundle.id}`}
            className="group flex items-start gap-4 p-5 rounded-2xl bg-surface border border-line hover:bg-bg-2 transition-colors"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            {/* Icon badge */}
            <div className="w-12 h-12 rounded-full bg-indigo-soft flex items-center justify-center shrink-0 group-hover:bg-indigo group-hover:text-white transition-colors">
                {bundle.iconEmoji ? (
                    <span className="text-2xl">{bundle.iconEmoji}</span>
                ) : (
                    <Icon name={iconName} size="md" className="text-indigo-ink group-hover:text-white" />
                )}
            </div>

            {/* Content */}
            <div className="flex-1 min-w-0">
                {/* Status pill */}
                <span className="inline-flex items-center gap-1 text-xs font-semibold text-olive bg-olive-soft px-2 py-0.5 rounded-full mb-1">
                    <Icon name="check" size="sm" />
                    Available
                </span>

                <h3 className="font-semibold text-base text-ink mb-1 truncate">
                    {bundle.title}
                </h3>
                <p className="text-sm text-ink-3 line-clamp-2 mb-2">
                    {bundle.description}
                </p>

                {/* Meta info */}
                <div className="flex items-center gap-3 text-xs text-ink-4">
                    <span className="flex items-center gap-1">
                        <Icon name="layers" size="sm" />
                        Scenarios
                    </span>
                </div>
            </div>

            {/* CTA arrow */}
            <div className="shrink-0 self-center">
                <Icon
                    name="arrow-right"
                    size="md"
                    className="text-ink-4 group-hover:text-ink transition-colors"
                />
            </div>
        </Link>
    );
}
