(() => {
  const bar = document.querySelector('#eeg-rate');
  const reading = bar.querySelector('strong');
  const message = bar.querySelector('[role="status"]');
  const messages = {
    unconfigured: 'Select an EEG stream to monitor its sampling rate.',
    measuring: 'Measuring the incoming stream…',
    no_samples: 'No EEG samples received. Start streaming in the Mindtooth app.',
    low: 'Low EEG sampling rate. Check the amplifier and other connected clients.',
    timestamps: 'Irregular EEG timestamps. Check the signal source.',
    receiving: 'Receiving samples.',
    offline: 'EEG rate unavailable. Check the console service.'
  };

  function display(state, actual, nominal, source, delivery) {
    reading.textContent = Number.isFinite(nominal)
      ? `Live EEG · ${Number.isFinite(actual) ? actual.toFixed(1) : '—'} Hz actual / ${nominal} Hz nominal`
      : 'Live EEG sampling rate';
    const deliveryText = Number.isFinite(delivery) ? `Receiver throughput: ${delivery.toFixed(1)} Hz.` : '';
    bar.title = [source, deliveryText].filter(Boolean).join(' · ');
    if (state === 'low' || state === 'timestamps') reading.textContent += ` · ${deliveryText}`;
    bar.dataset.state = state;
    if (message.textContent !== messages[state]) message.textContent = messages[state];
  }

  async function refresh() {
    try {
      const response = await fetch('/console/eeg-rate', {cache: 'no-store', signal: AbortSignal.timeout(1500)});
      if (!response.ok) throw Error(response.status);
      const {state, actualHz, nominalHz, source, deliveryHz} = await response.json();
      display(state, actualHz, nominalHz, source, deliveryHz);
    } catch {
      display('offline', null, null, null);
    } finally {
      setTimeout(refresh, 500);
    }
  }
  refresh();
})();
