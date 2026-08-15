export type WidgetRenderTree = {
  profileId: string;
  revision: number;
  status: "ready" | "error";
  error: string;
  family: string;
  items: Array<{ kind: string; key: string; label: string; value: string; accessibilityLabel: string }>;
  omitted: string[];
  targetUnixMilliseconds?: number;
};

export function WidgetPreview({ tree }: { tree: WidgetRenderTree }) {
  return (
    <section aria-label="Widget preview" data-family={tree.family}>
      {tree.status === "error" ? <p role="alert">{tree.error}</p> : tree.items.map(item => (
        <p key={item.key} aria-label={item.accessibilityLabel}>{item.value}</p>
      ))}
      {tree.omitted.length > 0 && <p role="status">Hidden by host limits: {tree.omitted.join(", ")}</p>}
    </section>
  );
}
