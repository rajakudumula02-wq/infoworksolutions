// ImageQualityChecker — Laplacian variance blur detection + histogram exposure analysis
// Works on raw image bytes without external image-decoding libraries.
// For PNG: decodes IDAT pixel data via Node's zlib.
// For JPEG / DICOM: operates on raw byte stream as a proxy for pixel data.

import { inflateSync } from "zlib";
import type { Bytes, QualityCheckResult } from "../types.js";

// ─── Thresholds ───────────────────────────────────────────────────────────────

/** Laplacian variance below this → blurry */
const BLUR_THRESHOLD = 50;

/** Fraction of pixels considered "bright" (≥ 240) above which → overexposed */
const OVEREXPOSED_FRACTION = 0.9;

/** Fraction of pixels considered "dark" (≤ 15) above which → underexposed */
const UNDEREXPOSED_FRACTION = 0.9;

// ─── PNG pixel extraction ─────────────────────────────────────────────────────

/**
 * Attempt to extract a grayscale pixel array from a PNG file.
 * Returns null if parsing fails (caller falls back to raw-byte heuristics).
 */
function extractPngGrayscale(bytes: Uint8Array): Uint8Array | null {
  try {
    if (bytes.length < 33) return null;
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);

    // Read IHDR
    const width = view.getUint32(16, false);
    const height = view.getUint32(20, false);
    const bitDepth = bytes[24];
    const colorType = bytes[25];
    // const compressionMethod = bytes[26]; // always 0
    // const filterMethod = bytes[27];      // always 0
    // const interlaceMethod = bytes[28];   // 0 = no interlace

    if (bitDepth !== 8) return null; // only handle 8-bit
    if (colorType !== 0 && colorType !== 2 && colorType !== 3) return null; // grayscale, RGB, indexed

    const samplesPerPixel = colorType === 2 ? 3 : 1; // RGB vs grayscale

    // Collect IDAT chunks
    const idatChunks: Uint8Array[] = [];
    let offset = 8; // skip signature
    while (offset + 12 <= bytes.length) {
      const chunkLen = view.getUint32(offset, false);
      const chunkType = String.fromCharCode(
        bytes[offset + 4],
        bytes[offset + 5],
        bytes[offset + 6],
        bytes[offset + 7]
      );
      if (chunkType === "IDAT") {
        idatChunks.push(bytes.slice(offset + 8, offset + 8 + chunkLen));
      }
      if (chunkType === "IEND") break;
      offset += 12 + chunkLen;
    }

    if (idatChunks.length === 0) return null;

    // Concatenate and inflate
    const totalLen = idatChunks.reduce((s, c) => s + c.length, 0);
    const combined = new Uint8Array(totalLen);
    let pos = 0;
    for (const chunk of idatChunks) {
      combined.set(chunk, pos);
      pos += chunk.length;
    }

    const decompressed = inflateSync(Buffer.from(combined));

    // Each row has a 1-byte filter type prefix
    const rowBytes = 1 + width * samplesPerPixel;
    const grayscale = new Uint8Array(width * height);

    for (let row = 0; row < height; row++) {
      const rowStart = row * rowBytes + 1; // skip filter byte
      for (let col = 0; col < width; col++) {
        const pixelStart = rowStart + col * samplesPerPixel;
        if (samplesPerPixel === 1) {
          grayscale[row * width + col] = decompressed[pixelStart];
        } else {
          // RGB → luminance (BT.601)
          const r = decompressed[pixelStart];
          const g = decompressed[pixelStart + 1];
          const b = decompressed[pixelStart + 2];
          grayscale[row * width + col] = Math.round(
            0.299 * r + 0.587 * g + 0.114 * b
          );
        }
      }
    }

    return grayscale;
  } catch {
    return null;
  }
}

// ─── Laplacian variance (blur detection) ─────────────────────────────────────

/**
 * Compute the variance of the Laplacian of a 1-D grayscale pixel array.
 * Uses the 1-D discrete Laplacian kernel [-1, 2, -1] as a proxy.
 * Higher variance → sharper image.
 */
