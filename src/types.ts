// ─── Primitive aliases ────────────────────────────────────────────────────────

export type Bytes = Uint8Array;

// ─── Domain types ─────────────────────────────────────────────────────────────

export type ImageMeta = {
  filename: string;
  format: "jpeg" | "png" | "dicom";
  fileSizeBytes: number;
  uploadTimestamp: string; // ISO 8601
};

export type AnalysisRequest = {
  analysisId: string; // UUID v4
  userId: string;
  uploadTimestamp: string; // ISO 8601
  status: "pending" | "processing" | "complete" | "failed";
  imageMeta: ImageMeta;
};

export type IndicatorName =
  | "cavity_risk"
  | "gum_health"
  | "plaque_level"
  | "overall_oral_health";

export type IndicatorScore = {
  indicator: IndicatorName;
  score: number; // integer 0–100
  confidence: number; // float 0.0–1.0
};

export type ScoreReport = {
  analysisId: string;
  completedAt: string; // ISO 8601
  indicators: IndicatorScore[];
  imageMeta: ImageMeta;
};

export type AnalysisError = {
  code: "quality_error" | "model_error" | "unknown";
  message: string;
};

export type AnalysisResult = {
  request: AnalysisRequest;
  report: ScoreReport | null;
  error: AnalysisError | null;
};

// ─── Validation types ─────────────────────────────────────────────────────────

export type ValidationResult =
  | { ok: true }
  | { ok: false; httpStatus: 400 | 415 | 422; message: string };

export type QualityCheckResult =
  | { pass: true }
  | {
      pass: false;
      reason: "blurry" | "overexposed" | "underexposed";
      detail: string;
    };

// ─── AI model types ───────────────────────────────────────────────────────────

export type ModelOutput = {
  indicators: Array<{
    name: IndicatorName;
    score: number; // 0–100
    confidence: number; // 0.0–1.0
  }>;
};

// ─── Pagination ───────────────────────────────────────────────────────────────

export type Page<T> = {
  items: T[];
  nextCursor: string | null;
};

// ─── Uploaded file (as received by the API layer) ────────────────────────────

export type UploadedFile = {
  filename: string;
  mimeType: string;
  sizeBytes: number;
  bytes: Bytes;
};

// ─── Service interfaces ───────────────────────────────────────────────────────

export interface ValidationService {
  validateUpload(file: UploadedFile): ValidationResult;
}

export interface ImageQualityChecker {
  check(imageBytes: Bytes): QualityCheckResult;
}

export interface AnalysisQueue {
  enqueue(request: AnalysisRequest): void;
  dequeue(): AnalysisRequest | null;
  markComplete(analysisId: string, result: ScoreReport): void;
  markFailed(analysisId: string, error: AnalysisError): void;
}

export interface AIWorker {
  process(request: AnalysisRequest): Promise<ScoreReport>;
}

export interface AIModelClient {
  infer(imageBytes: Bytes): ModelOutput;
}

export interface StorageService {
  saveImage(
    userId: string,
    analysisId: string,
    imageBytes: Bytes,
    meta: ImageMeta
  ): void;
  getImage(analysisId: string): Bytes;
  saveResult(result: AnalysisResult): void;
  getResult(analysisId: string): AnalysisResult | null;
  listResults(
    userId: string,
    cursor: string | null,
    pageSize: number
  ): Page<AnalysisResult>;
}

export interface ScoreReportSerializer {
  serialize(report: ScoreReport): string; // → JSON string
  deserialize(json: string): ScoreReport; // throws ParseError on malformed input
}

// ─── API response shapes ──────────────────────────────────────────────────────

export type UploadResponse = {
  analysisId: string;
  status: "pending";
};

export type ProcessingResponse = {
  analysisId: string;
  status: "processing";
};

export type ResultResponse = ScoreReport;

export type HistoryResponse = {
  results: AnalysisResult[];
  nextCursor: string | null;
};

export type ErrorEnvelope = {
  error: {
    code: string;
    message: string;
  };
};
