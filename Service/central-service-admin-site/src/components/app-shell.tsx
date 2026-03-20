"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useEffectEvent, useState } from "react";
import {
  ActivityIcon,
  ClipboardListIcon,
  LayoutDashboardIcon,
  LogOutIcon,
  MenuIcon,
  NetworkIcon,
  SettingsIcon,
  UsersIcon,
} from "lucide-react";
import { toast } from "sonner";

import { ApiResponseError, apiGet, apiPost, getApiErrorMessage } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useI18n } from "@/i18n/context";
import { type MessageKey } from "@/i18n/core";
import { Button } from "@/components/ui/button";
import { LocaleSwitcher } from "@/components/locale-switcher";
import { Separator } from "@/components/ui/separator";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { ThemeToggle } from "@/components/theme-toggle";

type NavItem = {
  href: string;
  labelKey: MessageKey;
  icon: React.ComponentType<{ className?: string }>;
};

type MeResponse = {
  userId: number;
  username: string;
  roles: string[];
  permissions: string[];
};

const navItems: NavItem[] = [
  { href: "/", labelKey: "appShell.nav.dashboard", icon: LayoutDashboardIcon },
  { href: "/services", labelKey: "appShell.nav.services", icon: NetworkIcon },
  { href: "/config-versions", labelKey: "appShell.nav.configVersions", icon: SettingsIcon },
  { href: "/audit", labelKey: "appShell.nav.audit", icon: ClipboardListIcon },
  { href: "/monitoring", labelKey: "appShell.nav.monitoring", icon: ActivityIcon },
  { href: "/users", labelKey: "appShell.nav.users", icon: UsersIcon },
];

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useI18n();
  const pathname = usePathname();

  return (
    <nav className="grid gap-1">
      {navItems.map((item) => {
        const active =
          pathname === item.href ||
          (item.href !== "/" && pathname.startsWith(item.href + "/"));
        const Icon = item.icon;

        return (
          <Link
            key={item.href}
            href={item.href}
            onClick={onNavigate}
            className={cn(
              "flex items-center gap-2 rounded-md px-3 py-2 text-sm hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
              active && "bg-sidebar-accent text-sidebar-accent-foreground"
            )}
          >
            <Icon className="size-4" />
            {t(item.labelKey)}
          </Link>
        );
      })}
    </nav>
  );
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();
  const router = useRouter();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [me, setMe] = useState<MeResponse | null>(null);

  const match =
    navItems.find((x) => x.href === pathname) ??
    navItems.find((x) => x.href !== "/" && pathname.startsWith(x.href + "/"));
  const pageTitle = match ? t(match.labelKey) : t("appShell.title");
  const notifySessionFetchFailed = useEffectEvent(() => {
    toast.error(t("appShell.sessionFetchFailed"));
  });

  useEffect(() => {
    let mounted = true;

    (async () => {
      try {
        const data = await apiGet<MeResponse>("/api/auth/me");
        if (mounted) {
          setMe(data);
        }
      } catch (error) {
        if (error instanceof ApiResponseError && error.code === 401) {
          router.replace(`/login?redirect=${encodeURIComponent(pathname)}`);
          return;
        }

        notifySessionFetchFailed();
      }
    })();

    return () => {
      mounted = false;
    };
  }, [pathname, router]);

  const logout = async () => {
    try {
      await apiPost<object>("/api/auth/logout");
      toast.success(t("appShell.logoutSuccess"));
      router.replace("/login");
    } catch (error) {
      if (error instanceof ApiResponseError) {
        toast.error(getApiErrorMessage(error, t));
        return;
      }
      toast.error(t("appShell.logoutFailed"));
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <div className="flex">
        <aside className="hidden md:flex md:w-64 md:flex-col md:border-r md:bg-sidebar">
          <div className="px-4 py-4 font-semibold tracking-tight">
            {t("appShell.title")}
          </div>
          <div className="px-2">
            <NavLinks />
          </div>
          <div className="mt-auto px-2 py-2">
            <Separator className="my-2" />
            <Button
              variant="ghost"
              className="w-full justify-start gap-2"
              onClick={logout}
            >
              <LogOutIcon className="size-4" />
              {t("appShell.logout")}
            </Button>
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-10 flex h-14 items-center gap-2 border-b bg-background/80 px-4 backdrop-blur">
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button variant="ghost" size="icon" className="md:hidden">
                  <MenuIcon className="size-5" />
                </Button>
              </SheetTrigger>
              <SheetContent side="left" className="w-72 p-0">
                <SheetHeader className="px-4 py-4">
                  <SheetTitle>{t("appShell.title")}</SheetTitle>
                </SheetHeader>
                <div className="px-2">
                  <NavLinks onNavigate={() => setMobileOpen(false)} />
                </div>
                <div className="mt-auto px-2 py-2">
                  <Separator className="my-2" />
                  <Button
                    variant="ghost"
                    className="w-full justify-start gap-2"
                    onClick={logout}
                  >
                    <LogOutIcon className="size-4" />
                    {t("appShell.logout")}
                  </Button>
                </div>
              </SheetContent>
            </Sheet>

            <div className="min-w-0 flex-1 truncate text-sm font-medium">
              {pageTitle}
            </div>

            {me ? (
              <div className="hidden text-sm text-muted-foreground sm:block">
                {me.username}
              </div>
            ) : null}

            <LocaleSwitcher variant="ghost" />
            <ThemeToggle />
          </header>

          <main className="min-w-0 flex-1 p-4">{children}</main>
        </div>
      </div>
    </div>
  );
}
