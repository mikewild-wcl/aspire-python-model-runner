# Python Model Service: Aspire, ACA, and Runtime Model Loading

Notes from a design conversation covering a Python API called by a C# API, orchestrated
by an Aspire AppHost in development and deployed to Azure Container Apps via Azure DevOps.

---

## 1. Structuring the Python app in the Aspire AppHost

Use `Aspire.Hosting.Python` (`aspire add python`). The Community Toolkit package
(`CommunityToolkit.Aspire.Hosting.Python.Extensions`) is deprecated as of Aspire 13.

For FastAPI / ASGI apps:

```csharp
var api = builder.AddUvicornApp("python-api", "../python-app", "main:app")
    .WithHttpEndpoint(port: 8000, env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithUv();
```

This gives you virtual environment management, `ConnectionStrings__*` injection,
service discovery, and breakpoint debugging.

Other entry points available:

| Method | Use for |
| --- | --- |
| `AddPythonApp` | A script file (`main.py`) |
| `AddPythonModule` | `python -m <module>` |
| `AddPythonExecutable` | A CLI tool installed in the venv |
| `AddUvicornApp` | ASGI apps — FastAPI, Starlette, Quart |

Debugging is VS Code only. Visual Studio does not currently attach to Python
processes in an Aspire solution.

If neither `WithUv()` nor `WithPip()` is specified, Aspire picks the package
manager from project files: `pyproject.toml` implies uv, `requirements.txt` implies pip.

---

## 2. Controlling the container base image

By default Aspire auto-generates a Dockerfile at publish time, inferring the Python
version from `.python-version` / `pyproject.toml` / the virtual environment. Fast,
but you do not choose the base image.

### Option A — own the Dockerfile, switch on execution context

```csharp
IResourceBuilder<IResourceWithEndpoints> api =
    builder.ExecutionContext.IsRunMode
        ? builder.AddUvicornApp("python-api", "../python-app", "main:app")
        : builder.AddDockerfile("python-api", "../python-app")
                 .WithBuildArg("PYTHON_VERSION", pyVersion);
```

Development keeps the Python-specific affordances; publish uses your Dockerfile with
whatever base you want (Azure Linux, distroless, a hardened internal base).

`WithBuildArg` accepts a `ParameterResource`, so the base image tag can be supplied
per environment at deploy time. `WithBuildSecret` is available for build-time tokens
(exposed via `--mount=type=secret` on `RUN` lines) — take care not to copy them into
intermediate or final layers.

Check whether your Aspire version still exposes `PublishAsDockerFile` on the Python
resource; if so, it is tidier than the run-mode branch.

### Option B — generate the Dockerfile from AppHost code

- `AddDockerfileFactory` / `WithDockerfileFactory` — return Dockerfile content as a string.
- `AddDockerfileBuilder` / `WithDockerfileBuilder` — fluent multi-stage API. Experimental;
  suppress `ASPIREDOCKERFILEBUILDER001`.

---

## 3. Deploying to Azure Container Apps from Azure DevOps

Aspire 13 ships `aspire deploy` / `aspire do`, and it works in CI, but `azd` remains
the more mature production path, and `azd pipeline config` handles Azure DevOps
federated credential setup.

**Do not mix the two** — they use different resource naming schemes by default.

Split `azd provision` / `azd package` / `azd deploy` into separate pipeline stages
for gating.

Two ACA settings worth making explicit rather than inheriting:

- Leave the Python service internal — omit `WithExternalHttpEndpoints()` unless a
  browser reaches it directly.
- Set the scale rule deliberately via `PublishAsAzureContainerApp`. Default scaling
  behaviour, and ingress configuration in particular, is where scale-to-zero usually
  goes wrong.

---

## 4. Getting the model file to the Python service

### Pass a reference, not the bytes

The C# API sends an identifier; Python resolves it:

```json
{ "model_ref": "forecast/daily-sales/v3", "params": { "horizon": 14 } }
```

Small requests, idempotent and cacheable calls, and a warm replica can skip the load
entirely. Sending the file body on every call forces a reload per request.

### Where the file lives

**Blob Storage — the default answer.** ACA cannot mount Blob Storage as a volume, so
you download it. Wire it through Aspire:

```csharp
var models = builder.AddAzureStorage("storage").AddBlobs("models");
api.WithReference(models);   // → ConnectionStrings__models
```

Python reads `ConnectionStrings__models` and uses `DefaultAzureCredential`. Aspire sets
`AZURE_TOKEN_CREDENTIALS=ManagedIdentityCredential` on ACA-deployed resources, so the
same code path works locally against Azurite and in production against managed identity.
Cache to `/tmp` (ephemeral, per-replica) keyed by ref.

**Azure Files volume.** To drop in a model without a redeploy:

```csharp
.WithVolume("models", "/models", isReadOnly: true)
```

Aspire translates volumes and bind mounts to Azure Files-backed storage mounts at deploy
time. Caveat: the file share attaches to the managed ACA environment, which Aspire cannot
reconfigure on an existing environment.

**Baked into the image.** If the model versions with the code, just `COPY` it. Simplest
thing that works and makes rollback trivial. Poor fit above a few hundred MB.

