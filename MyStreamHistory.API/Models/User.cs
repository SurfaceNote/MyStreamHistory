namespace MyStreamHistory.API.Models
{
    using MyStreamHistory.API.Enums;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Text.Json.Serialization;

    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public int TwitchId { get; set; }

        public string? Email { get; set; }

        [Required]
        public string Login { get; set; }
        
        [Required]
        public string DisplayName { get; set; }

        public ChannelStatusEnum BroadcasterType { get; set; }

        [MaxLength(200)]
        public string? LogoUser { get; set; }

        public bool? IsStreamer { get; set; }

        [MaxLength(300)]
        public string? AccessToken { get; set; }

        [MaxLength(300)]
        public string? RefreshToken { get; set; }

        public bool? FreshToken { get; set; }

        public DateTime TwitchCreatedAt { get; set; }
        public DateTime SiteCreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }
}
