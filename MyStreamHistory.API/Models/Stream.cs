namespace MyStreamHistory.API.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Stream
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public int StreamId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public User User { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public bool Finished { get; set; }

        public bool WithInfoAboutViewers { get; set; }

        public List<TwitchCategoryOnStream> CategoriesOnStream { get; set; } = new();


    }
}
