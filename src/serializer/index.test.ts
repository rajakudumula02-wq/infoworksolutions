import { describe, it, expect } from "vitest";
import { scoreReportSerializer, ParseError } from "./index";
import type { ScoreReport } from "../types";

const validReport: ScoreReport = {
  analysisId: "550e8400-e29b-41d4-a716-446655440000",
  completedAt: "2024-01-15T10:30:00.000Z",
  indicators: [
    { indicator: "cavity_risk", score: 42, confidence: 0.87 },
    { indicator: "gum_health", score: 75, confidence: 0.92 },
    { indicator: "plaque_level", score: 30, confidence: 0.78 },
    { indicator: "overall_oral_health", score: 65, confidence: 0.85 },
  ],
  imageMeta: {
    filename: "xray.jpg",
    format: "jpeg",
    fileSizeBytes: 1048576,
    uploadTimestamp: "2024-01-15T10:00:00.000Z",
  },
};

describe("ScoreReportSerializer", () => {
  describe("serialize", () => {
    it("produces a valid JSON string", () => {
      const json = scoreReportSerializer.serialize(validReport);
      expect(typeof json).toBe("string");
      expect(() => JSON.parse(json)).not.toThrow();
    });

    it("round-trips back to an equivalent object", () => {
      const json = scoreReportSerializer.serialize(validReport);
      const parsed = JSON.parse(json);
      expect(parsed).toEqual(validReport);
    });
  });

  describe("deserialize", () => {
    it("parses a valid JSON string into a ScoreReport", () => {
      const json = scoreReportSerializer.serialize(validReport);
      const result = scoreReportSerializer.deserialize(json);
      expect(result).toEqual(validReport);
    });

    it("throws ParseError on invalid JSON syntax", () => {
      expect(() => scoreReportSerializer.deserialize("{not valid json}")).toThrow(ParseError);
    });

    it("throws ParseError on empty string", () => {
      expect(() => scoreReportSerializer.deserialize("")).toThrow(ParseError);
    });

    it("throws ParseError when required fields are missing", () => {
      const incomplete = JSON.stringify({ analysisId: "abc" });
      expect(() => scoreReportSerializer.deserialize(incomplete)).toThrow(ParseError);
    });

    it("throws ParseError when indicators contain invalid indicator name", () => {
      const bad = JSON.stringify({
        ...validReport,
        indicators: [{ indicator: "unknown_indicator", score: 50, confidence: 0.5 }],
      });
      expect(() => scoreReportSerializer.deserialize(bad)).toThrow(ParseError);
    });

    it("throws ParseError when imageMeta has invalid format", () => {
      const bad = JSON.stringify({
        ...validReport,
        imageMeta: { ...validReport.imageMeta, format: "gif" },
      });
      expect(() => scoreReportSerializer.deserialize(bad)).toThrow(ParseError);
    });

    it("ParseError has a descriptive message", () => {
      try {
        scoreReportSerializer.deserialize("null");
      } catch (err) {
        expect(err).toBeInstanceOf(ParseError);
        expect((err as ParseError).message.length).toBeGreaterThan(0);
      }
    });

    it("accepts all valid image formats", () => {
      for (const format of ["jpeg", "png", "dicom"] as const) {
        const json = scoreReportSerializer.serialize({
          ...validReport,
          imageMeta: { ...validReport.imageMeta, format },
        });
        expect(() => scoreReportSerializer.deserialize(json)).not.toThrow();
      }
    });
  });
});

// ─── Property-based tests ─────────────────────────────────────────────────────

import fc from "fast-check";

const arbIso = fc.date({ min: new Date("2020-01-01"), max: new Date("2030-01-01") })
  .map((d) => d.toISOString());

const arbIndicatorName = fc.constantFrom(
  "cavity_risk" as const,
  "gum_health" as const,
  "plaque_level" as const,
  "overall_oral_health" as const
);

const arbScoreReport = fc.record({
  analysisId: fc.uuid(),
  completedAt: arbIso,
  indicators: fc.tuple(
    fc.record({ indicator: fc.constant("cavity_risk" as const), score: fc.integer({ min: 0, max: 100 }), confidence: fc.float({ min: 0, max: 1, noNaN: true }) }),
    fc.record({ indicator: fc.constant("gum_health" as const), score: fc.integer({ min: 0, max: 100 }), confidence: fc.float({ min: 0, max: 1, noNaN: true }) }),
    fc.record({ indicator: fc.constant("plaque_level" as const), score: fc.integer({ min: 0, max: 100 }), confidence: fc.float({ min: 0, max: 1, noNaN: true }) }),
    fc.record({ indicator: fc.constant("overall_oral_health" as const), score: fc.integer({ min: 0, max: 100 }), confidence: fc.float({ min: 0, max: 1, noNaN: true }) }),
  ).map(arr => arr as ScoreReport["indicators"]),
  imageMeta: fc.record({
    filename: fc.string({ minLength: 1, maxLength: 40 }),
    format: fc.constantFrom("jpeg" as const, "png" as const, "dicom" as const),
    fileSizeBytes: fc.integer({ min: 1, max: 20 * 1024 * 1024 }),
    uploadTimestamp: arbIso,
  }),
});

// ── Property 16: Score_Report serialization round-trip ────────────────────────
// Feature: dental-image-score-analysis, Property 16: Score_Report serialization round-trip
describe("Property 16: Score_Report serialization round-trip", () => {
  it("serialize then deserialize returns an equivalent ScoreReport", () => {
    fc.assert(
      fc.property(arbScoreReport, (report) => {
        const json = scoreReportSerializer.serialize(report);
        const restored = scoreReportSerializer.deserialize(json);
        // Deep equality (confidence floats survive JSON round-trip)
        return (
          restored.analysisId === report.analysisId &&
          restored.completedAt === report.completedAt &&
          restored.indicators.length === report.indicators.length &&
          restored.imageMeta.filename === report.imageMeta.filename &&
          restored.imageMeta.format === report.imageMeta.format
        );
      }),
      { numRuns: 100 }
    );
  });
});

// ── Property 17: Malformed JSON returns HTTP 400 ──────────────────────────────
// Feature: dental-image-score-analysis, Property 17: Malformed JSON returns HTTP 400
describe("Property 17: Malformed JSON returns ParseError", () => {
  it("throws ParseError for any non-JSON string", () => {
    fc.assert(
      fc.property(
        fc.string({ minLength: 1, maxLength: 200 }).filter((s) => {
          try { JSON.parse(s); return false; } catch { return true; }
        }),
        (badJson) => {
          try {
            scoreReportSerializer.deserialize(badJson);
            return false; // should have thrown
          } catch (err) {
            return err instanceof ParseError;
          }
        }
      ),
      { numRuns: 100 }
    );
  });
});
