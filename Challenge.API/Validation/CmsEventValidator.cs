using FluentValidation;
using Challenge.API.Models.Dto;

namespace Challenge.API.Validation
{
    /// <summary>
    /// Validator for individual CMS events.
    /// Ensures event data is valid and sanitized before processing.
    /// </summary>
    public class CmsEventValidator : AbstractValidator<CmsEventDto>
    {
        public CmsEventValidator()
        {
            // Validate type
            RuleFor(x => x.Type)
                .NotEmpty()
                .WithMessage("Type is required")
                .Must(x => new[] { "publish", "unpublish", "delete" }.Contains(x.ToLower()))
                .WithMessage("Type must be 'publish', 'unpublish', or 'delete'");

            // Validate ID
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("Id is required")
                .MaximumLength(255)
                .WithMessage("Id must not exceed 255 characters")
                .Matches(@"^[a-zA-Z0-9\-_\.]+$")
                .WithMessage("Id can only contain alphanumeric characters, hyphens, underscores, and dots");

            // Validate timestamp
            RuleFor(x => x.Timestamp)
                .NotEmpty()
                .WithMessage("Timestamp is required")
                .LessThanOrEqualTo(DateTime.UtcNow.AddSeconds(5))
                .WithMessage("Timestamp cannot be more than 5 seconds in the future");

            // Conditional validations based on event type
            When(x => x.Type.ToLower() == "publish" || x.Type.ToLower() == "unpublish", () =>
            {
                RuleFor(x => x.Version)
                    .NotNull()
                    .WithMessage("Version is required for publish/unpublish events")
                    .GreaterThan(0)
                    .WithMessage("Version must be greater than 0");

                RuleFor(x => x.Payload)
                    .NotNull()
                    .WithMessage("Payload is required for publish/unpublish events");
            });

            // Delete events should not have version/payload
            When(x => x.Type.ToLower() == "delete", () =>
            {
                RuleFor(x => x.Version)
                    .Null()
                    .WithMessage("Version should not be provided for delete events");
            });
        }
    }
}
