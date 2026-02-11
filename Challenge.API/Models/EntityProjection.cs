namespace Challenge.API.Models
{
    public class EntityProjection
    {
        public string Id { get; set; } = string.Empty;
        public int CurrentPublishedVersion { get; set; }
        public bool IsPublished { get; set; }
        public bool IsDisabledByAdmin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<EntityVersionProjection> Versions { get; set; } = new List<EntityVersionProjection>();
    }
}
