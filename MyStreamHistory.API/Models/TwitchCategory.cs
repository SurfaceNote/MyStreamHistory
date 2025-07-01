namespace MyStreamHistory.API.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class TwitchCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TwitchId { get; set; }

        [Required]
        public string Name { get; set; }

        public string? BoxArtUrl { get; set; }

        public List<TwitchCategoryOnStream> CategoriesOnStream { get; set; }

    }
}
