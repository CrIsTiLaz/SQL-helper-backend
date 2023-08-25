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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json;

namespace backend.Controllers
{
    
    [ApiController]
    public class ExportController : Controller
	{
		private string _currentDatabaseName;

		[Route("api/exportDatabases")]
		[HttpGet]
		public IActionResult GetAllDatabases()
		{
			var connectionString = "Data Source=.;Initial Catalog=master;Trusted_Connection = true; TrustServerCertificate = true; ";
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
			return Ok(new { message = "Conectare cu succes la baza de date!" });
		}

		[Route("api/export")]
		[HttpGet]
		public IActionResult ExportAllTablesToJson([FromQuery] DatabaseSelectionModel parameters)
		{
			string query = parameters.NumeBazaDeDate;
			_currentDatabaseName = query;
			
			var sessionId = HttpContext.Session.Id;
			HttpContext.Session.SetString("DatabaseName", query); // Stocați databaseName în sesiune

			string databaseName = HttpContext.Session.GetString("DatabaseName"); // Recuperați databaseName din sesiune
			Console.WriteLine(databaseName);
			if (string.IsNullOrEmpty(query))
			{
				return BadRequest("Numele bazei de date nu a fost furnizat.");
			}

			using (var dbContext = new ApplicationDbContext(query))
			{
				dbContext.Database.OpenConnection();

				if (dbContext.Database.GetDbConnection().State == ConnectionState.Open)
				{
					var t = 1; // conexiunea e deschisa
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

				var jsonResult = new JsonResult(exportData);
				return jsonResult;
			}
		}

		[Route("api/exportQuery")]
		[HttpPost]
		public IActionResult ExecuteSqlQuery([FromBody] SqlQueryModel model)
		{
			var sessionId = HttpContext.Session.Id;
			string databaseName = HttpContext.Session.GetString("DatabaseName"); // Recuperați databaseName din sesiune
			var dbName = model.DatabaseName;
			Console.WriteLine(model.Query);
			Console.WriteLine(databaseName);

			try
			{
				string connectionString = $"Server=.;Database={dbName};Trusted_Connection=true;TrustServerCertificate=true;";

				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();

					using (SqlCommand command = new SqlCommand(model.Query, connection))
					{
						DataTable dataTable = new DataTable();
						using (SqlDataReader reader = command.ExecuteReader())
						{
							dataTable.Load(reader);
						}

						// Convertim DataTable la JSON
						string jsonResult = JsonConvert.SerializeObject(dataTable, Formatting.Indented);
						Console.WriteLine(jsonResult);

						// Returnăm rezultatul ca JSON
						return Content(jsonResult, "application/json");
					}
				}
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}




	}
}

public class SqlQueryModel
{
	public string Query { get; set; }
	public string DatabaseName { get; set; }
}

