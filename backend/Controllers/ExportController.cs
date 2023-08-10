﻿using backend.Context;
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

namespace backend.Controllers
{
    
    [ApiController]
    public class ExportController : Controller
	{
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
			
			// Aici poți folosi numele bazei de date pentru a realiza conexiunea cu baza de date.
			// Poți utiliza un ORM (Object-Relational Mapping) sau alte metode specifice ASP.NET pentru conectarea la baza de date.

			// Întoarce un răspuns către frontend (dacă este necesar).
			//return Ok(new { message = "Conectare cu succes la baza de date!" });
			return Ok(new { message = "Conectare cu succes la baza de date!" });
		}


		//public string GetNumeFromUrl()
		//{
		//	// Obțineți URL-ul cererii curente
		//	string currentUrl = HttpContext.Request.Path.Value;

		//	// Extrageți numele din URL
		//	string nume = currentUrl.Split('/').Last();

		//	// Utilizați numele după cum doriți

		//	// Puteți returna o vedere care să utilizeze numele
		//	return nume;
		//}


		[Route("api/export")]
		[HttpGet]
		public IActionResult ExportAllTablesToJson([FromQuery] DatabaseSelectionModel parameters)
		{
			string query = parameters.NumeBazaDeDate;

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



	}
}


