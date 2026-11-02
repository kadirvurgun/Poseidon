FROM docker.io/library/golang:1.27.3-bookworm@sha256:d9b6a1beb9474adc2a1ce842573639b44b2a0dd49b6669f51d3821baf9ec37c9 AS build
WORKDIR /src
ADD --checksum=sha256:45521908307306e925c98d629e1c17d78c8b72b6ee242b1bfb1409f7d8ee5841 https://codeload.github.com/minio/minio/tar.gz/9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a /tmp/minio-source.tar.gz
RUN echo '45521908307306e925c98d629e1c17d78c8b72b6ee242b1bfb1409f7d8ee5841  /tmp/minio-source.tar.gz' | sha256sum -c - \
    && tar -xzf /tmp/minio-source.tar.gz --strip-components=1 -C /src \
    && CGO_ENABLED=0 GOTOOLCHAIN=local go build -mod=readonly -trimpath -o /out/minio . \
    && mkdir -p /out/data /out/tmp \
    && chmod 0700 /out/data \
    && chmod 1777 /out/tmp
FROM scratch
LABEL org.opencontainers.image.source="https://github.com/minio/minio"
LABEL org.opencontainers.image.revision="9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a"
COPY --from=build /out/minio /usr/bin/minio
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=build /usr/share/zoneinfo /usr/share/zoneinfo
COPY --from=build --chown=65532:65532 /out/data /data
COPY --from=build /out/tmp /tmp
ENV MINIO_BROWSER=off MINIO_UPDATE=off
USER 65532:65532
EXPOSE 9000
ENTRYPOINT ["/usr/bin/minio"]
CMD ["server", "/data"]
