using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;

namespace backend.Context
{
	public class ApplicationDbContext : DbContext
	{
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			optionsBuilder.UseSqlServer("Server=.;Database=Books;Trusted_Connection=true;TrustServerCertificate=true;");
		}
	}
}
