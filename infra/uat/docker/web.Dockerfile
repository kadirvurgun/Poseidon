# syntax=docker/dockerfile:1.7
# POSEIDON UAT web edge: Caddy (automatic TLS) serving the built SPA and proxying the API/Keycloak.
# Build context: repository root.
#   docker build -f infra/uat/docker/web.Dockerfile -t poseidon-web .
FROM node:26.13.0-bookworm-slim AS build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

FROM caddy:2.10.2-alpine AS web
ARG POSEIDON_VERSION=unknown
LABEL org.opencontainers.image.source="https://github.com/kadirvurgun/Poseidon" \
      org.opencontainers.image.version="${POSEIDON_VERSION}"
COPY --from=build /src/frontend/dist /srv/poseidon
COPY infra/uat/caddy/Caddyfile /etc/caddy/Caddyfile
RUN printf '%s\n' "${POSEIDON_VERSION}" > /srv/poseidon/version.txt
