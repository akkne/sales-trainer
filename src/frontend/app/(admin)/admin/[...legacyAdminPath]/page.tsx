"use client";

import { notFound, usePathname } from "next/navigation";
import { resolveLegacyAdminRedirect } from "@/features/org-shell/lib/legacy-admin-redirects";

/**
 * The route that lets `app/(admin)/layout.tsx` see the notification links at all.
 *
 * Every path in the §1.5 redirect table — `/admin/assignments/{id}`, `/admin/dialog-reviews`, the
 * eight others — has no page of its own, and Next.js answers an unmatched URL with the global
 * not-found without rendering any route-group layout. The redirect table would therefore never
 * run for the exact addresses it exists to rescue. This catch-all matches them, the layout above
 * redirects, and anything the table does not recognise still 404s.
 */
export default function LegacyAdminPathPage() {
    const pathname = usePathname();

    if (!resolveLegacyAdminRedirect(pathname)) {
        notFound();
    }

    return null;
}
