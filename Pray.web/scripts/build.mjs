import { spawn } from 'node:child_process';

const args = new Set(process.argv.slice(2));
const phone = args.has('--phone');

function run(command, commandArgs) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, commandArgs, {
      cwd: process.cwd(),
      shell: true,
      stdio: 'inherit',
      env: {
        ...process.env,
        PRAY_WEB_TARGET: phone ? 'phone' : 'web'
      }
    });

    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${command} ${commandArgs.join(' ')} failed with exit code ${code}`));
    });
  });
}

await run('vite', ['build', ...(phone ? ['--mode', 'phone'] : [])]);
await run('node', ['scripts/generate-manifest.mjs', ...(phone ? ['--phone'] : [])]);
await run('node', ['scripts/sync-maui-assets.mjs', ...(phone ? ['--phone'] : [])]);
