export {};

declare global {
  interface Window {
    mauiWebber?: {
      call: (method: string, payload?: unknown) => Promise<unknown>;
      navigation?: {
        canGoBack: () => boolean;
        canGoForward: () => boolean;
        back: () => boolean;
        forward: () => boolean;
      } | null;
    };
  }
}
