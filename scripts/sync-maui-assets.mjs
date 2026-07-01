import { cp, mkdir, readdir, rm } from 'node:fs/promises';
import { basename, resolve } from 'node:path';

const source = resolve('dist');
const mauiTarget = resolve('../PrayAdFree/Resources/Raw/web');
const siteRoot = resolve('.');
const phone = process.argv.includes('--phone');

if (phone) {
  await rm(mauiTarget, { recursive: true, force: true });
  await mkdir(mauiTarget, { recursive: true });
  await cp(source, mauiTarget, { recursive: true });
}

await rm(resolve(siteRoot, 'assets'), { recursive: true, force: true });
await rm(resolve(siteRoot, 'index.html'), { force: true });
await rm(resolve(siteRoot, 'webber-manifest.json'), { force: true });

for (const entry of await readdir(source, { withFileTypes: true })) {
  await cp(resolve(source, entry.name), resolve(siteRoot, basename(entry.name)), { recursive: true });
}
