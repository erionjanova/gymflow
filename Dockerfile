FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/src/GymFlow.Domain/GymFlow.Domain.csproj src/GymFlow.Domain/
COPY backend/src/GymFlow.Application/GymFlow.Application.csproj src/GymFlow.Application/
COPY backend/src/GymFlow.Infrastructure/GymFlow.Infrastructure.csproj src/GymFlow.Infrastructure/
COPY backend/src/GymFlow.Api/GymFlow.Api.csproj src/GymFlow.Api/
RUN dotnet restore src/GymFlow.Api/GymFlow.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/GymFlow.Api/GymFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app .
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GymFlow.Api.dll"]
