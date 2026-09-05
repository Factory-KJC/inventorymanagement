import net from 'node:net';
import { setTimeout as delay } from 'node:timers/promises';
import { toEscPos } from './receipt.js';
import { parseServerSentEvents } from './sse-events.js';

const settings = {
  apiUrl: required('PRINT_API_URL').replace(/\/$/, ''),
  apiKey: required('PRINT_WORKER_API_KEY'),
  printerHost: required('PRINTER_HOST'),
  printerPort: Number(process.env.PRINTER_PORT ?? '9100'),
  reconnectMilliseconds: Number(process.env.RECONNECT_MILLISECONDS ?? '2000'),
  timeoutMilliseconds: Number(process.env.PRINTER_TIMEOUT_MILLISECONDS ?? '10000')
};

while (true) {
  try {
    for await (const _ of jobEvents()) {
      await drainQueue();
    }
  } catch (error) {
    console.error(error);
    await delay(settings.reconnectMilliseconds);
  }
}

async function drainQueue() {
  let job;
  while ((job = await claim()) !== null) {
    try {
      logReceiptLine(job);
      const command = toEscPos(job.receiptLine, job.paperWidth);
      console.info(JSON.stringify({ event: 'escpos-generated', printJobId: job.id, byteLength: command.length }));
      await send(command);
      await complete(job.id);
    } catch (error) {
      await complete(job.id, error instanceof Error ? error.message : String(error));
    }
  }
}

async function* jobEvents() {
  const response = await fetch(`${settings.apiUrl}/api/print-jobs/worker/events`, { headers: headers() });
  if (!response.ok) throw new Error(`Event connection failed: HTTP ${response.status}`);
  if (!response.body) throw new Error('Event connection returned no response body.');

  for await (const event of parseServerSentEvents(response.body)) {
    if (event.includes('event: print-job')) yield true;
  }
  throw new Error('Event connection closed.');
}

async function claim() {
  const response = await fetch(`${settings.apiUrl}/api/print-jobs/worker/claim`, { method: 'POST', headers: headers() });
  if (response.status === 204) return null;
  if (!response.ok) throw new Error(`Claim failed: HTTP ${response.status}`);
  return response.json();
}

async function complete(id, error) {
  const response = await fetch(`${settings.apiUrl}/api/print-jobs/worker/${id}/complete`, {
    method: 'POST', headers: { ...headers(), 'content-type': 'application/json' },
    body: JSON.stringify({ error: error ? error.slice(0, 1000) : null })
  });
  if (!response.ok) throw new Error(`Completion failed: HTTP ${response.status}`);
}

function send(data) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host: settings.printerHost, port: settings.printerPort });
    socket.setTimeout(settings.timeoutMilliseconds);
    socket.once('connect', () => socket.end(data));
    socket.once('error', reject);
    socket.once('timeout', () => socket.destroy(new Error('Printer connection timed out.')));
    socket.once('close', hadError => { if (!hadError) resolve(); });
  });
}

function headers() { return { 'X-Print-Worker-Key': settings.apiKey }; }
function required(name) { const value = process.env[name]; if (!value) throw new Error(`${name} is required.`); return value; }

function logReceiptLine(job) {
  console.info(JSON.stringify({
    event: 'receiptline-preview',
    printJobId: job.id,
    paperWidth: job.paperWidth,
    receiptLine: job.receiptLine
  }));
}
