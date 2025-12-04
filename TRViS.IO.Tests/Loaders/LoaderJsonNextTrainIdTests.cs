using System.Text;

using TRViS.IO.Models;

namespace TRViS.IO.Tests;

public class LoaderJsonNextTrainIdTests
{
	[Test]
	public async Task NextTrainId_DefaultBehavior_SetsNextTrainInList()
	{
		// When NextTrainId is not specified, it should be set to the next train in the list
		string json = """
		[
			{
				"Name": "WorkGroup01",
				"Works": [
					{
						"Name": "Work01",
						"Trains": [
							{
								"Id": "train1",
								"TrainNumber": "Train01",
								"Direction": 1,
								"TimetableRows": []
							},
							{
								"Id": "train2",
								"TrainNumber": "Train02",
								"Direction": 1,
								"TimetableRows": []
							},
							{
								"Id": "train3",
								"TrainNumber": "Train03",
								"Direction": 1,
								"TimetableRows": []
							}
						]
					}
				]
			}
		]
		""";

		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		LoaderJson loader = await LoaderJson.InitFromStreamAsync(stream, CancellationToken.None);

		IReadOnlyList<WorkGroup> workGroups = loader.GetWorkGroupList();
		IReadOnlyList<Work> works = loader.GetWorkList(workGroups[0].Id);
		IReadOnlyList<TrainData> trains = loader.GetTrainDataList(works[0].Id);

		Assert.That(trains, Has.Count.EqualTo(3));
		Assert.Multiple(() =>
		{
			// First train should have NextTrainId set to the second train
			Assert.That(trains[0].NextTrainId, Is.EqualTo("train2"));
			// Second train should have NextTrainId set to the third train
			Assert.That(trains[1].NextTrainId, Is.EqualTo("train3"));
			// Last train should have NextTrainId set to null
			Assert.That(trains[2].NextTrainId, Is.Null);
		});
	}

	[Test]
	public async Task NextTrainId_ExplicitEmpty_SetsNull()
	{
		// When NextTrainId is explicitly set to empty string, it should be set to null (no next train button)
		string json = """
		[
			{
				"Name": "WorkGroup01",
				"Works": [
					{
						"Name": "Work01",
						"Trains": [
							{
								"Id": "train1",
								"TrainNumber": "Train01",
								"Direction": 1,
								"NextTrainId": "",
								"TimetableRows": []
							},
							{
								"Id": "train2",
								"TrainNumber": "Train02",
								"Direction": 1,
								"TimetableRows": []
							}
						]
					}
				]
			}
		]
		""";

		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		LoaderJson loader = await LoaderJson.InitFromStreamAsync(stream, CancellationToken.None);

		IReadOnlyList<WorkGroup> workGroups = loader.GetWorkGroupList();
		IReadOnlyList<Work> works = loader.GetWorkList(workGroups[0].Id);
		IReadOnlyList<TrainData> trains = loader.GetTrainDataList(works[0].Id);

		Assert.That(trains, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			// First train has NextTrainId explicitly set to empty string, so it should be null
			Assert.That(trains[0].NextTrainId, Is.Null);
			// Second train (last in list) has NextTrainId not set, so it should be null (default behavior for last train)
			Assert.That(trains[1].NextTrainId, Is.Null);
		});
	}

	[Test]
	public async Task NextTrainId_ExplicitId_SetsSpecifiedId()
	{
		// When NextTrainId is explicitly set to a train ID, it should be used
		string json = """
		[
			{
				"Name": "WorkGroup01",
				"Works": [
					{
						"Name": "Work01",
						"Trains": [
							{
								"Id": "train1",
								"TrainNumber": "Train01",
								"Direction": 1,
								"NextTrainId": "train3",
								"TimetableRows": []
							},
							{
								"Id": "train2",
								"TrainNumber": "Train02",
								"Direction": 1,
								"NextTrainId": "train1",
								"TimetableRows": []
							},
							{
								"Id": "train3",
								"TrainNumber": "Train03",
								"Direction": 1,
								"TimetableRows": []
							}
						]
					}
				]
			}
		]
		""";

		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		LoaderJson loader = await LoaderJson.InitFromStreamAsync(stream, CancellationToken.None);

		IReadOnlyList<WorkGroup> workGroups = loader.GetWorkGroupList();
		IReadOnlyList<Work> works = loader.GetWorkList(workGroups[0].Id);
		IReadOnlyList<TrainData> trains = loader.GetTrainDataList(works[0].Id);

		Assert.That(trains, Has.Count.EqualTo(3));
		Assert.Multiple(() =>
		{
			// First train has NextTrainId explicitly set to "train3"
			Assert.That(trains[0].NextTrainId, Is.EqualTo("train3"));
			// Second train has NextTrainId explicitly set to "train1"
			Assert.That(trains[1].NextTrainId, Is.EqualTo("train1"));
			// Third train (last in list) has NextTrainId not set, so it should be null (default behavior for last train)
			Assert.That(trains[2].NextTrainId, Is.Null);
		});
	}

	[Test]
	public async Task NextTrainId_MixedScenarios()
	{
		// Test a mix of default, empty, and explicit NextTrainId values
		string json = """
		[
			{
				"Name": "WorkGroup01",
				"Works": [
					{
						"Name": "Work01",
						"Trains": [
							{
								"Id": "train1",
								"TrainNumber": "Train01",
								"Direction": 1,
								"TimetableRows": []
							},
							{
								"Id": "train2",
								"TrainNumber": "Train02",
								"Direction": 1,
								"NextTrainId": "",
								"TimetableRows": []
							},
							{
								"Id": "train3",
								"TrainNumber": "Train03",
								"Direction": 1,
								"NextTrainId": "train1",
								"TimetableRows": []
							},
							{
								"Id": "train4",
								"TrainNumber": "Train04",
								"Direction": 1,
								"TimetableRows": []
							}
						]
					}
				]
			}
		]
		""";

		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		LoaderJson loader = await LoaderJson.InitFromStreamAsync(stream, CancellationToken.None);

		IReadOnlyList<WorkGroup> workGroups = loader.GetWorkGroupList();
		IReadOnlyList<Work> works = loader.GetWorkList(workGroups[0].Id);
		IReadOnlyList<TrainData> trains = loader.GetTrainDataList(works[0].Id);

		Assert.That(trains, Has.Count.EqualTo(4));
		Assert.Multiple(() =>
		{
			// First train: not set, should default to next train (train2)
			Assert.That(trains[0].NextTrainId, Is.EqualTo("train2"));
			// Second train: explicitly empty, should be null
			Assert.That(trains[1].NextTrainId, Is.Null);
			// Third train: explicitly set to "train1"
			Assert.That(trains[2].NextTrainId, Is.EqualTo("train1"));
			// Fourth train: last in list and not set, should be null
			Assert.That(trains[3].NextTrainId, Is.Null);
		});
	}
}
