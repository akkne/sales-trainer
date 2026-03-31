import { BottomNav } from "@/components/layout/BottomNav";

export default function MainLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="min-h-screen bg-white pb-20">
            {children}
            <BottomNav />
        </div>
    );
}
