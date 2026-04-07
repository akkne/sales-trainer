import type { Metadata } from "next";
import { Plus_Jakarta_Sans, Manrope } from "next/font/google";
import "./globals.css";
import { AppProviders } from "./providers";

const plusJakarta = Plus_Jakarta_Sans({
    variable: "--font-plus-jakarta",
    subsets: ["latin"],
    weight: ["400", "500", "600", "700"],
});

const manrope = Manrope({
    variable: "--font-manrope",
    subsets: ["latin", "cyrillic"],
    weight: ["400", "500", "600", "700", "800"],
});

export const metadata: Metadata = {
    title: "SalesMastery",
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
        <html
            lang="ru"
            className={`${plusJakarta.variable} ${manrope.variable} h-full antialiased`}
        >
            <head>
                {/* Material Symbols Outlined icons */}
                <link
                    rel="stylesheet"
                    href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200&display=swap"
                />
            </head>
            <body className="min-h-full flex flex-col bg-surface font-[var(--font-manrope)]">
                <AppProviders>{children}</AppProviders>
            </body>
        </html>
    );
}
