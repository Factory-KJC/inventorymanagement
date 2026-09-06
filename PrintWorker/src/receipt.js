import receiptline from 'receiptline';

const charactersPerLine = Object.freeze({ Mm58: 32, Mm80: 48 });

/** Converts a receiptline document directly to ESC/POS bytes using printer fonts. */
export function toEscPos(document, paperWidth) {
  const cpl = charactersPerLine[paperWidth];
  if (!cpl) {
    throw new Error(`Unsupported paper width: ${paperWidth}`);
  }

  const command = receiptline.transform(document, {
    asImage: false,
    cpl,
    encoding: 'shiftjis',
    command: 'escpos',
    cutting: true,
    spacing: true
  });
  return Buffer.from(command, 'binary');
}
