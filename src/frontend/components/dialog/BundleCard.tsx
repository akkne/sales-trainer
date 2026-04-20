import { DialogBundle } from "@/lib/hooks/useDialog";
import Link from "next/link";
import { Icon, IconName } from "@/components/ui/Icon";

interface BundleCardProps {
    bundle: DialogBundle;
}

// Map bundle titles/slugs to icons
const BUNDLE_ICONS: Record<string, IconName> = {
    "cold-calling": "phone",
    "negotiation": "users",
    "objection": "check",
    "discovery": "search",
    "closing": "target",
    "follow-up": "clock",
};

function getBundleIcon(bundle: DialogBundle): IconName {
    // Try to match by id or title
    const slug = bundle.id.toLowerCase();
    for (const [key, icon] of Object.entries(BUNDLE_ICONS)) {
        if (slug.includes(key)) return icon;
    }
    return "message"; // default
}

export function BundleCard({ bundle }: BundleCardProps) {
    const iconName = getBundleIcon(bundle);

    return (
        <Link
            href={`/dialog/${bundle.id}`}
            className="group flex items-start gap-4 p-5 rounded-2xl bg-surface-container-lowest hover:bg-surface-container tonal-transition"
        >
            {/* Icon badge */}
            <div className="w-12 h-12 rounded-full bg-primary-container flex items-center justify-center shrink-0 group-hover:bg-primary group-hover:text-on-primary tonal-transition">
                {bundle.iconEmoji ? (
                    <span className="text-2xl">{bundle.iconEmoji}</span>
                ) : (
                    <Icon name={iconName} size="md" className="text-on-primary-container group-hover:text-on-primary" />
                )}
            </div>

            {/* Content */}
            <div className="flex-1 min-w-0">
                {/* Status pill - always show as Unlocked for now */}
                <span className="inline-flex items-center gap-1 text-xs font-semibold text-secondary bg-secondary-container px-2 py-0.5 rounded-full mb-1">
                    <Icon name="check" size="sm" />
                    Доступно
                </span>

                <h3 className="font-semibold text-base text-on-surface mb-1 truncate">
                    {bundle.title}
                </h3>
                <p className="text-sm text-on-surface-variant line-clamp-2 mb-2">
                    {bundle.description}
                </p>

                {/* Meta info */}
                <div className="flex items-center gap-3 text-xs text-on-surface-variant">
                    <span className="flex items-center gap-1">
                        <Icon name="layers" size="sm" />
                        Сценарии
                    </span>
                </div>
            </div>

            {/* CTA arrow */}
            <div className="shrink-0 self-center">
                <Icon
                    name="arrow-right"
                    size="md"
                    className="text-on-surface-variant group-hover:text-primary tonal-transition"
                />
            </div>
        </Link>
    );
}
