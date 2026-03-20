"use client";

import { useEffect, useEffectEvent, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { toast } from "sonner";

import { ApiResponseError, apiGet, apiPost, getApiErrorMessage } from "@/lib/api";
import { translate, type MessageKey, type MessageParams } from "@/i18n/core";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { useI18n } from "@/i18n/context";

function createSchema(
  t: (key: MessageKey, params?: MessageParams) => string
) {
  return z.object({
    username: z.string().min(1, t("login.usernameRequired")),
    password: z.string().min(1, t("login.passwordRequired")),
  });
}

type FormValues = z.infer<ReturnType<typeof createSchema>>;

type LoginResponse = {
  userId: number;
  username: string;
  roles: string[];
  permissions: string[];
};

type BootstrapStatusResponse = {
  enabled: boolean;
  hasAnyUser: boolean;
  isLoopbackRequest: boolean;
  canBootstrap: boolean;
  message: string;
};

export function LoginPageClient() {
  const { locale, t } = useI18n();
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTo = useMemo(() => {
    const redirect = searchParams.get("redirect");
    return redirect && redirect.startsWith("/") ? redirect : "/";
  }, [searchParams]);

  const schema = useMemo(
    () => createSchema((key, params) => translate(locale, key, params)),
    [locale]
  );
  const [loading, setLoading] = useState(false);
  const [bootstrapStatus, setBootstrapStatus] = useState<BootstrapStatusResponse | null>(null);
  const [bootstrapStatusLoading, setBootstrapStatusLoading] = useState(true);
  const [bootstrapStatusError, setBootstrapStatusError] = useState<string | null>(null);
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { username: "", password: "" },
  });
  const resolveBootstrapStatusError = useEffectEvent((error: unknown) => {
    return error instanceof ApiResponseError
      ? getApiErrorMessage(error, t)
      : t("login.bootstrapStatusLoadFailed");
  });

  useEffect(() => {
    void form.trigger();
  }, [form, schema]);

  useEffect(() => {
    let mounted = true;

    (async () => {
      try {
        const status = await apiGet<BootstrapStatusResponse>("/api/auth/bootstrap/status");
        if (!mounted) {
          return;
        }

        setBootstrapStatus(status);
        setBootstrapStatusError(null);
      } catch (error) {
        if (!mounted) {
          return;
        }

        setBootstrapStatus(null);
        setBootstrapStatusError(resolveBootstrapStatusError(error));
      } finally {
        if (mounted) {
          setBootstrapStatusLoading(false);
        }
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  const submit = async (mode: "login" | "bootstrap", values: FormValues) => {
    if (mode === "bootstrap") {
      if (bootstrapStatusLoading) {
        toast.error(t("login.bootstrapStatusPending"));
        return;
      }

      if (bootstrapStatusError) {
        toast.error(bootstrapStatusError);
        return;
      }

      if (!bootstrapStatus?.canBootstrap) {
        toast.error(bootstrapStatus?.message ?? t("login.bootstrapUnavailable"));
        return;
      }
    }

    setLoading(true);
    try {
      const url = mode === "login" ? "/api/auth/login" : "/api/auth/bootstrap";
      await apiPost<LoginResponse>(url, values);
      toast.success(
        mode === "login" ? t("login.loginSuccess") : t("login.bootstrapSuccess")
      );
      router.replace(redirectTo);
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("login.actionFailed"));
    } finally {
      setLoading(false);
    }
  };

  const bootstrapDisabled =
    loading || bootstrapStatusLoading || !!bootstrapStatusError || !bootstrapStatus?.canBootstrap;

  const bootstrapHint = bootstrapStatusLoading
    ? t("login.bootstrapStatusLoading")
    : bootstrapStatusError ?? bootstrapStatus?.message ?? t("login.bootstrapUnavailable");

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>{t("login.title")}</CardTitle>
        <CardDescription>
          {t("login.description")}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form className="space-y-4">
            <FormField
              control={form.control}
              name="username"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("login.username")}</FormLabel>
                  <FormControl>
                    <Input autoComplete="username" placeholder="admin" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="password"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("login.password")}</FormLabel>
                  <FormControl>
                    <Input
                      type="password"
                      autoComplete="current-password"
                      placeholder="••••••••"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </form>
        </Form>
        <p className="mt-4 text-sm text-muted-foreground">{bootstrapHint}</p>
      </CardContent>
      <CardFooter className="flex gap-2">
        <Button
          className="flex-1"
          disabled={loading}
          onClick={form.handleSubmit((v) => submit("login", v))}
        >
          {t("login.login")}
        </Button>
        <Button
          className="flex-1"
          variant="secondary"
          disabled={bootstrapDisabled}
          onClick={form.handleSubmit((v) => submit("bootstrap", v))}
        >
          {t("login.bootstrap")}
        </Button>
      </CardFooter>
    </Card>
  );
}
