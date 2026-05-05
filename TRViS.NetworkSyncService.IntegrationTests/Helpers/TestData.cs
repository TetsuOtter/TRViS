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
}
