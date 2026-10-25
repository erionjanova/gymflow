FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/GymFlow.Domain/GymFlow.Domain.csproj src/GymFlow.Domain/
COPY src/GymFlow.Application/GymFlow.Application.csproj src/GymFlow.Application/
COPY src/GymFlow.Infrastructure/GymFlow.Infrastructure.csproj src/GymFlow.Infrastructure/
COPY src/GymFlow.Api/GymFlow.Api.csproj src/GymFlow.Api/
RUN dotnet restore src/GymFlow.Api/GymFlow.Api.csproj
COPY src/ src/
RUN dotnet publish src/GymFlow.Api/GymFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GymFlow.Api.dll"]
