# ─────────────────────────────────────────────────────────────────────────────
# Identity.Api — Multi-stage Dockerfile
# ─────────────────────────────────────────────────────────────────────────────

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY ["NovaStack.sln", "./"]
COPY ["src/BuildingBlocks/NovaStack.SharedKernel/NovaStack.SharedKernel.csproj", "src/BuildingBlocks/NovaStack.SharedKernel/"]
COPY ["src/BuildingBlocks/NovaStack.Infrastructure/NovaStack.Infrastructure.csproj", "src/BuildingBlocks/NovaStack.Infrastructure/"]
COPY ["src/BuildingBlocks/NovaStack.Contracts/NovaStack.Contracts.csproj", "src/BuildingBlocks/NovaStack.Contracts/"]
COPY ["src/Services/Identity.Domain/Identity.Domain.csproj", "src/Services/Identity.Domain/"]
COPY ["src/Services/Identity.Application/Identity.Application.csproj", "src/Services/Identity.Application/"]
COPY ["src/Services/Identity.Infrastructure/Identity.Infrastructure.csproj", "src/Services/Identity.Infrastructure/"]
COPY ["src/Services/Identity.Api/Identity.Api.csproj", "src/Services/Identity.Api/"]

RUN dotnet restore "src/Services/Identity.Api/Identity.Api.csproj"

# Copy the rest and build
COPY . .
WORKDIR /src/src/Services/Identity.Api
RUN dotnet publish "Identity.Api.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Copy published output
COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app

USER appuser

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "Identity.Api.dll"]
