"use client";

import { Loader2Icon } from "lucide-react";

import { useI18n } from "@/i18n/context";

/**
 * Renders a localized lightweight loading indicator for Suspense fallbacks and
 * other small async states.
 */
export function LoadingMessage() {
  const { t } = useI18n();

  return (
    <div className="flex items-center gap-2 text-sm text-muted-foreground">
      <Loader2Icon className="size-4 animate-spin" />
      {t("common.loading")}
    </div>
  );
}
