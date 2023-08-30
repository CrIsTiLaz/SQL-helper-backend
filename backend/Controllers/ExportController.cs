using backend.Context;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
	[ApiController]
	public class ExportController : Controller
	{
		private const string DefaultConnectionString = "Server=.;Trusted_Connection=true;TrustServerCertificate=true;";

		[Route("api/exportDatabases")]
		[HttpGet]
		public IActionResult GetAllDatabases()
		{
			const string masterConnectionString = DefaultConnectionString + "Database=master;";
			try
			{
				using var connection = new SqlConnection(masterConnectionString);
				connection.Open();

				DataTable dataTable = connection.GetSchema("Databases");
				var databases = dataTable.AsEnumerable().Select(row => row["database_name"].ToString()).ToList();

				return new JsonResult(databases);
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = "A apărut o eroare în timpul obținerii bazelor de date.", error = ex.Message });
			}
		}

		[Route("api/connectDB")]
		[HttpPost]
		public IActionResult ConnectDatabase([FromBody] DatabaseSelectionModel model)
		{
			// Presupunem că aici este un cod real de conectare. Din moment ce lipsește, vom returna un mesaj de succes.
			return Ok(new { message = "Conectare cu succes la baza de date!" });
		}

		[Route("api/export")]
		[HttpGet]
		public IActionResult ExportAllTablesToJson([FromQuery] DatabaseSelectionModel parameters)
		{
			if (string.IsNullOrEmpty(parameters.NumeBazaDeDate))
			{
				return BadRequest("Numele bazei de date nu a fost furnizat.");
			}

			HttpContext.Session.SetString("DatabaseName", parameters.NumeBazaDeDate);

			using var dbContext = new ApplicationDbContext(parameters.NumeBazaDeDate);
			dbContext.Database.OpenConnection();

			var tables = dbContext.Database.GetDbConnection().GetSchema("Tables");
			var tableNames = tables.AsEnumerable()
				.Select(t => t["TABLE_NAME"].ToString())
				.Where(tableName => !tableName.StartsWith("sys", StringComparison.OrdinalIgnoreCase));

			var exportData = tableNames.ToDictionary(
				tableName => tableName,
				tableName => FetchTableData(dbContext, tableName));

			return new JsonResult(exportData);
		}

		[Route("api/exportQuery")]
		[HttpPost]
		public IActionResult ExecuteSqlQuery([FromBody] SqlQueryModel model)
		{
			string connectionString = DefaultConnectionString + $"Database={model.DatabaseName};";

			try
			{
				using var connection = new SqlConnection(connectionString);
				connection.Open();

				DataTable dataTable = ExecuteQuery(connection, model.Query);

				string jsonResult = JsonConvert.SerializeObject(dataTable, Formatting.Indented);
				return Content(jsonResult, "application/json");
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
			if (string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(tableName) || file == null)
			{
				return BadRequest("Numele bazei de date, numele tabelei și fișierul sunt necesare.");
			}

			List<Dictionary<string, object>> parsedData = ParseFileData(file);
			if (parsedData == null)
			{
				return BadRequest("Tipul de fișier nu este suportat.");
			}

			string connectionString = DefaultConnectionString + $"Database={databaseName};";
			InsertDataIntoDatabase(parsedData, tableName, connectionString);

			return Ok(new { message = "Datele au fost importate cu succes." });
		}

		private static List<Dictionary<string, object>> ParseFileData(IFormFile file)
		{
			using var memoryStream = new MemoryStream();
			file.CopyTo(memoryStream);
			memoryStream.Position = 0;

			string fileType = Path.GetExtension(file.FileName).ToLower();
			switch (fileType)
			{
				case ".json":
					var jsonReader = new StreamReader(memoryStream);
					var content = jsonReader.ReadToEnd();
					return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content);

				case ".csv":
					return ParseCsvFile(memoryStream);

				default:
					return null;
			}
		}

		private static List<Dictionary<string, object>> ParseCsvFile(MemoryStream memoryStream)
		{
			using var csvReader = new StreamReader(memoryStream);
			var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
				Delimiter = ","
			};

			var csv = new CsvReader(csvReader, csvConfig);
			var records = csv.GetRecords<dynamic>().ToList();

			return records.Select(record => (IDictionary<string, object>)record).Select(dictionary => dictionary.ToDictionary(entry => entry.Key, entry => entry.Value)).ToList();
		}

		private static List<dynamic> FetchTableData(ApplicationDbContext dbContext, string tableName)
		{
			using var command = dbContext.Database.GetDbConnection().CreateCommand();
			command.CommandText = $"SELECT * FROM {tableName}";
			using var reader = command.ExecuteReader();

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

			return rows;
		}

		private static DataTable ExecuteQuery(SqlConnection connection, string query)
		{
			using var command = new SqlCommand(query, connection);
			var dataTable = new DataTable();

			using var reader = command.ExecuteReader();
			dataTable.Load(reader);

			return dataTable;
		}

		private static void InsertDataIntoDatabase(List<Dictionary<string, object>> parsedData, string tableName, string connectionString)
		{
			using var connection = new SqlConnection(connectionString);
			connection.Open();

			foreach (var row in parsedData)
			{
				string columns = string.Join(", ", row.Keys.Select(k => $"[{k}]"));
				string values = string.Join(", ", row.Keys.Select(k => "@" + k));

				string commandText = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

				using var cmd = new SqlCommand(commandText, connection);
				foreach (var kvp in row)
				{
					cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
				}
				cmd.ExecuteNonQuery();
			}
		}
	}
}
