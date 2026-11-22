import { test } from 'node:test'
import assert from 'node:assert/strict'
import { crc32, deflateSync, inflateSync } from 'node:zlib'
import { decodeRgbPng, maxChannelError, paletteScreenshot } from './png-palette.ts'

// USER-GUIDE-01 UG-2: the screenshot size reduction is deterministic, never moves a pixel by more than
// the compare threshold, never flattens transparency and keeps the original when it cannot be faithful.
const chunk = (type: string, data: Buffer) => {
  const head = Buffer.alloc(8); head.writeUInt32BE(data.length); head.write(type, 4, 'latin1')
  const tail = Buffer.alloc(4); tail.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])))
  return Buffer.concat([head, data, tail])
}
/** Minimal RGBA PNG encoder (filter 0) for fixtures. */
function rgba(width: number, height: number, pixel: (x: number, y: number) => [number, number, number, number]) {
  const rows = Buffer.alloc((width * 4 + 1) * height)
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) rows.set(pixel(x, y), y * (width * 4 + 1) + 1 + x * 4)
  const header = Buffer.alloc(13); header.writeUInt32BE(width); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk('IHDR', header), chunk('IDAT', deflateSync(rows)), chunk('IEND', Buffer.alloc(0))])
}
/** Decodes the indexed output back to RGB triples. */
function indexed(png: Buffer) {
  assert.equal(png[25], 3)
  const width = png.readUInt32BE(16), height = png.readUInt32BE(20)
  let palette = Buffer.alloc(0); const data: Buffer[] = []
  for (let offset = 8; offset < png.length;) {
    const length = png.readUInt32BE(offset), type = png.toString('latin1', offset + 4, offset + 8)
    if (type === 'PLTE') palette = png.subarray(offset + 8, offset + 8 + length)
    if (type === 'IDAT') data.push(png.subarray(offset + 8, offset + 8 + length))
    offset += 12 + length
  }
  const rows = inflateSync(Buffer.concat(data)), out: number[][] = []
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    assert.equal(rows[y * (width + 1)], 0)
    const slot = rows[y * (width + 1) + 1 + x]
    out.push([palette[slot * 3], palette[slot * 3 + 1], palette[slot * 3 + 2]])
  }
  return out
}

test('a UI-like opaque capture becomes an exact, smaller, deterministic palette PNG', () => {
  const source = rgba(120, 80, (x, y) => x < 60 ? [255, 255, 255, 255] : y < 40 ? [16, 122, 87, 255] : [(x * 3) % 7, 20, 30, 255])
  const once = paletteScreenshot(source)
  assert.ok(once.length < source.length)
  assert.deepEqual(paletteScreenshot(source), once)
  const original = decodeRgbPng(source)!
  indexed(once).forEach((rgb, index) => assert.deepEqual(rgb, [...original.pixels.subarray(index * 4, index * 4 + 3)]))
})

test('colours beyond the 256 most frequent move to their nearest entry within the compare threshold', () => {
  // 256 frequent grey levels plus rare near-duplicates (antialiasing-like): every pixel stays within the bound.
  const source = rgba(256, 40, (x, y) => y === 39 && x % 2 ? [x, Math.min(255, x + 3), x, 255] : [x, x, x, 255])
  const output = paletteScreenshot(source)
  assert.notEqual(output, source)
  const original = decodeRgbPng(source)!
  indexed(output).forEach((rgb, index) => {
    const error = Math.max(...rgb.map((value, channel) => Math.abs(value - original.pixels[index * 4 + channel])))
    assert.ok(error <= maxChannelError, String(error))
  })
})

test('transparency, colours too far from the palette and non-RGB input keep the original bytes', () => {
  const transparent = rgba(4, 4, () => [10, 20, 30, 128])
  assert.equal(paletteScreenshot(transparent), transparent)
  // 512 frequent, widely spaced colours: the rarer half is far from any palette entry.
  const far = rgba(512, 4, x => [x & 255, x < 256 ? 0 : 255, (x * 37) & 255, 255])
  assert.equal(paletteScreenshot(far), far)
  const notPng = Buffer.from('not a png at all, definitely not')
  assert.equal(paletteScreenshot(notPng), notPng)
})
