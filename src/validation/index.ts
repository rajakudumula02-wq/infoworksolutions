// Validation layer — file format, size, and image quality checks

import type { UploadedFile, ValidationResult } from "../types.js";

// ─── Constants ────────────────────────────────────────────────────────────────

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024; // 20 MB
const MIN_DIMENSION = 300; // pixels

const ALLOWED_MIME_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "application/dicom",
]);

// JPEG magic bytes: FF D8 FF
const JPEG_MAGIC = [0xff, 0xd8, 0xff];
// PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
const PNG_MAGIC = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
// DICOM magic at offset 128: "DICM"
const DICOM_MAGIC = [0x44, 0x49, 0x43, 0x4d]; // "DICM"

// ─── Header parsing helpers ───────────────────────────────────────────────────

function matchesBytes(
  buf: Uint8Array,
  magic: number[],
  offset = 0
): boolean {
  if (buf.length < offset + magic.length) return false;
  return magic.every((b, i) => buf[offset + i] === b);
}

/**
 * Detect format from file bytes (magic-number based).
 * Returns null if unrecognised.
 */
function detectFormat(
  bytes: Uint8Array
): "jpeg" | "png" | "dicom" | null {
  if (matchesBytes(bytes, JPEG_MAGIC)) return "jpeg";
  if (matchesBytes(bytes, PNG_MAGIC)) return "png";
  // DICOM: preamble is 128 bytes, then "DICM"
  if (bytes.length >= 132 && matchesBytes(bytes, DICOM_MAGIC, 128))
    return "dicom";
  return null;
}

/**
 * Read PNG pixel dimensions from the IHDR chunk.
 * PNG layout: 8-byte signature, then chunks.
 * First chunk is always IHDR: 4-byte length, 4-byte "IHDR", 4-byte width, 4-byte height, ...
 */
function readPngDimensions(
  bytes: Uint8Array
): { width: number; height: number } | null {
  // IHDR starts at offset 8 (after signature)
  // chunk structure: [4 length][4 type][data][4 crc]
  // IHDR data starts at offset 8 + 4 (length) + 4 (type) = 16
  if (bytes.length < 24) return null;
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const width = view.getUint32(16, false); // big-endian
  const height = view.getUint32(20, false);
  return { width, height };
}

/**
 * Read JPEG pixel dimensions by scanning for SOF markers.
 * SOF0 = FF C0, SOF1 = FF C1, SOF2 = FF C2 (progressive), etc.
 */
function readJpegDimensions(
  bytes: Uint8Array
): { width: number; height: number } | null {
  const SOF_MARKERS = new Set([
    0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce,
    0xcf,
  ]);

  let i = 2; // skip FF D8
  while (i < bytes.length - 1) {
    if (bytes[i] !== 0xff) break;
    const marker = bytes[i + 1];
    if (marker === 0xd9) break; // EOI

    // Markers with no length field
    if (marker === 0xd8 || (marker >= 0xd0 && marker <= 0xd7)) {
      i += 2;
      continue;
    }

    if (i + 3 >= bytes.length) break;
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const segLen = view.getUint16(i + 2, false); // big-endian

    if (SOF_MARKERS.has(marker)) {
      // SOF segment: [FF][marker][length 2][precision 1][height 2][width 2]
      if (i + 8 < bytes.length) {
        const height = view.getUint16(i + 5, false);
        const width = view.getUint16(i + 7, false);
        return { width, height };
      }
    }

    i += 2 + segLen;
  }
  return null;
}

// ─── ValidationService implementation ────────────────────────────────────────

export class ValidationServiceImpl {
  validateUpload(file: UploadedFile): ValidationResult {
    // 1. MIME type check (Req 1.1, 1.3, 1.5)
    if (!ALLOWED_MIME_TYPES.has(file.mimeType)) {
      // Also check by magic bytes as a fallback
      const detectedFormat = detectFormat(file.bytes);
      if (detectedFormat === null) {
        return {
          ok: false,
          httpStatus: 415,
          message: `Unsupported file format "${file.mimeType}". Accepted formats: image/jpeg, image/png, application/dicom.`,
        };
      }
    }

    // 2. File size check (Req 1.2, 1.4)
    if (file.sizeBytes > MAX_FILE_SIZE_BYTES) {
      const sizeMB = (file.sizeBytes / (1024 * 1024)).toFixed(1);
      return {
        ok: false,
        httpStatus: 400,
        message: `File size ${sizeMB} MB exceeds the maximum allowed size of 20 MB.`,
      };
    }

    // 3. Resolution check — decode header for pixel dimensions (Req 1.3, 5.2)
    const detectedFormat = detectFormat(file.bytes);

    if (detectedFormat === "png") {
      const dims = readPngDimensions(file.bytes);
      if (dims !== null) {
        if (dims.width < MIN_DIMENSION || dims.height < MIN_DIMENSION) {
          return {
            ok: false,
            httpStatus: 422,
            message: `Image resolution ${dims.width}×${dims.height} is below the minimum required 300×300 pixels.`,
          };
        }
      }
    } else if (detectedFormat === "jpeg") {
      const dims = readJpegDimensions(file.bytes);
      if (dims !== null) {
        if (dims.width < MIN_DIMENSION || dims.height < MIN_DIMENSION) {
          return {
            ok: false,
            httpStatus: 422,
            message: `Image resolution ${dims.width}×${dims.height} is below the minimum required 300×300 pixels.`,
          };
        }
      }
    }
    // DICOM: skip pixel dimension check (requires full DICOM parser)

    return { ok: true };
  }
}
