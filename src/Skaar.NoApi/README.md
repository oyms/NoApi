No Api
======

![A crash test dummy head](https://raw.githubusercontent.com/oyms/NoApi/main/resources/icon.png)

A local fake of any http api or integration. It responds to requests with the contents of files,
according to route manifests. Manifests and response files can be added and edited while it is running.

```shell
dotnet tool install --global Skaar.NoApi
noapi ./mocks
noapi generate https://example.com/openapi.json ./mocks   # a manifest from an OpenAPI document
```

[Documentation on GitHub](https://github.com/oyms/NoApi)
