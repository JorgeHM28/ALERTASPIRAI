# API del SAT Río Piraí. Se construye desde la raíz del repo:
#   docker build -f src/SistemaAlertas.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SistemaAlertas.sln ./
COPY src/SistemaAlertas.Domain/*.csproj src/SistemaAlertas.Domain/
COPY src/SistemaAlertas.Application/*.csproj src/SistemaAlertas.Application/
COPY src/SistemaAlertas.Infrastructure/*.csproj src/SistemaAlertas.Infrastructure/
COPY src/SistemaAlertas.Api/*.csproj src/SistemaAlertas.Api/
RUN dotnet restore src/SistemaAlertas.Api/SistemaAlertas.Api.csproj
COPY src/ src/
COPY datos/ datos/
RUN dotnet publish src/SistemaAlertas.Api/SistemaAlertas.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SistemaAlertas.Api.dll"]
