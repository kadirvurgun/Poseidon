# syntax=docker/dockerfile:1.7
# POSEIDON UAT backend images. Build context: repository root (see /.dockerignore).
#   docker build -f infra/uat/docker/backend.Dockerfile --target api      -t poseidon-api .
#   docker build -f infra/uat/docker/backend.Dockerfile --target migrator -t poseidon-migrator .
# SDK version follows global.json. Renovate pins base-image digests (docs/dependency-policy.md).
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY backend/ backend/
COPY contracts/ contracts/
COPY docs/tasks/import-foundation-01-source-fields.json docs/tasks/
RUN dotnet restore backend/Poseidon.Api/Poseidon.Api.csproj --locked-mode \
 && dotnet restore backend/Poseidon.DbMigrator/Poseidon.DbMigrator.csproj --locked-mode
RUN dotnet publish backend/Poseidon.Api/Poseidon.Api.csproj -c Release --no-restore -o /out/api \
 && dotnet publish backend/Poseidon.DbMigrator/Poseidon.DbMigrator.csproj -c Release --no-restore -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
ARG POSEIDON_VERSION=unknown
LABEL org.opencontainers.image.source="https://github.com/kadirvurgun/Poseidon" \
      org.opencontainers.image.version="${POSEIDON_VERSION}"
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    POSEIDON_VERSION=${POSEIDON_VERSION}
WORKDIR /app
COPY --from=build /out/api ./
# Data Protection key ring (named volume, see compose). Owned by the non-root app user (uid 1654).
RUN mkdir -p /var/lib/poseidon/keys && chown -R app:app /var/lib/poseidon
USER app
EXPOSE 8080
# The runtime image has no curl: probe /health/live with bash /dev/tcp.
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=4 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/live HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && grep -q alive <&3'
ENTRYPOINT ["dotnet", "Poseidon.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
ARG POSEIDON_VERSION=unknown
LABEL org.opencontainers.image.source="https://github.com/kadirvurgun/Poseidon" \
      org.opencontainers.image.version="${POSEIDON_VERSION}"
WORKDIR /app
COPY --from=build /out/migrator ./
# Synthetic seed (no credentials, signatures or verified evidence; docs/seed-data.md).
COPY backend/seed/ /app/backend/seed/
USER app
ENTRYPOINT ["dotnet", "Poseidon.DbMigrator.dll"]
