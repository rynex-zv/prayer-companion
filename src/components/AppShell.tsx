import { useEffect, useRef, type ReactNode } from "react";
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
  const routeStack = useRef<string[]>([]);
  const routeIndex = useRef(0);
  const nativeNavigation = useRef(false);

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

  useEffect(() => {
    if (routeStack.current.length === 0) {
      routeStack.current = [pathname];
      routeIndex.current = 0;
      return;
    }

    if (nativeNavigation.current) {
      nativeNavigation.current = false;
      return;
    }

    if (routeStack.current[routeIndex.current] === pathname) {
      return;
    }

    routeStack.current = routeStack.current.slice(0, routeIndex.current + 1);
    routeStack.current.push(pathname);
    routeIndex.current = routeStack.current.length - 1;
  }, [pathname]);

  useEffect(() => {
    const installNavigationHandler = () => {
      if (!window.mauiWebber) {
        return;
      }

      window.mauiWebber.navigation = {
      canGoBack: () => routeIndex.current > 0,
      canGoForward: () => routeIndex.current < routeStack.current.length - 1,
      back: () => {
        if (routeIndex.current <= 0) {
          return false;
        }

        routeIndex.current -= 1;
        nativeNavigation.current = true;
        void navigate({ to: routeStack.current[routeIndex.current] });
        return true;
      },
      forward: () => {
        if (routeIndex.current >= routeStack.current.length - 1) {
          return false;
        }

        routeIndex.current += 1;
        nativeNavigation.current = true;
        void navigate({ to: routeStack.current[routeIndex.current] });
        return true;
      },
      };
    };

    installNavigationHandler();
    window.addEventListener("mauiwebber:ready", installNavigationHandler);

    return () => {
      window.removeEventListener("mauiwebber:ready", installNavigationHandler);
      if (window.mauiWebber) {
        window.mauiWebber.navigation = null;
      }
    };
  }, [navigate]);

  const labels = data?.labels ?? {};
  const showTabs = TAB_ROUTES.includes(pathname);

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-md flex-col">
      <main className="safe-top flex-1 px-4 pb-6 pt-3">{children}</main>
      {showTabs ? <BottomTabs labels={labels} /> : null}
    </div>
  );
}
