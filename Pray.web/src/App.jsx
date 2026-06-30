import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

let renderTraceSent = false;

const emptySnapshot = {
  locationTitle: 'Loading...',
  hijriDate: '',
  gregorianDate: '',
  nextPrayerName: '--',
  nextPrayerClock: '--:--',
  nextPrayerBaseClock: '',
  showNextPrayerBaseClock: false,
  nextPrayerDayLabel: '',
  isRtl: false,
  labels: {
    nextPrayer: 'Next Prayer',
    timeLeft: 'Time Left',
    todayPrayerTimes: 'Today Prayer Times',
    iftar: 'Iftar',
    imsak: 'Imsak',
    refresh: 'Refresh',
    refreshing: 'Refreshing...',
    base: 'Base'
  },
  countdown: '--:--',
  statusMessage: 'Connecting to app...',
  imsakTime: '--:--',
  iftarTime: '--:--',
  isImsakNext: false,
  isIftarNext: false,
  nextFastingCountdown: '--:--:--',
  todayTimings: []
};

function callNative(method, payload = {}) {
  if (!window.mauiWebber?.call) {
    if (window.location.protocol === 'http:' || window.location.protocol === 'https:') {
      return Promise.resolve({
        ok: true,
        data: emptySnapshot
      });
    }

    return Promise.resolve({
      ok: false,
      error: 'MauiWebber bridge is not ready. Open this page inside the MAUI app.'
    });
  }

  return window.mauiWebber.call(method, payload);
}

function trace(name, detail = {}) {
  if (window.mauiWebber?.call) {
    window.mauiWebber.call('mauiWebber.trace', { name, at: performance.now(), ...detail });
  }
}

function App() {
  const [snapshot, setSnapshot] = useState(emptySnapshot);
  const [bridgeError, setBridgeError] = useState('');
  const [busy, setBusy] = useState(false);

  const loadSnapshot = useCallback(async (refresh = false) => {
    setBusy(refresh);
    const response = await callNative(refresh ? 'today.refresh' : 'today.getSnapshot');
    setBusy(false);
    if (!response?.ok) {
      setBridgeError(response?.error || 'Native call failed.');
      return;
    }

    setBridgeError('');
    setSnapshot({ ...emptySnapshot, ...response.data });
    if (refresh || !renderTraceSent) {
      renderTraceSent = true;
      trace(refresh ? 'manualRefreshRendered' : 'renderScheduled', { refresh });
    }
  }, []);

  useEffect(() => {
    const start = () => loadSnapshot(false);
    window.addEventListener('mauiwebber:ready', start);
    let timer = 0;
    if (window.location.protocol === 'http:' || window.location.protocol === 'https:') {
      start();
    }

    const beginTimer = () => {
      if (!timer) {
        timer = window.setInterval(() => loadSnapshot(false), 1000);
      }
    };
    window.addEventListener('mauiwebber:ready', beginTimer);
    if (window.location.protocol === 'http:' || window.location.protocol === 'https:') {
      beginTimer();
    }

    return () => {
      window.removeEventListener('mauiwebber:ready', start);
      window.removeEventListener('mauiwebber:ready', beginTimer);
      window.clearInterval(timer);
    };
  }, [loadSnapshot]);

  const nextBase = useMemo(() => {
    if (!snapshot.showNextPrayerBaseClock || !snapshot.nextPrayerBaseClock) {
      return null;
    }

    return <span className="muted small">{snapshot.labels?.base || 'Base'} {snapshot.nextPrayerBaseClock}</span>;
  }, [snapshot]);

  return (
    <main className="app-shell" dir={snapshot.isRtl ? 'rtl' : 'ltr'}>
      <section className="basmala">بِسْمِ اللهِ الرَّحْمٰنِ الرَّحِيْمِ</section>

      <section className="location-card">
        <div className="dot" />
        <div>
          <strong>{snapshot.locationTitle}</strong>
        </div>
        <div className="date-stack">
          <span>{snapshot.gregorianDate}</span>
          <span>{snapshot.hijriDate}</span>
        </div>
      </section>

      <section className="hero-card">
        <div>
          <div className="eyebrow">{snapshot.labels?.nextPrayer || 'Next Prayer'}</div>
          <h1>{snapshot.nextPrayerName}</h1>
          <div className="clock">{snapshot.nextPrayerClock}</div>
          {nextBase}
        </div>
        <div className="countdown">
          <span>{snapshot.labels?.timeLeft || 'Time Left'}</span>
          <strong>{snapshot.countdown}</strong>
          <em>{snapshot.nextPrayerDayLabel}</em>
        </div>
      </section>

      <section className="panel">
        <header>{snapshot.labels?.todayPrayerTimes || 'Today Prayer Times'}</header>
        <div className="timings">
          {snapshot.todayTimings.map((row) => (
            <div className={`timing-row ${row.isNext ? 'next' : ''}`} key={row.id}>
              <span className="next-dot" />
              <span>{row.name}</span>
              <strong>{row.time}</strong>
              {row.showBaseTime && <small>{row.baseTime}</small>}
            </div>
          ))}
        </div>
      </section>

      <section className="fasting-grid">
        <div className={`fasting-card ${snapshot.isIftarNext ? 'active' : ''}`}>
          <span>{snapshot.labels?.iftar || 'Iftar'}</span>
          <strong>{snapshot.iftarTime}</strong>
          {snapshot.isIftarNext && <em>{snapshot.nextFastingCountdown}</em>}
        </div>
        <div className={`fasting-card ${snapshot.isImsakNext ? 'active' : ''}`}>
          <span>{snapshot.labels?.imsak || 'Imsak'}</span>
          <strong>{snapshot.imsakTime}</strong>
          {snapshot.isImsakNext && <em>{snapshot.nextFastingCountdown}</em>}
        </div>
      </section>

      <button className="refresh" type="button" onClick={() => loadSnapshot(true)} disabled={busy}>
        {busy ? (snapshot.labels?.refreshing || 'Refreshing...') : (snapshot.labels?.refresh || 'Refresh')}
      </button>

      <p className="status">{bridgeError || snapshot.statusMessage}</p>
    </main>
  );
}

createRoot(document.getElementById('root')).render(<App />);
