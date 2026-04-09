using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Services;
using Moq;
using Xunit;

namespace MemberSmsCampaign.UnitTests.Services;

public class CampaignServiceTests
{
    private readonly Mock<ICampaignRepository> _repoMock;
    private readonly Mock<IAuditRepository> _auditMock;
    private readonly Mock<ITargetingService> _targetingMock;
    private readonly Mock<IEligibilityService> _eligibilityMock;
    private readonly Mock<IMemberRepository> _memberRepoMock;
    private readonly Mock<ISmsProviderClient> _smsMock;
    private readonly CampaignService _sut;

    public CampaignServiceTests()
    {
        _repoMock = new Mock<ICampaignRepository>();
        _auditMock = new Mock<IAuditRepository>();
        _targetingMock = new Mock<ITargetingService>();
        _eligibilityMock = new Mock<IEligibilityService>();
        _memberRepoMock = new Mock<IMemberRepository>();
        _smsMock = new Mock<ISmsProviderClient>();
        _repoMock
            .Setup(r => r.CreateAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Campaign c, CancellationToken _) => c);
        _repoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Campaign c, CancellationToken _) => c);

        _sut = new CampaignService(_repoMock.Object, _auditMock.Object, _targetingMock.Object, _eligibilityMock.Object, _memberRepoMock.Object, _smsMock.Object);
    }

    // --- Create Campaign Tests ---

    [Fact]
    public async Task CreateCampaign_WithValidInput_ReturnsDraftStatus()
    {
        var result = await _sut.CreateCampaignAsync("Test", CampaignType.Welcome, "Hello!");

        Assert.Equal(CampaignStatus.Draft, result.Status);
        Assert.Equal("Test", result.Name);
        Assert.Equal(CampaignType.Welcome, result.Type);
        Assert.Equal("Hello!", result.MessageTemplate);
    }

    [Fact]
    public async Task CreateCampaign_WithEmptyName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateCampaignAsync("", CampaignType.Welcome, "Hello!"));
    }

    [Fact]
    public async Task CreateCampaign_WithNullName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateCampaignAsync(null!, CampaignType.Welcome, "Hello!"));
    }

    [Fact]
    public async Task CreateCampaign_WithInvalidType_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateCampaignAsync("Test", (CampaignType)999, "Hello!"));
    }

    [Fact]
    public async Task CreateCampaign_WithEmptyMessageTemplate_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateCampaignAsync("Test", CampaignType.Welcome, ""));
    }

    [Fact]
    public async Task CreateCampaign_WithMessageOver160Chars_ThrowsArgumentException()
    {
        var longMessage = new string('A', 161);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateCampaignAsync("Test", CampaignType.Welcome, longMessage));
    }

    [Fact]
    public async Task CreateCampaign_WithMessageAt160Chars_Succeeds()
    {
        var message = new string('A', 160);
        var result = await _sut.CreateCampaignAsync("Test", CampaignType.Welcome, message);

        Assert.Equal(CampaignStatus.Draft, result.Status);
        Assert.Equal(160, result.MessageTemplate.Length);
    }

    [Theory]
    [InlineData(CampaignType.Welcome)]
    [InlineData(CampaignType.Referral)]
    [InlineData(CampaignType.Utilization)]
    [InlineData(CampaignType.Holiday)]
    public async Task CreateCampaign_WithAllValidTypes_Succeeds(CampaignType type)
    {
        var result = await _sut.CreateCampaignAsync("Test", type, "Hello!");
        Assert.Equal(type, result.Type);
        Assert.Equal(CampaignStatus.Draft, result.Status);
    }

    // --- Schedule Campaign Tests ---

    [Fact]
    public async Task ScheduleCampaign_DraftWithFutureDate_SetsScheduledStatus()
    {
        var campaign = CreateCampaign(CampaignStatus.Draft);
        SetupGetById(campaign);

        var futureDate = DateTimeOffset.UtcNow.AddDays(7);
        var result = await _sut.ScheduleCampaignAsync(campaign.Id, futureDate);

        Assert.Equal(CampaignStatus.Scheduled, result.Status);
        Assert.Equal(futureDate, result.ScheduledAt);
    }

    [Fact]
    public async Task ScheduleCampaign_WithPastDate_ThrowsArgumentException()
    {
        var pastDate = DateTimeOffset.UtcNow.AddDays(-1);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ScheduleCampaignAsync(Guid.NewGuid(), pastDate));
    }

    [Theory]
    [InlineData(CampaignStatus.Running)]
    [InlineData(CampaignStatus.Completed)]
    [InlineData(CampaignStatus.Cancelled)]
    public async Task ScheduleCampaign_WithNonSchedulableStatus_ThrowsInvalidOperationException(CampaignStatus status)
    {
        var campaign = CreateCampaign(status);
        SetupGetById(campaign);

        var futureDate = DateTimeOffset.UtcNow.AddDays(7);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleCampaignAsync(campaign.Id, futureDate));
    }

    [Fact]
    public async Task ScheduleCampaign_ScheduledCampaignWithNewDate_UpdatesSchedule()
    {
        var campaign = CreateCampaign(CampaignStatus.Scheduled);
        campaign.ScheduledAt = DateTimeOffset.UtcNow.AddDays(3);
        SetupGetById(campaign);

        var newDate = DateTimeOffset.UtcNow.AddDays(14);
        var result = await _sut.ScheduleCampaignAsync(campaign.Id, newDate);

        Assert.Equal(CampaignStatus.Scheduled, result.Status);
        Assert.Equal(newDate, result.ScheduledAt);
    }

    // --- Holiday Campaign Scheduling Tests ---

    [Fact]
    public async Task ScheduleHolidayCampaign_InNovember_Succeeds()
    {
        var campaign = CreateCampaign(CampaignStatus.Draft, CampaignType.Holiday);
        SetupGetById(campaign);

        var novDate = new DateTimeOffset(DateTimeOffset.UtcNow.Year + 1, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var result = await _sut.ScheduleCampaignAsync(campaign.Id, novDate);

        Assert.Equal(CampaignStatus.Scheduled, result.Status);
    }

    [Fact]
    public async Task ScheduleHolidayCampaign_InDecember_Succeeds()
    {
        var campaign = CreateCampaign(CampaignStatus.Draft, CampaignType.Holiday);
        SetupGetById(campaign);

        var decDate = new DateTimeOffset(DateTimeOffset.UtcNow.Year + 1, 12, 25, 10, 0, 0, TimeSpan.Zero);
        var result = await _sut.ScheduleCampaignAsync(campaign.Id, decDate);

        Assert.Equal(CampaignStatus.Scheduled, result.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    public async Task ScheduleHolidayCampaign_OutsideNovDec_ThrowsArgumentException(int month)
    {
        var campaign = CreateCampaign(CampaignStatus.Draft, CampaignType.Holiday);
        SetupGetById(campaign);

        var date = new DateTimeOffset(DateTimeOffset.UtcNow.Year + 1, month, 15, 10, 0, 0, TimeSpan.Zero);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ScheduleCampaignAsync(campaign.Id, date));
    }

    // --- Cancel Campaign Tests ---

    [Fact]
    public async Task CancelCampaign_DraftCampaign_SetsCancelledStatus()
    {
        var campaign = CreateCampaign(CampaignStatus.Draft);
        SetupGetById(campaign);

        var result = await _sut.CancelCampaignAsync(campaign.Id);

        Assert.Equal(CampaignStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task CancelCampaign_ScheduledCampaign_SetsCancelledStatus()
    {
        var campaign = CreateCampaign(CampaignStatus.Scheduled);
        SetupGetById(campaign);

        var result = await _sut.CancelCampaignAsync(campaign.Id);

        Assert.Equal(CampaignStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task CancelCampaign_RunningCampaign_ThrowsInvalidOperationException()
    {
        var campaign = CreateCampaign(CampaignStatus.Running);
        SetupGetById(campaign);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelCampaignAsync(campaign.Id));
    }

    [Fact]
    public async Task CancelCampaign_CompletedCampaign_ThrowsInvalidOperationException()
    {
        var campaign = CreateCampaign(CampaignStatus.Completed);
        SetupGetById(campaign);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelCampaignAsync(campaign.Id));
    }

    [Fact]
    public async Task CancelCampaign_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Campaign?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.CancelCampaignAsync(Guid.NewGuid()));
    }

    // --- Helpers ---

    private static Campaign CreateCampaign(CampaignStatus status, CampaignType type = CampaignType.Welcome)
    {
        return new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Test Campaign",
            Type = type,
            MessageTemplate = "Hello!",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupGetById(Campaign campaign)
    {
        _repoMock.Setup(r => r.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);
    }
}
