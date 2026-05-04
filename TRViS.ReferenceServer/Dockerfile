FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore TRViS.ReferenceServer/TRViS.ReferenceServer.csproj
RUN dotnet publish TRViS.ReferenceServer/TRViS.ReferenceServer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
# curl を追加してヘルスチェックに使用
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ENV PORT=8080
EXPOSE 8080
HEALTHCHECK --interval=3s --timeout=5s --start-period=20s --retries=15 \
  CMD curl -sf http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "TRViS.ReferenceServer.dll"]
