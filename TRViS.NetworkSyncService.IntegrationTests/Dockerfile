FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# プロジェクトファイルを先にコピーして restore をキャッシュ
COPY TRViS.NetworkSyncService.IntegrationTests/TRViS.NetworkSyncService.IntegrationTests.csproj \
     TRViS.NetworkSyncService.IntegrationTests/
COPY TRViS.NetworkSyncService/TRViS.NetworkSyncService.csproj \
     TRViS.NetworkSyncService/
COPY TRViS.ReferenceServer/TRViS.ReferenceServer.csproj \
     TRViS.ReferenceServer/
COPY TRViS.IO.ILoader/TRViS.IO.ILoader.csproj \
     TRViS.IO.ILoader/
COPY TRViS.ILocationService/TRViS.ILocationService.csproj \
     TRViS.ILocationService/

RUN dotnet restore TRViS.NetworkSyncService.IntegrationTests/TRViS.NetworkSyncService.IntegrationTests.csproj

# ソースコードを全コピー
COPY . .

# テスト実行エントリーポイント
# 終了コードがテスト結果を示す (0=全テスト成功)
ENTRYPOINT [ \
  "dotnet", "test", \
  "TRViS.NetworkSyncService.IntegrationTests/TRViS.NetworkSyncService.IntegrationTests.csproj", \
  "--no-restore", \
  "--configuration", "Debug", \
  "--verbosity", "normal", \
  "--logger", "trx;LogFileName=results.trx", \
  "--results-directory", "/test-results" \
]
