import { createHash } from 'node:crypto';
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const distDir = process.env.PRAY_WEB_DIST_DIR || 'dist';
const root = new URL(`../${distDir}/`, import.meta.url);
const rootPath = fileURLToPath(root);
const target = process.argv.includes('--phone') ? 'phone' : 'web';
const versionPath = resolve(rootPath, '..', 'version.web.info');
const contractPath = resolve(rootPath, '..', 'src', 'generated', 'core-contract.json');

async function nextBuildVersion() {
  let current = 0;
  try {
    const raw = (await readFile(versionPath, 'utf8')).trim();
    current = Number.parseInt(raw, 10);
  } catch {
    current = 0;
  }

  const next = Number.isFinite(current) && current >= 0 ? current + 1 : 1;
  await writeFile(versionPath, `${next}\n`, 'utf8');
  return String(next);
}

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...await walk(path));
    } else if (
      entry.name !== 'webber-manifest.json' &&
      entry.name !== 'version.web.info' &&
      entry.name !== 'web.config' &&
      !entry.name.endsWith('.br') &&
      !entry.name.endsWith('.gz')
    ) {
      files.push(path);
    }
  }
  return files;
}

const version = await nextBuildVersion();
const contract = JSON.parse(await readFile(contractPath, 'utf8'));
await writeFile(join(rootPath, 'version.web.info'), `${version}\n`, 'utf8');

const files = [];
for (const file of await walk(rootPath)) {
  const bytes = await readFile(file);
  files.push({
    path: relative(rootPath, file).replaceAll('\\', '/'),
    sha256: createHash('sha256').update(bytes).digest('hex')
  });
}

files.sort((a, b) => a.path.localeCompare(b.path));

await writeFile(
  join(rootPath, 'webber-manifest.json'),
  JSON.stringify({
    version,
    contractVersion: contract.schemaVersion,
    target,
    entry: 'index.html',
    files
  }, null, 2)
);
