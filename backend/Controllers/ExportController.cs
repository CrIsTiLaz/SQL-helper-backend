using backend.Context;
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
		[Route("api/export")]
		[HttpGet]
		public IActionResult ExportAllTablesToJson()
		{
			using (var dbContext = new ApplicationDbContext())
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
		[Route("api/exportDatabases")]
		[HttpGet]
		public IActionResult GetAllDatabases()
		{
			using (var dbContext = new ApplicationDbContext())
			{
				dbContext.Database.OpenConnection();

				var databases = new List<string>();

				if (dbContext.Database.GetDbConnection().State == ConnectionState.Open)
				{
					var dataTable = dbContext.Database.GetDbConnection().GetSchema("Databases");

					foreach (DataRow row in dataTable.Rows)
					{
						var databaseName = row["database_name"].ToString();
						databases.Add(databaseName);
					}
				}

				return new JsonResult(databases);
			}
		}
	}
}
