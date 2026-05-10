namespace TRViS.NetworkSyncService.IntegrationTests.Helpers;

/// <summary>
/// 統合テストで使用するサンプルデータ定数。
/// JSON フォーマットは JsonModels.WorkGroupData の構造に準拠している。
/// </summary>
public static class TestData
{
	public const string WorkGroupId = "wg-integ-test-1";
	public const string WorkId = "w-integ-test-1";
	public const string TrainId = "t-integ-test-1";
	public const string TrainId2 = "t-integ-test-2";

	/// <summary>
	/// All スコープ配信用: WorkGroupData[] の JSON。
	/// WorkGroup > Work > Train の階層構造を持つ。
	/// </summary>
	public static readonly string AllScopeJson = $$"""
		[
		  {
		    "Id": "{{WorkGroupId}}",
		    "Name": "統合テスト用 WorkGroup",
		    "DBVersion": 1,
		    "Works": [
		      {
		        "Id": "{{WorkId}}",
		        "Name": "統合テスト用 Work",
		        "AffectDate": "20240101",
		        "Trains": [
		          {
		            "Id": "{{TrainId}}",
		            "TrainNumber": "T-001",
		            "Direction": 1,
		            "TimetableRows": [
		              {
		                "StationName": "テスト駅A",
		                "Location_m": 0.0,
		                "Longitude_deg": 135.0,
		                "Latitude_deg": 35.0,
		                "OnStationDetectRadius_m": 300.0,
		                "Departure": "10:00:00"
		              },
		              {
		                "StationName": "テスト駅B",
		                "Location_m": 1000.0,
		                "Longitude_deg": 135.01,
		                "Latitude_deg": 35.01,
		                "OnStationDetectRadius_m": 300.0,
		                "Arrive": "10:10:00",
		                "Departure": "10:12:00"
		              },
		              {
		                "StationName": "テスト駅C",
		                "Location_m": 2000.0,
		                "Longitude_deg": 135.02,
		                "Latitude_deg": 35.02,
		                "OnStationDetectRadius_m": 300.0,
		                "Arrive": "10:20:00"
		              }
		            ]
		          },
		          {
		            "Id": "{{TrainId2}}",
		            "TrainNumber": "T-002",
		            "Direction": -1,
		            "TimetableRows": [
		              {
		                "StationName": "テスト駅C",
		                "Location_m": 2000.0,
		                "Longitude_deg": 135.02,
		                "Latitude_deg": 35.02,
		                "OnStationDetectRadius_m": 300.0,
		                "Departure": "11:00:00"
		              },
		              {
		                "StationName": "テスト駅A",
		                "Location_m": 0.0,
		                "Longitude_deg": 135.0,
		                "Latitude_deg": 35.0,
		                "OnStationDetectRadius_m": 300.0,
		                "Arrive": "11:20:00"
		              }
		            ]
		          }
		        ]
		      }
		    ]
		  }
		]
		""";

	/// <summary>
	/// WorkGroup スコープ配信用: 単一 WorkGroupData の JSON。
	/// </summary>
	public static readonly string WorkGroupScopeJson = $$"""
		{
		  "Id": "{{WorkGroupId}}",
		  "Name": "統合テスト用 WorkGroup (更新)",
		  "DBVersion": 2,
		  "Works": []
		}
		""";

	/// <summary>
	/// Work スコープ配信用: 単一 WorkData の JSON。
	/// </summary>
	public static readonly string WorkScopeJson = $$"""
		{
		  "Id": "{{WorkId}}",
		  "Name": "統合テスト用 Work (更新)",
		  "AffectDate": "20240201",
		  "Trains": []
		}
		""";

	/// <summary>
	/// Train スコープ配信用: 単一 TrainData の JSON。
	/// </summary>
	public static readonly string TrainScopeJson = $$"""
		{
		  "Id": "{{TrainId}}",
		  "TrainNumber": "T-001-Updated",
		  "Direction": 1,
		  "TimetableRows": [
		    {
		      "StationName": "テスト駅A",
		      "Location_m": 0.0,
		      "Longitude_deg": 135.0,
		      "Latitude_deg": 35.0,
		      "OnStationDetectRadius_m": 300.0,
		      "Departure": "10:00:00"
		    },
		    {
		      "StationName": "テスト駅B (更新)",
		      "Location_m": 1500.0,
		      "Longitude_deg": 135.015,
		      "Latitude_deg": 35.015,
		      "OnStationDetectRadius_m": 300.0,
		      "Arrive": "10:15:00"
		    }
		  ]
		}
		""";

	/// <summary>
	/// Train スコープ配信用 (リアルタイム編集): TrainNumber は維持しつつ、行の TrackName を変更する。
	/// AC-1 (TimetableRow の番線変更がリアルタイム反映される) のテストで使う。
	/// </summary>
	public const string UpdatedTrackName = "TRACK-X-UPDATED";
	public static readonly string TrainScopeJson_TrackNameOnly = $$"""
		{
		  "Id": "{{TrainId}}",
		  "TrainNumber": "T-001",
		  "Direction": 1,
		  "TimetableRows": [
		    {
		      "StationName": "テスト駅A",
		      "Location_m": 0.0,
		      "Longitude_deg": 135.0,
		      "Latitude_deg": 35.0,
		      "OnStationDetectRadius_m": 300.0,
		      "Departure": "10:00:00",
		      "TrackName": "{{UpdatedTrackName}}"
		    },
		    {
		      "StationName": "テスト駅B",
		      "Location_m": 1000.0,
		      "Longitude_deg": 135.01,
		      "Latitude_deg": 35.01,
		      "OnStationDetectRadius_m": 300.0,
		      "Arrive": "10:10:00",
		      "Departure": "10:12:00"
		    },
		    {
		      "StationName": "テスト駅C",
		      "Location_m": 2000.0,
		      "Longitude_deg": 135.02,
		      "Latitude_deg": 35.02,
		      "OnStationDetectRadius_m": 300.0,
		      "Arrive": "10:20:00"
		    }
		  ]
		}
		""";

