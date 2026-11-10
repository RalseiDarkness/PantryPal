# PantryPal

PantryPal's Find Recipes workflow uses the MAUI app, the PantryPal API, Azure SQL,
and an Azure OpenAI-compatible GPT-4o deployment:

1. The app searches `Ingredients` through `GET /api/ingredients/search`.
2. The user selects database ingredient IDs.
3. `POST /api/recipes/recommendations` resolves those IDs, finds matching recipes,
   and calculates matching/missing ingredients and selected-ingredient coverage.
4. The API sends only the top 10 matches and the selected ingredient names to
   Azure AI Foundry. If AI analysis is unavailable, the SQL matches are still
   returned with an explanatory status.

The existing `POST /api/recipes/search` endpoint and Favorites page remain in
place. Pantry items continue to be stored locally as JSON; the database-backed
recipe search uses the separately hosted API.

## Local Windows demo

Install the .NET 10 SDK and MAUI workload on Windows (Visual Studio with the
.NET MAUI workload and Windows App SDK is recommended). Start the API and the
MAUI app as separate processes.

1. Ensure the Azure SQL firewall allows the demo machine and that the signed-in
   Azure identity can read the existing database tables. The API uses
   `DefaultAzureCredential`; run `az login` locally if using Azure CLI auth.
2. Configure the Azure OpenAI-compatible GPT-4o deployment in the API process:

   ```powershell
   $env:AzureOpenAI__Endpoint = "https://<your-resource>.openai.azure.com/"
   $env:AzureOpenAI__Deployment = "<your-gpt-4o-deployment-name>"
   $env:AzureOpenAI__ApiVersion = "2024-10-21"
   ```

   For key authentication, set `AzureOpenAI__ApiKey` in the API process
   environment. Do not put keys in the MAUI project, source control, or
   `appsettings.json`. Alternatively, leave the key unset and grant the API's
   Azure identity the appropriate Azure OpenAI inference role.
3. Start the API:

   ```powershell
   dotnet run --project .\PantryPal.Api\PantryPal.Api.csproj --launch-profile http
   ```

   Its HTTP launch profile listens on port `5222`. The MAUI Windows Debug app
   defaults to `http://localhost:5222/`.
4. In Visual Studio, set PantryPal as the startup project and run the Windows
   Machine target. On Windows, the app can also be built with:

   ```powershell
   dotnet build .\PantryPal.csproj -f net10.0-windows10.0.19041.0
   ```

5. Type at least two characters in Find Recipes, select one or more suggestions,
   and choose **Find Recipes**. The API requests and an empty-selection check are
   in [PantryPal.Api.http](./PantryPal.Api/PantryPal.Api.http).

The Azure SQL server and database are currently specified in
`PantryPal.Api/Data/SqlConnectionFactory.cs`. The implementation relies on the
existing schema/columns already used by the recipe query:
`Ingredients(IngredientID, IngredientName)`,
`RecipeIngredients(RecipeID, IngredientID)`, and the recipe columns in
`RecipeService.cs`. No schema migration is required. This repository does not
include a database schema export, so verify those columns and the Azure identity's
permissions against the target database before the demo.

The AI integration uses the Azure OpenAI deployment-style chat-completions
endpoint (`/openai/deployments/{deployment}/chat/completions`) and JSON-object
responses. If your Foundry deployment exposes a different endpoint contract,
adapt the backend service/configuration to that endpoint; the MAUI app never
connects directly to Foundry.

## Verification

Build the API with `dotnet build PantryPal.Api/PantryPal.Api.csproj`. Build the
Windows MAUI target on Windows using the command above. Verify
`GET /health/database`, then exercise autocomplete and recommendations using the
requests in the API `.http` file. A successful build alone does not verify Azure
credentials, firewall access, schema compatibility, or the Foundry deployment;
those require a live environment.
