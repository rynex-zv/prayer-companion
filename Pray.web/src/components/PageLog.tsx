export function PageLog({ page }: { page: string }) {
  return (
    <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-primary">
      log:{page}
    </span>
  );
}
