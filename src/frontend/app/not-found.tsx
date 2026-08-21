"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";

// A-7: unmatched routes (e.g. /notifications, which only exists as the bell dropdown, not
// a page) used to fall through to Next.js's default English 404 with no app chrome and no
// way back. In this Next.js version, only the root `app/not-found.tsx` (or
// `app/global-not-found.tsx`) catches unmatched URLs app-wide — a nested `not-found.tsx`
// inside `(admin)` or `(org)` only fires for an explicit `notFound()` call in that segment,
// not for a typo'd URL — so this is the one file that ever renders for those. It picks a
// variant by path prefix, the same way `(admin)/layout.tsx` and `(org)/layout.tsx` already
// branch their own UI off `usePathname()`, because those two areas otherwise get the
// learner app's informal Russian copy and its `/tree` destination (R-21):
// `/admin/*` is platform staff tooling, entirely in English; `/org/*` is a paid product
// surface addressed to the customer in the formal «вы» register (see
// `app/demo/page.tsx` and `app/(org)/layout.tsx`).
export default function NotFound() {
    const pathname = usePathname();

    if (pathname?.startsWith("/admin")) {
        return (
            <NotFoundLayout>
                <h1 className="font-medium text-ink" style={{ fontSize: 20, marginBottom: 8 }}>
                    Page not found
                </h1>
                <p className="text-sm text-ink-3" style={{ maxWidth: 360, marginBottom: 24 }}>
                    This page doesn&apos;t exist or has been moved.
                </p>
                <Link href="/admin">
                    <Button variant="primary">Back to admin</Button>
                </Link>
            </NotFoundLayout>
        );
    }

    if (pathname?.startsWith("/org")) {
        return (
            <NotFoundLayout>
                <h1 className="font-medium text-ink" style={{ fontSize: 20, marginBottom: 8 }}>
                    Страница не найдена
                </h1>
                <p className="text-sm text-ink-3" style={{ maxWidth: 360, marginBottom: 24 }}>
                    Такой страницы не существует или она была перемещена. Вернитесь в панель
                    организации и откройте нужный раздел оттуда.
                </p>
                <Link href="/org">
                    <Button variant="primary">Вернуться в панель</Button>
                </Link>
            </NotFoundLayout>
        );
    }

    return (
        <NotFoundLayout>
            <h1 className="font-medium text-ink" style={{ fontSize: 20, marginBottom: 8 }}>
                Страница не найдена
            </h1>
            <p className="text-sm text-ink-3" style={{ maxWidth: 360, marginBottom: 24 }}>
                Такой страницы не существует или она была перемещена.
            </p>
            <Link href="/tree">
                <Button variant="primary">Вернуться к пути</Button>
            </Link>
        </NotFoundLayout>
    );
}

function NotFoundLayout({ children }: { children: React.ReactNode }) {
    return (
        <div
            className="flex flex-col items-center justify-center text-center"
            style={{ minHeight: "100vh", padding: "24px" }}
        >
            <div
                className="flex items-center justify-center"
                style={{ width: 56, height: 56, borderRadius: 16, background: "var(--surface-2)", marginBottom: 16 }}
            >
                <Icon name="compass" size="lg" />
            </div>
            {children}
        </div>
    );
}
