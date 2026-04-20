import { TopAppBar } from "@/components/layout/TopAppBar";

export default function MainLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="min-h-screen bg-surface">
            {/* Top navigation (desktop always visible, mobile shows hamburger menu) */}
            <TopAppBar />

            {/* Main content area */}
            <main>
                {children}
            </main>
        </div>
    );
}
