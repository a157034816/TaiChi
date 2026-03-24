import { Suspense } from "react";
import { LoadingMessage } from "@/components/loading-message";
import { LoginPageClient } from "./page-client";

export default function LoginPage() {
  return (
    <Suspense fallback={<LoadingMessage />}>
      <LoginPageClient />
    </Suspense>
  );
}

