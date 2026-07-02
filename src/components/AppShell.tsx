import { useEffect, type ReactNode } from "react";
import { useNavigate, useRouterState } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { BottomTabs } from "./BottomTabs";

type Shell = {
  language: string; isRtl: boolean; themeMode: string; labels: Record<string, string>;
  onboardingCompleted: boolean;
};

const TAB_ROUTES = ["/", "/calendar", "/qibla", "/tasbih", "/settings"];

export function AppShell({ children }: { children: ReactNode }) {
  const { data } = useSnapshot<Shell>("app.getShellSnapshot");
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const navigate = useNavigate();

  useEffect(() => {
    if (!data) return;
    document.documentElement.dir = data.isRtl ? "rtl" : "ltr";
    document.documentElement.lang = data.language || (data.isRtl ? "ar" : "en");
    if (data.themeMode === "dark") document.documentElement.classList.add("dark");
    else document.documentElement.classList.remove("dark");
  }, [data]);

  useEffect(() => {
    if (!data || data.onboardingCompleted || pathname === "/onboarding") {
      return;
    }

    void navigate({ to: "/onboarding", replace: true });
  }, [data, navigate, pathname]);

  const labels = data?.labels ?? {};
  const showTabs = TAB_ROUTES.includes(pathname);

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-md flex-col">
      <main className="safe-top flex-1 px-4 pb-6 pt-3">{children}</main>
      {showTabs ? <BottomTabs labels={labels} /> : null}
    </div>
  );
}
