import { MapPin } from "lucide-react";

type Props = {
  latitude: number;
  longitude: number;
  bearing: number;
  locationTitle: string;
};

const TILE_SIZE = 256;
const ZOOM = 12;

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
      <div className="flex h-72 items-center justify-center rounded-xl bg-muted/50 text-sm text-muted-foreground">
        Location is not available.
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

  return (
    <div className="relative h-72 overflow-hidden rounded-xl border border-border bg-muted">
      <div className="absolute left-1/2 top-1/2 h-[768px] w-[768px] -translate-x-1/2 -translate-y-1/2">
        {tiles.map((tile) => (
          <img
            key={tile.key}
            src={`https://tile.openstreetmap.org/${ZOOM}/${normalizeTile(tile.x, ZOOM)}/${tile.y}.png`}
            alt=""
            className="absolute h-64 w-64 select-none"
            draggable={false}
            style={{ left: tile.left, top: tile.top }}
          />
        ))}
      </div>

      <div
        className="absolute left-1/2 top-1/2 h-28 w-1 origin-bottom -translate-x-1/2 -translate-y-full rounded-full bg-primary shadow"
        style={{ transform: `translate(-50%, -100%) rotate(${bearing}deg)` }}
      >
        <div className="absolute -top-2 left-1/2 h-0 w-0 -translate-x-1/2 border-x-[8px] border-b-[12px] border-x-transparent border-b-primary" />
      </div>

      <div className="absolute left-1/2 top-1/2 flex h-11 w-11 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-card text-primary shadow-lg">
        <MapPin className="h-6 w-6" />
      </div>

      <div className="absolute bottom-3 left-3 rounded-md bg-card/90 px-2 py-1 text-xs font-medium shadow-sm">
        {locationTitle}
      </div>
      <a
        href="https://www.openstreetmap.org/copyright"
        className="absolute bottom-3 right-3 rounded-md bg-card/90 px-2 py-1 text-[10px] text-muted-foreground shadow-sm"
      >
        OpenStreetMap
      </a>
    </div>
  );
}
