"use client";

import Link from "next/link";
import { use, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import {
  ArrowLeftIcon,
  Loader2Icon,
  PencilLineIcon,
  RefreshCwIcon,
  ShieldOffIcon,
  Trash2Icon,
  ZapIcon,
} from "lucide-react";

import {
  ApiResponseError,
  apiDelete,
  apiGet,
  apiGetRaw,
  apiPut,
  apiPostRaw,
  getApiErrorMessage,
} from "@/lib/api";
import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  ServiceCircuitEditorDialog,
  type ServiceCircuitFormValue,
} from "@/components/services/service-circuit-editor-dialog";
import {
  ServiceOpenClientsList,
  type ServiceOpenClient,
} from "@/components/services/service-open-clients-list";
import { Separator } from "@/components/ui/separator";
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

type ServiceCircuitInstance = {
  serviceId: string;
  lastSeenAtUtc: string;
  maxAttempts: number;
  failureThreshold: number;
  breakDurationMinutes: number;
  recoveryThreshold: number;
  openClients: ServiceOpenClient[];
};

type ServiceCircuitServiceDetailResponse = {
  serviceName: string;
  instances: ServiceCircuitInstance[];
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

function statusLabel(
  status: number,
  t: ReturnType<typeof useI18n>["t"]
) {
  if (status === 1) return t("common.online");
  if (status === 2) return t("common.fault");
  return t("common.offline");
}

function statusVariant(status: number) {
  if (status === 1) return "default";
  if (status === 2) return "destructive";
  return "outline";
}

function formatIpValue(value?: string | null) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : "-";
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

function equalsIgnoreCase(a: string, b: string) {
  return a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;
}

function normalizeRouteParam(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return trimmed;
  }

  const plusFixed = trimmed.replace(/\+/g, " ");
  try {
    return decodeURIComponent(plusFixed);
  } catch {
    return plusFixed;
  }
}

function matchesServiceName(actual: string, routeRaw: string, routeNormalized: string) {
  const a = actual.trim();
  const raw = routeRaw.trim();
  const normalized = routeNormalized.trim();

  if (!a) return false;
  if (normalized && equalsIgnoreCase(a, normalized)) return true;
  if (raw && equalsIgnoreCase(a, raw)) return true;

  const encoded = encodeURIComponent(a);
  return raw ? equalsIgnoreCase(encoded, raw) : false;
}

