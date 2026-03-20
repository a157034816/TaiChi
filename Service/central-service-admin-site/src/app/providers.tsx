"use client";

import type { ReactNode } from "react";

import { ThemeProvider } from "@/components/theme-provider";
import { Toaster } from "@/components/ui/sonner";
import { I18nProvider } from "@/i18n/context";
import type { SupportedLocale } from "@/i18n/core";

type Props = {
  initialLocale: SupportedLocale;
  children: ReactNode;
};

/**
 * Centralizes client-side providers so the root layout can stay server-driven
 * while still passing the initial locale into client context.
 */
export function Providers({ initialLocale, children }: Props) {
  return (
    <I18nProvider initialLocale={initialLocale}>
      <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
        {children}
        <Toaster />
      </ThemeProvider>
    </I18nProvider>
  );
}
