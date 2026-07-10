import { MapPin } from "lucide-react";

type Props = {
  latitude: number;
  longitude: number;
  bearing: number;
  locationTitle: string;
};

const TILE_SIZE = 256;
const ZOOM = 5;
const LINE_LENGTH = 150;

function lonToTileX(lon: number, zoom: number) {
  return ((lon + 180) / 360) * 2 ** zoom;
}

function latToTileY(lat: number, zoom: number) {
  const rad = (lat * Math.PI) / 180;
  return ((1 - Math.log(Math.tan(rad) + 1 / Math.cos(rad)) / Math.PI) / 2) * 2 ** zoom;
}

function normalizeTile(value: number, zoom: number) {
  const max = 2 ** zoom;
  return ((value % max) + max) % max;
}

export function QiblaMap({ latitude, longitude, bearing, locationTitle }: Props) {
  const valid = Number.isFinite(latitude) && Number.isFinite(longitude) && (latitude !== 0 || longitude !== 0);

  if (!valid) {
    return (
      <div className="flex h-72 items-center justify-center rounded-2xl bg-muted/50 text-sm text-muted-foreground">
        —
      </div>
    );
  }

  const centerX = lonToTileX(longitude, ZOOM);
  const centerY = latToTileY(latitude, ZOOM);
  const baseX = Math.floor(centerX);
  const baseY = Math.floor(centerY);
  const offsetX = (centerX - baseX) * TILE_SIZE;
  const offsetY = (centerY - baseY) * TILE_SIZE;
  const tiles = [-1, 0, 1].flatMap((dy) =>
    [-1, 0, 1].map((dx) => ({
      key: `${dx}:${dy}`,
      x: baseX + dx,
      y: baseY + dy,
      left: (dx + 1) * TILE_SIZE - offsetX,
      top: (dy + 1) * TILE_SIZE - offsetY,
    })),
  );

  // Endpoint of the bearing line (Kaaba direction)
  const rad = (bearing * Math.PI) / 180;
  const endDx = Math.sin(rad) * LINE_LENGTH;
  const endDy = -Math.cos(rad) * LINE_LENGTH;

  return (
    <div className="relative h-80 overflow-hidden rounded-2xl border border-border bg-muted shadow-inner">
      {/* Tile layer */}
      <div className="absolute inset-0 overflow-hidden">
        <div className="absolute left-1/2 top-1/2 h-[768px] w-[768px] -translate-x-1/2 -translate-y-1/2">
          {tiles.map((tile) => (
            <img
              key={tile.key}
              src={`https://tile.openstreetmap.org/${ZOOM}/${normalizeTile(tile.x, ZOOM)}/${tile.y}.png`}
              alt=""
              className="absolute h-64 w-64 select-none opacity-90"
              draggable={false}
              style={{ left: tile.left, top: tile.top }}
            />
          ))}
        </div>
      </div>

      {/* Soft radial overlay for legibility */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(circle at center, transparent 40%, color-mix(in oklab, var(--color-background) 55%, transparent) 100%)",
        }}
      />

      {/* SVG overlay: line + endpoints */}
      <svg className="pointer-events-none absolute inset-0 h-full w-full" aria-hidden>
        <defs>
          <marker id="qibla-arrow" viewBox="0 0 10 10" refX="5" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" fill="var(--color-primary)" />
          </marker>
        </defs>
        <line
          x1="50%"
          y1="50%"
          x2={`calc(50% + ${endDx}px)`}
          y2={`calc(50% + ${endDy}px)`}
          stroke="var(--color-primary)"
          strokeWidth="3"
          strokeLinecap="round"
          markerEnd="url(#qibla-arrow)"
        />
      </svg>

      {/* Kaaba marker at line endpoint */}
      <div
        className="pointer-events-none absolute left-1/2 top-1/2 flex h-9 w-9 items-center justify-center rounded-full bg-card text-lg shadow-lg ring-2 ring-primary"
        style={{ transform: `translate(calc(-50% + ${endDx}px), calc(-50% + ${endDy}px))` }}
      >
        🕋
      </div>

      {/* User location marker at center */}
      <div className="pointer-events-none absolute left-1/2 top-1/2 flex h-10 w-10 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg ring-4 ring-background">
        <MapPin className="h-5 w-5" />
      </div>

      {/* Location badge */}
      <div className="absolute bottom-3 left-3 max-w-[70%] truncate rounded-full bg-card/95 px-3 py-1.5 text-xs font-medium shadow-md backdrop-blur">
        {locationTitle}
      </div>

      {/* Bearing badge */}
      <div className="absolute bottom-3 right-3 rounded-full bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground shadow-md tabular-nums" dir="ltr">
        {Math.round(bearing)}°
      </div>

      <a
        href="https://www.openstreetmap.org/copyright"
        target="_blank"
        rel="noreferrer"
        className="absolute top-2 right-2 rounded-md bg-card/85 px-1.5 py-0.5 text-[9px] text-muted-foreground shadow-sm backdrop-blur"
      >
        © OpenStreetMap
      </a>
    </div>
  );
}