export default function ServiceDetailPage({
  params,
}: Readonly<{ params: Promise<{ serviceName: string }> }>) {
  const { formatDateTime, t } = useI18n();
  const { serviceName } = use(params);
  const routeServiceName = useMemo(() => normalizeRouteParam(serviceName), [serviceName]);

  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const [instances, setInstances] = useState<ServiceInfo[]>([]);
  const [loadedTotal, setLoadedTotal] = useState(0);
  const [networkById, setNetworkById] = useState<Record<string, ServiceNetworkStatus>>({});
  const [circuitById, setCircuitById] = useState<Record<string, ServiceCircuitInstance>>({});
  const [runtimeConfig, setRuntimeConfig] = useState<CentralServiceRuntimeConfig | null>(null);
  const [editorTargetId, setEditorTargetId] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const currentServiceName = routeServiceName || serviceName;
      const [servicesResult, networkResult, configResult, circuitResult] = await Promise.allSettled([
        apiGet<ServiceListResponse>("/api/Service/list"),
        apiGetRaw<ServiceNetworkStatus[]>("/api/ServiceDiscovery/network/all"),
        apiGet<CurrentConfigResponse>("/api/admin/config/current"),
        apiGet<ServiceCircuitServiceDetailResponse>(
          `/api/admin/service-circuits/services/${encodeURIComponent(currentServiceName)}`
        ),
      ]);

      if (servicesResult.status === "fulfilled") {
        const all = servicesResult.value.services ?? [];
        setLoadedTotal(all.length);
        const filtered = all.filter((x) =>
          matchesServiceName(x.name, serviceName, routeServiceName)
        );
        setInstances(filtered);
      } else {
        throw servicesResult.reason;
      }

      if (networkResult.status === "fulfilled") {
        const map: Record<string, ServiceNetworkStatus> = {};
        for (const x of networkResult.value ?? []) {
          if (x.serviceId) {
            map[x.serviceId] = x;
          }
        }
        setNetworkById(map);
      } else {
        setNetworkById({});
      }

      if (configResult.status === "fulfilled") {
        setRuntimeConfig(parseRuntimeConfig(configResult.value.configJson));
      } else {
        setRuntimeConfig(null);
      }

      if (circuitResult.status === "fulfilled") {
        const map: Record<string, ServiceCircuitInstance> = {};
        for (const item of circuitResult.value.instances ?? []) {
          if (item.serviceId) {
            map[item.serviceId] = item;
          }
        }
        setCircuitById(map);
      } else {
        setCircuitById({});
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

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [routeServiceName, serviceName]);

  const overridesById = useMemo(() => {
    const map = new Map<string, CentralServiceServiceInstanceOverrideConfig>();
    for (const x of runtimeConfig?.instances ?? []) {
      if (x.serviceId) {
        map.set(x.serviceId, x);
      }
    }
    return map;
  }, [runtimeConfig]);

  const policy = useMemo(() => {
    const list = runtimeConfig?.services ?? [];
    return (
      list.find((x) => matchesServiceName(x.serviceName, serviceName, routeServiceName)) ??
      null
    );
  }, [runtimeConfig, routeServiceName, serviceName]);

  const summary = useMemo(() => {
    const online = instances.filter((x) => x.status === 1).length;
    const fault = instances.filter((x) => x.status === 2).length;
    const offline = instances.filter((x) => x.status === 0).length;
    const disabled = instances.filter((x) => overridesById.get(x.id)?.disabled).length;

    const known = instances.map((x) => networkById[x.id]).filter((x): x is ServiceNetworkStatus => !!x);
    const available = known.filter((x) => x.isAvailable);
    const scores = available.map(calcNetworkScore);
    const avg = scores.length ? scores.reduce((a, b) => a + b, 0) / scores.length : null;

    return {
      total: instances.length,
      online,
      fault,
      offline,
      disabled,
      networkKnown: known.length,
      networkAvailable: available.length,
      networkAverage: avg,
    };
  }, [instances, networkById, overridesById]);

  const evaluateNetwork = async (id: string) => {
    setActionLoading(id);
    try {
      const status = await apiPostRaw<ServiceNetworkStatus>(
        `/api/ServiceDiscovery/network/evaluate/${encodeURIComponent(id)}`
      );
      setNetworkById((prev) => ({ ...prev, [id]: status }));
      toast.success(t("serviceDetail.evaluateSuccess"));
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("serviceDetail.evaluateFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  const deregister = async (id: string) => {
    if (!window.confirm(t("serviceDetail.confirmDeregister", { id }))) {
      return;
    }

    setActionLoading(id);
    try {
      await apiDelete<object>(`/api/Service/deregister/${encodeURIComponent(id)}`);
      toast.success(t("serviceDetail.deregisterSuccess"));
      await load();
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("serviceDetail.deregisterFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  const clearCircuitState = async (id: string) => {
    if (!window.confirm(t("serviceDetail.confirmClearCircuit", { id }))) {
      return;
    }

    setActionLoading(`clear:${id}`);
    try {
      await apiPostRaw<object>(`/api/admin/service-circuits/instances/${encodeURIComponent(id)}/clear`, {});
      toast.success(t("serviceDetail.clearCircuitSuccess"));
      await load();
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("serviceDetail.clearCircuitFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  const saveCircuitConfig = async (id: string, formValue: ServiceCircuitFormValue) => {
    setActionLoading(`save:${id}`);
    try {
      await apiPut<object>(
        `/api/admin/service-circuits/instances/${encodeURIComponent(id)}/config`,
        formValue
      );
      toast.success(t("serviceDetail.saveCircuitSuccess"));
      setEditorTargetId(null);
      await load();
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("serviceDetail.saveCircuitFailed"));
    } finally {
      setActionLoading(null);
    }
  };

  const editorValue = useMemo<ServiceCircuitFormValue>(() => {
    const target = editorTargetId ? circuitById[editorTargetId] : null;
    return {
      maxAttempts: target?.maxAttempts ?? 2,
      failureThreshold: target?.failureThreshold ?? 3,
      breakDurationMinutes: target?.breakDurationMinutes ?? 1,
      recoveryThreshold: target?.recoveryThreshold ?? 2,
    };
  }, [circuitById, editorTargetId]);

  const warnMinHealthy =
    policy?.minHealthyInstances != null && summary.online < policy.minHealthyInstances;

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="ghost" size="sm" asChild>
            <Link href="/services">
              <ArrowLeftIcon className="mr-1 size-4" />
              {t("serviceDetail.back")}
            </Link>
          </Button>
          <div className="font-mono text-sm">{routeServiceName || serviceName}</div>
          {policy?.preferLocalNetwork ? (
            <Badge variant="secondary">{t("services.preferLocalNetworkBadge")}</Badge>
          ) : null}
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
          <CardTitle>{t("serviceDetail.overviewTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-2 text-sm">
          <div className="flex flex-wrap gap-2">
            <Badge variant="secondary">
              {t("serviceDetail.instancesCount", { count: summary.total })}
            </Badge>
            <Badge variant="default">{t("common.online")} {summary.online}</Badge>
            <Badge variant={summary.fault ? "destructive" : "secondary"}>
              {t("common.fault")} {summary.fault}
            </Badge>
            <Badge variant="outline">{t("common.offline")} {summary.offline}</Badge>
            <Badge variant={summary.disabled ? "destructive" : "secondary"}>
              {t("common.disabled")} {summary.disabled}
            </Badge>
          </div>

          <Separator />

          <div className="grid gap-1 text-xs text-muted-foreground">
            <div className={warnMinHealthy ? "text-destructive" : ""}>
              {t("serviceDetail.minimumHealthyInstances", {
                value: policy?.minHealthyInstances ?? "-",
              })}
            </div>
            <div>
              {t("serviceDetail.preferLocalNetwork", {
                value: policy?.preferLocalNetwork ? t("common.yes") : t("common.no"),
              })}
            </div>
            <div>
              {t("serviceDetail.networkSummary", {
                available: summary.networkAvailable,
                known: summary.networkKnown,
                average:
                  summary.networkAverage != null
                    ? summary.networkAverage.toFixed(1)
                    : "-",
              })}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("serviceDetail.instancesTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-72">{t("serviceDetail.instanceColumn")}</TableHead>
                  <TableHead className="w-32">{t("serviceDetail.statusColumn")}</TableHead>
                  <TableHead className="w-64">{t("serviceDetail.healthCheckColumn")}</TableHead>
                  <TableHead className="w-48">{t("serviceDetail.overridesColumn")}</TableHead>
                  <TableHead className="w-72">{t("serviceDetail.networkColumn")}</TableHead>
                  <TableHead>{t("serviceDetail.actionsColumn")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {instances.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center text-sm">
                      {loadedTotal > 0 ? (
                        <div className="grid gap-1">
                          <div>{t("common.noData")}</div>
                          <div className="text-xs text-muted-foreground">
                            {t("services.instancesLoadedButNotMatched", {
                              loadedTotal,
                              serviceName: routeServiceName || serviceName,
                            })}
                          </div>
                        </div>
                      ) : (
                        t("common.noData")
                      )}
                    </TableCell>
                  </TableRow>
                ) : (
                  instances.map((x) => {
                    const ov = overridesById.get(x.id) ?? null;
                    const disabled = ov?.disabled ?? false;
                    const effectiveWeight = ov?.weight != null ? ov.weight : x.weight;

                    const ns = networkById[x.id] ?? null;
                    const circuit = circuitById[x.id] ?? null;
                    const score = ns ? calcNetworkScore(ns) : null;
                    const busy =
                      actionLoading === x.id ||
                      actionLoading === `clear:${x.id}` ||
                      actionLoading === `save:${x.id}`;

                    return (
                      <TableRow key={x.id}>
                        <TableCell>
                          <div className="grid gap-1">
                            <div className="font-mono text-xs text-muted-foreground">
                              {x.id}
                            </div>
                            <div className="text-sm">
                              {x.host}:{x.port}
                              {x.isLocalNetwork ? (
                                <Badge className="ml-2" variant="secondary">
                                  {t("common.local")}
                                </Badge>
                              ) : null}
                            </div>
                            <div className="grid gap-1 text-xs text-muted-foreground">
                              <div>
                                <span className="font-mono">
                                  {t("serviceDetail.lanIp", {
                                    value: formatIpValue(x.localIp ?? x.host),
                                  })}
                                </span>
                              </div>
                              <div>
                                <span className="font-mono">
                                  {t("serviceDetail.operatorIp", {
                                    value: formatIpValue(x.operatorIp),
                                  })}
                                </span>
                              </div>
                              <div>
                                <span className="font-mono">
                                  {t("serviceDetail.publicIp", {
                                    value: formatIpValue(x.publicIp),
                                  })}
                                </span>
                              </div>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {t("serviceDetail.registeredHeartbeat", {
                                registered: formatDateTime(x.registerTime),
                                heartbeat: formatDateTime(x.lastHeartbeatTime),
                              })}
                            </div>
                            <ServiceOpenClientsList clients={circuit?.openClients ?? []} />
                          </div>
                        </TableCell>

                        <TableCell>
                          <div className="grid gap-1">
                            <Badge variant={statusVariant(x.status)}>
                              {statusLabel(x.status, t)}
                            </Badge>
                            {disabled ? (
                              <Badge variant="destructive">{t("common.disabled")}</Badge>
                            ) : null}
                          </div>
                        </TableCell>

                        <TableCell className="text-xs">
                          <div className="grid gap-1">
                            <div className="text-muted-foreground">
                              {t("serviceDetail.healthType", {
                                value: x.healthCheckType ?? "-",
                              })}
                            </div>
                            {x.healthCheckType?.toLowerCase() === "http" ? (
                              <div className="font-mono break-all">
                                {x.healthCheckUrl || "-"}
                              </div>
                            ) : (
                              <div className="text-muted-foreground">
                                {t("serviceDetail.healthPort", {
                                  value: x.healthCheckPort ?? x.port,
                                })}
                              </div>
                            )}
                          </div>
                        </TableCell>

                        <TableCell className="text-xs">
                          <div className="grid gap-1">
                            <div>{t("serviceDetail.weight", {
                              value:
                                ov?.weight != null
                                  ? `${x.weight} → ${effectiveWeight}`
                                  : x.weight,
                            })}</div>
                            <div className="text-muted-foreground">
                              {t("serviceDetail.override", {
                                value: ov ? t("common.yes") : t("common.no"),
                              })}
                            </div>
                            <div>{t("serviceDetail.circuit", {
                              value: circuit
                                ? `${circuit.maxAttempts}/${circuit.failureThreshold}/${circuit.breakDurationMinutes}/${circuit.recoveryThreshold}`
                                : "-",
                            })}</div>
                            <div className="text-muted-foreground">
                              {t("serviceDetail.lastSeen", {
                                value: formatDateTime(circuit?.lastSeenAtUtc),
                              })}
                            </div>
                          </div>
                        </TableCell>

                        <TableCell className="text-xs">
                          {!ns ? (
                            <div className="text-muted-foreground">{t("common.noData")}</div>
                          ) : (
                            <div className="grid gap-1">
                              <div>{t("serviceDetail.score", {
                                value: score != null ? score : "-",
                              })}</div>
                              <div className="text-muted-foreground">
                                {t("serviceDetail.networkStatus", {
                                  availability: ns.isAvailable
                                    ? t("serviceDetail.available")
                                    : t("serviceDetail.unavailable"),
                                  responseTime: ns.responseTime,
                                  packetLoss: ns.packetLoss.toFixed(1),
                                })}
                              </div>
                              <div className="text-muted-foreground">
                                {t("serviceDetail.checkedAt", {
                                  value: formatDateTime(ns.lastCheckTime),
                                })}
                              </div>
                            </div>
                          )}
                        </TableCell>

                        <TableCell>
                          <div className="flex flex-wrap gap-2">
                            <Button
                              size="xs"
                              variant="secondary"
                              disabled={busy}
                              onClick={() => void evaluateNetwork(x.id)}
                            >
                              <ZapIcon className="size-3" />
                              {t("serviceDetail.evaluate")}
                            </Button>
                            <Button
                              size="xs"
                              variant="outline"
                              disabled={busy}
                              onClick={() => setEditorTargetId(x.id)}
                            >
                              <PencilLineIcon className="size-3" />
                              {t("serviceDetail.configure")}
                            </Button>
                            <Button
                              size="xs"
                              variant="outline"
                              disabled={busy}
                              onClick={() => void clearCircuitState(x.id)}
                            >
                              <ShieldOffIcon className="size-3" />
                              {t("serviceDetail.clearCircuit")}
                            </Button>
                            <Button
                              size="xs"
                              variant="destructive"
                              disabled={busy}
                              onClick={() => void deregister(x.id)}
                            >
                              <Trash2Icon className="size-3" />
                              {t("serviceDetail.deregister")}
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ServiceCircuitEditorDialog
        key={editorTargetId ?? "service-circuit-editor-closed"}
        open={!!editorTargetId}
        busy={!!(editorTargetId && actionLoading === `save:${editorTargetId}`)}
        value={editorValue}
        onOpenChange={(open) => {
          if (!open) {
            setEditorTargetId(null);
          }
        }}
        onSubmit={(value) => (editorTargetId ? saveCircuitConfig(editorTargetId, value) : undefined)}
      />
    </div>
  );
}
