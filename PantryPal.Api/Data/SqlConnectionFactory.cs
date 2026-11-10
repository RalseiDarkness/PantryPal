
using Azure.Core;
using Microsoft.Data.SqlClient;

namespace PantryPal.Api.Data;

public class SqlConnectionFactory
{
    private readonly TokenCredential _credential;

    private const string Server =
        "grantsazureserver.database.windows.net";

    private const string Database =
        "Sophomore-Project-DB";

    public SqlConnectionFactory(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<SqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(
                new[] { "https://database.windows.net/.default" }
            ),
            cancellationToken
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
