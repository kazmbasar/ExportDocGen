# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# restore first for layer caching
COPY src/ExportDocGen/ExportDocGen.csproj src/ExportDocGen/
RUN dotnet restore src/ExportDocGen/ExportDocGen.csproj

COPY src/ src/
RUN dotnet publish src/ExportDocGen/ExportDocGen.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# non-root; /data is the writable volume (SQLite DB + Data Protection keys)
RUN adduser --system --group --uid 1001 app \
    && mkdir /data && chown app:app /data
COPY --from=build /app ./

USER app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DataDir=/data
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "ExportDocGen.dll"]
