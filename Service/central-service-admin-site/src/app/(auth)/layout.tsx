import { LocaleSwitcher } from "@/components/locale-switcher";

export default function AuthLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <div className="relative flex min-h-screen items-center justify-center bg-muted/40 p-6">
      <div className="absolute right-6 top-6">
        <LocaleSwitcher />
      </div>
      {children}
    </div>
  );
}

