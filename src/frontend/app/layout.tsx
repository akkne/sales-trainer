import type { Metadata } from "next";
import { Space_Grotesk, Lexend, Manrope } from "next/font/google";
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

const manrope = Manrope({
    variable: "--font-manrope",
    subsets: ["latin", "cyrillic"],
    weight: ["400", "500", "600", "700", "800"],
});

export const metadata: Metadata = {
    title: "SalesTrainer",
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
            className={`${spaceGrotesk.variable} ${lexend.variable} ${manrope.variable} h-full antialiased`}
        >
            <body className="min-h-full flex flex-col bg-white font-[var(--font-manrope)]">
                <AppProviders>{children}</AppProviders>
            </body>
        </html>
    );
}
