import type { Metadata } from "next";
import { Space_Grotesk, Lexend } from "next/font/google";
import "./globals.css";
import { AppProviders } from "./providers";

const spaceGrotesk = Space_Grotesk({
    variable: "--font-space-grotesk",
    subsets: ["latin"],
});

const lexend = Lexend({
    variable: "--font-lexend",
    subsets: ["latin"],
});

export const metadata: Metadata = {
    title: "SalesTrainer",
    description: "Тренажёр навыков продаж",
};

export default function RootLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <html
            lang="ru"
            className={`${spaceGrotesk.variable} ${lexend.variable} h-full antialiased`}
        >
            <body className="min-h-full flex flex-col bg-white font-[var(--font-lexend)]">
                <AppProviders>{children}</AppProviders>
            </body>
        </html>
    );
}
