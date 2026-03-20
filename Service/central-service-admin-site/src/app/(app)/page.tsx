"use client";

import { useEffect, useEffectEvent, useState } from "react";
import { toast } from "sonner";
import { Loader2Icon, RefreshCwIcon } from "lucide-react";

import { ApiResponseError, apiGet, getApiErrorMessage } from "@/lib/api";
import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

type MonitoringSummaryResponse = {
  totalServiceInstances: number;
  onlineServiceInstances: number;
  faultServiceInstances: number;
  network: {
    averageScore: number;
    availableCount: number;
    knownStatusCount: number;
    lastEvaluatedAtUtc?: string | null;
  };
};

type CurrentConfigResponse = {
  currentVersionId?: number | null;
  currentVersionNo?: number | null;
};

type PublishHistoryItem = {
  id: number;
  action: string;
  fromVersionId?: number | null;
  toVersionId: number;
  note?: string | null;
  actorUsername?: string | null;
  createdAtUtc: string;
};

type PagedResult<T> = {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
};

type AuditLogListItem = {
  id: number;
  createdAtUtc: string;
  actorUsername?: string | null;
  action: string;
  resource: string;
};

function actionVariant(action: string) {
  const a = action.toLowerCase();
  if (a === "publish") return "default";
  if (a === "rollback") return "destructive";
  return "secondary";
}

export default function DashboardPage() {
  const { formatDateTime, t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [summary, setSummary] = useState<MonitoringSummaryResponse | null>(null);
  const [current, setCurrent] = useState<CurrentConfigResponse | null>(null);
  const [latestPublish, setLatestPublish] = useState<PublishHistoryItem | null>(null);
  const [audit, setAudit] = useState<AuditLogListItem[]>([]);

  const load = async () => {
    setLoading(true);
    try {
      const [summaryResp, currentResp, historyResp, auditResp] =
        await Promise.all([
          apiGet<MonitoringSummaryResponse>("/api/admin/monitoring/summary"),
          apiGet<CurrentConfigResponse>("/api/admin/config/current"),
          apiGet<PublishHistoryItem[]>(
            "/api/admin/config/publish-history?take=1"
          ),
          apiGet<PagedResult<AuditLogListItem>>(
            "/api/admin/audit?page=1&pageSize=8"
          ),
        ]);

      setSummary(summaryResp);
      setCurrent(currentResp);
      setLatestPublish(historyResp?.[0] ?? null);
      setAudit(auditResp.items ?? []);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        if (error.code === 401) {
          return;
        }
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("common.loadFailed"));
    } finally {
      setLoading(false);
    }
  };

  const loadOnMount = useEffectEvent(() => {
    void load();
  });

  useEffect(() => {
    loadOnMount();
  }, []);

  const avgScore = summary?.network?.averageScore ?? 0;
  const latestAction = latestPublish?.action.toLowerCase();
  const latestActionLabel =
    latestAction === "publish"
      ? t("common.publish")
      : latestAction === "rollback"
        ? t("common.rollback")
        : (latestPublish?.action ?? "-");

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-xs text-muted-foreground">
          {t("dashboard.recentEvaluation", {
            value: formatDateTime(summary?.network?.lastEvaluatedAtUtc),
          })}
        </div>
        <Button variant="secondary" onClick={load} disabled={loading}>
          {loading ? (
            <Loader2Icon className="mr-2 size-4 animate-spin" />
          ) : (
            <RefreshCwIcon className="mr-2 size-4" />
          )}
          {t("common.refresh")}
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>{t("dashboard.servicesTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1 text-sm">
            <div>{t("common.total")}：{summary?.totalServiceInstances ?? "-"}</div>
            <div>{t("common.online")}：{summary?.onlineServiceInstances ?? "-"}</div>
            <div className={summary?.faultServiceInstances ? "text-destructive" : ""}>
              {t("common.fault")}：{summary?.faultServiceInstances ?? "-"}
            </div>
            <div>
              {t("dashboard.networkScore", {
                value: avgScore ? avgScore.toFixed(1) : "0.0",
              })}
            </div>
            <div className="text-xs text-muted-foreground">
              {t("dashboard.evaluated", {
                available: summary?.network?.availableCount ?? 0,
                known: summary?.network?.knownStatusCount ?? 0,
              })}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("dashboard.releasesTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-2 text-sm">
            <div>
              {t("dashboard.currentVersion")}
              <span className="ml-2 font-mono">
                {current?.currentVersionNo ? `v${current.currentVersionNo}` : "-"}
              </span>
              <span className="ml-2 text-xs text-muted-foreground">
                #{current?.currentVersionId ?? "-"}
              </span>
            </div>
            {!latestPublish ? (
              <div className="text-xs text-muted-foreground">
                {t("dashboard.noPublishHistory")}
              </div>
            ) : (
              <div className="grid gap-1 text-xs">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={actionVariant(latestPublish.action)}>
                    {latestActionLabel}
                  </Badge>
                  <span className="text-muted-foreground">
                    {t("dashboard.publishedTo", {
                      versionId: latestPublish.toVersionId,
                    })}
                  </span>
                </div>
                <div className="text-muted-foreground">
                  {formatDateTime(latestPublish.createdAtUtc)} ·{" "}
                  {latestPublish.actorUsername ?? "-"}
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("dashboard.auditSummaryTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-28">{t("common.time")}</TableHead>
                  <TableHead className="w-24">{t("common.user")}</TableHead>
                  <TableHead className="w-40">{t("common.action")}</TableHead>
                  <TableHead>{t("common.resource")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {audit.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center text-sm">
                      {t("common.noData")}
                    </TableCell>
                  </TableRow>
                ) : (
                  audit.map((x) => (
                    <TableRow key={x.id}>
                      <TableCell className="text-xs text-muted-foreground">
                        {formatDateTime(x.createdAtUtc)}
                      </TableCell>
                      <TableCell className="text-xs">
                        {x.actorUsername ?? "-"}
                      </TableCell>
                      <TableCell className="text-xs font-mono">
                        {x.action}
                      </TableCell>
                      <TableCell className="text-xs font-mono">
                        {x.resource}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
