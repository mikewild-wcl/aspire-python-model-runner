# Aspire Python Model Runner

## Contents

- [TODO](#todo)
- [Proposed model runner structure](#proposed-model-runner-structure)
- [Feature flags](#feature-flags)
- [Aspire dashboard](#aspire-dashboard)
- [ServiceBus](#servicebus)
- [Python app](#python-app)
- [Authentication (Microsoft Entra)](#authentication-microsoft-entra)
  - [Entra app registrations](#entra-app-registrations)
    - [Creating the registrations](#creating-the-registrations)
  - [Blazor authentication](#blazor-authentication)
  - [Troubleshooting Python API 401 responses](#troubleshooting-python-api-401-responses)
- [Troubleshooting Aspire publish and deploy](#troubleshooting-aspire-publish-and-deploy)

## TODO

- add authentication implementation
- add servicebus
- add python app
- link api to python app
    - use fastAPI call
    - use servicebus

## Proposed model runner structure

The Python model runner should have its own project boundary with separate FastAPI and Azure Service Bus worker entry points. This keeps Python-specific files together while allowing the HTTP API and queue consumer to be deployed and scaled independently using the same package and container image.

```text
├── src/
│   ├── Aspire.PythonModelRunner.AppHost/
│   ├── Aspire.PythonModelRunner.Backend/
│   ├── Aspire.PythonModelRunner.Common/
│   ├── Aspire.PythonModelRunner.Frontend/
│   ├── Aspire.PythonModelRunner.ServiceDefaults/
│   └── Aspire.PythonModelRunner.Shared/
├── python/
│   └── aspire-python-model-runner/
│       ├── pyproject.toml
│       ├── uv.lock
│       ├── Dockerfile
│       ├── .python-version
│       ├── src/
│       │   └── aspire_python_model_runner/
│       │       ├── __init__.py
│       │       ├── settings.py
│       │       ├── telemetry.py
│       │       ├── api/
│       │       │   ├── main.py
│       │       │   ├── dependencies.py
│       │       │   └── routes/
│       │       │       ├── health.py
│       │       │       └── runs.py
│       │       ├── worker/
│       │       │   ├── main.py
│       │       │   └── handlers.py
│       │       ├── messaging/
│       │       │   ├── service_bus.py
│       │       │   └── contracts.py
│       │       └── models/
│       │           ├── runner.py
│       │           ├── registry.py
│       │           └── implementations/
│       └── tests/
│           ├── unit/
│           └── integration/
└── tests/
    ├── Aspire.PythonModelRunner.AppHost.Tests/
    └── Aspire.PythonModelRunner.Common.Tests/
```

The process entry points are:

```text
API:    uvicorn aspire_python_model_runner.api.main:app
Worker: python -m aspire_python_model_runner.worker.main
```

Use `python/aspire-python-model-runner` for the Python project folder, `aspire_python_model_runner` for the Python package, and `aspire-python-model-runner` for the container image and Azure Container App. The API and worker should be registered as separate Aspire resources with different commands, referencing the same Python package and Service Bus resource.

## Feature flags

Feature flags can be set by adding parameters in the AppHost secrets, e.g.
```
  "Parameters:UseEntraAuthentication": true,
```

These will be converted into configuration passed to the services. Note that we use the new style feature flag configuration, which is documented here: https://learn.microsoft.com/en-us/azure/azure-app-configuration/use-feature-flags-dotnet-core?tabs=core6#configure-feature-flags-in-your-application.
Copying into configuration is handled in `ResourceBuilderExtensions` with extension method `WithFeatureFlag` - this takes optional parameter for the flag name and the style. 

When testing the generated environment variables, call `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)` rather than the parameterless overload. The parameterless overload uses Aspire's runtime (`Run`) operation and can asynchronously resolve value providers while waiting for AppHost runtime state, making composition-only unit tests appear to hang. `Publish` evaluates the environment synchronously without starting runtime resolution.

## Aspire dashboard

The Aspire dashboard has been lightly customized using tips from [Cozy Aspire Dashboarding | Victor Frye](https://victorfrye.com/blog/posts/cozy-aspire-dashboarding)

Icons are from [Fluent UI v0-v9 Icon Catalog](https://storybooks.fluentui.dev/react/iframe.html?viewMode=docs&id=concepts-migration-from-v0-icons--docs&globals=#v0---v9-icon-catalog).

## ServiceBus

The back end and Python apps may communicate over service bus to enable asynchronous communication. The service bus is added as an emulated resource in `AppHost`. See:
- [Set up Azure Service Bus in the AppHost](https://aspire.dev/integrations/cloud/azure/azure-service-bus/azure-service-bus-host)
- [Connect to Azure Service Bus | Aspire](https://aspire.dev/integrations/cloud/azure/azure-service-bus/azure-service-bus-connect/)

## Python app

The Python app is under the `python` folder. Create the intial folders and cd into the base folder where the virtual environment will be created:
```
md python/aspire-python-model-runner/src/aspire_python_model_runner
cd python/aspire-python-model-runner
```

Make sure you have uv installed. On Windows you can install it with 
```
winget install astral-sh.uv
```

Initialise the workspace - you can use the `-p` option to specify the Python version, e.g. 3.13:
```
uv init -p 3.13
```

This will add a couple of files. A couple of things to note:
- Keep `README.md` while `pyproject.toml` contains `readme = "README.md"`; otherwise the container build cannot install the project.
- In a completely new environment, these commands might generate a .gitignore file, but if it doesn't make sure python files are excluded. In this project I have added new `.gitignore` to the `python` folder. 
- You don't need to create a virtual environment - the uv tool will do that when commands are run below.

You can then add any packages you need - use uv commands rather than pip so the requirements will be written to pyproject.toml.
```
uv add uvicorn python-dotenv "fastapi[standard]" uvicorn azure-servicebus 
uv add opentelemetry-sdk opentelemetry-exporter-otlp-proto-grpc opentelemetry-instrumentation-fastapi opentelemetry-instrumentation-httpx azure-monitor-opentelemetry-exporter
```

> [!NOTE]
> If uv commands fail with error *"invalid peer certificate: UnknownIssuer"* then try adding this to the end of the commands: 
>    ```
>    --allow-insecure-host pypi.org --allow-insecure-host files.pythonhosted.org
>    ```
> Only do this in a trusted network. The preferred fix is to install/trust the correct corporate root CA so regular TLS validation works.

You can then open the project in VS Code and start adding code. The `src/aspire_python_model_runner` folder is the package root, so you can add subfolders and modules under that.
```
code .
```

Select the python interpreter - *ctrl-shift-P* -> *Python:Select interpreter*, and select the virtual environment matching `.venv`.

If you are just running from the Aspire AppHost, Aspire uvicorn startup will create the `.venv` if it doesn't exist.

## Authentication (Microsoft Entra)

If the `UseEntraAuthentication` feature flag parameter is turned on, the frontend authenticates users via OpenID Connect (OIDC) against Microsoft Entra, and the Python FastAPI backend validates Bearer tokens issued by Entra. Several app registrations are required.

### Entra app registrations

| Registration | Entra name | Purpose |
|---|---|---|
| Python app (resource) | `aspire-python-model-runner-python` | Defines the API scope that the backend API requests |
| Backend API (resource) | `aspire-python-model-runner-api` | Defines the API scope that the web app requests |
| Frontend web client | `aspire-python-model-runner-web` | Authenticates users and acquires tokens for the API |

#### Creating the registrations

**Step 1 — API app registrations** in [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** 
1. Name: `aspire-python-model-runner-python`, account type: *Accounts in this organizational directory only* or *Single tenant only* 
1. Redirect URI: leave this empty 
1. Register → note the **Application (client) ID**
1. **Expose an API** → **Add** next to *Application ID URI* → accept the default (`api://{client-id}`) → Save
1. **Add a scope**: name `access_as_user`, who can consent: *Admins and users*. Fill in the display names and descriptions

Add a second app registration for the backend API:
1, **New registration** name: `aspire-python-model-runner-api`, account type: *Accounts in this organizational directory only* or *Single tenant only* 
1. Redirect URI: leave this empty 
1. Register → note the **Application (client) ID** and **Directory (tenant) ID**
1. **Expose an API** → **Add** next to *Application ID URI* → accept the default (`api://{client-id}`) → Save
1. **Add a scope**: name `access_as_user`, who can consent: *Admins and users*. Fill in the display names and descriptions
1. **Certificates & secrets** → **New client secret** → note the **Value** immediately (shown once). This secret is required for the backend to perform the on-behalf-of token exchange when it calls the Python API.
1. **API permissions** → **Add a permission** → **My APIs** → `aspire-python-model-runner-python` → Delegated → `access_as_user` → Add
    - if you can't see the API under **My APIs**, search in "**APIs my organisation uses**"
1. **Grant admin consent** for your tenant

**Step 2 — Web client registration**
1. **New registration**, name: `aspire-python-model-runner-web`, account type: *Accounts in this organizational directory only*  or *Single tenant only* 
1. Redirect URI: **Web** → use the first of the URIs below, and add the others after deployment
1. Register → note the **Application (client) ID**
1. Add any additional redirect URIs from below via the **Authentication** blade
1. **Certificates & secrets** → **New client secret** → note the **Value** immediately (shown once)    1. 
1. **API permissions** → **Add a permission** → **My APIs** → `aspire-python-model-runner-api` → Delegated → `access_as_user` → Add
    - if you can't see the API under **My APIs**, search in "**APIs my organisation uses**"
1. **Grant admin consent** for your tenant

Redirect URIs should be (these can be found in the web project `launchSettings.json`):
- https://aspire-pythonmodelrunner-frontend.dev.localhost:7233/signin-oidc
- https://localhost:7233/signin-oidc

### Entra secrets

Update your Aspire AppHost `secrets.json` (or your deployed environment's config) with the values gathered above:

```json
"Parameters:authentication-type": "federatedIdentity",
"Parameters:entra-instance": "https://login.microsoftonline.com/",
"Parameters:entra-tenant-id": "<directory-tenant-id>",
"Parameters:entra-client-id": "<frontend-app-registration-client-id>",
"Parameters:entra-client-secret": "<frontend-web-app-registration-client-secret>",
"Parameters:entra-api-client-id": "<backend-app-registration-client-id>",
"Parameters:entra-api-client-secret": "<backend-api-app-registration-client-secret>",
"Parameters:entra-python-client-id": "<python-app-registration-client-id>",
```

Note that `Parameters:entra-instance` is included in `appsettings.json` so it can be omitted from secrets. `entra-client-secret` belongs to the frontend web registration, while `entra-api-client-secret` belongs to the backend API registration and is passed only to the backend. Other settings are left empty in `appsettings.json`.

### Blazor authentication

The frontend is an Interactive Server Blazor application. Microsoft Identity Web stores the signed-in session in an authentication cookie and, in this project, stores downstream API tokens in an in-memory token cache.

The cookie can remain valid when the AppHost restarts, but the in-memory token cache is recreated. The UI therefore still shows the user as authorized while a subsequent call to `GetAccessTokenForUserAsync` can throw `MicrosoftIdentityWebChallengeUserException` with an inner `MsalUiRequiredException`, such as:

```text
No account or login hint was passed to the AcquireTokenSilent call.
```

The frontend uses the Microsoft Identity Web Blazor consent and conditional-access handler to recover from this state:

- `Microsoft.Identity.Web.UI` supplies the `/MicrosoftIdentity/Account/Challenge` controller endpoint.
- `Program.cs` registers the Identity UI controllers and calls `AddMicrosoftIdentityConsentHandler` for Interactive Server components.
- `Home.razor` catches `MicrosoftIdentityWebChallengeUserException` and passes it to `MicrosoftIdentityConsentAndConditionalAccessHandler`.

The handler performs a full-page OpenID Connect challenge. Because the browser normally still has an Entra session, Entra SSO signs the user in again and repopulates the token cache without requiring a manual logout followed by login. The existing `/login` and `/logout` routes remain available for explicit session management.

When debugging, Visual Studio may pause where `GetAccessTokenForUserAsync` throws `MicrosoftIdentityWebChallengeUserException` or its inner `MsalUiRequiredException`. This is expected first-chance exception behavior: the exception is subsequently caught by `Home.razor` and passed to the consent handler. Continuing execution allows the OpenID Connect challenge to complete, and the next API call uses the repopulated token cache.

To prevent Visual Studio from pausing on this expected flow:

1. Open **Debug** → **Windows** → **Exception Settings**.
1. Search for `MicrosoftIdentityWebChallengeUserException` and `MsalUiRequiredException`.
1. Clear **Break when thrown** for both exception types.
1. Keep breaking enabled for unhandled CLR exceptions.

Do not suppress or replace these exceptions in `BearerTokenHandler`; Microsoft Identity Web uses them to communicate that an interactive challenge is required.

For a scaled or production deployment, replace `AddInMemoryTokenCaches` with a shared distributed token cache so token state is available across restarts and application instances. The challenge handler should remain registered to handle incremental consent and conditional-access requirements.

### Troubleshooting Python API 401 responses

When the frontend calls `/api/v1/python/hello`, authentication uses two delegated token exchanges:

1. The frontend acquires a token for the backend API.
1. The backend uses the on-behalf-of flow to acquire a token for the Python API and sends it as a Bearer token.

The backend requests `api://{python-client-id}/.default`. This is expected: Entra resolves `.default` to the delegated permissions already granted to the backend, including `access_as_user`.

If the backend successfully acquires this token but the Python API returns `401 Unauthorized`, check the Python resource logs in the Aspire dashboard. JWT validation failures are logged as warnings without logging the token or returning validation details to the caller:

```text
Python API bearer token validation failed: <reason>
```

Entra access tokens can represent the same configured resource using either of these audience values:

- The Application (client) ID: `{python-client-id}`
- The Application ID URI: `api://{python-client-id}`

Depending on the app registration's requested access-token version, the issuer can also use either of these tenant-specific forms:

- v2: `https://login.microsoftonline.com/{tenant-id}/v2.0`
- v1: `https://sts.windows.net/{tenant-id}/`

The validator in `python/aspire-python-model-runner/src/aspire_python_model_runner/authentication.py` accepts both documented audience and issuer forms while still requiring the configured Python application and tenant. Signing keys are loaded from the tenant's Entra JWKS endpoint, and only RS256-signed tokens are accepted.

To verify the complete flow, confirm that the backend token-acquisition logs contain the Python API scope and that the subsequent Python request succeeds:

```text
Scopes: api://{python-client-id}/.default
scopes: api://{python-client-id}/access_as_user
```

## Troubleshooting Aspire publish and deploy

The generated Python Dockerfile expects its build context to be the Python project directory containing `pyproject.toml` and `uv.lock`. If `AddUvicornApp` points directly to the package source directory, `aspire deploy` fails while building the image:

```text
COPY pyproject.toml /app/
"/pyproject.toml": not found
```

Configure the resource with the Python project root as `appDirectory` and use the installed package module as the ASGI target:

```csharp
const string pythonAppModule = "aspire_python_model_runner.main:app";

builder.AddUvicornApp(
        name: ResourceNames.PythonApp,
        appDirectory: "../../python/aspire-python-model-runner",
        app: pythonAppModule)
    .WithVirtualEnvironment(".venv", createIfNotExists: false)
    .WithUv()
    .PublishAsDockerFile(container => container
        .WithArgs(pythonAppModule, "--host", "0.0.0.0", "--port", "8000"));
```

The `PublishAsDockerFile` callback is required because executable arguments are not automatically copied to the published container resource. Without it, the image starts `uvicorn` without the application module.

### Visual Studio frontend startup

If the frontend fails during startup with an error similar to:

```text
Could not load file or assembly 'Microsoft.WebTools.ApiEndpointDiscovery'
```

this is usually a local Visual Studio web tooling issue rather than an application error. Visual Studio may inject the optional `Microsoft.WebTools.ApiEndpointDiscovery` hosting startup assembly even when it is not installed correctly. Ensure the frontend uses the standard `Properties\launchSettings.json` location and restart Visual Studio.

For a permanent fix, repair or update the Visual Studio **ASP.NET and web development** workload.

The Python project also needs:

- `src/aspire_python_model_runner/__init__.py`, which is required by the `uv_build` backend.
- `README.md`, because it is referenced by `pyproject.toml`.
- A project-level `.dockerignore` that excludes `.venv/`, `__pycache__/`, and compiled Python files so a Windows virtual environment is not copied into the Linux image.

