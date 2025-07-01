namespace MyStreamHistory.API.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class TwitchCategoryOnStream
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public int TwitchCategoryId { get; set; }

        [Required]
        public TwitchCategory Category { get; set; }

        [Required]
        public int StreamId { get; set; }
        [Required]
        public Stream Stream { get; set; }

        public int Duration { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<ViewerPerTwitchCategoryOnStream> Viewers { get; set; } = new();
    }
}
