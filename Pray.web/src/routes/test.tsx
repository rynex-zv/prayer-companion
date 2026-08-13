import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/test")({
  beforeLoad: () => {
    if (import.meta.env.VITE_PRAY_AUTOMATION !== "true") {
      throw redirect({ to: "/" });
    }
  },
  head: () => ({ meta: [] }),
  component: TestRoute,
});

function TestRoute() {
  return (
    <main data-selector-name="route:/test" className="flex min-h-screen items-center justify-center px-4">
      <p className="text-sm text-muted-foreground">Automation test route</p>
    </main>
  );
}
