FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restauración por capas: primero solo los .csproj
COPY src/Web/Frontend.Contracts/UltimaMilla.Frontend.Contracts.csproj src/Web/Frontend.Contracts/
COPY src/Web/Frontend.Infrastructure/UltimaMilla.Frontend.Infrastructure.csproj src/Web/Frontend.Infrastructure/
COPY src/Web/SeguimientoPublico/UltimaMilla.SeguimientoPublico.csproj src/Web/SeguimientoPublico/
RUN dotnet restore src/Web/SeguimientoPublico/UltimaMilla.SeguimientoPublico.csproj

COPY src/Web/Frontend.Contracts/ src/Web/Frontend.Contracts/
COPY src/Web/Frontend.Infrastructure/ src/Web/Frontend.Infrastructure/
COPY src/Web/SeguimientoPublico/ src/Web/SeguimientoPublico/

WORKDIR /source/src/Web/SeguimientoPublico
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Es Blazor WebAssembly puro (sin servidor anfitrión): basta con servir los archivos estáticos.
FROM nginx:1.27-alpine AS final
COPY src/Web/SeguimientoPublico/docker/nginx.conf /etc/nginx/conf.d/default.conf
# Genera wwwroot/appsettings.json al arrancar con la URL de la API (variable API_BASE_URL)
COPY src/Web/SeguimientoPublico/docker/40-appsettings.sh /docker-entrypoint.d/40-appsettings.sh
RUN chmod +x /docker-entrypoint.d/40-appsettings.sh
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
