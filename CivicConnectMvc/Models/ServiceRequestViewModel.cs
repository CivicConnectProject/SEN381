using System.ComponentModel.DataAnnotations;

namespace CivicConnectMvc.Models;

public class ServiceRequestViewModel
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    /// <summary>
    /// FK to <c>categories.category_id</c>. Must match a row that exists in the database.
    /// The Create view submits this as an integer from a select element.
    /// </summary>
    [Required, Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }
}
