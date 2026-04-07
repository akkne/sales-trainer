import { BottomNav } from "@/components/layout/BottomNav";
import { TopAppBar } from "@/components/layout/TopAppBar";

export default function MainLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="min-h-screen bg-surface">
            {/* Desktop top navigation */}
            <TopAppBar />

            {/* Main content area */}
            <main className="pb-[calc(4rem+env(safe-area-inset-bottom))] md:pb-0">
                {children}
            </main>

            {/* Mobile bottom navigation */}
            <BottomNav />
        </div>
    );
}
