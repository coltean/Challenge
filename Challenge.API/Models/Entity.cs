namespace Challenge.API.Models
{
    public class Entity
    {
        public string Id { get; set; } = string.Empty;
        public int CurrentPublishedVersion { get; set; } = 0;
        public bool IsPublished { get; set; }
        public bool IsDisabledByAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<EntityVersion> Versions { get; set; } = new List<EntityVersion>();
    }
}
