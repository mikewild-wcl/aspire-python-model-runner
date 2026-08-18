# Using Pydantic Settings

## Status

This document describes a possible future migration from the current dataclass-based Python configuration to Pydantic Settings. It is a design note only; the application currently continues to use `EntraSettings` from `authentication.py` as a frozen dataclass.

## When Pydantic Settings is useful

The current authentication configuration contains only three values:

- Entra instance URL
- Tenant ID
- Python API client ID

A dataclass is simple and sufficient for this scope. Pydantic Settings becomes more valuable if the Python application gains a larger configuration model covering authentication, Azure Service Bus, storage, telemetry, model selection, or runtime limits.

Potential benefits include:

- Automatic environment-variable loading
- Required-value and type validation
- URL and identifier validation
- Structured startup errors
- Nested configuration models
- A single typed configuration object that can be injected into FastAPI dependencies

The tradeoffs are an additional direct dependency, more framework-specific code, and less control over the wording of configuration errors unless validation errors are handled explicitly.

## Dependency

Pydantic is used by FastAPI, but settings support is distributed as the separate `pydantic-settings` package. If this migration is implemented, add it as a direct project dependency with `uv`:

```powershell
cd python/aspire-python-model-runner
uv add pydantic-settings
```

Do not rely on FastAPI's transitive Pydantic dependency for code imported directly by this project.

## Authentication settings example

The existing Aspire AppHost supplies these environment variables to the Python resource when Entra authentication is enabled:

- `AZUREAD__INSTANCE`
- `AZUREAD__TENANTID`
- `AZUREAD__CLIENTID`

A Pydantic Settings equivalent of the current `EntraSettings` class could look like this:

```python
from pydantic import AnyHttpUrl, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class EntraSettings(BaseSettings):
	model_config = SettingsConfigDict(
		frozen=True,
		extra="ignore",
	)

	instance: AnyHttpUrl = Field(
		validation_alias="AZUREAD__INSTANCE",
	)
	tenant_id: str = Field(
		min_length=1,
		validation_alias="AZUREAD__TENANTID",
	)
	client_id: str = Field(
		min_length=1,
		validation_alias="AZUREAD__CLIENTID",
	)

	@property
	def authority(self) -> str:
		return f"{str(self.instance).rstrip('/')}/{self.tenant_id}"

	@property
	def issuer(self) -> str:
		return f"{self.authority}/v2.0"

	@property
	def valid_issuers(self) -> tuple[str, str]:
		return (
			self.issuer,
			f"https://sts.windows.net/{self.tenant_id}/",
		)

	@property
	def jwks_uri(self) -> str:
		return f"{self.authority}/discovery/v2.0/keys"

	@property
	def audience(self) -> str:
		return f"api://{self.client_id}"

	@property
	def valid_audiences(self) -> tuple[str, str]:
		return self.client_id, self.audience
```

Ordinary properties are preferable here because the derived values are consumed by authentication code and do not need to be included when the settings model is serialized. Use Pydantic `computed_field` only if serialized model output should include them.

`AnyHttpUrl` provides validation for the Entra instance. Converting it with `str(...)` prevents Pydantic's URL type from leaking into string operations and third-party APIs.

## Application initialization

`BaseSettings` reads its configured environment aliases during construction, replacing the current `from_environment` method:

```python
settings = EntraSettings()
```

The conditional setup in `main.py` could then use:

```python
if is_entra_authentication_enabled():
	settings = EntraSettings()
	authentication_dependencies = [
		fastapi.Depends(create_access_token_validator(settings))
	]
```

Settings should only be constructed when authentication is enabled. This preserves the current behavior in which Entra configuration is not required while the `UseEntraAuthentication` feature flag is off.

## Consolidated application settings

If additional configuration is introduced, prefer one application-level settings object rather than creating an unrelated `BaseSettings` class for every module. Nested Pydantic models can separate concerns while retaining one environment-loading boundary.

For example:

```python
from pydantic import BaseModel
from pydantic_settings import BaseSettings, SettingsConfigDict


class AuthenticationSettings(BaseModel):
	instance: str
	tenant_id: str
	client_id: str


class ServiceBusSettings(BaseModel):
	namespace: str
	queue_name: str


class ApplicationSettings(BaseSettings):
	model_config = SettingsConfigDict(
		env_nested_delimiter="__",
		frozen=True,
		extra="ignore",
	)

	authentication: AuthenticationSettings
	service_bus: ServiceBusSettings
```

This convention would use environment variables such as:

```text
AUTHENTICATION__INSTANCE
AUTHENTICATION__TENANT_ID
AUTHENTICATION__CLIENT_ID
SERVICE_BUS__NAMESPACE
SERVICE_BUS__QUEUE_NAME
```

Adopting a consolidated model would require coordinating these names with the Aspire AppHost environment configuration. Existing environment names should not be changed without updating AppHost composition tests and deployment configuration.

## Validation behavior

Missing or malformed settings cause Pydantic to raise `ValidationError`. Because settings are created during application initialization, invalid configuration should fail startup rather than allowing an incorrectly secured application to run.

If customized diagnostics are needed, catch the validation error at the composition boundary, log a concise configuration message, and re-raise. Do not log secrets or complete environment contents.

Consider whether tenant and client IDs should remain non-empty strings or use `uuid.UUID`. UUID validation is stricter and appropriate when only directory and application IDs are accepted, but it would reject tenant domain names and placeholder values used in some development or test configurations.

## FastAPI dependency injection

For a larger application, expose the initialized settings through a cached FastAPI dependency:

```python
from functools import lru_cache


@lru_cache
def get_settings() -> ApplicationSettings:
	return ApplicationSettings()
```

Then consume it with `Depends(get_settings)`. A single cached instance avoids repeatedly parsing the environment and gives tests a standard dependency to override.

## Testing strategy

Tests should cover:

1. Successful loading from the Aspire environment-variable names.
2. Failure when each required authentication value is absent and authentication is enabled.
3. Rejection of an invalid Entra instance URL.
4. Correct authority, issuer, JWKS URI, and accepted audience values.
5. Authentication-disabled startup without any Entra environment variables.
6. FastAPI dependency overrides if settings are exposed through a cached dependency.

Use `unittest.mock.patch.dict` or Pytest's `monkeypatch` to isolate environment variables. If a settings dependency is cached, clear its cache between tests to avoid leaking configuration state.

## Migration checklist

1. Add `pydantic-settings` using `uv add` and commit both `pyproject.toml` and `uv.lock`.
2. Replace the dataclass with a `BaseSettings` model while preserving issuer and audience behavior.
3. Construct settings only when Entra authentication is enabled.
4. Update authentication configuration tests for Pydantic validation errors.
5. Keep the health endpoint anonymous.
6. Confirm both feature-flag modes through tests.
7. Run the Python test suite and the .NET solution build.
8. Validate an authenticated backend-to-Python request against the configured Entra registrations.

## Recommendation

Keep the current dataclass while authentication remains the only substantial Python configuration. Revisit Pydantic Settings when configuration expands enough that automatic loading, nested models, and centralized validation offset the added dependency and abstraction.
