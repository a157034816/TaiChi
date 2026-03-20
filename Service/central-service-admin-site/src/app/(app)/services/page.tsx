"use client";

import Link from "next/link";
import { useEffect, useEffectEvent, useMemo, useState } from "react";
import { toast } from "sonner";
import { Loader2Icon, RefreshCwIcon } from "lucide-react";

import { ApiResponseError, apiGet, apiGetRaw, getApiErrorMessage } from "@/lib/api";
import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

type ServiceInfo = {
  id: string;
  name: string;
  host: string;
  localIp?: string | null;
  operatorIp?: string | null;
  publicIp?: string | null;
  port: number;
  serviceType: string;
  status: number;
  healthCheckUrl?: string | null;
  healthCheckPort?: number;
  healthCheckType?: string | null;
  registerTime?: string;
  lastHeartbeatTime?: string;
  weight: number;
  isLocalNetwork?: boolean;
};

type ServiceListResponse = {
  services: ServiceInfo[];
};

type ServiceNetworkStatus = {
  serviceId: string;
  responseTime: number;
  packetLoss: number;
  lastCheckTime: string;
  consecutiveSuccesses: number;
  consecutiveFailures: number;
  isAvailable: boolean;
};

type CurrentConfigResponse = {
  configJson: string;
};

type CentralServiceServicePolicyConfig = {
  serviceName: string;
  preferLocalNetwork: boolean;
  minHealthyInstances?: number | null;
};

type CentralServiceServiceInstanceOverrideConfig = {
  serviceId: string;
  disabled: boolean;
  weight?: number | null;
};

type CentralServiceRuntimeConfig = {
  services?: CentralServiceServicePolicyConfig[];
  instances?: CentralServiceServiceInstanceOverrideConfig[];
};

type ServiceGroupRow = {
  name: string;
  total: number;
  online: number;
  fault: number;
  offline: number;
  local: number;
  disabled: number;
  weightOverrides: number;
  preferLocalNetwork: boolean;
  minHealthyInstances?: number | null;
  networkKnown: number;
  networkAvailable: number;
  networkAverageScore?: number | null;
};

function parseRuntimeConfig(configJson?: string | null): CentralServiceRuntimeConfig {
  if (!configJson) {
    return { services: [], instances: [] };
  }

  try {
    const parsed = JSON.parse(configJson) as CentralServiceRuntimeConfig;
    return {
      services: parsed.services ?? [],
      instances: parsed.instances ?? [],
    };
  } catch {
    return { services: [], instances: [] };
  }
}

function calcNetworkScore(status: ServiceNetworkStatus) {
  if (!status.isAvailable) {
    return 0;
  }

  const responseTime = status.responseTime ?? 0;
  const packetLoss = status.packetLoss ?? 0;

  const responseTimeScore =
    responseTime >= 1000 ? 0 : responseTime <= 50 ? 50 : 50 * (1 - (responseTime - 50) / 950);
  const packetLossScore =
    packetLoss >= 50 ? 0 : packetLoss <= 0 ? 50 : 50 * (1 - packetLoss / 50);

  const score = Math.floor(responseTimeScore) + Math.floor(packetLossScore);
  return Math.max(0, Math.min(100, score));
}

