import { BottomNav } from "@/components/layout/BottomNav";

export default function MainLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="min-h-screen bg-white pb-[calc(5rem+env(safe-area-inset-bottom))]">
            {children}
            <BottomNav />
        </div>
    );
}
