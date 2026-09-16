# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar archivos de proyecto y restaurar dependencias
COPY ["GoalLineBackend.csproj", "./"]
RUN dotnet restore "GoalLineBackend.csproj"

# Copiar el resto del código y compilar
COPY . .
RUN dotnet publish "GoalLineBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (imagen ligera de Alpine)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Configuraciones para optimizar el uso de memoria en contenedores con baja RAM (1GB AWS Tier)
# El GC modo workstation usa menos memoria.
ENV DOTNET_gcServer=0
ENV DOTNET_TieredPGO=0

EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GoalLineBackend.dll"]