	/// <summary>
	/// Work スコープ配信用 (フル): 配下の Trains 配列を含む完全な Work データ。
	/// AC-2 / AC-5 の検証で、配下の Trains キャッシュが再構築されることを確認する。
	/// </summary>
	public static readonly string WorkScopeJsonFull = $$"""
		{
		  "Id": "{{WorkId}}",
		  "Name": "統合テスト用 Work (フル更新)",
		  "AffectDate": "20240301",
		  "Trains": [
		    {
		      "Id": "{{TrainId}}",
		      "TrainNumber": "T-001-Work",
		      "Direction": 1,
		      "TimetableRows": [
		        {
		          "StationName": "テスト駅A",
		          "Location_m": 0.0,
		          "Longitude_deg": 135.0,
		          "Latitude_deg": 35.0,
		          "OnStationDetectRadius_m": 300.0,
		          "Departure": "10:00:00"
		        }
		      ]
		    },
		    {
		      "Id": "{{TrainId2}}",
		      "TrainNumber": "T-002-Work",
		      "Direction": -1,
		      "TimetableRows": [
		        {
		          "StationName": "テスト駅C",
		          "Location_m": 2000.0,
		          "Longitude_deg": 135.02,
		          "Latitude_deg": 35.02,
		          "OnStationDetectRadius_m": 300.0,
		          "Departure": "11:00:00"
		        }
		      ]
		    }
		  ]
		}
		""";

	/// <summary>
	/// Train スコープ配信用 (Color 明示指定): TrainData.Color (=路線色) を含む。
	/// JsonModelsConverter が Color を LineColor_RGB に変換することを検証する。
	/// </summary>
	public static readonly string TrainScopeJson_WithColor = $$"""
		{
		  "Id": "{{TrainId}}",
		  "TrainNumber": "T-001",
		  "Direction": 1,
		  "Color": "FF0000",
		  "TimetableRows": [
		    {
		      "StationName": "テスト駅A",
		      "Location_m": 0.0,
		      "Longitude_deg": 135.0,
		      "Latitude_deg": 35.0,
		      "OnStationDetectRadius_m": 300.0,
		      "Departure": "10:00:00"
		    }
		  ]
		}
		""";

	/// <summary>
	/// 横型時刻表 (ETrainTimetable) を含む Work スコープ配信用 JSON。
	/// "hello" を base64 化した内容と、ContentType=2 (PNG 想定) を持つ。
	/// </summary>
	public const string ETrainTimetableContentBase64 = "aGVsbG8="; // "hello"
	public const int ETrainTimetableContentTypePng = 2;
	public static readonly byte[] ETrainTimetableContentBytes = [0x68, 0x65, 0x6C, 0x6C, 0x6F];
	public static readonly string WorkScopeJsonWithETrainTimetable = $$"""
		{
		  "Id": "{{WorkId}}",
		  "Name": "横型時刻表付き Work",
		  "AffectDate": "20240501",
		  "HasETrainTimetable": true,
		  "ETrainTimetableContentType": {{ETrainTimetableContentTypePng}},
		  "ETrainTimetableContent": "{{ETrainTimetableContentBase64}}",
		  "Trains": []
		}
		""";

	/// <summary>
	/// WorkGroup スコープ配信用 (フル): 配下の Works/Trains 構造を含む完全な WorkGroup データ。
	/// AC-3 / AC-5 の検証で、配下の Works/Trains キャッシュが再構築されることを確認する。
	/// </summary>
	public static readonly string WorkGroupScopeJsonFull = $$"""
		{
		  "Id": "{{WorkGroupId}}",
		  "Name": "統合テスト用 WorkGroup (フル更新)",
		  "DBVersion": 3,
		  "Works": [
		    {
		      "Id": "{{WorkId}}",
		      "Name": "統合テスト用 Work (WG経由)",
		      "AffectDate": "20240401",
		      "Trains": [
		        {
		          "Id": "{{TrainId}}",
		          "TrainNumber": "T-001-WG",
		          "Direction": 1,
		          "TimetableRows": [
		            {
		              "StationName": "テスト駅A",
		              "Location_m": 0.0,
		              "Longitude_deg": 135.0,
		              "Latitude_deg": 35.0,
		              "OnStationDetectRadius_m": 300.0,
		              "Departure": "10:00:00"
		            }
		          ]
		        },
		        {
		          "Id": "{{TrainId2}}",
		          "TrainNumber": "T-002-WG",
		          "Direction": -1,
		          "TimetableRows": []
		        }
		      ]
		    }
		  ]
		}
		""";
}
