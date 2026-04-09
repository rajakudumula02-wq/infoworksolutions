// Integration tests for PreauthController — Claim/$submit and Claim/$inquire
using System.Security.Claims;
using HealthcareFhirApi.Api.Controllers;
using HealthcareFhirApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FhirClaim = Hl7.Fhir.Model.Claim;

namespace HealthcareFhirApi.UnitTests.Controllers;

public class PreauthControllerTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private readonly Mock<IFhirResourceRepository<FhirClaim>> _claimRepo = new();
    private readonly Mock<IFhirResourceRepository<ClaimResponse>> _claimResponseRepo = new();
    private readonly FhirValidationService _validator = new();
    private readonly Mock<IAuditService> _audit = new();

    private PreauthController CreateController()
    {
        var controller = new PreauthController(
            _claimRepo.Object,
            _claimResponseRepo.Object,
            _validator,
            _audit.Object);

        // Set up a fake authenticated user
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim("client_id", "test-client"),
            new System.Security.Claims.Claim("sub", "user-1"),
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    private static FhirClaim MakeValidPreauthClaim() => new()
    {
        Status = FinancialResourceStatusCodes.Active,
        Type = new CodeableConcept("http://terminology.hl7.org/CodeSystem/claim-type", "professional"),
        UseElement = new Code<ClaimUseCode>(ClaimUseCode.Preauthorization),
        Patient = new ResourceReference("Patient/123"),
        Created = "2026-04-02",
        Provider = new ResourceReference("Practitioner/456"),
        Priority = new CodeableConcept("http://terminology.hl7.org/CodeSystem/processpriority", "normal"),
        Insurance = new List<FhirClaim.InsuranceComponent>
        {
            new() { Sequence = 1, Focal = true, Coverage = new ResourceReference("Coverage/789") }
        }
    };

    private static Bundle MakePreauthBundle(FhirClaim? claim = null)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Collection };
        bundle.Entry.Add(new Bundle.EntryComponent { Resource = claim ?? MakeValidPreauthClaim() });
        return bundle;
    }

    // ─── $submit: Happy path ──────────────────────────────────────────────────

    [Fact]
    public async SystemTask Submit_ValidPreauthClaim_ReturnsClaimResponseWithQueuedOutcome()
    {
        var claim = MakeValidPreauthClaim();
        claim.Id = "claim-001";

        _claimRepo
            .Setup(r => r.CreateAsync(It.IsAny<FhirClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        var expectedResponse = new ClaimResponse
        {
            Id = "resp-001",
            Status = FinancialResourceStatusCodes.Active,
            Outcome = ClaimProcessingCodes.Queued
        };

        _claimResponseRepo
            .Setup(r => r.CreateAsync(It.IsAny<ClaimResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ClaimResponse>(okResult.Value);
        Assert.Equal(ClaimProcessingCodes.Queued, response.Outcome);
    }

    [Fact]
    public async SystemTask Submit_ValidClaim_PersistsClaimViaRepository()
    {
        var claim = MakeValidPreauthClaim();
        claim.Id = "claim-002";

        _claimRepo
            .Setup(r => r.CreateAsync(It.IsAny<FhirClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        _claimResponseRepo
            .Setup(r => r.CreateAsync(It.IsAny<ClaimResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaimResponse());

        var controller = CreateController();
        await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        _claimRepo.Verify(r => r.CreateAsync(It.IsAny<FhirClaim>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async SystemTask Submit_ValidClaim_RecordsAuditEntry()
    {
        var claim = MakeValidPreauthClaim();
        claim.Id = "claim-003";

        _claimRepo
            .Setup(r => r.CreateAsync(It.IsAny<FhirClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        _claimResponseRepo
            .Setup(r => r.CreateAsync(It.IsAny<ClaimResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaimResponse());

        var controller = CreateController();
        await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        _audit.Verify(a => a.RecordAsync(
            It.Is<AuditContext>(ctx =>
                ctx.ResourceType == "Claim" &&
                ctx.Action == "create"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── $submit: Error cases ─────────────────────────────────────────────────

    [Fact]
    public async SystemTask Submit_EmptyBundle_Returns422()
    {
        var emptyBundle = new Bundle { Type = Bundle.BundleType.Collection };

        var controller = CreateController();
        var result = await controller.Submit(emptyBundle, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimWithUseClaim_NotPreauth_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.UseElement = new Code<ClaimUseCode>(ClaimUseCode.Claim); // not preauthorization

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingPatient_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Patient = null; // mandatory field missing

        _claimRepo
            .Setup(r => r.CreateAsync(It.IsAny<FhirClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingInsurance_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Insurance = new List<FhirClaim.InsuranceComponent>(); // empty

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingProvider_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Provider = null;

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingStatus_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Status = null;

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingType_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Type = null;

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Submit_ClaimMissingPriority_Returns422()
    {
        var claim = MakeValidPreauthClaim();
        claim.Priority = null;

        var controller = CreateController();
        var result = await controller.Submit(MakePreauthBundle(claim), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    // ─── $inquire: Happy path ─────────────────────────────────────────────────

    [Fact]
    public async SystemTask Inquire_ValidClaimId_ReturnsClaimResponse()
    {
        var expectedResponse = new ClaimResponse
        {
            Id = "resp-001",
            Outcome = ClaimProcessingCodes.Queued
        };

        var dummyParams = new SearchParameters(new Dictionary<string, string?>(), 0, 1, null, false, Array.Empty<string>(), Array.Empty<string>());

        _claimResponseRepo
            .Setup(r => r.SearchAsync(It.IsAny<SearchParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ClaimResponse>(
                new List<ClaimResponse> { expectedResponse }, 1, dummyParams));

        var parameters = new Parameters();
        parameters.Add("claim-id", new FhirString("claim-001"));

        var controller = CreateController();
        var result = await controller.Inquire(parameters, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ClaimResponse>(okResult.Value);
        Assert.Equal("resp-001", response.Id);
    }

    [Fact]
    public async SystemTask Inquire_SearchesWithCorrectClaimReference()
    {
        var dummyParams = new SearchParameters(new Dictionary<string, string?>(), 0, 1, null, false, Array.Empty<string>(), Array.Empty<string>());

        _claimResponseRepo
            .Setup(r => r.SearchAsync(It.IsAny<SearchParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ClaimResponse>(
                new List<ClaimResponse> { new ClaimResponse() }, 1, dummyParams));

        var parameters = new Parameters();
        parameters.Add("claim-id", new FhirString("claim-xyz"));

        var controller = CreateController();
        await controller.Inquire(parameters, CancellationToken.None);

        _claimResponseRepo.Verify(r => r.SearchAsync(
            It.Is<SearchParameters>(sp =>
                sp.Filters.ContainsKey("request") &&
                sp.Filters["request"] == "Claim/claim-xyz"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── $inquire: Error cases ────────────────────────────────────────────────

    [Fact]
    public async SystemTask Inquire_MissingClaimId_Returns422()
    {
        var parameters = new Parameters(); // no claim-id parameter

        var controller = CreateController();
        var result = await controller.Inquire(parameters, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Inquire_EmptyClaimId_Returns422()
    {
        var parameters = new Parameters();
        parameters.Add("claim-id", new FhirString(""));

        var controller = CreateController();
        var result = await controller.Inquire(parameters, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, statusResult.StatusCode);
    }

    [Fact]
    public async SystemTask Inquire_NonExistentClaimId_Returns404()
    {
        var dummyParams = new SearchParameters(new Dictionary<string, string?>(), 0, 1, null, false, Array.Empty<string>(), Array.Empty<string>());

        _claimResponseRepo
            .Setup(r => r.SearchAsync(It.IsAny<SearchParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ClaimResponse>(
                new List<ClaimResponse>(), 0, dummyParams));

        var parameters = new Parameters();
        parameters.Add("claim-id", new FhirString("nonexistent-id"));

        var controller = CreateController();
        var result = await controller.Inquire(parameters, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, statusResult.StatusCode);
    }
}
