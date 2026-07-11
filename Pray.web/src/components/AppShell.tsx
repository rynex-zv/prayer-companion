import { useEffect, useRef, type ReactNode } from "react";
import { useNavigate, useRouterState } from "@tanstack/react-router";
import { BottomTabs } from "./BottomTabs";
import { mauiCall } from "@/native/mauiWebberClient";
import { bootstrapAppState, languageProxy, useAppStore } from "@/state/appStore";
import { cn } from "@/lib/utils";
import { ShakeDataResetButton } from "./ShakeDataResetButton";

const TAB_ROUTES = ["/", "/calendar", "/qibla", "/tasbih", "/settings"];
const INSPECTABLE_ROUTES = [
  "/",
  "/calendar",
  "/qibla",
  "/tasbih",
  "/settings",
  "/settings/locations",
  "/settings/theme",
  "/settings/adhan",
  "/settings/notifications",
  "/settings/permissions",
  "/settings/alarms",
  "/settings/tasbih",
  "/settings/about",
  "/onboarding",
  "/alarm",
] as const;

declare global {
  interface Window {
    prayerCompanion?: {
      getRoutes: () => readonly string[];
      navigate: (route: string) => Promise<boolean>;
      currentRoute: () => string;
      inspect: () => {
        route: string;
        lang: string;
        dir: string;
        selectors: { name: string; tag: string; text: string; value?: string; checked?: boolean }[];
      };
      click: (selectorName: string) => boolean;
      setValue: (selectorName: string, value: string | number | boolean) => boolean;
      call: typeof mauiCall;
    };
  }
}

export function AppShell({ children }: { children: ReactNode }) {
  const shell = useAppStore((state) => ({
    language: state.languageObject.code,
    direction: state.languageObject.direction,
    onboardingCompleted: state.onboardingCompleted,
  }));
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const navigate = useNavigate();
  const routeStack = useRef<string[]>([]);
  const routeIndex = useRef(0);
  const nativeNavigation = useRef(false);

  useEffect(() => {
    void bootstrapAppState();
  }, []);

  useEffect(() => {
    if (shell.onboardingCompleted || pathname === "/onboarding" || pathname === "/alarm") {
      return;
    }

    void navigate({ to: "/onboarding", replace: true });
  }, [navigate, pathname, shell.onboardingCompleted]);

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

  useEffect(() => {
    window.prayerCompanion = {
      getRoutes: () => INSPECTABLE_ROUTES,
      navigate: async (route: string) => {
        if (!INSPECTABLE_ROUTES.includes(route as (typeof INSPECTABLE_ROUTES)[number])) {
          return false;
        }

        void navigate({ to: route });
        return true;
      },
      currentRoute: () => pathname,
      inspect: () => ({
        route: pathname,
        lang: shell.language,
        dir: shell.direction,
        selectors: Array.from(document.querySelectorAll<HTMLElement>("[data-selector-name]")).map((element) => ({
          name: element.dataset.selectorName ?? "",
          tag: element.tagName.toLowerCase(),
          text: element.innerText?.trim().slice(0, 120) ?? "",
          value: element.isContentEditable
            ? element.textContent?.trim()
            : element instanceof HTMLInputElement || element instanceof HTMLSelectElement
            ? element.value
            : undefined,
          checked: element instanceof HTMLInputElement && element.type === "checkbox"
            ? element.checked
            : element.getAttribute("aria-checked") === "true"
              ? true
              : element.getAttribute("aria-checked") === "false"
                ? false
                : undefined,
        })),
      }),
      click: (selectorName: string) => {
        const element = document.querySelector<HTMLElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
        if (!element) {
          return false;
        }

        element.click();
        return true;
      },
      setValue: (selectorName: string, value: string | number | boolean) => {
        const element = document.querySelector<HTMLElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
        if (element?.isContentEditable) {
          element.textContent = String(value);
          element.dispatchEvent(new Event("input", { bubbles: true }));
          element.dispatchEvent(new FocusEvent("blur", { bubbles: true }));
          return true;
        }

        if (!(element instanceof HTMLInputElement || element instanceof HTMLSelectElement || element instanceof HTMLTextAreaElement)) {
          return false;
        }

        const next = String(value);
        const valueSetter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(element), "value")?.set;
        valueSetter?.call(element, next);
        element.dispatchEvent(new Event("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
        element.dispatchEvent(new FocusEvent("blur", { bubbles: true }));
        return true;
      },
      call: mauiCall,
    };

    return () => {
      delete window.prayerCompanion;
    };
  }, [navigate, pathname, shell.direction, shell.language]);

  const showTabs = TAB_ROUTES.includes(pathname);

  return (
    <div className="min-h-screen w-full bg-background md:h-screen md:overflow-hidden md:bg-[radial-gradient(ellipse_at_top,_color-mix(in_oklab,var(--color-primary)_12%,transparent),transparent_60%),radial-gradient(ellipse_at_bottom_right,_color-mix(in_oklab,var(--color-primary)_8%,transparent),transparent_55%)] md:flex md:items-center md:justify-center md:py-8 md:px-6">
      <div
        className="relative mx-auto flex min-h-screen w-full max-w-md flex-col bg-background md:min-h-0 md:h-[min(880px,90vh)] md:max-h-[min(880px,90vh)] md:rounded-[2.25rem] md:shadow-[0_40px_100px_-30px_oklch(0.2_0.04_220_/_0.35)] md:ring-1 md:ring-border/60 md:overflow-hidden"
        data-selector-name="app-shell"
      >
        <main
          key={pathname}
          className={cn(
            "safe-top min-h-0 flex-1 px-4 pb-3 pt-3 animate-in fade-in duration-150",
            pathname === "/calendar" ? "overflow-hidden" : "overflow-y-auto",
          )}
          data-selector-name={`route:${pathname}`}
        >
          {children}
        </main>
        <ShakeDataResetButton />
        {showTabs ? <BottomTabs labels={languageProxy} /> : null}
      </div>
    </div>
  );
}
