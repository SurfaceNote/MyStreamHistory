namespace MyStreamHistory.API.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class ViewerPerTwitchCategoryOnStream
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; private set; }

        [Required]
        public int TwitchCategoryOnStreamId { get; set; }

        [Required]
        public TwitchCategoryOnStream TwitchCategoryOnStream { get; set; }

        [Required]
        public int ViewerId { get; set; }

        [Required]
        public Viewer Viewer { get; set; }

        public int WatchedMinutes { get; set; }
        public decimal EarnedMsgPoints { get; set; }
    }
}
