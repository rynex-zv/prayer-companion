import { useRef, useState, useEffect } from "react";
import { cn } from "@/lib/utils";

type Props = {
  bearing: number;
  needleRotation: number;
  compassRotation: number;
  state: string;
  visualFilter?: string;
  manual?: boolean;
  onDrag?: (delta: number) => void;
  onDragEnd?: () => void;
};

export function QiblaCompass({ bearing, needleRotation, compassRotation, state, visualFilter = "None", manual, onDrag, onDragEnd }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const [startX, setStartX] = useState<number | null>(null);
  const [dragDelta, setDragDelta] = useState(0);
  const muted = state === "searching" || state === "permissionMissing";
  const aligned = state === "aligned";

  useEffect(() => {
    if (startX === null) return;
    const move = (e: PointerEvent) => {
      const delta = (e.clientX - startX) * 0.5;
      setDragDelta(delta);
      onDrag?.(delta);
    };
    const up = () => { setStartX(null); setDragDelta(0); onDragEnd?.(); };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
  }, [startX, onDrag, onDragEnd]);

  const filterStyle =
    visualFilter === "Night" ? { filter: "brightness(0.6) saturate(0.8)" } :
    visualFilter === "Contrast" ? { filter: "contrast(1.4) saturate(1.2)" } : undefined;

  return (
    <div
      ref={ref}
      onPointerDown={manual ? (e) => setStartX(e.clientX) : undefined}
      className={cn("relative mx-auto aspect-square w-full max-w-[280px] select-none touch-none", manual && "cursor-grab active:cursor-grabbing")}
      style={filterStyle}
    >
      {/* Outer ring */}
      <div
        className={cn(
          "absolute inset-0 rounded-full border-[3px] transition-all duration-300",
          muted ? "border-muted" : aligned ? "border-success shadow-[0_0_40px_-8px_var(--color-success)]" : "border-border",
        )}
        style={{ transform: `rotate(${manual ? compassRotation - dragDelta : compassRotation}deg)`, transformOrigin: "center" }}
      >
        {/* Tick marks */}
        {Array.from({ length: 36 }).map((_, i) => (
          <div
            key={i}
            className={cn("absolute left-1/2 top-0 h-3 w-[2px] -translate-x-1/2 origin-[1px_140px]", i % 9 === 0 ? "bg-foreground" : "bg-muted-foreground/40")}
            style={{ transform: `rotate(${i * 10}deg)` }}
          />
        ))}
        {/* Cardinals */}
        {[
          { l: "N", a: 0 }, { l: "E", a: 90 }, { l: "S", a: 180 }, { l: "W", a: 270 },
        ].map((c) => (
          <span
            key={c.l}
            className="absolute left-1/2 top-5 -translate-x-1/2 text-xs font-bold"
            style={{ transform: `translate(-50%, 0) rotate(${c.a}deg) translateY(0)`, transformOrigin: "0 115px" }}
          >
            {c.l}
          </span>
        ))}
      </div>

      {/* Center face */}
      <div className="absolute inset-6 rounded-full bg-card shadow-inner" />

      {/* Needle */}
      <div
        className="absolute inset-0 transition-transform duration-300"
        style={{ transform: `rotate(${needleRotation}deg)` }}
      >
        <div className={cn("absolute left-1/2 top-4 h-[45%] w-1 -translate-x-1/2 rounded-full", muted ? "bg-muted-foreground" : "bg-primary")}>
          <div className={cn("absolute -top-2 left-1/2 h-0 w-0 -translate-x-1/2 border-x-[10px] border-b-[14px] border-x-transparent",
            muted ? "border-b-muted-foreground" : "border-b-primary")} />
        </div>
        {/* Kaaba marker */}
        <div className="absolute left-1/2 top-8 -translate-x-1/2 text-lg">🕋</div>
      </div>

      {/* Center dot */}
      <div className="absolute left-1/2 top-1/2 h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full bg-foreground" />

      {/* Bearing label */}
      <div className="absolute -bottom-1 left-1/2 -translate-x-1/2 rounded-full bg-card px-3 py-1 text-xs font-semibold shadow-sm">
        {Math.round(bearing)}°
      </div>
    </div>
  );
}
