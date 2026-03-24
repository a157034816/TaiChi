"use client";

import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Loader2Icon, RefreshCwIcon, SearchIcon } from "lucide-react";

import { ApiResponseError, apiGet, getApiErrorMessage } from "@/lib/api";
import { useI18n } from "@/i18n/context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
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

type PagedResult<T> = {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
};

type AuditLogListItem = {
  id: number;
  createdAtUtc: string;
  actorUserId?: number | null;
  actorUsername?: string | null;
  action: string;
  resource: string;
  traceId?: string | null;
  ip?: string | null;
  userAgent?: string | null;
};

type AuditLogDetail = AuditLogListItem & {
  beforeJson?: string | null;
  afterJson?: string | null;
};

function prettyJson(text?: string | null) {
  if (!text) {
    return "";
  }

  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

export default function AuditLogsPage() {
  const { formatDateTime, t } = useI18n();
  const [keyword, setKeyword] = useState("");
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<AuditLogListItem[]>([]);
  const [total, setTotal] = useState(0);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<AuditLogDetail | null>(null);

  const queryUrl = useMemo(() => {
    const sp = new URLSearchParams();
    sp.set("page", "1");
    sp.set("pageSize", "50");
    if (keyword.trim()) {
      sp.set("keyword", keyword.trim());
    }
    return `/api/admin/audit?${sp.toString()}`;
  }, [keyword]);

  const load = async () => {
    setLoading(true);
    try {
      const data = await apiGet<PagedResult<AuditLogListItem>>(queryUrl);
      setItems(data.items ?? []);
      setTotal(data.total ?? 0);
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
  }, []);

  const openDetail = async (id: number) => {
    setDialogOpen(true);
    setDetail(null);
    setDetailLoading(true);
    try {
      const data = await apiGet<AuditLogDetail>(`/api/admin/audit/${id}`);
      setDetail(data);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("audit.detailLoadFailed"));
    } finally {
      setDetailLoading(false);
    }
  };

  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-2">
          <CardTitle>{t("audit.title")}</CardTitle>
          <div className="flex items-center gap-2">
            <div className="relative w-64">
              <SearchIcon className="absolute left-2 top-2.5 size-4 text-muted-foreground" />
              <Input
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                placeholder={t("audit.searchPlaceholder")}
                className="pl-8"
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
        </CardHeader>
        <CardContent>
          <div className="mb-2 text-xs text-muted-foreground">
            {t("audit.totalCount", { total })}
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-24">{t("common.time")}</TableHead>
                <TableHead className="w-40">{t("common.user")}</TableHead>
                <TableHead className="w-48">{t("common.action")}</TableHead>
                <TableHead>{t("common.resource")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-sm">
                    {t("common.noData")}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((x) => (
                  <TableRow
                    key={x.id}
                    className="cursor-pointer"
                    onClick={() => void openDetail(x.id)}
                  >
                    <TableCell className="text-xs text-muted-foreground">
                      {formatDateTime(x.createdAtUtc)}
                    </TableCell>
                    <TableCell className="text-sm">
                      {x.actorUsername ?? "-"}
                    </TableCell>
                    <TableCell>
                      <Badge variant="secondary" className="font-mono">
                        {x.action}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">
                      {x.resource}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>{t("audit.detailTitle")}</DialogTitle>
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
              <div className="grid gap-1 text-sm">
                <div className="text-muted-foreground">
                  #{detail.id} · {formatDateTime(detail.createdAtUtc)}
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="secondary" className="font-mono">
                    {detail.action}
                  </Badge>
                  <span className="text-muted-foreground">
                    {detail.actorUsername ?? "-"}
                  </span>
                  <span className="font-mono text-xs text-muted-foreground">
                    {detail.resource}
                  </span>
                </div>
              </div>

              <Separator />

              <div className="grid gap-2">
                <div className="text-sm font-medium">{t("audit.before")}</div>
                <pre className="max-h-60 overflow-auto rounded-md border bg-muted p-3 text-xs">
                  {prettyJson(detail.beforeJson) || "-"}
                </pre>
              </div>

              <div className="grid gap-2">
                <div className="text-sm font-medium">{t("audit.after")}</div>
                <pre className="max-h-60 overflow-auto rounded-md border bg-muted p-3 text-xs">
                  {prettyJson(detail.afterJson) || "-"}
                </pre>
              </div>

              <Separator />

              <div className="grid gap-1 text-xs text-muted-foreground">
                <div>{t("audit.traceId")}: {detail.traceId ?? "-"}</div>
                <div>{t("audit.ip")}: {detail.ip ?? "-"}</div>
                <div className="break-all">{t("audit.userAgent")}: {detail.userAgent ?? "-"}</div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
