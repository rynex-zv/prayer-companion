import { mauiTrace } from "@/native/mauiWebberClient";

export function traceClient(name: string, detail: Record<string, unknown> = {}): void {
  mauiTrace(name, detail);
}
