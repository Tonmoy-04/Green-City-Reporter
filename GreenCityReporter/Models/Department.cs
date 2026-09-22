using System.ComponentModel.DataAnnotations;

namespace GreenCityReporter.Models
{
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Report> Reports { get; set; } = new List<Report>();
        public ICollection<Category> DefaultCategories { get; set; } = new List<Category>();
    }
}