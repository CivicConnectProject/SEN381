namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.categories</c> table.
/// </summary>
public class Category
{
    public int CategoryId { get; set; }

    /// <summary>Unique display name – UNIQUE constraint in the database.</summary>
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    // Navigation: every service request that belongs to this category.
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
}
