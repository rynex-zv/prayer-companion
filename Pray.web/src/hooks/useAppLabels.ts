import { getLabel, languageProxy, useAppStore } from "@/state/appStore";

export type LabelProxy = typeof languageProxy;

export function createLabelProxy() {
  return languageProxy;
}

export function useAppLabels() {
  const labels = useAppStore((state) => state.languageObject.labels);
  return (key: string) => labels[key] ?? getLabel(key);
}

export function refreshShellLabels() {
  window.dispatchEvent(new CustomEvent("prayadfree:shell-refresh"));
}
