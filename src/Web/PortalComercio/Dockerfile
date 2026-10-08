FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies
COPY UltimaMilla.sln ./
COPY src/Web/PortalComercio.Client/UltimaMilla.PortalComercio.Client.csproj src/Web/PortalComercio.Client/
COPY src/Web/PortalComercio/UltimaMilla.PortalComercio.csproj src/Web/PortalComercio/

RUN dotnet restore src/Web/PortalComercio/UltimaMilla.PortalComercio.csproj

# Copy source code and publish
COPY src/Web/PortalComercio.Client/ src/Web/PortalComercio.Client/
COPY src/Web/PortalComercio/ src/Web/PortalComercio/

WORKDIR /source/src/Web/PortalComercio
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimaMilla.PortalComercio.dll"]
