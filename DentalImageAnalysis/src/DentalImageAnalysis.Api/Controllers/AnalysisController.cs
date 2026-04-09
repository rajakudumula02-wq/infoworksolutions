using DentalImageAnalysis.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace DentalImageAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, ScoreReport> _results = new();
    private static readonly ConcurrentDictionary<Guid, SmartScanSession> _sessions = new();

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile image, [FromForm] string? position, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "No image provided" });
        if (image.Length > 20 * 1024 * 1024)
            return BadRequest(new { error = "File exceeds 20MB limit" });

        var allowed = new[] { "image/jpeg", "image/png", "application/dicom" };
        if (!allowed.Contains(image.ContentType))
            return StatusCode(415, new { error = $"Unsupported format: {image.ContentType}" });

        var analysisId = Guid.NewGuid();

        // Simulate AI analysis
        var report = GenerateScoreReport(analysisId, image.FileName, image.ContentType, image.Length, position ?? "front");
        _results[analysisId] = report;

        return Ok(new { analysisId, status = "complete", report });
    }

    [HttpPost("smart-scan/start")]
    public IActionResult StartSession([FromBody] StartSessionRequest? req)
    {
        var session = new SmartScanSession { UserId = req?.UserId ?? "anonymous" };
        _sessions[session.SessionId] = session;
        return Ok(new { session.SessionId, status = "in_progress" });
    }

    [HttpPost("smart-scan/{sessionId}/photo")]
    public async Task<IActionResult> AddPhoto(Guid sessionId, IFormFile image, [FromForm] string position, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return NotFound(new { error = "Session not found" });

        if (image is null || image.Length == 0)
            return BadRequest(new { error = "No image provided" });

        var validPositions = new[] { "front", "left", "right", "top" };
        if (!validPositions.Contains(position?.ToLower()))
            return BadRequest(new { error = "Position must be: front, left, right, or top" });

        session.Photos.Add(new SmartScanPhoto
        {
            Position = position.ToLower(),
            Filename = image.FileName,
            SizeBytes = image.Length,
        });

        return Ok(new { sessionId, position, photosCount = session.Photos.Count, remaining = 4 - session.Photos.Count });
    }

    [HttpPost("smart-scan/{sessionId}/analyze")]
    public IActionResult Analyze(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return NotFound(new { error = "Session not found" });

        if (session.Photos.Count < 4)
            return BadRequest(new { error = $"Need 4 photos, have {session.Photos.Count}. Missing positions.", photosCount = session.Photos.Count });

        // Generate combined score from all 4 photos
        var report = GenerateCombinedReport(sessionId, session.Photos);
        session.Result = report;
        session.Status = "complete";

        return Ok(new { sessionId, status = "complete", report });
    }

    [HttpGet("smart-scan/{sessionId}")]
    public IActionResult GetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return NotFound(new { error = "Session not found" });
        return Ok(session);
    }

    [HttpGet("{analysisId}")]
    public IActionResult GetResult(Guid analysisId)
    {
        if (!_results.TryGetValue(analysisId, out var report))
            return NotFound(new { error = "Analysis not found" });
        return Ok(report);
    }

    private static ScoreReport GenerateScoreReport(Guid id, string filename, string contentType, long size, string position)
    {
        var rng = new Random(id.GetHashCode());
        var scores = new List<IndicatorScore>
        {
            new() { Indicator = "cavity_risk", Score = rng.Next(10, 70), Confidence = Math.Round(0.7 + rng.NextDouble() * 0.25, 2) },
            new() { Indicator = "gum_health", Score = rng.Next(50, 95), Confidence = Math.Round(0.75 + rng.NextDouble() * 0.2, 2) },
            new() { Indicator = "plaque_level", Score = rng.Next(15, 60), Confidence = Math.Round(0.7 + rng.NextDouble() * 0.25, 2) },
            new() { Indicator = "overall_oral_health", Score = rng.Next(55, 90), Confidence = Math.Round(0.8 + rng.NextDouble() * 0.15, 2) },
        };
        var overall = scores.First(s => s.Indicator == "overall_oral_health").Score;
        return new ScoreReport
        {
            AnalysisId = id,
            CompletedAt = DateTimeOffset.UtcNow,
            Indicators = scores,
            ImageMeta = new ImageMeta { Filename = filename, Format = contentType, FileSizeBytes = size, Position = position },
            OverallRating = overall >= 70 ? "green" : overall >= 40 ? "yellow" : "red",
        };
    }

    private static ScoreReport GenerateCombinedReport(Guid sessionId, List<SmartScanPhoto> photos)
    {
        var rng = new Random(sessionId.GetHashCode());
        var scores = new List<IndicatorScore>
        {
            new() { Indicator = "cavity_risk", Score = rng.Next(10, 65), Confidence = Math.Round(0.82 + rng.NextDouble() * 0.15, 2) },
            new() { Indicator = "gum_health", Score = rng.Next(55, 95), Confidence = Math.Round(0.85 + rng.NextDouble() * 0.12, 2) },
            new() { Indicator = "plaque_level", Score = rng.Next(10, 55), Confidence = Math.Round(0.8 + rng.NextDouble() * 0.15, 2) },
            new() { Indicator = "overall_oral_health", Score = rng.Next(60, 92), Confidence = Math.Round(0.88 + rng.NextDouble() * 0.1, 2) },
        };
        var overall = scores.First(s => s.Indicator == "overall_oral_health").Score;
        return new ScoreReport
        {
            AnalysisId = sessionId,
            CompletedAt = DateTimeOffset.UtcNow,
            Indicators = scores,
            OverallRating = overall >= 70 ? "green" : overall >= 40 ? "yellow" : "red",
        };
    }
}

public record StartSessionRequest(string? UserId);
