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

type NetworkScoreSummary = {
  knownStatusCount: number;
  availableCount: number;
  unavailableCount: number;
  averageScore: number;
  minScore?: number | null;
  maxScore?: number | null;
  lastEvaluatedAtUtc?: string | null;
};

type BackgroundTaskStatusDto = {
  taskName: string;
  isHealthy: boolean;
  lastRunAtUtc?: string | null;
  lastSuccessAtUtc?: string | null;
  lastErrorAtUtc?: string | null;
  lastError?: string | null;
};

type MonitoringSummaryResponse = {
  totalServiceInstances: number;
  onlineServiceInstances: number;
  faultServiceInstances: number;
  offlineServiceInstances: number;
  network: NetworkScoreSummary;
  backgroundTasks: BackgroundTaskStatusDto[];
};

type HealthCheckItem = {
  name: string;
  status: string;
  description?: string | null;
  durationMs: number;
  error?: string | null;
};

type MonitoringHealthResponse = {
  status: string;
  checkedAtUtc: string;
  checks: HealthCheckItem[];
};

function statusVariant(status: string) {
  const s = status.toLowerCase();
  if (s === "healthy") return "default";
  if (s === "degraded") return "secondary";
  if (s === "unhealthy") return "destructive";
  return "outline";
}

export default function MonitoringPage() {
  const { formatDateTime, t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [summary, setSummary] = useState<MonitoringSummaryResponse | null>(null);
  const [health, setHealth] = useState<MonitoringHealthResponse | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [summaryResp, healthResp] = await Promise.all([
        apiGet<MonitoringSummaryResponse>("/api/admin/monitoring/summary"),
        apiGet<MonitoringHealthResponse>("/api/admin/monitoring/health"),
      ]);
      setSummary(summaryResp);
      setHealth(healthResp);
    } catch (error) {
      if (error instanceof ApiResponseError) {
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
  const minScore = summary?.network?.minScore ?? null;
  const maxScore = summary?.network?.maxScore ?? null;
  const healthStatus = health?.status.toLowerCase();
  const healthStatusLabel =
    healthStatus === "healthy"
      ? t("common.healthy")
      : healthStatus === "degraded"
        ? t("common.degraded")
        : healthStatus === "unhealthy"
          ? t("common.unhealthy")
          : t("monitoring.unknown");

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-xs text-muted-foreground">
          {t("common.lastRefresh")}：{formatDateTime(health?.checkedAtUtc)}
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
            <CardTitle>{t("monitoring.serviceInstancesTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1 text-sm">
            <div>{t("common.total")}：{summary?.totalServiceInstances ?? "-"}</div>
            <div>{t("common.online")}：{summary?.onlineServiceInstances ?? "-"}</div>
            <div className={summary?.faultServiceInstances ? "text-destructive" : ""}>
              {t("common.fault")}：{summary?.faultServiceInstances ?? "-"}
            </div>
            <div>{t("common.offline")}：{summary?.offlineServiceInstances ?? "-"}</div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("monitoring.networkScoreTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1 text-sm">
            <div>
              {t("common.average")}：{avgScore ? avgScore.toFixed(1) : "0.0"}{" "}
              <span className="text-xs text-muted-foreground">/ 100</span>
            </div>
            <div>{t("common.minimum")}：{minScore ?? "-"}</div>
            <div>{t("common.maximum")}：{maxScore ?? "-"}</div>
            <div className="text-xs text-muted-foreground">
              {t("monitoring.recentEvaluation", {
                value: formatDateTime(summary?.network?.lastEvaluatedAtUtc),
              })}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-2">
            <CardTitle>{t("monitoring.healthTitle")}</CardTitle>
            <Badge variant={statusVariant(health?.status ?? "unknown")}>
              {healthStatusLabel}
            </Badge>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            {health
              ? t("monitoring.checksCount", { count: health.checks.length })
              : t("common.loading")}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("monitoring.healthChecksTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-56">{t("common.name")}</TableHead>
                <TableHead className="w-32">{t("common.status")}</TableHead>
                <TableHead className="w-24">{t("monitoring.duration")}</TableHead>
                <TableHead>{t("monitoring.descriptionOrError")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {!health || health.checks.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                health.checks.map((x) => (
                  <TableRow key={x.name}>
                    <TableCell className="font-mono text-xs">{x.name}</TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(x.status)}>
                        {x.status.toLowerCase() === "healthy"
                          ? t("common.healthy")
                          : x.status.toLowerCase() === "degraded"
                            ? t("common.degraded")
                            : x.status.toLowerCase() === "unhealthy"
                              ? t("common.unhealthy")
                              : x.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {x.durationMs}ms
                    </TableCell>
                    <TableCell className="text-xs">
                      {x.error ? (
                        <span className="text-destructive">{x.error}</span>
                      ) : (
                        <span className="text-muted-foreground">
                          {x.description ?? "-"}
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("monitoring.backgroundTasksTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-56">{t("monitoring.task")}</TableHead>
                <TableHead className="w-24">{t("common.healthy")}</TableHead>
                <TableHead className="w-40">{t("monitoring.lastRun")}</TableHead>
                <TableHead className="w-40">{t("monitoring.lastSuccess")}</TableHead>
                <TableHead>{t("monitoring.lastError")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {!summary || summary.backgroundTasks.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                summary.backgroundTasks.map((x) => (
                  <TableRow key={x.taskName}>
                    <TableCell className="font-mono text-xs">{x.taskName}</TableCell>
                    <TableCell>
                      <Badge variant={x.isHealthy ? "default" : "destructive"}>
                        {x.isHealthy ? t("monitoring.ok") : t("monitoring.error")}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {formatDateTime(x.lastRunAtUtc)}
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {formatDateTime(x.lastSuccessAtUtc)}
                    </TableCell>
                    <TableCell className="text-xs">
                      {x.lastError ? (
                        <span className="text-destructive">{x.lastError}</span>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
