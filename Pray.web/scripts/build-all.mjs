#!/usr/bin/env node
// One-shot build orchestrator so we stop re-debugging build steps every turn.
// Flags:
//   --love    : Lovable-managed output directory
//   --dev     : development mode
//   --phone   : phone-embedded HTML build
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { rm } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(__dirname, '..');
const repoRoot = resolve(webRoot, '..');

const args = new Set(process.argv.slice(2));
const love = args.has('--love') || args.has('-love');
const dev = args.has('--dev');
const phone = args.has('--phone');

function run(cmd, cargs, cwd = webRoot, extraEnv = {}) {
  return new Promise((res, rej) => {
    const child = spawn(cmd, cargs, {
      cwd,
      stdio: 'inherit',
      shell: false,
      env: { ...process.env, ...extraEnv },
    });
    child.on('exit', (code) =>
      code === 0 ? res() : rej(new Error(`${cmd} ${cargs.join(' ')} failed (exit ${code})`)),
    );
  });
}

async function main() {
  console.log(`[build-all] mode=${love ? 'love' : phone ? 'phone' : 'web'} dev=${dev}`);

  const skipDotnet = process.env.PRAY_WEB_SKIP_DOTNET === '1';
  if (!skipDotnet) {
    const bridgeCsproj = resolve(repoRoot, 'PrayAdFree.WebBridge', 'PrayAdFree.WebBridge.csproj');
    if (!existsSync(bridgeCsproj)) {
      throw new Error(`WebBridge project not found: ${bridgeCsproj}`);
    }
    const publishRoot = resolve(repoRoot, 'PrayAdFree.WebBridge', 'bin', 'Release', 'net10.0', 'publish');
    console.log('[build-all] 1/3 clean + publish Core/WebBridge/WASM');
    await rm(publishRoot, { recursive: true, force: true });
    await run('dotnet', ['publish', bridgeCsproj, '-c', 'Release'], repoRoot);

    const contractsCsproj = resolve(repoRoot, 'tools', 'generate-web-contracts', 'GenerateWebContracts.csproj');
    if (!existsSync(contractsCsproj)) {
      throw new Error(`Contract generator not found: ${contractsCsproj}`);
    }
    console.log('[build-all] 2/3 regenerate Core contract');
    await run('dotnet', ['run', '--project', contractsCsproj, '-c', 'Release'], repoRoot);
  } else {
    console.log('[build-all] skipping dotnet steps (PRAY_WEB_SKIP_DOTNET=1)');
  }

  const buildArgs = [];
  if (love) buildArgs.push('--love');
  if (dev) buildArgs.push('--dev');
  if (phone) buildArgs.push('--phone');
  console.log(`[build-all] 3/3 Vite bundle ${buildArgs.join(' ')}`);
  await run(process.execPath, ['scripts/build.mjs', ...buildArgs], webRoot, { PRAY_WEB_SKIP_DOTNET: '1' });

  console.log('[build-all] ✓ done');
}

main().catch((err) => {
  console.error('[build-all] ✗', err.message);
  process.exit(1);
});