### Loading it

Load lazily, cache in process, guard against concurrent loads of the same file:

```python
_cache: dict[str, Any] = {}
_lock = asyncio.Lock()

async def get_model(ref: str):
    if ref not in _cache:
        async with _lock:
            if ref not in _cache:
                path = await fetch_to_tmp(ref)
                _cache[ref] = await asyncio.to_thread(joblib.load, path)
    return _cache[ref]
```

`to_thread` matters — a synchronous load on the event loop stalls every other request in
that replica. If you preload at startup instead, do it in the lifespan handler so
`/health` stays honest and Aspire's readiness check reflects reality.

---

## 5. What `joblib.load` actually does

It deserializes a Python object and returns it. For a scikit-learn model that is a fitted
estimator instance:

```python
model = joblib.load(path)
preds = model.predict(X)
```

The file is data, not a script. There is no entry point in it and nothing to invoke —
no main method required on either side.

But it is not inert. joblib uses pickle underneath, and unpickling reconstructs objects
by importing modules and calling constructors:

- **Your container needs the same libraries.** The pickle stores references like
  `sklearn.ensemble.RandomForestRegressor`, not the code. If the class cannot be imported
  the load fails outright; if it imports at a different version you can get silent
  behaviour changes. Pin training and serving to the same lockfile — a direct argument for
  controlling your base image and running `uv sync` against a committed `uv.lock`.
- **Import-and-construct is where arbitrary code can execute.** A hostile pickle runs at
  load time, before `predict` is ever called. Restrict refs to a known prefix in your own
  storage, or use a format without that property (ONNX, safetensors).

For anything crossing a trust boundary, ONNX is cleaner: `onnxruntime` loads a computation
graph, executes no Python, and does not couple you to the training library's version.

---

## 6. Loading caller-supplied logic with `importlib`

Only appropriate when the file comes from a trusted internal pipeline — you are running
supplied code inside your container.

### By name

```python
importlib.import_module("scorers.daily_sales")
```

`import` with a runtime string. The module must be findable on `sys.path`.

### By path

```python
import importlib.util, sys

def load_scorer(name: str, path: str):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module          # before exec_module
    spec.loader.exec_module(module)     # module body runs HERE
    return module
```

`exec_module` executes the file top to bottom in that module's namespace. **The module
body is the entry point.** An `if __name__ == "__main__":` block will not fire, because
`__name__` is whatever you passed as `name`.

### Four things that will bite

- **`importlib.invalidate_caches()`** after writing a new file at runtime. The path finder
  caches directory listings, so a freshly downloaded file may be invisible. Call it after
  the download, before the load.
- **Unique module names.** Two versions loaded as `scorer` collide in `sys.modules`. Key
  the name to the ref (`scorer_daily_sales_v3`). This also gives you free caching — check
  `sys.modules` before reloading.
- **Insert into `sys.modules` before `exec_module`.** Modules that reference themselves,
  use dataclasses, or get pickled need to be findable by name mid-execution.
- **It blocks.** `await asyncio.to_thread(load_scorer, name, path)`.

Validate the contract before trusting it:

```python
fn = getattr(module, "predict", None)
if not callable(fn):
    raise ValueError(f"{name} does not expose predict()")
```

**Avoid `importlib.reload()`** for versioned scorers. It rebinds names in the existing
module object, but anything already holding a reference to the old class keeps it — you
end up with two incompatible versions of the same class alive at once. Load under a new
name and drop the old entry instead.

Multi-file scorers: zip them and prepend the zip to `sys.path`. `zipimport` handles
pure-Python packages transparently; native extensions will not load from a zip.

---

## 7. Example scorer module

Minimal, matching the contract:

```python
"""Daily sales scorer, v3."""

CONTRACT_VERSION = 1

def predict(params: dict) -> dict:
    horizon = int(params["horizon"])
    baseline = float(params.get("baseline", 100.0))
    growth = float(params.get("growth", 0.02))

    values = [baseline * (1 + growth) ** d for d in range(1, horizon + 1)]

    return {
        "horizon": horizon,
        "values": values,
    }
```

Three things it deliberately does not do: no `if __name__ == "__main__":` (never fires
under `exec_module`), no work at import time, no I/O of its own.

Wrapping a serialized estimator, with a lazy load so import stays cheap and a broken
artifact fails on first call rather than at load:

```python
from pathlib import Path
import joblib

_model = None

def _get_model():
    global _model
    if _model is None:
        _model = joblib.load(Path(__file__).with_name("model.joblib"))
    return _model

def predict(params: dict) -> dict:
    preds = _get_model().predict([[params["x1"], params["x2"]]])
    return {"values": preds.tolist()}
```

`Path(__file__).with_name(...)` resolves relative to the module rather than the container's
working directory — which matters when loading from `/tmp`. This variant will not work from
a zip, where `__file__` is not a real filesystem path.

### Conventions worth fixing early

- Return plain JSON-serialisable types. `.tolist()`, not raw numpy arrays — FastAPI will
  not serialise those.
- Put a `CONTRACT_VERSION` int at module level so the loader can reject scorers written
  against an older signature, rather than failing on a missing key at request time.
