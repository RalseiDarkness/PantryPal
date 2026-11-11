# PantryPal

## How the app uses the database

The MAUI app never connects to Azure SQL itself. It calls the deployed PantryPal API
(`Services/RecipeApiService.cs`), and the API reads the database.

1. **My Pantry** (`PageViews/PantryView`): typing 2+ letters calls
   `GET /api/ingredients/search` for ingredient names. Picked items are saved on
   the device in `pantry.json`.
2. **Find Recipes** (`PageViews/RecipesView`): sends the saved pantry names to
   `POST /api/recipes/search` and shows the best 20 matches.
3. **Recipe page** (`PageViews/RecipeDetailView`): tapping a recipe shows what you
   have, what you still need, the steps and a link to the original recipe.

Every build of the app (Debug and Release) uses the deployed API, so it works for
the whole team without Azure access. To test API changes locally, run the API and
change `ApiAddress` in `RecipeApiService.cs` to `http://localhost:5222/`.

The API also has `POST /api/recipes/recommendations`, which adds Azure AI
suggestions. The app doesn't use it right now.

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

   Its HTTP launch profile listens on port `5222`. The app uses the deployed API
   unless `ApiAddress` in `Services/RecipeApiService.cs` is changed to
   `http://localhost:5222/`.
4. In Visual Studio, set PantryPal as the startup project and run the Windows
   Machine target. On Windows, the app can also be built with:

   ```powershell
   dotnet build .\PantryPal.csproj -f net10.0-windows10.0.19041.0
   ```

5. Add ingredients on **My Pantry** (type at least two letters to see database
   suggestions), then open **Find Recipes** and choose **Find recipes**. Example API
   requests are in [PantryPal.Api.http](./PantryPal.Api/PantryPal.Api.http).
   Run `dotnet test PantryPal.Tests` for the app tests.

The Azure SQL server and database are currently specified in
`PantryPal.Api/Data/SqlConnectionFactory.cs`. The implementation relies on the
existing schema/columns already used by the recipe query:
`Ingredients(IngredientID, IngredientName)`,
`RecipeIngredients(RecipeID, IngredientID)`, and the recipe columns in
`RecipeService.cs`. No schema migration is required. This repository does not
include a database schema export, so verify those columns and the Azure identity's
permissions against the target database before the demo.

The API connection factory uses managed identity and retries transient SQL
connection errors (including auto-pause/resume timeouts) with 2, 4, 8, then
10-second maximum backoff. Each connection attempt is capped at 15 seconds and
the entire token-acquisition/connection process is capped at 100 seconds.
Permanent credential, permission, and firewall errors are not retried. The API
returns a service-unavailable response if the connection window expires.

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
