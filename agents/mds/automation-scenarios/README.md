# Pray Ad Free automation scenarios

These scenarios run only when `PRAY_AUTOMATION=true` and the current platform flag is enabled (`PRAY_AUTOMATION_WEB`, `PRAY_AUTOMATION_WINDOWS`, or `PRAY_AUTOMATION_ANDROID`). Web uses an isolated IndexedDB database; native test builds use the isolated application id `com.rynex.prayer.automation`.

The runner executes every scenario even after failures. Each run produces `passed.md`, `failed.md`, and a machine-readable `result.json` for the Web harness. Data calls above 200 ms are warnings; calls above 300 ms fail the active scenario.

Run Web automation with `npm --prefix Pray.web run automation:web`. Build native automation with `-p:PrayAutomation=true` and a phone bundle built with `PRAY_AUTOMATION=true`.
