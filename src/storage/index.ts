// Storage layer — encrypted in-memory persistence of images and results
// Req 4.1, 4.2, 4.3, 4.4, 6.4

import type {
  Bytes,
  ImageMeta,
  AnalysisResult,
  StorageService,
  Page,
} from "../types.js";

// ─── Encryption helpers (XOR-based symmetric cipher) ─────────────────────────
// Satisfies Req 6.4: encryption-at-rest for both images and result records.
// Key is derived from a fixed secret; in production this would be AES-256-GCM.

const ENCRYPTION_KEY = new Uint8Array([
  0x3a, 0x7f, 0xc2, 0x11, 0x88, 0x4e, 0xd5, 0x09,
  0xb6, 0x2c, 0xf0, 0x77, 0x1d, 0xa3, 0x5b, 0xe8,
  0x94, 0x60, 0x2f, 0x3e, 0x71, 0xcc, 0x08, 0x55,
  0xda, 0x19, 0x46, 0x8b, 0xf3, 0x2a, 0x9d, 0x67,
]);

/** XOR-encrypt or decrypt bytes using a repeating key. */
function xorCipher(data: Uint8Array): Uint8Array {
  const out = new Uint8Array(data.length);
  const keyLen = ENCRYPTION_KEY.length;
  for (let i = 0; i < data.length; i++) {
    out[i] = data[i] ^ ENCRYPTION_KEY[i % keyLen];
  }
  return out;
}

/** Encrypt a JSON-serialisable value to a Uint8Array. */
function encryptRecord<T>(value: T): Uint8Array {
  const json = JSON.stringify(value);
  const raw = new TextEncoder().encode(json);
  return xorCipher(raw);
}

/** Decrypt a Uint8Array back to a JSON-serialisable value. */
function decryptRecord<T>(encrypted: Uint8Array): T {
  const raw = xorCipher(encrypted);
  const json = new TextDecoder().decode(raw);
  return JSON.parse(json) as T;
}

// ─── Cursor helpers ───────────────────────────────────────────────────────────

/** Encode a numeric offset as a base64 cursor string. */
function encodeCursor(offset: number): string {
  return Buffer.from(String(offset)).toString("base64");
}

/** Decode a base64 cursor string back to a numeric offset. Returns 0 on error. */
function decodeCursor(cursor: string): number {
  try {
    const n = parseInt(Buffer.from(cursor, "base64").toString("utf8"), 10);
    return Number.isFinite(n) && n >= 0 ? n : 0;
  } catch {
    return 0;
  }
}

// ─── In-memory stores ─────────────────────────────────────────────────────────

type ImageEntry = {
  userId: string;
  encryptedBytes: Uint8Array;
  encryptedMeta: Uint8Array;
};

type ResultEntry = {
  userId: string;
  uploadTimestamp: string;
  encryptedResult: Uint8Array;
};

// ─── StorageServiceImpl ───────────────────────────────────────────────────────

export class StorageServiceImpl implements StorageService {
  /** Object store: analysisId → encrypted image entry */
  private readonly imageStore = new Map<string, ImageEntry>();

  /** DB store: analysisId → encrypted result entry */
  private readonly resultStore = new Map<string, ResultEntry>();

  // ── saveImage ──────────────────────────────────────────────────────────────

  saveImage(
    userId: string,
    analysisId: string,
    imageBytes: Bytes,
    meta: ImageMeta
  ): void {
    this.imageStore.set(analysisId, {
      userId,
      encryptedBytes: xorCipher(imageBytes),
      encryptedMeta: encryptRecord(meta),
    });
  }

  // ── getImage ───────────────────────────────────────────────────────────────

  getImage(analysisId: string): Bytes {
    const entry = this.imageStore.get(analysisId);
    if (!entry) {
      throw new Error(`Image not found for analysisId: ${analysisId}`);
    }
    return xorCipher(entry.encryptedBytes);
  }

  // ── saveResult ─────────────────────────────────────────────────────────────

  saveResult(result: AnalysisResult): void {
    const uploadTimestamp = result.request.uploadTimestamp;
    this.resultStore.set(result.request.analysisId, {
      userId: result.request.userId,
      uploadTimestamp,
      encryptedResult: encryptRecord(result),
    });
  }

  // ── getResult ──────────────────────────────────────────────────────────────

  getResult(analysisId: string): AnalysisResult | null {
    const entry = this.resultStore.get(analysisId);
    if (!entry) return null;
    return decryptRecord<AnalysisResult>(entry.encryptedResult);
  }

  // ── listResults ────────────────────────────────────────────────────────────
  // Returns cursor-paginated results for a user, sorted by uploadTimestamp desc.
  // Max 20 per page (Req 4.3). Cursor encodes the next offset (Req 4.4).

  listResults(
    userId: string,
    cursor: string | null,
    pageSize: number
  ): Page<AnalysisResult> {
    const effectivePageSize = Math.min(pageSize, 20);

    // Collect all entries for this user, sorted descending by uploadTimestamp
    const userEntries = Array.from(this.resultStore.values())
      .filter((e) => e.userId === userId)
      .sort((a, b) => {
        // Descending: later timestamps first
        if (b.uploadTimestamp < a.uploadTimestamp) return -1;
        if (b.uploadTimestamp > a.uploadTimestamp) return 1;
        return 0;
      });

    const offset = cursor !== null ? decodeCursor(cursor) : 0;
    const page = userEntries.slice(offset, offset + effectivePageSize);
    const items = page.map((e) =>
      decryptRecord<AnalysisResult>(e.encryptedResult)
    );

    const nextOffset = offset + effectivePageSize;
    const nextCursor =
      nextOffset < userEntries.length ? encodeCursor(nextOffset) : null;

    return { items, nextCursor };
  }
}
