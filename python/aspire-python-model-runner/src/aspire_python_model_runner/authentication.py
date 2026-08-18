import os
import logging
from dataclasses import dataclass
from functools import lru_cache

import jwt
from fastapi import HTTPException, Security, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

_bearer_scheme = HTTPBearer(auto_error=False)
_logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class EntraSettings:
	instance: str
	tenant_id: str
	client_id: str

	@property
	def authority(self) -> str:
		return f"{self.instance.rstrip('/')}/{self.tenant_id}"

	@property
	def issuer(self) -> str:
		return f"{self.authority}/v2.0"

	@property
	def valid_issuers(self) -> tuple[str, str]:
		return self.issuer, f"https://sts.windows.net/{self.tenant_id}/"

	@property
	def jwks_uri(self) -> str:
		return f"{self.authority}/discovery/v2.0/keys"

	@property
	def audience(self) -> str:
		return f"api://{self.client_id}"

	@property
	def valid_audiences(self) -> tuple[str, str]:
		return self.client_id, self.audience

	@classmethod
	def from_environment(cls) -> "EntraSettings":
		values = {
			"instance": os.getenv("AZUREAD__INSTANCE"),
			"tenant_id": os.getenv("AZUREAD__TENANTID"),
			"client_id": os.getenv("AZUREAD__CLIENTID"),
		}
		missing = [name for name, value in values.items() if not value]
		if missing:
			joined_names = ", ".join(missing)
			raise RuntimeError(f"Missing Entra configuration: {joined_names}.")

		return cls(**values)


@lru_cache
def _get_signing_keys(jwks_uri: str) -> jwt.PyJWKClient:
	return jwt.PyJWKClient(jwks_uri)


def create_access_token_validator(settings: EntraSettings):
	signing_keys = _get_signing_keys(settings.jwks_uri)

	async def validate_access_token(
		credentials: HTTPAuthorizationCredentials | None = Security(_bearer_scheme),
	) -> dict[str, object]:
		if credentials is None:
			raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Bearer token is required.")

		try:
			signing_key = signing_keys.get_signing_key_from_jwt(credentials.credentials)
			return jwt.decode(
				credentials.credentials,
				signing_key.key,
				algorithms=["RS256"],
				audience=settings.valid_audiences,
				issuer=settings.valid_issuers,
			)
		except jwt.PyJWTError as error:
			_logger.warning("Python API bearer token validation failed: %s", error)
			raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid bearer token.") from error

	return validate_access_token


def is_entra_authentication_enabled() -> bool:
	return os.getenv("USE_ENTRA_AUTHENTICATION", "false").lower() == "true"
