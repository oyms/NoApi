FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/Skaar.NoApi/ ./Skaar.NoApi/
RUN dotnet publish ./Skaar.NoApi -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.description="A local fake of any http api, serving files according to route manifests"
WORKDIR /app
COPY --from=build /app ./

# Mount the manifests (and response files) here. The folder is searched recursively for routes.json.
WORKDIR /manifests
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "/app/Skaar.NoApi.dll", "/manifests"]
