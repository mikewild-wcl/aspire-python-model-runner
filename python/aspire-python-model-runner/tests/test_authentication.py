import os
import unittest
from unittest.mock import MagicMock, patch

from fastapi.security import HTTPAuthorizationCredentials

from aspire_python_model_runner.authentication import (
	EntraSettings,
	create_access_token_validator,
	is_entra_authentication_enabled,
)


class AuthenticationConfigurationTests(unittest.TestCase):
	def test_is_entra_authentication_enabled_defaults_to_false(self):
		with patch.dict(os.environ, {}, clear=True):
			self.assertFalse(is_entra_authentication_enabled())

	def test_entra_settings_uses_python_environment_variables(self):
		environment = {
			"AZUREAD__INSTANCE": "https://login.microsoftonline.com/",
			"AZUREAD__TENANTID": "tenant-id",
			"AZUREAD__CLIENTID": "client-id",
		}

		with patch.dict(os.environ, environment, clear=True):
			settings = EntraSettings.from_environment()

		self.assertEqual("https://login.microsoftonline.com/tenant-id", settings.authority)
		self.assertEqual("https://login.microsoftonline.com/tenant-id/v2.0", settings.issuer)
		self.assertEqual(
			("https://login.microsoftonline.com/tenant-id/v2.0", "https://sts.windows.net/tenant-id/"),
			settings.valid_issuers,
		)
		self.assertEqual("https://login.microsoftonline.com/tenant-id/discovery/v2.0/keys", settings.jwks_uri)
		self.assertEqual("api://client-id", settings.audience)
		self.assertEqual(("client-id", "api://client-id"), settings.valid_audiences)


class AccessTokenValidatorTests(unittest.IsolatedAsyncioTestCase):
	async def test_validator_accepts_entra_v1_and_v2_token_metadata(self):
		settings = EntraSettings(
			instance="https://login.microsoftonline.com/",
			tenant_id="tenant-id",
			client_id="client-id",
		)
		signing_keys = MagicMock()
		signing_key = MagicMock()
		signing_keys.get_signing_key_from_jwt.return_value = signing_key
		credentials = HTTPAuthorizationCredentials(scheme="Bearer", credentials="access-token")

		with (
			patch(
				"aspire_python_model_runner.authentication._get_signing_keys",
				return_value=signing_keys,
			),
			patch(
				"aspire_python_model_runner.authentication.jwt.decode",
				return_value={"sub": "user-id"},
			) as decode,
		):
			validator = create_access_token_validator(settings)
			claims = await validator(credentials)

		self.assertEqual({"sub": "user-id"}, claims)
		decode.assert_called_once_with(
			"access-token",
			signing_key.key,
			algorithms=["RS256"],
			audience=("client-id", "api://client-id"),
			issuer=("https://login.microsoftonline.com/tenant-id/v2.0", "https://sts.windows.net/tenant-id/"),
		)


if __name__ == "__main__":
	unittest.main()
