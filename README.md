# Sistema de Alertas Tempranas · Río Piraí

Sistema de alertas de inundación basado en CAP 1.2. Proyecto de Sistemas I (Scrum).

## Stack
- Backend: ASP.NET Core (.NET 10), EF Core + Npgsql + NetTopologySuite
- Base de datos: PostgreSQL 17 + PostGIS (Docker)
- Frontend: React 19 + Vite + TypeScript + Leaflet

## Estructura
```
src/SistemaAlertas.Domain          Entidades y reglas del dominio
src/SistemaAlertas.Application     Casos de uso, interfaces, patrones (Factory Method, Builder)
src/SistemaAlertas.Infrastructure  EF Core, PostGIS, importadores CAP
src/SistemaAlertas.Api             API REST
tests/SistemaAlertas.Tests         Pruebas xUnit
frontend/                          SPA React + Leaflet
```

## Cómo ejecutar
```bash
docker compose up -d                                  # PostgreSQL + PostGIS en el puerto 5433
dotnet run --project src/SistemaAlertas.Api           # API en http://localhost:5080
cd frontend && npm install && npm run dev             # Web en http://localhost:5173
```
Comprobar la API: `GET http://localhost:5080/api/salud`
