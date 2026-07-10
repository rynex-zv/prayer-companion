import { cp, mkdir, rm } from 'node:fs/promises';
import { resolve } from 'node:path';

const source = resolve(process.env.PRAY_WEB_DIST_DIR || 'dist');
const mauiTarget = resolve('../PrayAdFree/Resources/Raw/web');
const phone = process.argv.includes('--phone');

if (phone) {
  await rm(mauiTarget, { recursive: true, force: true });
  await mkdir(mauiTarget, { recursive: true });
  await cp(source, mauiTarget, { recursive: true });
  process.exit(0);
}

// Web builds are consumed from dist/. Do not copy dist back over the Vite source root.
