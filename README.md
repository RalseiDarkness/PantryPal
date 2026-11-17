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

Release builds use the deployed API. Debug builds use the local API by default:
Windows and Mac Catalyst use `http://localhost:5222/`, while Android emulators
use `http://10.0.2.2:5222/` and iOS simulators use `http://localhost:5222/`.
Set `PANTRYPAL_API_BASE_URL` in the Debug app process to override its address.
For physical devices, use a reachable LAN address or the deployed API; mobile
launch configurations for passing a device-specific override are not included
in this phase. The API service is registered through MAUI dependency injection;
API URLs are selected centrally.

On Windows or macOS, select the matching PantryPal desktop configuration in
VS Code and press F5. The pre-launch task starts the API with its `http` launch
profile and waits until it listens on port `5222` before the MAUI target starts.
This requires the .NET MAUI extension and C# Dev Kit, plus the platform's
installed MAUI workload and selected debug target.

The API also has `POST /api/recipes/recommendations`, which adds Azure AI
suggestions. The app doesn't use it right now.

## Local Windows demo

Install the .NET 10 SDK and MAUI workload on Windows (Visual Studio with the
.NET MAUI workload and Windows App SDK is recommended). For VS Code, install
the .NET MAUI extension and configure its Windows debug target.

1. Ensure the Azure SQL firewall allows the demo machine and that the signed-in
   Azure CLI identity can read the existing database tables. The API uses
   `AzureCliCredential` in Development, so run `az login` before starting it.
   In production, the API uses its managed identity.
2. The Foundry project endpoint and GPT-4o deployment are configured under
   `AzureOpenAI` in `PantryPal.Api/appsettings.json`. Environment variables can
   override these values for local development:

   ```powershell
   $env:AzureOpenAI__Endpoint = "https://SophomoreProject-AI.services.ai.azure.com/openai/v1"
   $env:AzureOpenAI__Deployment = "gpt-4o"
   ```

   The API authenticates to Foundry with Microsoft Entra ID using the
   `https://ai.azure.com/.default` scope. Do not configure an API key. The
   production managed identity needs the **Cognitive Services OpenAI User**
   role on the Foundry resource.
3. To run the API by itself, start it with:

   ```powershell
   dotnet run --project .\PantryPal.Api\PantryPal.Api.csproj --launch-profile http
   ```

   Its HTTP launch profile listens on port `5222`. When using VS Code F5, skip
   this manual command: the `PantryPal: Windows Desktop` configuration starts
   the API task automatically.
4. In Visual Studio, set PantryPal as the startup project and run the Windows
   Machine target. In VS Code, select the Windows MAUI debug target. The app can
   also be built with:

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

The AI integration uses the Azure OpenAI Responses API at the configured
Foundry project endpoint (`/openai/v1/responses`). The deployment name is sent
as the Responses API model value. The MAUI app never connects directly to
Foundry.

## Verification

Build the API with `dotnet build PantryPal.Api/PantryPal.Api.csproj`. Build the
Windows MAUI target on Windows using the command above. Verify
`GET /health/database`, then exercise autocomplete and recommendations using the
requests in the API `.http` file. A successful build alone does not verify Azure
credentials, firewall access, schema compatibility, or the Foundry deployment;
those require a live environment.
