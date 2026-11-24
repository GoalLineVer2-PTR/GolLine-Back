# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["GoalLineBackend.csproj", "./"]
RUN dotnet restore "GoalLineBackend.csproj"

COPY . .
RUN dotnet publish "GoalLineBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (imagen ligera Alpine para Docker y Azure Container Apps)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Optimización de memoria para Azure Free Tier / contenedores ligeros
ENV DOTNET_gcServer=0
ENV DOTNET_TieredPGO=0
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "GoalLineBackend.dll"]
