namespace MyStreamHistory.API.Models
{
    using MyStreamHistory.API.Enums;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Streamer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public int TwitchId { get; set; }
        
        [Required]
        public string ChannelName { get; set; }

        public ChannelStatusEnum BroadcasterType { get; set; }

        [MaxLength(200)]
        public string? LogoUser { get; set; }

        [MaxLength(300)]
        public string? AccessToken { get; set; }

        [MaxLength(300)]
        public string? RefreshToken { get; set; }

        public bool? FreshToken { get; set; }
    }
}
