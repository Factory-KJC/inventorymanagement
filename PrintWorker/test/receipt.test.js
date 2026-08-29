import assert from 'node:assert/strict';
import test from 'node:test';
import { toEscPos } from '../src/receipt.js';

test('receiptline generates ESC/POS bytes without raster image commands', () => {
  const command = toEscPos('{align:center}\n買い物リスト\n洗剤 | 2 本', 'Mm80');
  assert.ok(command.length > 10);
  assert.equal(command.includes(Buffer.from([0x1d, 0x76, 0x30])), false);
  assert.equal(command.includes(Buffer.from([0x1b, 0x2a])), false);
  assert.equal(command.includes(Buffer.from([0x1d, 0x56])), true);
});
