using System.ComponentModel.DataAnnotations;

namespace CivicConnectMvc.Models;

public class ServiceRequestViewModel
{
    [Required, StringLength(100)]
    public string Title { get; set; } = "";

    [Required, StringLength(1000)]
    public string Description { get; set; } = "";

    [Required]
    public string Category { get; set; } = "Other";
}
