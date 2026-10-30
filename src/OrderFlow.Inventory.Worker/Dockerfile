# Se construye desde la raíz del repo (necesita BuildingBlocks y los Directory.*.props):
#   docker build -f src/OrderFlow.Inventory.Worker/Dockerfile .

# ---- Etapa 1: compilar con el SDK completo (~800 MB, no llega a la imagen final) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo los .csproj: si no cambian, Docker reutiliza la capa del restore (builds más rápidos).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrderFlow.BuildingBlocks/OrderFlow.BuildingBlocks.csproj src/OrderFlow.BuildingBlocks/
COPY src/OrderFlow.Inventory.Worker/OrderFlow.Inventory.Worker.csproj src/OrderFlow.Inventory.Worker/
RUN dotnet restore src/OrderFlow.Inventory.Worker/OrderFlow.Inventory.Worker.csproj

COPY src/OrderFlow.BuildingBlocks/ src/OrderFlow.BuildingBlocks/
COPY src/OrderFlow.Inventory.Worker/ src/OrderFlow.Inventory.Worker/
RUN dotnet publish src/OrderFlow.Inventory.Worker/OrderFlow.Inventory.Worker.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- Etapa 2: solo el runtime de ASP.NET sobre Alpine (~110 MB) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
# Usuario sin privilegios que trae la imagen oficial.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Inventory.Worker.dll"]
