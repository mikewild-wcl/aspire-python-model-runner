def configure_telemetry(app, service_name: str = "python-app"):
    """Configure OpenTelemetry tracing, metrics and logging for a FastAPI app.

    Local dev: exports to the Aspire dashboard via OTLP (OTEL_EXPORTER_OTLP_ENDPOINT).
    Deployed:  also exports to Azure Application Insights when
               APPLICATIONINSIGHTS_CONNECTION_STRING is present.
    """
    pass
