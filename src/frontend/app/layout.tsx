import type { Metadata } from "next";
import { GeistSans, GeistMono } from "geist/font";
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
        <html
            lang="ru"
            className={`${GeistSans.variable} ${GeistMono.variable} h-full antialiased`}
        >
            <head />
            <body>
                <AppProviders>{children}</AppProviders>
            </body>
        </html>
    );
}
