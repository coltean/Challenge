using Challenge.API.Models.Dto;

namespace Challenge.API.Services
{
    public interface IEventQueueService
    {
        Task EnqueueEventsAsync(List<CmsEventDto> events);
    }
}
