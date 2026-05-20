FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json は sdk.workloadVersion (=.NET MAUI workload set) をリポジトリ全体で
# ピン留めしており、これは開発環境と MAUI ビルド/CD ジョブの workload バージョンを
# 一致させる単一の真実なので消さない・Docker からも除外しない。一方でこの
# IntegrationTests / ReferenceServer は MAUI を一切使わない素の net10.0 で、
# workload は本当に不要。SDK の workload resolver は global.json の
# workloadVersion を「すべての」`dotnet` 実行で強制するため、放置すると
# "Workload version <x>, which was specified in /src/global.json, was not found"
# で restore/build/test が必ず失敗する。pin を満たすためだけに
# `dotnet workload restore` するのは不要な CO/CI 時間の浪費。代わりに
# MSBuildEnableWorkloadResolver=false で workload resolver 自体を無効化する
# (= pin を強制している当の機構を切る)。workload を 1 つも import しない
# net10.0 プロジェクトでは解決すべき workload provided import が無いので無害。
# global.json は残るので、この env を設定しない MAUI ビルドは従来どおり pin される。
ENV MSBuildEnableWorkloadResolver=false

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
COPY TRViS.Core/TRViS.Core.csproj \
     TRViS.Core/

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
