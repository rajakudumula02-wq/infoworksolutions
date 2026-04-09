import type { ScoreReport, ScoreReportSerializer } from "../types";

// ─── ParseError ───────────────────────────────────────────────────────────────

export class ParseError extends Error {
  constructor(message: string) {
    super(`ParseError: ${message}`);
    this.name = "ParseError";
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function isValidScoreReport(value: unknown): value is ScoreReport {
  if (typeof value !== "object" || value === null) return false;
  const obj = value as Record<string, unknown>;

  if (typeof obj.analysisId !== "string") return false;
  if (typeof obj.completedAt !== "string") return false;
  if (!Array.isArray(obj.indicators)) return false;

  for (const ind of obj.indicators) {
    if (typeof ind !== "object" || ind === null) return false;
    const i = ind as Record<string, unknown>;
    if (
      !["cavity_risk", "gum_health", "plaque_level", "overall_oral_health"].includes(
        i.indicator as string
      )
    )
      return false;
    if (typeof i.score !== "number") return false;
    if (typeof i.confidence !== "number") return false;
  }

  if (typeof obj.imageMeta !== "object" || obj.imageMeta === null) return false;
  const meta = obj.imageMeta as Record<string, unknown>;
  if (typeof meta.filename !== "string") return false;
  if (!["jpeg", "png", "dicom"].includes(meta.format as string)) return false;
  if (typeof meta.fileSizeBytes !== "number") return false;
  if (typeof meta.uploadTimestamp !== "string") return false;

  return true;
}

// ─── Implementation ───────────────────────────────────────────────────────────

export const scoreReportSerializer: ScoreReportSerializer = {
  serialize(report: ScoreReport): string {
    return JSON.stringify(report);
  },

  deserialize(json: string): ScoreReport {
    let parsed: unknown;
    try {
      parsed = JSON.parse(json);
    } catch (err) {
      throw new ParseError(
        `Invalid JSON: ${err instanceof Error ? err.message : String(err)}`
      );
    }

    if (!isValidScoreReport(parsed)) {
      throw new ParseError(
        "JSON does not conform to the ScoreReport schema"
      );
    }

    return parsed;
  },
};
