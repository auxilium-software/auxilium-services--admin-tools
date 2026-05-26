
# restore dependencies
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore

WORKDIR /src
COPY *.sln ./
COPY AuxiliumSoftware.AuxiliumServices.AdministrationTools/*.csproj \
    AuxiliumSoftware.AuxiliumServices.AdministrationTools/
RUN dotnet restore


# publish
FROM restore AS publish

COPY . .
RUN dotnet publish AuxiliumSoftware.AuxiliumServices.AdministrationTools/AuxiliumSoftware.AuxiliumServices.AdministrationTools.csproj \
    -c Release -o /app/publish --no-restore


# migrate - SDK stays in image, dotnet-ef pre-installed at build time
FROM restore AS migrate

COPY . .
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN mkdir -p /etc/auxilium

ENTRYPOINT ["dotnet", "ef", "database", "update", "--", "--config-path", "/etc/auxilium/config.yaml"]


# dev
FROM restore AS dev

ENV DOTNET_ENVIRONMENT=Development
# source mounted at /src; drop into a shell and run commands manually
ENTRYPOINT ["/bin/sh"]


# prod
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS prod

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "AuxiliumSoftware.AuxiliumServices.AdministrationTools.dll"]
