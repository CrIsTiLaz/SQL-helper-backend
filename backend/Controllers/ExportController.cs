using backend.Context;
using backend.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Dynamic;

namespace backend.Controllers
{
    
    [ApiController]
    public class ExportController : ControllerBase
    {

		private string numeBazaDeDateAleasa;
		[Route("api/exportDatabases")]
		[HttpGet]
		public IActionResult GetAllDatabases()
		{
			var connectionString = "Data Source=.;Initial Catalog=master;Trusted_Connection = true; TrustServerCertificate = true;";
			try
			{
				using (var connection = new SqlConnection(connectionString))
				{
					connection.Open();

					var databases = new List<string>();

					// Obține numele tuturor bazelor de date disponibile
					DataTable dataTable = connection.GetSchema("Databases");
					foreach (DataRow row in dataTable.Rows)
					{
						var databaseName = row["database_name"].ToString();
						databases.Add(databaseName);
					}

					return new JsonResult(databases);
				}
			}
			catch (Exception ex)
			{
				// Poți trata eventualele erori sau să returnezi un mesaj de eroare în răspuns
				return BadRequest(new { message = "A apărut o eroare în timpul obținerii bazelor de date.", error = ex.Message });
			}
		}

		[Route("api/connectDB")]
		[HttpPost]
		public IActionResult ConectareBazaDeDate([FromBody] DatabaseSelectionModel model)
		{
			string numeBazaDeDate = model.NumeBazaDeDate;
			this.numeBazaDeDateAleasa = numeBazaDeDate;
			Console.WriteLine(numeBazaDeDate);
			// Aici poți folosi numele bazei de date pentru a realiza conexiunea cu baza de date.
			// Poți utiliza un ORM (Object-Relational Mapping) sau alte metode specifice ASP.NET pentru conectarea la baza de date.

			// Întoarce un răspuns către frontend (dacă este necesar).
			return Ok(new { message = "Conectare cu succes la baza de date!" });
		}

		[Route("api/export")]
		[HttpGet]
		public IActionResult ExportAllTablesToJson()
		{
			Console.WriteLine(numeBazaDeDateAleasa);
			//Console.WriteLine(model.NumeBazaDeDate);
			if (!string.IsNullOrEmpty(numeBazaDeDateAleasa))
			{
				// Poți folosi numele bazei de date în logică aici
				Console.WriteLine("Nume baza de date aleasă: " + numeBazaDeDateAleasa);

				var connectionString = "Data Source=.;Initial Catalog=_numeBazaDeDate;Trusted_Connection = true; TrustServerCertificate = true;";
				using (var dbContext = new ApplicationDbContext(numeBazaDeDateAleasa))
				{
					dbContext.Database.OpenConnection();

					if (dbContext.Database.GetDbConnection().State == ConnectionState.Open)
					{
						var t = 1;//conexiunea e deschisa
					}
					else
					{
						var t = 0;
					}

					var tables = dbContext.Database.GetDbConnection().GetSchema("Tables");

					var tableNames = tables.AsEnumerable()
				.Select(t => t["TABLE_NAME"].ToString())
				.Where(tableName => !tableName.StartsWith("sys", StringComparison.OrdinalIgnoreCase));

					var exportData = new Dictionary<string, List<dynamic>>();

					foreach (var tableName in tableNames)
					{
						using (var command = dbContext.Database.GetDbConnection().CreateCommand())
						{
							command.CommandText = $"SELECT * FROM {tableName}";
							using (var reader = command.ExecuteReader())
							{
								var rows = new List<dynamic>();

								while (reader.Read())
								{
									var row = new ExpandoObject() as IDictionary<string, object>;

									for (var i = 0; i < reader.FieldCount; i++)
									{
										var columnName = reader.GetName(i);
										var columnValue = reader.IsDBNull(i) ? null : reader.GetValue(i);

										row[columnName] = columnValue;
									}

									rows.Add(row);
								}

								exportData[tableName] = rows;
							}
						}
					}
					var x = new JsonResult(exportData);
					return x;
				}
				

				
			
			}
			return null;

		}
		
		

	}
}


