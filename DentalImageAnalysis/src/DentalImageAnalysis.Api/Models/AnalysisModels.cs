namespace DentalImageAnalysis.Api.Models;

public class AnalysisRequest
{
    public Guid AnalysisId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, processing, complete, failed
    public ImageMeta ImageMeta { get; set; } = new();
    public DateTimeOffset UploadTimestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class ImageMeta
{
    public string Filename { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Position { get; set; } = string.Empty; // front, left, right, top
}

public class ScoreReport
{
    public Guid AnalysisId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public List<IndicatorScore> Indicators { get; set; } = new();
    public ImageMeta ImageMeta { get; set; } = new();
    public string OverallRating { get; set; } = string.Empty; // green, yellow, red
}

public class IndicatorScore
{
    public string Indicator { get; set; } = string.Empty;
    public int Score { get; set; }
    public double Confidence { get; set; }
}

public class SmartScanSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public List<SmartScanPhoto> Photos { get; set; } = new();
    public ScoreReport? Result { get; set; }
    public string Status { get; set; } = "in_progress";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SmartScanPhoto
{
    public string Position { get; set; } = string.Empty; // front, left, right, top
    public string Filename { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UploadResponse
{
    public Guid AnalysisId { get; set; }
    public string Status { get; set; } = "pending";
}
