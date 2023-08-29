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
using System.ComponentModel.DataAnnotations;

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

		[Route("api/importToTable")]
		[HttpPost]
		public IActionResult ImportDataToTable([FromForm] string databaseName, [FromForm] string tableName, [FromForm] IFormFile file)
		{
			Console.WriteLine(databaseName);
			//string BaseConnectionString = "Server=.;Database={databaseName};Trusted_Connection=true;TrustServerCertificate=true;";

			if (string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(tableName) || file == null)
			{
				return BadRequest("Numele bazei de date, numele tabelei și fișierul sunt necesare.");
			}

			List<Dictionary<string, object>> parsedData;
			using (var memoryStream = new MemoryStream())
			{
				file.CopyTo(memoryStream);
				memoryStream.Position = 0;

				using (var reader = new StreamReader(memoryStream))
				{
					var content = reader.ReadToEnd();
					parsedData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content);
				}
			}

			// După parsare, conectează-te la baza de date și inserează datele
			string connectionString = $"Server=.;Database={databaseName};Trusted_Connection=true;TrustServerCertificate=true;";

			using (SqlConnection connection = new SqlConnection(connectionString))
			{
				connection.Open();

				foreach (var row in parsedData)
				{
					string columns = string.Join(", ", row.Keys);
					string values = string.Join(", ", row.Keys.Select(k => "@" + k));
					string commandText = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

					using (SqlCommand cmd = new SqlCommand(commandText, connection))
					{
						foreach (var kvp in row)
						{
							cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);
						}
						cmd.ExecuteNonQuery();
					}
				}
			}

			return Ok(new { message = "Datele au fost importate cu succes." });
		}

	}
}

public class SqlQueryModel
{
	public string Query { get; set; }
	public string DatabaseName { get; set; }
}

public class TableImportModel
{
	[Required]
	public string TableName { get; set; }
	[Required]
	public IFormFile File { get; set; }
}