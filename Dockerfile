# =============================================================================
# POTS PWA — backend image (Render)
# Builds ONLY the ASP.NET Core 10 API. The Blazor WebAssembly client is
# published separately and hosted on Vercel; the API is purely a JSON gateway.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Cache restore by copying csproj/sln files first.
COPY Pots.sln .
COPY src/Pots.Api/Pots.Api.csproj         src/Pots.Api/
COPY src/Pots.Domain/Pots.Domain.csproj   src/Pots.Domain/
COPY src/Pots.Infrastructure/Pots.Infrastructure.csproj src/Pots.Infrastructure/
COPY src/Pots.Shared/Pots.Shared.csproj   src/Pots.Shared/
# Client + Tests csprojs referenced by the .sln (restore would otherwise miss them).
COPY src/Pots.Client/Pots.Client.csproj   src/Pots.Client/
COPY tests/Pots.Domain.Tests/Pots.Domain.Tests.csproj         tests/Pots.Domain.Tests/
COPY tests/Pots.Infrastructure.Tests/Pots.Infrastructure.Tests.csproj tests/Pots.Infrastructure.Tests/
COPY tests/Pots.Api.Tests/Pots.Api.Tests.csproj               tests/Pots.Api.Tests/

RUN dotnet restore src/Pots.Api/Pots.Api.csproj

# Bring API + shared sources only — no client code in the image.
COPY src/Pots.Api/         src/Pots.Api/
COPY src/Pots.Domain/      src/Pots.Domain/
COPY src/Pots.Infrastructure/ src/Pots.Infrastructure/
COPY src/Pots.Shared/      src/Pots.Shared/

RUN dotnet publish src/Pots.Api/Pots.Api.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish/ ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Pots.Api.dll"]
