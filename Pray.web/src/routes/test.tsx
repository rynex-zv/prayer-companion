import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/test")({
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
