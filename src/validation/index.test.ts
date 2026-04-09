import { describe, it, expect } from "vitest";
import { ValidationServiceImpl } from "./index.js";
import type { UploadedFile } from "../types.js";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const MB = 1024 * 1024;

/** Minimal valid PNG: 1×1 white pixel */
function makePng(width: number, height: number): Uint8Array {
  // Build a minimal PNG with the given dimensions in the IHDR.
  // We won't bother with real IDAT data — validation only reads the header.
  const sig = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  // IHDR chunk: length=13, type="IHDR", data(13 bytes), crc(4 bytes)
  const ihdrData = new Uint8Array(13);
  const dv = new DataView(ihdrData.buffer);
  dv.setUint32(0, width, false);
  dv.setUint32(4, height, false);
  ihdrData[8] = 8; // bit depth
  ihdrData[9] = 2; // color type RGB
  ihdrData[10] = 0; // compression
  ihdrData[11] = 0; // filter
  ihdrData[12] = 0; // interlace

  const ihdrChunk = new Uint8Array(4 + 4 + 13 + 4);
  const cv = new DataView(ihdrChunk.buffer);
  cv.setUint32(0, 13, false); // length
  ihdrChunk[4] = 0x49; // I
  ihdrChunk[5] = 0x48; // H
  ihdrChunk[6] = 0x44; // D
  ihdrChunk[7] = 0x52; // R
  ihdrChunk.set(ihdrData, 8);
  // CRC left as zeros (we don't validate CRC in our parser)

  const result = new Uint8Array(sig.length + ihdrChunk.length);
  result.set(sig, 0);
  result.set(ihdrChunk, sig.length);
  return result;
}

/** Minimal JPEG with given dimensions encoded in SOF0 marker */
function makeJpeg(width: number, height: number): Uint8Array {
  // SOI + APP0 + SOF0 + EOI
  const bytes: number[] = [];

  // SOI
  bytes.push(0xff, 0xd8);

  // APP0 (minimal, length=16)
  bytes.push(0xff, 0xe0);
  bytes.push(0x00, 0x10); // length 16
  bytes.push(0x4a, 0x46, 0x49, 0x46, 0x00); // "JFIF\0"
  bytes.push(0x01, 0x01); // version
  bytes.push(0x00); // aspect ratio units
  bytes.push(0x00, 0x01, 0x00, 0x01); // Xdensity, Ydensity
  bytes.push(0x00, 0x00); // thumbnail

  // SOF0 (baseline DCT)
  // length = 8 + 3*components = 8 + 3 = 11 for 1 component
  bytes.push(0xff, 0xc0);
  bytes.push(0x00, 0x0b); // length 11
  bytes.push(0x08); // precision
  bytes.push((height >> 8) & 0xff, height & 0xff);
  bytes.push((width >> 8) & 0xff, width & 0xff);
  bytes.push(0x01); // 1 component
  bytes.push(0x01, 0x11, 0x00); // component spec

  // EOI
  bytes.push(0xff, 0xd9);

  return new Uint8Array(bytes);
}

function makeFile(
  overrides: Partial<UploadedFile> & { bytes?: Uint8Array }
): UploadedFile {
  const bytes = overrides.bytes ?? makePng(400, 400);
  return {
    filename: "test.png",
    mimeType: "image/png",
    sizeBytes: bytes.length,
    bytes,
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe("ValidationServiceImpl.validateUpload", () => {
  const svc = new ValidationServiceImpl();

  describe("MIME type / format validation", () => {
    it("accepts image/jpeg", () => {
      const bytes = makeJpeg(400, 400);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/jpeg", bytes, filename: "x.jpg" })
      );
      expect(result.ok).toBe(true);
    });

    it("accepts image/png", () => {
      const result = svc.validateUpload(makeFile({ mimeType: "image/png" }));
      expect(result.ok).toBe(true);
    });

    it("accepts application/dicom", () => {
      // Build minimal DICOM bytes (128-byte preamble + "DICM")
      const dicomBytes = new Uint8Array(132);
      dicomBytes[128] = 0x44; // D
      dicomBytes[129] = 0x49; // I
      dicomBytes[130] = 0x43; // C
      dicomBytes[131] = 0x4d; // M
      const result = svc.validateUpload(
        makeFile({
          mimeType: "application/dicom",
          bytes: dicomBytes,
          filename: "scan.dcm",
        })
      );
      expect(result.ok).toBe(true);
    });

    it("rejects image/gif with HTTP 415", () => {
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/gif", bytes: new Uint8Array([0x47, 0x49, 0x46]) })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(415);
        expect(result.message).toBeTruthy();
      }
    });

    it("rejects application/pdf with HTTP 415", () => {
      const result = svc.validateUpload(
        makeFile({ mimeType: "application/pdf", bytes: new Uint8Array([0x25, 0x50, 0x44, 0x46]) })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(415);
      }
    });
  });

  describe("File size validation", () => {
    it("accepts a file exactly at 20 MB", () => {
      const bytes = new Uint8Array(20 * MB);
      // Give it a valid PNG header so format check passes
      const pngHeader = makePng(400, 400);
      bytes.set(pngHeader, 0);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes, sizeBytes: 20 * MB })
      );
      expect(result.ok).toBe(true);
    });

    it("rejects a file 1 byte over 20 MB with HTTP 400", () => {
      const bytes = new Uint8Array(20 * MB + 1);
      const pngHeader = makePng(400, 400);
      bytes.set(pngHeader, 0);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes, sizeBytes: 20 * MB + 1 })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(400);
        expect(result.message).toMatch(/20 MB/i);
      }
    });

    it("rejects a 25 MB file with HTTP 400", () => {
      const bytes = new Uint8Array(25 * MB);
      const pngHeader = makePng(400, 400);
      bytes.set(pngHeader, 0);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes, sizeBytes: 25 * MB })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(400);
      }
    });
  });

  describe("Resolution validation", () => {
    it("accepts a PNG with exactly 300×300 pixels", () => {
      const bytes = makePng(300, 300);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes })
      );
      expect(result.ok).toBe(true);
    });

    it("rejects a PNG with 299×300 pixels with HTTP 422", () => {
      const bytes = makePng(299, 300);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(422);
        expect(result.message).toMatch(/300/);
      }
    });

    it("rejects a PNG with 100×100 pixels with HTTP 422", () => {
      const bytes = makePng(100, 100);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/png", bytes })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(422);
      }
    });

    it("accepts a JPEG with 400×400 pixels", () => {
      const bytes = makeJpeg(400, 400);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/jpeg", bytes, filename: "x.jpg" })
      );
      expect(result.ok).toBe(true);
    });

    it("rejects a JPEG with 200×200 pixels with HTTP 422", () => {
      const bytes = makeJpeg(200, 200);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/jpeg", bytes, filename: "x.jpg" })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(422);
      }
    });
  });

  describe("Error ordering", () => {
    it("returns 415 before checking size for unsupported format", () => {
      // Oversized AND wrong format — should get 415
      const bytes = new Uint8Array(25 * MB);
      const result = svc.validateUpload(
        makeFile({ mimeType: "image/gif", bytes, sizeBytes: 25 * MB })
      );
      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.httpStatus).toBe(415);
      }
    });
  });
});