export default function ServicesPage() {
  const { compareText, t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [services, setServices] = useState<ServiceInfo[]>([]);
  const [networkStatuses, setNetworkStatuses] = useState<ServiceNetworkStatus[]>([]);
  const [runtimeConfig, setRuntimeConfig] = useState<CentralServiceRuntimeConfig | null>(null);
  const [keyword, setKeyword] = useState("");

  const load = async () => {
    setLoading(true);
    try {
      const [servicesResult, networkResult, configResult] = await Promise.allSettled([
        apiGet<ServiceListResponse>("/api/Service/list"),
        apiGetRaw<ServiceNetworkStatus[]>("/api/ServiceDiscovery/network/all"),
        apiGet<CurrentConfigResponse>("/api/admin/config/current"),
      ]);

      if (servicesResult.status === "fulfilled") {
        setServices(servicesResult.value.services ?? []);
      } else {
        throw servicesResult.reason;
      }

      if (networkResult.status === "fulfilled") {
        setNetworkStatuses(networkResult.value ?? []);
      } else {
        setNetworkStatuses([]);
      }

      if (configResult.status === "fulfilled") {
        setRuntimeConfig(parseRuntimeConfig(configResult.value.configJson));
      } else {
        setRuntimeConfig(null);
      }
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

  const rows = useMemo(() => {
    const overridesById = new Map<string, CentralServiceServiceInstanceOverrideConfig>();
    for (const x of runtimeConfig?.instances ?? []) {
      if (x.serviceId) {
        overridesById.set(x.serviceId, x);
      }
    }

    const policyByName = new Map<string, CentralServiceServicePolicyConfig>();
    for (const x of runtimeConfig?.services ?? []) {
      if (x.serviceName) {
        policyByName.set(x.serviceName, x);
      }
    }

    const networkById = new Map<string, ServiceNetworkStatus>();
    for (const x of networkStatuses ?? []) {
      if (x.serviceId) {
        networkById.set(x.serviceId, x);
      }
    }

    const groups = new Map<string, ServiceInfo[]>();
    for (const s of services) {
      if (!s?.name) continue;
      const list = groups.get(s.name) ?? [];
      list.push(s);
      groups.set(s.name, list);
    }

    const allRows: ServiceGroupRow[] = Array.from(groups.entries()).map(
      ([name, instances]) => {
        const online = instances.filter((x) => x.status === 1).length;
        const fault = instances.filter((x) => x.status === 2).length;
        const offline = instances.filter((x) => x.status === 0).length;
        const local = instances.filter((x) => x.isLocalNetwork).length;

        const disabled = instances.filter((x) => overridesById.get(x.id)?.disabled).length;
        const weightOverrides = instances.filter((x) => overridesById.get(x.id)?.weight != null)
          .length;

        const policy = policyByName.get(name);

        const knownStatuses = instances
          .map((x) => networkById.get(x.id))
          .filter((x): x is ServiceNetworkStatus => !!x);
        const availableStatuses = knownStatuses.filter((x) => x.isAvailable);
        const scores = availableStatuses.map(calcNetworkScore);
        const networkAverageScore =
          scores.length > 0 ? scores.reduce((a, b) => a + b, 0) / scores.length : null;

        return {
          name,
          total: instances.length,
          online,
          fault,
          offline,
          local,
          disabled,
          weightOverrides,
          preferLocalNetwork: policy?.preferLocalNetwork ?? false,
          minHealthyInstances: policy?.minHealthyInstances ?? null,
          networkKnown: knownStatuses.length,
          networkAvailable: availableStatuses.length,
          networkAverageScore,
        };
      }
    );

    const k = keyword.trim().toLowerCase();
    const filtered = k ? allRows.filter((x) => x.name.toLowerCase().includes(k)) : allRows;
    filtered.sort((a, b) => compareText(a.name, b.name));
    return filtered;
  }, [compareText, keyword, networkStatuses, runtimeConfig, services]);

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="w-full max-w-xs">
          <Input
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder={t("services.searchPlaceholder")}
          />
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

      <Card>
        <CardHeader>
          <CardTitle>{t("services.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-64">{t("services.serviceColumn")}</TableHead>
                <TableHead className="w-24">{t("services.instanceColumn")}</TableHead>
                <TableHead className="w-64">{t("services.healthColumn")}</TableHead>
                <TableHead className="w-40">{t("services.overridesColumn")}</TableHead>
                <TableHead className="w-48">{t("services.policyColumn")}</TableHead>
                <TableHead>{t("services.networkScoreColumn")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                rows.map((x) => {
                  const warnMinHealthy =
                    x.minHealthyInstances != null && x.online < x.minHealthyInstances;

                  return (
                    <TableRow key={x.name}>
                      <TableCell>
                        <div className="grid gap-1">
                          <Link
                            href={`/services/${encodeURIComponent(x.name)}`}
                            className="font-mono text-sm hover:underline"
                          >
                            {x.name}
                          </Link>
                          {x.preferLocalNetwork ? (
                            <div className="text-xs text-muted-foreground">
                              {t("services.preferLocalNetworkBadge")}
                            </div>
                          ) : null}
                        </div>
                      </TableCell>

                      <TableCell className="text-sm">
                        {x.total}
                        {x.local ? (
                          <span className="ml-2 text-xs text-muted-foreground">
                            {t("services.localCount", { count: x.local })}
                          </span>
                        ) : null}
                      </TableCell>

                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant="default">{t("common.online")} {x.online}</Badge>
                          <Badge variant={x.fault ? "destructive" : "secondary"}>
                            {t("common.fault")} {x.fault}
                          </Badge>
                          <Badge variant="outline">{t("common.offline")} {x.offline}</Badge>
                        </div>
                      </TableCell>

                      <TableCell>
                        <div className="flex flex-wrap gap-2 text-xs">
                          <Badge variant={x.disabled ? "destructive" : "secondary"}>
                            {t("services.disabledCount", { count: x.disabled })}
                          </Badge>
                          <Badge variant={x.weightOverrides ? "default" : "secondary"}>
                            {t("services.weightOverrideCount", {
                              count: x.weightOverrides,
                            })}
                          </Badge>
                        </div>
                      </TableCell>

                      <TableCell>
                        <div className="grid gap-1 text-xs">
                          <div className={warnMinHealthy ? "text-destructive" : "text-muted-foreground"}>
                            {t("services.minimumHealthy", {
                              value: x.minHealthyInstances ?? "-",
                            })}
                          </div>
                          <div className="text-muted-foreground">
                            {t("services.preferLocalNetwork", {
                              value: x.preferLocalNetwork ? t("common.yes") : t("common.no"),
                            })}
                          </div>
                        </div>
                      </TableCell>

                      <TableCell>
                        <div className="grid gap-1 text-xs">
                          <div>{t("services.averageScore", {
                            value:
                              x.networkAverageScore != null
                                ? x.networkAverageScore.toFixed(1)
                                : "-",
                          })}</div>
                          <div className="text-muted-foreground">
                            {t("services.evaluatedStatus", {
                              available: x.networkAvailable,
                              total: x.total,
                              known: x.networkKnown,
                            })}
                          </div>
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
