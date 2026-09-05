import test from 'node:test';
import assert from 'node:assert/strict';
import { parseServerSentEvents } from '../src/sse-events.js';

test('SSE parser handles events split across chunks', async () => {
  const encoder = new TextEncoder();
  const chunks = [
    encoder.encode('event: print-'),
    encoder.encode('job\ndata: available\n\nevent: print-job\n'),
    encoder.encode('data: available\n\n')
  ];

  const events = [];
  for await (const event of parseServerSentEvents(chunks)) events.push(event);

  assert.deepEqual(events, [
    'event: print-job\ndata: available',
    'event: print-job\ndata: available'
  ]);
});

test('SSE parser accepts CRLF line endings', async () => {
  const chunks = [new TextEncoder().encode('event: print-job\r\ndata: available\r\n\r\n')];

  const events = [];
  for await (const event of parseServerSentEvents(chunks)) events.push(event);

  assert.deepEqual(events, ['event: print-job\ndata: available']);
});
