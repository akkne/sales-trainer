import type { Metadata } from "next";
import "./globals.css";
import { AppProviders } from "./providers";

export const metadata: Metadata = {
    title: "Sellevate",
    description: "Тренажёр навыков продаж",
};

export const viewport = {
    width: "device-width",
    initialScale: 1,
    viewportFit: "cover",
};

export default function RootLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <html lang="ru" className="h-full antialiased">
            <head>
                <link rel="preconnect" href="https://fonts.googleapis.com" />
                <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
                <link
                    href="https://fonts.googleapis.com/css2?family=Manrope:wght@400;500;600;700;800&family=Unbounded:wght@600;700;800&family=JetBrains+Mono:wght@400;500;600;700&display=swap"
                    rel="stylesheet"
                />
            </head>
            <body>
                <AppProviders>{children}</AppProviders>
            </body>
        </html>
    );
}
