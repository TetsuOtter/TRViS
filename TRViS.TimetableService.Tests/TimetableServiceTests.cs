namespace TRViS.TimetableService.Tests;

public class TimetableServiceTests
{
	[Fact]
	public void SetTrainData_ShouldStoreTrainData()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem { Id = "train1", TrainNumber = "123" };

		// Act
		service.SetTrainData(trainData);
		var result = service.GetTrainData("train1");

		// Assert
		Assert.NotNull(result);
		Assert.Equal("train1", result.Id);
		Assert.Equal("123", result.TrainNumber);
	}

	[Fact]
	public void GetTrainData_WithInvalidId_ShouldReturnNull()
	{
		// Arrange
		var service = new TimetableService();

		// Act
		var result = service.GetTrainData("nonexistent");

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void RemoveTrainData_ShouldRemoveData()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem { Id = "train1", TrainNumber = "123" };
		service.SetTrainData(trainData);

		// Act
		var removed = service.RemoveTrainData("train1");
		var result = service.GetTrainData("train1");

		// Assert
		Assert.True(removed);
		Assert.Null(result);
	}

	[Fact]
	public void GetAllTrainData_ShouldReturnAllData()
	{
		// Arrange
		var service = new TimetableService();
		service.SetTrainData(new TrainDataItem { Id = "train1", TrainNumber = "123" });
		service.SetTrainData(new TrainDataItem { Id = "train2", TrainNumber = "456" });

		// Act
		var result = service.GetAllTrainData();

		// Assert
		Assert.Equal(2, result.Count);
	}

	[Fact]
	public void InsertTimetableRow_ShouldInsertAtPosition()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem
		{
			Id = "train1",
			Rows = new List<TimetableRowItem>
			{
				new TimetableRowItem { Id = "row1", StationName = "Station1" },
				new TimetableRowItem { Id = "row3", StationName = "Station3" }
			}
		};
		service.SetTrainData(trainData);
		var newRow = new TimetableRowItem { Id = "row2", StationName = "Station2" };

		// Act
		service.InsertTimetableRow("train1", 1, newRow);
		var rows = service.GetTimetableRows("train1");

		// Assert
		Assert.Equal(3, rows.Count);
		Assert.Equal("row1", rows[0].Id);
		Assert.Equal("row2", rows[1].Id);
		Assert.Equal("row3", rows[2].Id);
	}

	[Fact]
	public void RemoveTimetableRow_ShouldRemoveRow()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem
		{
			Id = "train1",
			Rows = new List<TimetableRowItem>
			{
				new TimetableRowItem { Id = "row1", StationName = "Station1" },
				new TimetableRowItem { Id = "row2", StationName = "Station2" }
			}
		};
		service.SetTrainData(trainData);

		// Act
		var removed = service.RemoveTimetableRow("train1", "row1");
		var rows = service.GetTimetableRows("train1");

		// Assert
		Assert.True(removed);
		Assert.Single(rows);
		Assert.Equal("row2", rows[0].Id);
	}

	[Fact]
	public void UpdateTimetableRow_ShouldUpdateRow()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem
		{
			Id = "train1",
			Rows = new List<TimetableRowItem>
			{
				new TimetableRowItem { Id = "row1", StationName = "Station1" }
			}
		};
		service.SetTrainData(trainData);
		var updatedRow = new TimetableRowItem { Id = "row1", StationName = "UpdatedStation" };

		// Act
		service.UpdateTimetableRow("train1", "row1", updatedRow);
		var row = service.GetTimetableRow("train1", "row1");

		// Assert
		Assert.NotNull(row);
		Assert.Equal("UpdatedStation", row.StationName);
	}

	[Fact]
	public void Clear_ShouldRemoveAllData()
	{
		// Arrange
		var service = new TimetableService();
		service.SetTrainData(new TrainDataItem { Id = "train1" });
		service.SetTrainData(new TrainDataItem { Id = "train2" });

		// Act
		service.Clear();
		var result = service.GetAllTrainData();

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void SetTrainData_WithNullId_ShouldThrowException()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem { Id = "" };

		// Act & Assert
		Assert.Throws<ArgumentException>(() => service.SetTrainData(trainData));
	}

	[Fact]
	public void InsertTimetableRow_WithInvalidPosition_ShouldThrowException()
	{
		// Arrange
		var service = new TimetableService();
		var trainData = new TrainDataItem { Id = "train1", Rows = new List<TimetableRowItem>() };
		service.SetTrainData(trainData);
		var newRow = new TimetableRowItem { Id = "row1", StationName = "Station1" };

		// Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => service.InsertTimetableRow("train1", 10, newRow));
	}
}
