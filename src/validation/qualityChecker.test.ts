import { describe, it, expect } from "vitest";
import { deflateSync } from "zlib";
import { ImageQualityCheckerImpl } from "./qualityChecker.js";

// ─── PNG builder ──────────────────────────────────────────────────────────────

function crc32(data: Uint8Array): number {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let c = i;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[i] = c;
  }
  let crc = 0xffffffff;
  for (const b of data) crc = table[(crc ^ b) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function uint32BE(n: number): Uint8Array {
  const b = new Uint8Array(4);
  new DataView(b.buffer).setUint32(0, n, false);
  return b;
}

function makeChunk(type: string, data: Uint8Array): Uint8Array {
  const typeBytes = new TextEncoder().encode(type);
  const crcInput = new Uint8Array(4 + data.length);
  crcInput.set(typeBytes, 0);
  crcInput.set(data, 4);
  const crc = crc32(crcInput);
  const chunk = new Uint8Array(4 + 4 + data.length + 4);
  chunk.set(uint32BE(data.length), 0);
  chunk.set(typeBytes, 4);
  chunk.set(data, 8);
  chunk.set(uint32BE(crc), 8 + data.length);
  return chunk;
}

/**
 * Build a real, decodable PNG from a grayscale pixel array.
 * colorType=0 (grayscale), bitDepth=8.
 */
function buildGrayscalePng(
  width: number,
  height: number,
  pixels: Uint8Array
): Uint8Array {
  const sig = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

  // IHDR
  const ihdrData = new Uint8Array(13);
  const dv = new DataView(ihdrData.buffer);
  dv.setUint32(0, width, false);
  dv.setUint32(4, height, false);
  ihdrData[8] = 8; // bit depth
  ihdrData[9] = 0; // color type: grayscale
  const ihdr = makeChunk("IHDR", ihdrData);

  // Raw image data: each row prefixed with filter byte 0
  const rawRows = new Uint8Array(height * (1 + width));
  for (let row = 0; row < height; row++) {
    rawRows[row * (1 + width)] = 0; // filter type None
    rawRows.set(pixels.subarray(row * width, (row + 1) * width), row * (1 + width) + 1);
  }

  const compressed = deflateSync(Buffer.from(rawRows));
  const idat = makeChunk("IDAT", new Uint8Array(compressed));
  const iend = makeChunk("IEND", new Uint8Array(0));

  const total = sig.length + ihdr.length + idat.length + iend.length;
  const out = new Uint8Array(total);
  let off = 0;
  out.set(sig, off); off += sig.length;
  out.set(ihdr, off); off += ihdr.length;
  out.set(idat, off); off += idat.length;
  out.set(iend, off);
  return out;
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe("ImageQualityCheckerImpl.check", () => {
  const checker = new ImageQualityCheckerImpl();

  describe("blur detection", () => {
    it("passes a sharp image (high Laplacian variance)", () => {
      // Alternating black/white pixels → very high variance
      const w = 64, h = 64;
      const pixels = new Uint8Array(w * h);
      for (let i = 0; i < pixels.length; i++) pixels[i] = i % 2 === 0 ? 0 : 255;
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(true);
    });

    it("fails a blurry image (uniform pixels → low Laplacian variance)", () => {
      // All pixels the same value → variance = 0
      const w = 64, h = 64;
      const pixels = new Uint8Array(w * h).fill(128);
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(false);
      if (!result.pass) {
        expect(result.reason).toBe("blurry");
        expect(result.detail.length).toBeGreaterThan(0);
      }
    });
  });

  describe("overexposure detection", () => {
    it("fails an overexposed image (>90% near-white pixels)", () => {
      const w = 64, h = 64;
      const pixels = new Uint8Array(w * h).fill(255); // all white
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(false);
      if (!result.pass) {
        // Could be blurry first (uniform), but detail must be non-empty
        expect(result.detail.length).toBeGreaterThan(0);
      }
    });

    it("fails an image with 95% near-white pixels as overexposed", () => {
      const w = 64, h = 64;
      const pixels = new Uint8Array(w * h);
      // 95% white (245), 5% mid-gray to add some variance
      const threshold = Math.floor(pixels.length * 0.95);
      for (let i = 0; i < pixels.length; i++) {
        pixels[i] = i < threshold ? 245 : 128;
      }
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(false);
      if (!result.pass) {
        expect(result.detail).toBeTruthy();
      }
    });
  });

  describe("underexposure detection", () => {
    it("fails an underexposed image (>90% near-black pixels)", () => {
      const w = 64, h = 64;
      const pixels = new Uint8Array(w * h).fill(0); // all black
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(false);
      if (!result.pass) {
        expect(result.detail.length).toBeGreaterThan(0);
      }
    });
  });

  describe("failure detail string", () => {
    it("always returns a non-empty detail string on failure", () => {
      const w = 32, h = 32;
      const pixels = new Uint8Array(w * h).fill(128); // uniform → blurry
      const png = buildGrayscalePng(w, h, pixels);
      const result = checker.check(png);
      expect(result.pass).toBe(false);
      if (!result.pass) {
        expect(typeof result.detail).toBe("string");
        expect(result.detail.trim().length).toBeGreaterThan(0);
      }
    });
  });

  describe("raw byte fallback (non-PNG)", () => {
    it("returns a QualityCheckResult for arbitrary bytes", () => {
      const bytes = new Uint8Array(1024);
      for (let i = 0; i < bytes.length; i++) bytes[i] = i % 256;
      const result = checker.check(bytes);
      // Just verify it returns a valid result shape
      expect(typeof result.pass).toBe("boolean");
      if (!result.pass) {
        expect(result.reason).toMatch(/blurry|overexposed|underexposed/);
        expect(result.detail.length).toBeGreaterThan(0);
      }
    });
  });
});

// ─── Property-based tests ─────────────────────────────────────────────────────

import fc from "fast-check";

// ── Property 6: Quality error includes a descriptive reason ───────────────────
// Feature: dental-image-score-analysis, Property 6: Quality error includes a descriptive reason
describe("Property 6: Quality error includes a descriptive reason", () => {
  const checker = new ImageQualityCheckerImpl();

  it("every failing result has a non-empty reason and detail string", () => {
    fc.assert(
      fc.property(
        fc.constantFrom("uniform", "allWhite", "allBlack"),
        (kind) => {
          const w = 32, h = 32;
          let pixels: Uint8Array;
          if (kind === "uniform") pixels = new Uint8Array(w * h).fill(128);
          else if (kind === "allWhite") pixels = new Uint8Array(w * h).fill(255);
          else pixels = new Uint8Array(w * h).fill(0);

          const png = buildGrayscalePng(w, h, pixels);
          const result = checker.check(png);

          if (!result.pass) {
            return (
              typeof result.reason === "string" &&
              result.reason.length > 0 &&
              typeof result.detail === "string" &&
              result.detail.trim().length > 0
            );
          }
          return true; // pass is also acceptable
        }
      ),
      { numRuns: 100 }
    );
  });
});