function laplacianVariance(pixels: Uint8Array): number {
  if (pixels.length < 3) return 0;
  const n = pixels.length;
  let sum = 0;
  let sumSq = 0;
  const count = n - 2;

  for (let i = 1; i < n - 1; i++) {
    const lap = -pixels[i - 1] + 2 * pixels[i] - pixels[i + 1];
    sum += lap;
    sumSq += lap * lap;
  }

  const mean = sum / count;
  return sumSq / count - mean * mean;
}

// ─── Histogram exposure analysis ─────────────────────────────────────────────

/**
 * Analyse the histogram of pixel values to detect over/underexposure.
 */
function analyseExposure(pixels: Uint8Array): {
  overexposedFraction: number;
  underexposedFraction: number;
} {
  if (pixels.length === 0)
    return { overexposedFraction: 0, underexposedFraction: 0 };

  let bright = 0;
  let dark = 0;
  for (const p of pixels) {
    if (p >= 240) bright++;
    else if (p <= 15) dark++;
  }

  return {
    overexposedFraction: bright / pixels.length,
    underexposedFraction: dark / pixels.length,
  };
}

// ─── Raw-byte fallback heuristics ────────────────────────────────────────────

/**
 * When we cannot decode pixels, use the raw compressed/encoded byte stream
 * as a proxy. This is a coarse heuristic only.
 */
function rawByteHeuristics(bytes: Uint8Array): {
  blurVariance: number;
  overexposedFraction: number;
  underexposedFraction: number;
} {
  // Sample up to 4096 bytes evenly spread through the file
  const sampleSize = Math.min(bytes.length, 4096);
  const step = Math.max(1, Math.floor(bytes.length / sampleSize));
  const sample = new Uint8Array(sampleSize);
  for (let i = 0; i < sampleSize; i++) {
    sample[i] = bytes[i * step];
  }

  return {
    blurVariance: laplacianVariance(sample),
    ...analyseExposure(sample),
  };
}

// ─── ImageQualityChecker implementation ──────────────────────────────────────

export class ImageQualityCheckerImpl {
  check(imageBytes: Bytes): QualityCheckResult {
    let pixels: Uint8Array | null = null;

    // Detect PNG by magic bytes and attempt full pixel decode
    const isPng =
      imageBytes.length >= 8 &&
      imageBytes[0] === 0x89 &&
      imageBytes[1] === 0x50 &&
      imageBytes[2] === 0x4e &&
      imageBytes[3] === 0x47;

    if (isPng) {
      pixels = extractPngGrayscale(imageBytes);
    }

    let blurVariance: number;
    let overexposedFraction: number;
    let underexposedFraction: number;

    if (pixels !== null && pixels.length > 0) {
      blurVariance = laplacianVariance(pixels);
      const exposure = analyseExposure(pixels);
      overexposedFraction = exposure.overexposedFraction;
      underexposedFraction = exposure.underexposedFraction;
    } else {
      // Fallback: raw byte heuristics
      const h = rawByteHeuristics(imageBytes);
      blurVariance = h.blurVariance;
      overexposedFraction = h.overexposedFraction;
      underexposedFraction = h.underexposedFraction;
    }

    // Blur check
    if (blurVariance < BLUR_THRESHOLD) {
      return {
        pass: false,
        reason: "blurry",
        detail: `Image appears blurry (Laplacian variance ${blurVariance.toFixed(2)} is below threshold ${BLUR_THRESHOLD}).`,
      };
    }

    // Overexposure check
    if (overexposedFraction > OVEREXPOSED_FRACTION) {
      const pct = (overexposedFraction * 100).toFixed(1);
      return {
        pass: false,
        reason: "overexposed",
        detail: `Image is overexposed: ${pct}% of pixels are near-white (≥240), exceeding the ${(OVEREXPOSED_FRACTION * 100).toFixed(0)}% threshold.`,
      };
    }

    // Underexposure check
    if (underexposedFraction > UNDEREXPOSED_FRACTION) {
      const pct = (underexposedFraction * 100).toFixed(1);
      return {
        pass: false,
        reason: "underexposed",
        detail: `Image is underexposed: ${pct}% of pixels are near-black (≤15), exceeding the ${(UNDEREXPOSED_FRACTION * 100).toFixed(0)}% threshold.`,
      };
    }

    return { pass: true };
  }
}
