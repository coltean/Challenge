using FluentValidation;
using Challenge.API.Models.Dto;

namespace Challenge.API.Validation
{
    /// <summary>
    /// Validator for batch of CMS events.
    /// Ensures batch size is reasonable and all events are valid.
    /// </summary>
    public class BatchEventValidator : AbstractValidator<List<CmsEventDto>>
    {
        public BatchEventValidator()
        {
            RuleFor(x => x)
                .NotEmpty()
                .WithMessage("Events batch cannot be empty");

            RuleFor(x => x)
                .Must(x => x.Count <= 1000)
                .WithMessage("Batch size cannot exceed 1000 events");

            // Validate each event in the batch
            RuleForEach(x => x).SetValidator(new CmsEventValidator());
        }
    }
}
