FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# global.json の sdk.workloadVersion pin はリポジトリ全体の単一の真実なので消さず
# Docker にも含めるが、この ReferenceServer は MAUI を使わない素の net10.0 で
# workload は本当に不要。SDK の workload resolver は global.json の pin を
# すべての `dotnet` 実行で強制し、未インストールだと restore/publish が
# "Workload version <x> ... was not found" で失敗する。pin を満たすためだけに
# `dotnet workload restore` するのは無駄なので、pin を強制している workload
# resolver 自体を無効化する。workload を import しない net10.0 では解決対象が
# 無く無害。この env を設定しない MAUI ビルドは従来どおり pin される。
ENV MSBuildEnableWorkloadResolver=false
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
