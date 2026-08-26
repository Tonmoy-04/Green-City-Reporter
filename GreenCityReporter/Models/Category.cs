using System.ComponentModel.DataAnnotations;

namespace GreenCityReporter.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Navigation Property
        public ICollection<Report> Reports { get; set; } = new List<Report>();
    }
}
