"use client";

import { useEffect, useEffectEvent, useMemo, useState } from "react";
import { toast } from "sonner";
import {
  CheckCircleIcon,
  CopyIcon,
  DiffIcon,
  FilePlus2Icon,
  Loader2Icon,
  RefreshCwIcon,
  RotateCcwIcon,
  SaveIcon,
  SendIcon,
} from "lucide-react";

import { ApiResponseError, apiGet, apiPost, apiPut, getApiErrorMessage } from "@/lib/api";
import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

type ConfigVersionListItem = {
  id: number;
  versionNo: number;
  status: string;
  comment?: string | null;
  basedOnVersionId?: number | null;
  createdAtUtc: string;
  createdByUsername?: string | null;
  updatedAtUtc: string;
  updatedByUsername?: string | null;
  publishedAtUtc?: string | null;
  publishedByUsername?: string | null;
};

type ConfigVersionDetail = ConfigVersionListItem & {
  configJson: string;
};

type CurrentConfigResponse = {
  currentVersionId?: number | null;
  currentVersionNo?: number | null;
  configJson: string;
};

type ConfigDiffResponse = {
  baseVersionId: number;
  targetVersionId: number;
  baseJson: string;
  targetJson: string;
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

function statusVariant(status: string) {
  if (status.toLowerCase() === "published") return "default";
  if (status.toLowerCase() === "draft") return "secondary";
  return "outline";
}

export default function ConfigVersionsPage() {
  const { formatDateTime, t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [versions, setVersions] = useState<ConfigVersionListItem[]>([]);
  const [current, setCurrent] = useState<CurrentConfigResponse | null>(null);
  const [history, setHistory] = useState<PublishHistoryItem[]>([]);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogTab, setDialogTab] = useState<"json" | "diff">("json");
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<ConfigVersionDetail | null>(null);
  const [editComment, setEditComment] = useState("");
  const [editJson, setEditJson] = useState("");

  const [diffLoading, setDiffLoading] = useState(false);
  const [diff, setDiff] = useState<ConfigDiffResponse | null>(null);

  const currentVersionId = current?.currentVersionId ?? null;
  const formatStatusLabel = (status: string) => {
    const normalized = status.toLowerCase();
    if (normalized === "draft") return t("common.draft");
    if (normalized === "published") return t("common.publishedState");
    if (normalized === "publish") return t("common.publish");
    if (normalized === "rollback") return t("common.rollback");
    return status;
  };

  const load = async () => {
    setLoading(true);
    try {
      const [currentResp, versionsResp, historyResp] = await Promise.all([
        apiGet<CurrentConfigResponse>("/api/admin/config/current"),
        apiGet<ConfigVersionListItem[]>("/api/admin/config/versions"),
        apiGet<PublishHistoryItem[]>("/api/admin/config/publish-history?take=50"),
      ]);
      setCurrent(currentResp);
      setVersions(versionsResp);
      setHistory(historyResp);
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

  const openVersion = async (id: number) => {
    setDialogOpen(true);
    setDialogTab("json");
    setDetailLoading(true);
    setDiff(null);
    setDiffLoading(false);
    try {
      const data = await apiGet<ConfigVersionDetail>(
        `/api/admin/config/versions/${id}`
      );
      setDetail(data);
      setEditComment(data.comment ?? "");
      setEditJson(data.configJson ?? "");
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("common.loadFailed"));
    } finally {
      setDetailLoading(false);
    }
  };

  const createDraft = async () => {
    try {
      const data = await apiPost<ConfigVersionDetail>("/api/admin/config/versions", {
        comment: "draft",
        basedOnVersionId: currentVersionId,
      });
      toast.success(
        t("configVersions.createDraftSuccess", { versionNo: data.versionNo })
      );
      await load();
      await openVersion(data.id);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.createDraftFailed"));
    }
  };

  const canEdit = useMemo(() => {
    return detail?.status?.toLowerCase() === "draft";
  }, [detail?.status]);

  const saveDraft = async () => {
    if (!detail) return;
    try {
      const data = await apiPut<ConfigVersionDetail>(
        `/api/admin/config/versions/${detail.id}`,
        {
          configJson: editJson,
          comment: editComment,
        }
      );
      setDetail(data);
      toast.success(t("configVersions.saveSuccess"));
      await load();
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.saveFailed"));
    }
  };

  const validate = async () => {
    if (!detail) return;
    try {
      const errors = await apiPost<string[]>(
        `/api/admin/config/versions/${detail.id}/validate`
      );
      if (!errors || errors.length === 0) {
        toast.success(t("configVersions.validateSuccess"));
      } else {
        toast.error(errors.join("；"));
      }
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.validateFailed"));
    }
  };

  const publish = async () => {
    if (!detail) return;
    try {
      await apiPost<object>(`/api/admin/config/versions/${detail.id}/publish`, {
        note: "publish",
      });
      toast.success(t("configVersions.publishSuccess"));
      await load();
      await openVersion(detail.id);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.publishFailed"));
    }
  };

  const rollback = async () => {
    if (!detail) return;
    try {
      await apiPost<object>(`/api/admin/config/versions/${detail.id}/rollback`, {
        note: "rollback",
      });
      toast.success(t("configVersions.rollbackSuccess"));
      await load();
      await openVersion(detail.id);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.rollbackFailed"));
    }
  };

  const loadDiff = async () => {
    if (!detail) return;
    setDiffLoading(true);
    try {
      const url = currentVersionId
        ? `/api/admin/config/versions/${detail.id}/diff?baseVersionId=${currentVersionId}`
        : `/api/admin/config/versions/${detail.id}/diff`;
      const data = await apiGet<ConfigDiffResponse>(url);
      setDiff(data);
      setDialogTab("diff");
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("configVersions.diffLoadFailed"));
    } finally {
      setDiffLoading(false);
    }
  };

  const currentSummary =
    current?.currentVersionId && current.currentVersionNo
      ? `v${current.currentVersionNo} (#${current.currentVersionId})`
      : t("common.unpublished");

  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-2">
          <div className="grid gap-1">
            <CardTitle>{t("configVersions.title")}</CardTitle>
            <CardDescription>
              {t("configVersions.description")}
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="secondary" onClick={load} disabled={loading}>
              {loading ? (
                <Loader2Icon className="mr-2 size-4 animate-spin" />
              ) : (
                <RefreshCwIcon className="mr-2 size-4" />
              )}
              {t("common.refresh")}
            </Button>
            <Button onClick={createDraft} disabled={loading}>
              <FilePlus2Icon className="mr-2 size-4" />
              {t("configVersions.createDraft")}
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="mb-3 text-sm">
            {t("configVersions.currentVersion")}<span className="font-mono">{currentSummary}</span>
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-28">{t("common.version")}</TableHead>
                <TableHead className="w-24">{t("common.status")}</TableHead>
                <TableHead>{t("common.comment")}</TableHead>
                <TableHead className="w-40">{t("common.updated")}</TableHead>
                <TableHead className="w-40">{t("common.published")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {versions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                versions.map((x) => {
                  const isCurrent = currentVersionId === x.id;
                  return (
                    <TableRow
                      key={x.id}
                      className="cursor-pointer"
                      onClick={() => void openVersion(x.id)}
                    >
                      <TableCell className="font-mono">
                        v{x.versionNo}{" "}
                        {isCurrent ? (
                          <Badge className="ml-2" variant="default">
                            {t("configVersions.currentBadge")}
                          </Badge>
                        ) : null}
                      </TableCell>
                      <TableCell>
                        <Badge variant={statusVariant(x.status)}>
                          {formatStatusLabel(x.status)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {x.comment ?? "-"}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {formatDateTime(x.updatedAtUtc)}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {formatDateTime(x.publishedAtUtc)}
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("configVersions.publishHistoryTitle")}</CardTitle>
          <CardDescription>{t("configVersions.publishHistoryDescription")}</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-28">{t("common.time")}</TableHead>
                <TableHead className="w-24">{t("common.action")}</TableHead>
                <TableHead className="w-32">{t("common.from")}</TableHead>
                <TableHead className="w-32">{t("common.to")}</TableHead>
                <TableHead className="w-40">{t("common.actor")}</TableHead>
                <TableHead>{t("common.note")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {history.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                history.map((x) => (
                  <TableRow key={x.id}>
                    <TableCell className="text-xs text-muted-foreground">
                      {formatDateTime(x.createdAtUtc)}
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(x.action)}>{formatStatusLabel(x.action)}</Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">
                      {x.fromVersionId ? `#${x.fromVersionId}` : "-"}
                    </TableCell>
                    <TableCell className="font-mono text-xs">
                      #{x.toVersionId}
                    </TableCell>
                    <TableCell className="text-sm">
                      {x.actorUsername ?? "-"}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {x.note ?? "-"}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>{t("configVersions.configVersionTitle")}</DialogTitle>
          </DialogHeader>

          {detailLoading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2Icon className="size-4 animate-spin" />
              {t("common.loading")}
            </div>
          ) : !detail ? (
            <div className="text-sm text-muted-foreground">{t("common.noData")}</div>
          ) : (
            <div className="grid gap-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex flex-wrap items-center gap-2">
                  <div className="font-mono text-sm">
                    v{detail.versionNo} (#{detail.id})
                  </div>
                  <Badge variant={statusVariant(detail.status)}>
                    {formatStatusLabel(detail.status)}
                  </Badge>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <Button variant="secondary" onClick={validate}>
                    <CheckCircleIcon className="mr-2 size-4" />
                    {t("common.validate")}
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={loadDiff}
                    disabled={diffLoading}
                  >
                    {diffLoading ? (
                      <Loader2Icon className="mr-2 size-4 animate-spin" />
                    ) : (
                      <DiffIcon className="mr-2 size-4" />
                    )}
                    {t("common.diff")}
                  </Button>
                  {canEdit ? (
                    <>
                      <Button onClick={saveDraft}>
                        <SaveIcon className="mr-2 size-4" />
                        {t("common.save")}
                      </Button>
                      <Button variant="default" onClick={publish}>
                        <SendIcon className="mr-2 size-4" />
                        {t("common.publish")}
                      </Button>
                    </>
                  ) : (
                    <Button variant="destructive" onClick={rollback}>
                      <RotateCcwIcon className="mr-2 size-4" />
                      {t("common.rollback")}
                    </Button>
                  )}
                </div>
              </div>

              <Separator />

              <div className="grid gap-2">
                <div className="text-sm font-medium">{t("configVersions.commentLabel")}</div>
                <Input
                  value={editComment}
                  onChange={(e) => setEditComment(e.target.value)}
                  placeholder={t("configVersions.commentPlaceholder")}
                  disabled={!canEdit}
                />
              </div>

              <Tabs value={dialogTab} onValueChange={(v) => setDialogTab(v as "json" | "diff")}>
                <TabsList>
                  <TabsTrigger value="json">{t("common.json")}</TabsTrigger>
                  <TabsTrigger value="diff">{t("common.diff")}</TabsTrigger>
                </TabsList>

                <TabsContent value="json" className="mt-2">
                  <div className="grid gap-2">
                    <div className="flex items-center justify-between">
                      <div className="text-sm font-medium">{t("configVersions.configJsonLabel")}</div>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={async () => {
                          await navigator.clipboard.writeText(editJson);
                          toast.success(t("configVersions.copySuccess"));
                        }}
                      >
                        <CopyIcon className="mr-2 size-4" />
                        {t("common.copy")}
                      </Button>
                    </div>
                    <textarea
                      value={editJson}
                      onChange={(e) => setEditJson(e.target.value)}
                      className="min-h-72 w-full rounded-md border bg-muted p-3 font-mono text-xs"
                      readOnly={!canEdit}
                    />
                  </div>
                </TabsContent>

                <TabsContent value="diff" className="mt-2">
                  {!diff ? (
                    <div className="text-sm text-muted-foreground">
                      {t("configVersions.diffEmpty")}
                    </div>
                  ) : (
                    <div className="grid gap-3 md:grid-cols-2">
                      <div className="grid gap-2">
                        <div className="text-sm font-medium">
                          {t("configVersions.base", { value: diff.baseVersionId })}
                        </div>
                        <pre className="max-h-80 overflow-auto rounded-md border bg-muted p-3 text-xs">
                          {diff.baseJson}
                        </pre>
                      </div>
                      <div className="grid gap-2">
                        <div className="text-sm font-medium">
                          {t("configVersions.target", { value: diff.targetVersionId })}
                        </div>
                        <pre className="max-h-80 overflow-auto rounded-md border bg-muted p-3 text-xs">
                          {diff.targetJson}
                        </pre>
                      </div>
                    </div>
                  )}
                </TabsContent>
              </Tabs>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
