namespace MyStreamHistory.API.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Company
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int IgdbId { get; set; }

        [Required]
        public Guid Checksum { get; set; }

        public DateTime StartDate { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; }

        [Required]
        [StringLength(255)]
        public string Slug { get; set; }

        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

    }
}
