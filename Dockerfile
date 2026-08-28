FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenHealthMCP.csproj ./
RUN dotnet restore OpenHealthMCP.csproj

COPY . ./
RUN dotnet publish OpenHealthMCP.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /var/lib/openhealthmcp/garmin \
    && chown -R app:app /var/lib/openhealthmcp

COPY --from=build --chown=app:app /app/publish ./

USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    GARMIN_SESSION_PATH=/var/lib/openhealthmcp/garmin/token.json
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpenHealthMCP.dll"]