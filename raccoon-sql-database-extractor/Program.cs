using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Insert(1, JsonDocumentSerializerContext.Default);
});


builder.Services.AddScoped<SqlConnection>(_ => new SqlConnection(Environment.GetEnvironmentVariable("AZURE_RACCOON_SQL_DATABASE")));

var app = builder.Build();

app.MapGet("/", async(
            [FromQuery] string sql,
            [FromHeader] string user,
            [FromHeader] string password) =>
{
    if(user != Environment.GetEnvironmentVariable("AZURE_RACCOON_SQL_DATABASE_USER") ||
        password != Environment.GetEnvironmentVariable("AZURE_RACCOON_SQL_DATABASE_PASSWORD"))
    {
        return Results.Unauthorized();
    }


    using SqlConnection connection = new(Environment.GetEnvironmentVariable("AZURE_RACCOON_SQL_DATABASE"));
    await connection.OpenAsync();
    using SqlCommand command = new(sql)
    {
        Connection = connection,
        CommandType = System.Data.CommandType.Text
    };

    SqlDataReader reader = await command.ExecuteReaderAsync();
    var columns = await reader.GetColumnSchemaAsync();

    List<Dictionary<string, object?>> rows = new();
    while (reader.Read())
    {
        Dictionary<string, object?> row = new();
        for (int i = 0; i < columns.Count; i++)
        {
            object? value = reader.GetValue(i);
            row.Add(columns[i].ColumnName, value);
        }
        rows.Add(row);
    }

    return Results.Ok(JsonSerializer.SerializeToDocument(rows));

});

app.Run();


[JsonSerializable(typeof(JsonDocument))]
internal partial class JsonDocumentSerializerContext : JsonSerializerContext
{

}

[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
