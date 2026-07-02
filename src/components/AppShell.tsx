import { useEffect, useRef, useState, type ReactNode } from "react";
import { useNavigate, useRouterState } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { BottomTabs } from "./BottomTabs";

type Shell = {
  language: string; isRtl: boolean; themeMode: string; labels: Record<string, string>;
  onboardingCompleted: boolean;
};

const TAB_ROUTES = ["/", "/calendar", "/qibla", "/tasbih", "/settings"];

export function AppShell({ children }: { children: ReactNode }) {
  const [shellVersion, setShellVersion] = useState(0);
  const { data } = useSnapshot<Shell>("app.getShellSnapshot", undefined, [shellVersion]);
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const navigate = useNavigate();
  const routeStack = useRef<string[]>([]);
  const routeIndex = useRef(0);
  const nativeNavigation = useRef(false);

  useEffect(() => {
    const refresh = () => setShellVersion((value) => value + 1);
    window.addEventListener("prayadfree:shell-refresh", refresh);
    return () => window.removeEventListener("prayadfree:shell-refresh", refresh);
  }, []);

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
    const navigateDirection = (direction: "back" | "forward") => {
      if (direction === "back") {
        if (routeIndex.current <= 0) {
          return false;
        }

        routeIndex.current -= 1;
      } else {
        if (routeIndex.current >= routeStack.current.length - 1) {
          return false;
        }

        routeIndex.current += 1;
      }

      nativeNavigation.current = true;
      void navigate({ to: routeStack.current[routeIndex.current] });
      return true;
    };

    const installNavigationHandler = () => {
      if (!window.mauiWebber) {
        return;
      }

      window.mauiWebber.navigation = {
        canGoBack: () => routeIndex.current > 0,
        canGoForward: () => routeIndex.current < routeStack.current.length - 1,
        back: () => navigateDirection("back"),
        forward: () => navigateDirection("forward"),
      };
    };

    const handleNavigationEvent = (event: Event) => {
      const customEvent = event as CustomEvent<{ direction?: string; handled?: boolean }>;
      const direction = customEvent.detail?.direction;
      if (direction !== "back" && direction !== "forward") {
        return;
      }

      if (!navigateDirection(direction)) {
        return;
      }

      customEvent.detail.handled = true;
      customEvent.preventDefault();
    };

    installNavigationHandler();
    window.addEventListener("mauiwebber:ready", installNavigationHandler);
    window.addEventListener("mauiwebber:navigation", handleNavigationEvent);

    return () => {
      window.removeEventListener("mauiwebber:ready", installNavigationHandler);
      window.removeEventListener("mauiwebber:navigation", handleNavigationEvent);
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
