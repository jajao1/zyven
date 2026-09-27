FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/Api/Api.csproj -c Release -o /publish/api /p:UseAppHost=false
RUN dotnet publish src/Workers/Workers.csproj -c Release -o /publish/worker /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080

FROM runtime AS api
COPY --from=build /publish/api .
ENTRYPOINT ["dotnet", "Api.dll"]

FROM runtime AS worker
COPY --from=build /publish/worker .
ENTRYPOINT ["dotnet", "Workers.dll"]
