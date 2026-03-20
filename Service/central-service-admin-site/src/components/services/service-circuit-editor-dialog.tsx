"use client";

import { useState } from "react";

import { useI18n } from "@/i18n/context";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export type ServiceCircuitFormValue = {
  maxAttempts: number;
  failureThreshold: number;
  breakDurationMinutes: number;
  recoveryThreshold: number;
};

type Props = {
  open: boolean;
  busy: boolean;
  value: ServiceCircuitFormValue;
  onOpenChange: (open: boolean) => void;
  onSubmit: (value: ServiceCircuitFormValue) => Promise<void> | void;
};

function normalizePositiveInt(value: string, fallback: number) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return fallback;
  }
  return parsed;
}

export function ServiceCircuitEditorDialog({
  open,
  busy,
  value,
  onOpenChange,
  onSubmit,
}: Props) {
  const { t } = useI18n();
  const [form, setForm] = useState<ServiceCircuitFormValue>(value);

  const submit = async () => {
    await onSubmit({
      maxAttempts: normalizePositiveInt(String(form.maxAttempts), value.maxAttempts),
      failureThreshold: normalizePositiveInt(String(form.failureThreshold), value.failureThreshold),
      breakDurationMinutes: normalizePositiveInt(
        String(form.breakDurationMinutes),
        value.breakDurationMinutes
      ),
      recoveryThreshold: normalizePositiveInt(
        String(form.recoveryThreshold),
        value.recoveryThreshold
      ),
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t("serviceCircuitEditor.title")}</DialogTitle>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="grid gap-2">
            <Label htmlFor="maxAttempts">{t("serviceCircuitEditor.maxAttempts")}</Label>
            <Input
              id="maxAttempts"
              type="number"
              min={1}
              value={form.maxAttempts}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  maxAttempts: normalizePositiveInt(event.target.value, prev.maxAttempts),
                }))
              }
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="failureThreshold">
              {t("serviceCircuitEditor.failureThreshold")}
            </Label>
            <Input
              id="failureThreshold"
              type="number"
              min={1}
              value={form.failureThreshold}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  failureThreshold: normalizePositiveInt(
                    event.target.value,
                    prev.failureThreshold
                  ),
                }))
              }
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="breakDurationMinutes">
              {t("serviceCircuitEditor.breakDurationMinutes")}
            </Label>
            <Input
              id="breakDurationMinutes"
              type="number"
              min={1}
              value={form.breakDurationMinutes}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  breakDurationMinutes: normalizePositiveInt(
                    event.target.value,
                    prev.breakDurationMinutes
                  ),
                }))
              }
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="recoveryThreshold">
              {t("serviceCircuitEditor.recoveryThreshold")}
            </Label>
            <Input
              id="recoveryThreshold"
              type="number"
              min={1}
              value={form.recoveryThreshold}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  recoveryThreshold: normalizePositiveInt(
                    event.target.value,
                    prev.recoveryThreshold
                  ),
                }))
              }
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button onClick={() => void submit()} disabled={busy}>
            {t("common.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