// ─── Property-based tests ─────────────────────────────────────────────────────

import fc from "fast-check";

// ── Property 1: Format validation accepts supported types and rejects unsupported types ──
// Feature: dental-image-score-analysis, Property 1: Format validation accepts supported types and rejects unsupported types
describe("Property 1: Format validation accepts supported types and rejects unsupported types", () => {
  const svc = new ValidationServiceImpl();

  it("accepts any of the three supported MIME types with valid bytes", () => {
    const arbSupported = fc.constantFrom(
      { mime: "image/jpeg", makeBytes: () => makeJpeg(400, 400) },
      { mime: "image/png", makeBytes: () => makePng(400, 400) },
      {
        mime: "application/dicom",
        makeBytes: () => {
          const b = new Uint8Array(132);
          b[128] = 0x44; b[129] = 0x49; b[130] = 0x43; b[131] = 0x4d;
          return b;
        },
      }
    );
    fc.assert(
      fc.property(arbSupported, ({ mime, makeBytes }) => {
        const bytes = makeBytes();
        const result = svc.validateUpload(makeFile({ mimeType: mime, bytes, sizeBytes: bytes.length }));
        return result.ok === true;
      }),
      { numRuns: 100 }
    );
  });

  it("rejects unsupported MIME types with HTTP 415", () => {
    const arbUnsupported = fc.constantFrom(
      "image/gif", "image/bmp", "image/webp", "application/pdf",
      "text/plain", "application/octet-stream", "video/mp4"
    );
    fc.assert(
      fc.property(arbUnsupported, (mime) => {
        const bytes = new Uint8Array(64).fill(0x00);
        const result = svc.validateUpload(makeFile({ mimeType: mime, bytes, sizeBytes: bytes.length }));
        return result.ok === false && result.httpStatus === 415;
      }),
      { numRuns: 100 }
    );
  });
});

// ── Property 2: Size validation rejects oversized files with HTTP 400 ─────────
// Feature: dental-image-score-analysis, Property 2: Size validation rejects oversized files with HTTP 400
describe("Property 2: Size validation rejects oversized files with HTTP 400", () => {
  const svc = new ValidationServiceImpl();

  it("rejects files larger than 20 MB with HTTP 400", () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 20 * MB + 1, max: 30 * MB }),
        (size) => {
          const bytes = new Uint8Array(64);
          const pngHeader = makePng(400, 400);
          bytes.set(pngHeader.subarray(0, Math.min(pngHeader.length, 64)), 0);
          const result = svc.validateUpload(makeFile({ mimeType: "image/png", bytes, sizeBytes: size }));
          return result.ok === false && result.httpStatus === 400;
        }
      ),
      { numRuns: 100 }
    );
  });
});

// ── Property 13: Low-resolution images are rejected and not stored ────────────
// Feature: dental-image-score-analysis, Property 13: Low-resolution images are rejected and not stored
describe("Property 13: Low-resolution images are rejected and not stored", () => {
  const svc = new ValidationServiceImpl();

  it("rejects PNG images below 300×300 with HTTP 422", () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 1, max: 299 }),
        fc.integer({ min: 1, max: 299 }),
        (w, h) => {
          const bytes = makePng(w, h);
          const result = svc.validateUpload(makeFile({ mimeType: "image/png", bytes }));
          return result.ok === false && result.httpStatus === 422;
        }
      ),
      { numRuns: 100 }
    );
  });

  it("rejects JPEG images below 300×300 with HTTP 422", () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 1, max: 299 }),
        fc.integer({ min: 1, max: 299 }),
        (w, h) => {
          const bytes = makeJpeg(w, h);
          const result = svc.validateUpload(makeFile({ mimeType: "image/jpeg", bytes, filename: "x.jpg" }));
          return result.ok === false && result.httpStatus === 422;
        }
      ),
      { numRuns: 100 }
    );
  });
});
