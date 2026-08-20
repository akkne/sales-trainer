import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";

// A-7: unmatched routes (e.g. /notifications, which only exists as the bell dropdown, not
// a page) used to fall through to Next.js's default English 404 with no app chrome and no
// way back. Root `app/not-found.tsx` catches every unmatched URL app-wide (Next.js has done
// this since 13.3.0), so this is the only place needed to localize that screen.
export default function NotFound() {
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
            <h1 className="font-medium text-ink" style={{ fontSize: 20, marginBottom: 8 }}>
                Страница не найдена
            </h1>
            <p className="text-sm text-ink-3" style={{ maxWidth: 360, marginBottom: 24 }}>
                Такой страницы не существует или она была перемещена.
            </p>
            <Link href="/tree">
                <Button variant="primary">Вернуться к пути</Button>
            </Link>
        </div>
    );
}
