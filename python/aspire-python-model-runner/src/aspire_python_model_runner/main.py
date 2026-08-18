from contextlib import asynccontextmanager
import os

import fastapi
import fastapi.responses
import fastapi.staticfiles

from aspire_python_model_runner.authentication import (
    EntraSettings,
    create_access_token_validator,
    is_entra_authentication_enabled,
)
from aspire_python_model_runner.telemetry import configure_telemetry

@asynccontextmanager
async def lifespan(app):
    configure_telemetry(app, service_name="python-model-runner")
    yield

app = fastapi.FastAPI(lifespan=lifespan)
authentication_dependencies = []

if is_entra_authentication_enabled():
    authentication_dependencies = [fastapi.Depends(create_access_token_validator(EntraSettings.from_environment()))]

if not os.path.exists("static"):
    @app.get("/", response_class=fastapi.responses.HTMLResponse, dependencies=authentication_dependencies)
    async def root():
        """Root endpoint."""
        return "API service is running. Navigate to <a href='/api/v1/hello'>/api/v1/hello</a> to confirm connectivity."

@app.get("/api/v1/hello", name="Hello Endpoint", dependencies=authentication_dependencies)
async def hello_endpoint():
    """Hello endpoint."""
    return {"message": "hello world"}

@app.get("/health", response_class=fastapi.responses.PlainTextResponse)
async def health_check():
    """Health check endpoint."""
    return "Healthy"
