import type { Metadata } from "next";
import { cookies } from "next/headers";
import { Geist, Geist_Mono } from "next/font/google";

import "./globals.css";
import { Providers } from "@/app/providers";
import {
  LOCALE_COOKIE_NAME,
  getMessages,
  resolveLocale,
} from "@/i18n/core";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

/**
 * Builds locale-aware metadata from the persisted locale cookie so the browser
 * tab reflects the selected language on first render.
 */
export async function generateMetadata(): Promise<Metadata> {
  const cookieStore = await cookies();
  const locale = resolveLocale(cookieStore.get(LOCALE_COOKIE_NAME)?.value);
  const messages = getMessages(locale);

  return {
    title: messages.metadata.title,
    description: messages.metadata.description,
  };
}

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const cookieStore = await cookies();
  const locale = resolveLocale(cookieStore.get(LOCALE_COOKIE_NAME)?.value);

  return (
    <html lang={locale} suppressHydrationWarning>
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        <Providers initialLocale={locale}>
          {children}
        </Providers>
      </body>
    </html>
  );
}
