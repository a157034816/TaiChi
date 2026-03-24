"use client";

import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";

export type ServiceOpenClient = {
  clientName: string;
  localIp: string;
  operatorIp: string;
  publicIp: string;
  openedAtUtc: string;
  openUntilUtc: string;
  consecutiveFailures: number;
};

type Props = {
  clients: ServiceOpenClient[];
};

function formatIpValue(value?: string | null) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : "-";
}

export function ServiceOpenClientsList({ clients }: Props) {
  const { formatDateTime, t } = useI18n();

  return (
    <div className="grid gap-2 rounded-lg border border-dashed border-border/70 bg-muted/20 p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="text-xs font-medium">{t("serviceOpenClients.title")}</div>
        <Badge variant={clients.length > 0 ? "destructive" : "secondary"}>{clients.length}</Badge>
      </div>

      {clients.length === 0 ? (
        <div className="text-xs text-muted-foreground">{t("serviceOpenClients.empty")}</div>
      ) : (
        <div className="grid gap-2">
          {clients.map((client) => (
            <div key={`${client.clientName}-${client.localIp}-${client.publicIp}`} className="grid gap-1 rounded-md bg-background/80 p-2 text-xs">
              <div className="font-mono">{client.clientName}</div>
              <div className="text-muted-foreground">
                {t("serviceOpenClients.networkLine", {
                  lan: formatIpValue(client.localIp),
                  operator: formatIpValue(client.operatorIp),
                  public: formatIpValue(client.publicIp),
                })}
              </div>
              <div className="text-muted-foreground">
                {t("serviceOpenClients.timeLine", {
                  openedAt: formatDateTime(client.openedAtUtc),
                  openUntil: formatDateTime(client.openUntilUtc),
                  failures: client.consecutiveFailures,
                })}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
