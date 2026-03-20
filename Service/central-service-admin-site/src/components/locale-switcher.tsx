"use client";

import type { ComponentProps } from "react";
import { LanguagesIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { useI18n, useLocale } from "@/i18n/context";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";

type Props = {
  className?: string;
  variant?: ComponentProps<typeof Button>["variant"];
};

const localeOptions = [
  { value: "zh-CN", shortLabel: "中文", labelKey: "language.zhCN" },
  { value: "en-US", shortLabel: "EN", labelKey: "language.enUS" },
] as const;

/**
 * Renders the shared language switcher used by both the login page and the
 * authenticated app shell.
 */
export function LocaleSwitcher({
  className,
  variant = "outline",
}: Props) {
  const { locale, setLocale } = useLocale();
  const { t } = useI18n();

  const currentOption =
    localeOptions.find((option) => option.value === locale) ?? localeOptions[0];

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant={variant}
          size="sm"
          className={cn("gap-2", className)}
          aria-label={t("language.switch")}
        >
          <LanguagesIcon className="size-4" />
          <span>{currentOption.shortLabel}</span>
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{t("language.label")}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuRadioGroup
          value={locale}
          onValueChange={(value) => setLocale(value as typeof locale)}
        >
          {localeOptions.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {t(option.labelKey)}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
