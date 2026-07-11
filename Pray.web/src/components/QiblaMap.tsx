import { useMemo, useState } from "react";
import { MapPin, Plus, Minus } from "lucide-react";

type Props = {
  latitude: number;
  longitude: number;
  bearing: number;
  locationTitle: string;
};

const KAABA_LAT = 21.4225241;
const KAABA_LON = 39.8261818;
const TILE_SIZE = 256;
const MIN_ZOOM = 2;
const MAX_ZOOM = 12;
const DEFAULT_ZOOM = 5;
const VIEW = 320;

type Vec3 = { x: number; y: number; z: number };
type Point = { x: number; y: number };

function project(lat: number, lon: number, zoom: number): Point {
  const scale = TILE_SIZE * 2 ** zoom;
  const x = ((lon + 180) / 360) * scale;
  const latRad = (lat * Math.PI) / 180;
  const y = ((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2) * scale;
  return { x, y };
}

function toXYZ(lat: number, lon: number): Vec3 {
  const φ = (lat * Math.PI) / 180;
  const λ = (lon * Math.PI) / 180;
  return { x: Math.cos(φ) * Math.cos(λ), y: Math.cos(φ) * Math.sin(λ), z: Math.sin(φ) };
}

function fromXYZ(p: Vec3): { lat: number; lon: number } {
  const lat = (Math.atan2(p.z, Math.sqrt(p.x * p.x + p.y * p.y)) * 180) / Math.PI;
  const lon = (Math.atan2(p.y, p.x) * 180) / Math.PI;
  return { lat, lon };
}

function slerp(a: Vec3, b: Vec3, t: number): Vec3 {
  const dot = Math.max(-1, Math.min(1, a.x * b.x + a.y * b.y + a.z * b.z));
  const omega = Math.acos(dot);
  if (omega < 1e-6) return a;
  const s1 = Math.sin((1 - t) * omega) / Math.sin(omega);
  const s2 = Math.sin(t * omega) / Math.sin(omega);
  return { x: a.x * s1 + b.x * s2, y: a.y * s1 + b.y * s2, z: a.z * s1 + b.z * s2 };
}

function normalizeTile(v: number, zoom: number) {
  const m = 2 ** zoom;
  return ((v % m) + m) % m;
}

function intersectViewportEdge(from: Point, to: Point, w: number, h: number): Point | null {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const ts: number[] = [];
  const eps = 1e-6;
  if (Math.abs(dx) > eps) {
    ts.push((0 - from.x) / dx);
    ts.push((w - from.x) / dx);
  }
  if (Math.abs(dy) > eps) {
    ts.push((0 - from.y) / dy);
    ts.push((h - from.y) / dy);
  }
  const inside = ts.filter((t) => t >= 0 && t <= 1).sort((a, b) => a - b);
  for (const t of inside) {
    const x = from.x + dx * t;
    const y = from.y + dy * t;
    if (x >= -0.5 && x <= w + 0.5 && y >= -0.5 && y <= h + 0.5) return { x, y };
  }
  return null;
}

export function QiblaMap({ latitude, longitude, bearing, locationTitle }: Props) {
  const [zoom, setZoom] = useState(DEFAULT_ZOOM);
  const valid =
    Number.isFinite(latitude) &&
    Number.isFinite(longitude) &&
    (latitude !== 0 || longitude !== 0);

  const geometry = useMemo(() => {
    if (!valid) return null;
    const userWorld = project(latitude, longitude, zoom);
    const kaabaWorld = project(KAABA_LAT, KAABA_LON, zoom);
    const originX = userWorld.x - VIEW / 2;
    const originY = userWorld.y - VIEW / 2;
    const worldWidth = TILE_SIZE * 2 ** zoom;

    // pick the kaaba copy nearest the user (handle antimeridian)
    let kaabaX = kaabaWorld.x;
    while (kaabaX - userWorld.x > worldWidth / 2) kaabaX -= worldWidth;
    while (kaabaX - userWorld.x < -worldWidth / 2) kaabaX += worldWidth;
    const kaabaPx: Point = { x: kaabaX - originX, y: kaabaWorld.y - originY };

    // tiles
    const startTileX = Math.floor(originX / TILE_SIZE);
    const startTileY = Math.floor(originY / TILE_SIZE);
    const endTileX = Math.floor((originX + VIEW) / TILE_SIZE);
    const endTileY = Math.floor((originY + VIEW) / TILE_SIZE);
    const tiles: { key: string; url: string; left: number; top: number }[] = [];
    for (let ty = startTileY; ty <= endTileY; ty++) {
      for (let tx = startTileX; tx <= endTileX; tx++) {
        const nx = normalizeTile(tx, zoom);
        if (ty < 0 || ty >= 2 ** zoom) continue;
        tiles.push({
          key: `${tx}:${ty}`,
          url: `https://tile.openstreetmap.org/${zoom}/${nx}/${ty}.png`,
          left: tx * TILE_SIZE - originX,
          top: ty * TILE_SIZE - originY,
        });
      }
    }

    // great-circle waypoints
    const a = toXYZ(latitude, longitude);
    const b = toXYZ(KAABA_LAT, KAABA_LON);
    const N = 160;
    const path: Point[] = [];
    let prevX: number | null = null;
    for (let i = 0; i <= N; i++) {
      const p = slerp(a, b, i / N);
      const ll = fromXYZ(p);
      const w = project(ll.lat, ll.lon, zoom);
      let px = w.x - originX;
      if (prevX !== null) {
        while (px - prevX > worldWidth / 2) px -= worldWidth;
        while (px - prevX < -worldWidth / 2) px += worldWidth;
      }
      prevX = px;
      path.push({ x: px, y: w.y - originY });
    }

    const inView = (p: Point) => p.x >= 0 && p.x <= VIEW && p.y >= 0 && p.y <= VIEW;
    const kaabaVisible = inView(kaabaPx);

    // clip: keep points from user (index 0) up to and including first exit
    let visible: Point[] = [];
    if (kaabaVisible) {
      visible = path;
    } else {
      // find last consecutive index in view starting from the user side
      let lastInside = -1;
      for (let i = 0; i < path.length; i++) {
        if (inView(path[i])) lastInside = i;
        else break;
      }
      if (lastInside < 0) {
        // even the user isn't inside (shouldn't happen); fall back to straight line
        const edge = intersectViewportEdge({ x: VIEW / 2, y: VIEW / 2 }, kaabaPx, VIEW, VIEW);
        visible = edge ? [{ x: VIEW / 2, y: VIEW / 2 }, edge] : [{ x: VIEW / 2, y: VIEW / 2 }];
      } else {
        visible = path.slice(0, lastInside + 1);
        const next = path[Math.min(lastInside + 1, path.length - 1)];
        const edge = intersectViewportEdge(path[lastInside], next, VIEW, VIEW);
        if (edge) visible.push(edge);
      }
    }

    return { tiles, kaabaPx, kaabaVisible, path: visible };
  }, [latitude, longitude, zoom, valid]);

  if (!valid || !geometry) {
    return (
      <div className="flex h-72 items-center justify-center rounded-2xl bg-muted/50 text-sm text-muted-foreground">
        —
      </div>
    );
  }

  const { tiles, kaabaPx, kaabaVisible, path } = geometry;
  const d = path.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");

  return (
    <div
      className="relative mx-auto overflow-hidden rounded-2xl border border-border bg-muted shadow-inner"
      style={{ width: VIEW, height: VIEW }}
    >
      {/* tiles */}
      <div className="absolute inset-0">
        {tiles.map((t) => (
          <img
            key={t.key}
            src={t.url}
            alt=""
            draggable={false}
            className="absolute select-none opacity-95"
            style={{ left: t.left, top: t.top, width: TILE_SIZE, height: TILE_SIZE }}
          />
        ))}
      </div>

      {/* soft radial vignette */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(circle at center, transparent 55%, color-mix(in oklab, var(--color-background) 45%, transparent) 100%)",
        }}
      />

      {/* great-circle path + arrow */}
      <svg className="pointer-events-none absolute inset-0" width={VIEW} height={VIEW} aria-hidden>
        <defs>
          <marker
            id="qibla-arrow"
            viewBox="0 0 12 12"
            refX="9"
            refY="6"
            markerWidth="7"
            markerHeight="7"
            orient="auto"
          >
            <path d="M0 0 L12 6 L0 12 L3 6 Z" fill="var(--color-primary)" />
          </marker>
        </defs>
        <path
          d={d}
          fill="none"
          stroke="var(--color-primary)"
          strokeWidth="3.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          markerEnd={kaabaVisible ? undefined : "url(#qibla-arrow)"}
          style={{ filter: "drop-shadow(0 1px 2px rgba(0,0,0,0.35))" }}
        />
      </svg>

      {/* user marker (center) */}
      <div
        className="pointer-events-none absolute z-10 flex h-10 w-10 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg ring-4 ring-background"
        style={{ left: VIEW / 2, top: VIEW / 2, transform: "translate(-50%, -50%)" }}
      >
        <MapPin className="h-5 w-5" />
      </div>

      {/* kaaba marker only when actually visible on the map */}
      {kaabaVisible ? (
        <div
          className="pointer-events-none absolute z-10 flex h-9 w-9 items-center justify-center rounded-full bg-card text-lg shadow-lg ring-2 ring-primary"
          style={{ left: kaabaPx.x, top: kaabaPx.y, transform: "translate(-50%, -50%)" }}
        >
          🕋
        </div>
      ) : null}

      {/* zoom controls */}
      <div className="absolute right-2 top-2 z-20 flex flex-col gap-1">
        <button
          type="button"
          aria-label="Zoom in"
          onClick={() => setZoom((z) => Math.min(MAX_ZOOM, z + 1))}
          className="grid h-8 w-8 place-items-center rounded-md bg-card/95 shadow ring-1 ring-border hover:bg-card"
        >
          <Plus className="h-4 w-4" />
        </button>
        <button
          type="button"
          aria-label="Zoom out"
          onClick={() => setZoom((z) => Math.max(MIN_ZOOM, z - 1))}
          className="grid h-8 w-8 place-items-center rounded-md bg-card/95 shadow ring-1 ring-border hover:bg-card"
        >
          <Minus className="h-4 w-4" />
        </button>
      </div>

      {/* location + bearing badges */}
      <div className="absolute bottom-2 left-2 z-20 max-w-[65%] truncate rounded-full bg-card/95 px-3 py-1.5 text-xs font-medium shadow-md backdrop-blur">
        {locationTitle}
      </div>
      <div
        className="absolute bottom-2 right-2 z-20 rounded-full bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground shadow-md tabular-nums"
        dir="ltr"
      >
        {Math.round(bearing)}°
      </div>

      <a
        href="https://www.openstreetmap.org/copyright"
        target="_blank"
        rel="noreferrer"
        className="absolute left-2 top-2 z-20 rounded-md bg-card/85 px-1.5 py-0.5 text-[9px] text-muted-foreground shadow-sm backdrop-blur"
      >
        © OpenStreetMap
      </a>
    </div>
  );
}
