using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnectMvc.Data;

public static class DbInitializer
{
    public static void Initialize(CivicConnectDbContext db) =>
        throw new NotSupportedException("Database schema and seed data are managed outside the application.");
}
