namespace MyStreamHistory.API.Models
{
    using MyStreamHistory.API.Enums;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Viewer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public string Login { get; set; }

        [Required]
        public string DisplayName { get; set; }

        [Required]
        public int TwitchId { get; set; }

        [MaxLength(200)]
        public string? LogoUser { get; set; }

        public ChannelStatusEnum BroadcasterType { get; set; }

        public bool? IsBot { get; set; }

        public List<ViewerPerTwitchCategoryOnStream> ViewerPerTwitchCategoriesOnStream { get; set; }
    }
}
