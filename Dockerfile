
# ==================================================
# restore dependencies
# ==================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore

WORKDIR /src

COPY *.sln ./

COPY AuxiliumSoftware.AuxiliumServices.AdministrationTools/*.csproj AuxiliumSoftware.AuxiliumServices.AdministrationTools/

RUN dotnet restore AuxiliumSoftware.AuxiliumServices.AdministrationTools/AuxiliumSoftware.AuxiliumServices.AdministrationTools.csproj


# ==================================================
# publish
# ==================================================
FROM restore AS publish

COPY . .

RUN dotnet publish \
    AuxiliumSoftware.AuxiliumServices.AdministrationTools/AuxiliumSoftware.AuxiliumServices.AdministrationTools.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore


# ==================================================
# database migration
# ==================================================
FROM restore AS migrate

COPY . .

RUN dotnet tool install \
        --global \
        dotnet-ef \
    && mkdir -p /etc/auxilium

ENV PATH="${PATH}:/root/.dotnet/tools"

ENTRYPOINT [
    "sh",
    "-c",
    "dotnet ef database update \
        --project AuxiliumSoftware.AuxiliumServices.AdministrationTools/AuxiliumSoftware.AuxiliumServices.AdministrationTools.csproj \
        -- \
        --config-path /etc/auxilium/config.yaml"
]


# ==================================================
# development
# ==================================================
FROM restore AS dev

ENV DOTNET_ENVIRONMENT=Development

RUN mkdir -p /etc/auxilium

ENTRYPOINT ["/bin/sh"]


# ==================================================
# production
# ==================================================
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS prod

WORKDIR /app

COPY --from=publish /app/publish ./

RUN mkdir -p /etc/auxilium \
    && chown -R app:app \
        /app \
        /etc/auxilium

USER app

ENTRYPOINT [
    "dotnet",
    "AuxiliumSoftware.AuxiliumServices.AdministrationTools.dll",
    "--config-path", "/etc/auxilium/config.yaml"
]
