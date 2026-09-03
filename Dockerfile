# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# NB: do NOT split this into `restore` + `publish --no-restore`. On SDK 10.0.400
# that combination drops the framework static web assets (wwwroot/_framework/
# blazor.web.js), so Blazor interactivity 404s at runtime. A single publish that
# restores as part of the build emits them correctly.
COPY src/ src/
RUN dotnet publish src/ExportDocGen/ExportDocGen.csproj \
    -c Release -o /app /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The base image ships a non-root user "app" (UID 1654). /data is the writable
# volume (SQLite DB + Data Protection keys); a named volume inherits this owner.
RUN mkdir -p /data && chown app:app /data
COPY --from=build /app ./

USER app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DataDir=/data
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "ExportDocGen.dll"]
