export type WebBootState = "loading" | "slow" | "offline" | "failed";

declare global {
  interface Window {
    __prayBoot?: {
      isWeb: true;
      update: (state: WebBootState, message?: string, detail?: string) => void;
      resource: (name: string, loaded: number, total: number) => void;
      ready: () => void;
      retry: () => void;
      restart: (detail?: string) => boolean;
    };
  }
}

export function reportWebBoot(state: WebBootState, message?: string, detail?: string): void {
  window.__prayBoot?.update(state, message, detail);
}

export function completeWebBoot(): void {
  window.__prayBoot?.ready();
  if ("serviceWorker" in navigator && navigator.serviceWorker.controller) {
    const commitVersion = sessionStorage.getItem("pray.web.commitVersion") ?? "";
    if (commitVersion) {
      navigator.serviceWorker.controller.postMessage({ type: "COMMIT_VERSION", version: commitVersion });
      sessionStorage.setItem("pray.web.lastCommitVersion", commitVersion);
      sessionStorage.removeItem("pray.web.commitVersion");
      sessionStorage.removeItem("pray.web.resumeUrl");
    }
  }
}

export function restartWebBoot(detail?: string): boolean {
  return window.__prayBoot?.restart(detail) ?? false;
}

export function isRemoteWebRuntime(): boolean {
  return Boolean(window.__prayBoot?.isWeb);
}
