# NoApi

[![NuGet Version](https://img.shields.io/nuget/v/Skaar.NoApi.svg)](https://www.nuget.org/packages/Skaar.NoApi)

![Icon](./resources/icon.svg)

A local fake of any http api or integration. It responds to requests with the contents of files,
according to route manifests. Manifests and response files can be added and edited while it is running.

## Run as a dotnet tool

```shell
dotnet tool install --global Skaar.NoApi
noapi ./mocks
```

By default it listens to `https://localhost:9998` (using the ASP.NET Core development certificate,
see `dotnet dev-certs https --trust`). Change it with `--urls`:

```shell
noapi ./mocks --urls=http://localhost:5000
```

The first argument is the manifest location:

* A **file**: that file is the manifest.
* A **folder**: the folder and its subfolders are searched for manifests named `routes.json`
  (change with `--NoApi:ManifestFileName=api.json`).
* **Omitted**: the working directory is used as a folder.

Manifests added, changed, moved or removed while running are read again.
Response files are read on each request, so changes to them are served at once.

## Manifest

A manifest lists the routes, in order of precedence. Each request gets the response of the first matching route.
With several manifests in a folder, they are read in order of their (full) paths.

```json
{
  "$schema": "https://raw.githubusercontent.com/oyms/NoApi/main/src/Skaar.NoApi/schema.json",
  "routes": [
    {
      "name": "Single user",
      "method": "GET",
      "path": "^/api/users/1$",
      "response": {
        "path": "./responses/user.json"
      }
    },
    {
      "name": "Search by name",
      "method": "GET, POST",
      "path": "^/api/search$",
      "parameters": [
        { "parameter": "name", "value": "^(?i)ola" }
      ],
      "response": {
        "path": "./responses/users.json",
        "contentType": "application/json",
        "statusCode": 200,
        "headers": { "X-Total-Count": "2" }
      }
    }
  ]
}
```

| Property                 | Description                                                                                                                   | Default            |
|--------------------------|-------------------------------------------------------------------------------------------------------------------------------|--------------------|
| `name`                   | The name of the route, shown in the log.                                                                                      |                    |
| `method`                 | The http method(s), comma separated: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS` or `ANY`.                     | `ANY`              |
| `path`                   | A regex pattern the (url encoded) request path must match. Use `^` and `$` to match the whole path.                           | any path           |
| `parameters`             | Query parameters or form fields which must all be present. `value` is a regex pattern the value must match (default any value). | none             |
| `response.path`          | The response file, relative to the manifest. Without it the body is empty.                                                    | empty body         |
| `response.contentType`   | The `Content-Type` of the response.                                                                                           | `application/json` |
| `response.statusCode`    | The status code of the response.                                                                                              | `200`              |
| `response.headers`       | Additional response headers.                                                                                                  |                    |

See [the samples](./samples) and [the schema](./src/Skaar.NoApi/schema.json).
Invalid routes and manifests are skipped, and the errors are logged.

### Built-in routes

| Path        | Response                                                                       |
|-------------|--------------------------------------------------------------------------------|
| `/__routes` | All routes, with the manifest they are read from.                               |
| `/__schema` | The json schema of the manifest.                                               |
| any other   | When no route matches, an empty json document (`{}`) with status code 200.      |

## Run in a container

```shell
docker build -t noapi .
docker run --rm -p 8080:8080 -v ./samples:/manifests:ro noapi
```

Inside the container the server listens to `http://+:8080` (`ASPNETCORE_URLS`), and reads manifests from
`/manifests`. Or use [`compose.yaml`](./compose.yaml), which serves the samples.

## Configuration

Settings are read from `appsettings.json` (installed with the tool), environment variables and command line arguments.

| Setting                    | Description                                                     | Default                  |
|----------------------------|-----------------------------------------------------------------|--------------------------|
| `NoApi:Url`                | The url to listen to, unless `--urls`/`ASPNETCORE_URLS` is set. | `https://localhost:9998` |
| `NoApi:ManifestFileName`   | The manifest file name to look for in a folder.                 | `routes.json`            |

As environment variables, use `__` as separator, e.g. `NoApi__ManifestFileName=api.json`.
