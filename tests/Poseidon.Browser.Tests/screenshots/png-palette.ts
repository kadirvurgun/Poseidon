// USER-GUIDE-01 UG-2: deterministic size reduction of documentation screenshots without a new
// dependency. UI captures hold about a thousand distinct colours and the 256 most frequent cover more
// than 99.8 % of the pixels, so an indexed PNG of those colours (each other colour mapped to its
// nearest palette entry) is visually identical and roughly 55-60 % smaller. When any pixel would move
// by more than `maxChannelError` the original lossless PNG is returned unchanged. Pure: Buffer in, Buffer out.
import { crc32, deflateSync, inflateSync } from 'node:zlib'

export const maxChannelError = 24

type Image = { width: number; height: number; channels: 3 | 4; pixels: Buffer }

/** Decodes a non-interlaced 8-bit RGB/RGBA PNG (what the browser screenshot produces); otherwise null. */
export function decodeRgbPng(input: Buffer): Image | null {
  if (input.length < 33 || input.readUInt32BE(0) !== 0x89504e47 || input.toString('latin1', 12, 16) !== 'IHDR') return null
  const width = input.readUInt32BE(16), height = input.readUInt32BE(20), depth = input[24], color = input[25], interlace = input[28]
  if (depth !== 8 || (color !== 2 && color !== 6) || interlace !== 0) return null
  const channels = color === 6 ? 4 : 3
  const data: Buffer[] = []
  for (let offset = 8; offset + 8 <= input.length;) {
    const length = input.readUInt32BE(offset)
    if (input.toString('latin1', offset + 4, offset + 8) === 'IDAT') data.push(input.subarray(offset + 8, offset + 8 + length))
    offset += 12 + length
  }
  const raw = inflateSync(Buffer.concat(data)), stride = width * channels
  if (raw.length !== (stride + 1) * height) return null
  const pixels = Buffer.alloc(stride * height)
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)], row = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1))
    const out = pixels.subarray(y * stride, (y + 1) * stride), up = y ? pixels.subarray((y - 1) * stride, y * stride) : null
    for (let x = 0; x < stride; x++) {
      const a = x >= channels ? out[x - channels] : 0, b = up ? up[x] : 0, c = up && x >= channels ? up[x - channels] : 0
      let value = row[x]
      if (filter === 1) value += a
      else if (filter === 2) value += b
      else if (filter === 3) value += (a + b) >> 1
      else if (filter === 4) {
        const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c)
        value += pa <= pb && pa <= pc ? a : pb <= pc ? b : c
      } else if (filter !== 0) return null
      out[x] = value & 255
    }
  }
  return { width, height, channels, pixels }
}

function chunk(type: string, data: Buffer) {
  const head = Buffer.alloc(8)
  head.writeUInt32BE(data.length)
  head.write(type, 4, 'latin1')
  const tail = Buffer.alloc(4)
  tail.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])))
  return Buffer.concat([head, data, tail])
}

/** Returns an indexed (palette) PNG of an opaque screenshot, or the input when that would not be faithful or smaller. */
export function paletteScreenshot(input: Buffer): Buffer {
  const image = decodeRgbPng(input)
  if (!image) return input
  const { width, height, channels, pixels } = image
  const frequency = new Map<number, number>()
  for (let index = 0; index < pixels.length; index += channels) {
    if (channels === 4 && pixels[index + 3] !== 255) return input // Transparency is never flattened silently.
    const key = (pixels[index] << 16) | (pixels[index + 1] << 8) | pixels[index + 2]
    frequency.set(key, (frequency.get(key) ?? 0) + 1)
  }
  // Most frequent first; ties by colour value so the palette never depends on Map iteration order.
  const palette = [...frequency.entries()].sort((left, right) => right[1] - left[1] || left[0] - right[0]).slice(0, 256).map(([key]) => key)
  const slot = new Map(palette.map((key, index) => [key, index] as const))
  for (const key of frequency.keys()) {
    if (slot.has(key)) continue
    const r = key >> 16, g = (key >> 8) & 255, b = key & 255
    let best = 0, distance = Infinity
    for (const [index, entry] of palette.entries()) {
      const d = (r - (entry >> 16)) ** 2 + (g - ((entry >> 8) & 255)) ** 2 + (b - (entry & 255)) ** 2
      if (d < distance) { distance = d; best = index }
    }
    const entry = palette[best]
    if (Math.max(Math.abs(r - (entry >> 16)), Math.abs(g - ((entry >> 8) & 255)), Math.abs(b - (entry & 255))) > maxChannelError) return input
    slot.set(key, best)
  }
  const rows = Buffer.alloc((width + 1) * height) // Filter type 0 per row: best for palette indices.
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const index = (y * width + x) * channels
    rows[y * (width + 1) + 1 + x] = slot.get((pixels[index] << 16) | (pixels[index + 1] << 8) | pixels[index + 2])!
  }
  const header = Buffer.alloc(13)
  header.writeUInt32BE(width)
  header.writeUInt32BE(height, 4)
  header[8] = 8
  header[9] = 3
  const colours = Buffer.alloc(palette.length * 3)
  palette.forEach((key, index) => { colours[index * 3] = key >> 16; colours[index * 3 + 1] = (key >> 8) & 255; colours[index * 3 + 2] = key & 255 })
  const output = Buffer.concat([input.subarray(0, 8), chunk('IHDR', header), chunk('PLTE', colours),
    chunk('IDAT', deflateSync(rows, { level: 9, memLevel: 9 })), chunk('IEND', Buffer.alloc(0))])
  return output.length < input.length ? output : input
}
