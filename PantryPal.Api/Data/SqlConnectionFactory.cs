
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace PantryPal.Api.Data;

public class SqlConnectionFactory
{
    private readonly DefaultAzureCredential _credential = new();

    private const string Server =
        "grantsazureserver.database.windows.net";

    private const string Database =
        "Sophomore-Project-DB";

    public async Task<SqlConnection> CreateConnectionAsync()
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(
                new[] { "https://database.windows.net/.default" }
            )
        );

        var connectionString =
            $"Server=tcp:{Server},1433;" +
            $"Database={Database};" +
            "Encrypt=True;" +
            "TrustServerCertificate=False;";

        var connection = new SqlConnection(connectionString)
        {
            AccessToken = token.Token
        };

        return connection;
    }
}
