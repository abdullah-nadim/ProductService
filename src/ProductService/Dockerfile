# Multi-stage build

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ProductService/ProductService.csproj", "ProductService/"]
COPY ["src/ProductService.API/ProductService.API.csproj", "ProductService.API/"]
COPY ["src/ProductService.Services/ProductService.Services.csproj", "ProductService.Services/"]
COPY ["src/ProductService.Repository/ProductService.Repository.csproj", "ProductService.Repository/"]
COPY ["src/ProductService.Models/ProductService.Models.csproj", "ProductService.Models/"]
COPY ["src/ProductService.Contracts/ProductService.Contracts.csproj", "ProductService.Contracts/"]
COPY ["src/Core/Core.csproj", "Core/"]

RUN dotnet restore "ProductService/ProductService.csproj"

COPY src/ .
RUN dotnet publish "ProductService/ProductService.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductService.dll"]
