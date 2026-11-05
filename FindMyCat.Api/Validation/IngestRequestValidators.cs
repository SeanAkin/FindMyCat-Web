using System.Text.Json;
using FindMyCat.Api.Contracts;
using FluentValidation;

namespace FindMyCat.Api.Validation;

public sealed class IngestStatusRequestValidator : AbstractValidator<IngestStatusRequest>
{
    public IngestStatusRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Payload)
            .Must(payload => payload.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            .WithMessage("'payload' is required.");
    }
}

public sealed class IngestWifiScanRequestValidator : AbstractValidator<IngestWifiScanRequest>
{
    public IngestWifiScanRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Scan).NotEmpty();
    }
}

public sealed class IngestGnssFixRequestValidator : AbstractValidator<IngestGnssFixRequest>
{
    public IngestGnssFixRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Fix).NotEmpty();
    }
}

public sealed class IngestCellScanRequestValidator : AbstractValidator<IngestCellScanRequest>
{
    public IngestCellScanRequestValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Scan).NotEmpty();
    }
}
