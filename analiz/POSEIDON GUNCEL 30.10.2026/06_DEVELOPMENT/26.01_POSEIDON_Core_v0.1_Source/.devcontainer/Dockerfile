FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS dotnet
FROM node:24.19.0-bookworm-slim AS node
FROM postgres:17.11-trixie
USER root
RUN apt-get update && apt-get install -y --no-install-recommends python3 git gh libicu76 libgssapi-krb5-2 ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet && ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx
ENV DOTNET_ROOT=/usr/share/dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false
USER postgres
ENTRYPOINT []
CMD ["sleep", "infinity"]
