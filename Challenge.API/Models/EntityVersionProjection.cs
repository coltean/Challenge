namespace Challenge.API.Models
{
    public class EntityVersionProjection
    {
        public int Id { get; set; }
        public string EntityId { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string Payload { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime? UnpublishedAt { get; set; }

        public EntityProjection Entity { get; set; } = null!;
    }
}
