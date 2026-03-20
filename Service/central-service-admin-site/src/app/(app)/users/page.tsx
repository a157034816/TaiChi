"use client";

import { useI18n } from "@/i18n/context";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function UsersPage() {
  const { t } = useI18n();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("users.title")}</CardTitle>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">
        {t("users.placeholder")}
      </CardContent>
    </Card>
  );
}

