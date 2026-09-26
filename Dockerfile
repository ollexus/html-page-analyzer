FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

EXPOSE 8090

ENTRYPOINT []

CMD ["sh", "-c", "dotnet restore && dotnet build --no-restore && ASPNETCORE_HTTP_PORTS=8090 ASPNETCORE_HTTPS_PORTS= dotnet run --no-build --no-launch-profile"]
