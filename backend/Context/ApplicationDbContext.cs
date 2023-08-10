using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;

namespace backend.Context
{
	public class ApplicationDbContext : DbContext
	{
		private readonly string _numeBazaDeDate;

		public ApplicationDbContext(string numeBazaDeDate)
		{
			_numeBazaDeDate = numeBazaDeDate;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			
			optionsBuilder.UseSqlServer($"Server=.;Database={_numeBazaDeDate};Trusted_Connection=true;TrustServerCertificate=true;");
		}
	}
}
