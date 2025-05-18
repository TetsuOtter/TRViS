#if IOS || MACCATALYST
using System.Text;

using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.Controls;

public class MyMap : MyMapBase
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly Microsoft.Maui.Controls.Maps.Map map;

	public MyMap()
	{
		logger.Trace("MyMap Creating");

		Location centerLocation = new(35.681111, 139.766667); // Tokyo Station
		map = new(MapSpan.FromCenterAndRadius(centerLocation, Distance.FromMeters(500)))
		{
			IsShowingUser = false,
			MapType = MapType.Street,
		};

		// ボタン等を追加する準備
		AbsoluteLayout views = [
			map,
		];
		AbsoluteLayout.SetLayoutFlags(map, AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(map, new Rect(0, 0, 1, 1));
		Content = views;
		ZIndex = 1;

		logger.Trace("MyMap Created");
	}

	Circle? currentLocationCircle;
	public override void SetCurrentLocation(double latitude, double longitude, double accuracy_m)
	{
		logger.Trace("SetCurrentLocation(lat={0}, lon={1}, acc={2})", latitude, longitude, accuracy_m);
		if (currentLocationCircle is null)
		{
			logger.Warn("SetCurrentLocation() called before SetIsLocationServiceEnabled()");
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			currentLocationCircle.Center = new Location(latitude, longitude);
			currentLocationCircle.Radius = Distance.FromMeters(accuracy_m);
			map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(latitude, longitude), map.VisibleRegion?.Radius ?? Distance.FromMeters(500)));
		});
	}

	public override void SetIsLocationServiceEnabled(bool isEnabled)
	{
		logger.Trace("SetIsLocationServiceEnabled({0})", isEnabled);
		if (isEnabled)
		{
			if (currentLocationCircle is not null)
			{
				return;
			}
			currentLocationCircle = new()
			{
				Center = new Location(0, 0),
				Radius = Distance.FromMeters(0),
				StrokeColor = Colors.Blue,
				FillColor = Colors.Blue.WithAlpha(0.2f),
				StrokeWidth = 2,
			};
			map.MapElements.Add(currentLocationCircle);
		}
		else
		{
			if (currentLocationCircle is not null)
			{
				map.MapElements.Remove(currentLocationCircle);
				currentLocationCircle = null;
			}
		}
	}

	public override void SetTimetableRowList(TimetableRow[]? Rows)
	{
		logger.Trace("SetTimetableRowList({0})", Rows?.Length ?? 0);
		map.Pins.Clear();
		map.MapElements.Clear();
		if (Rows is null || Rows.Length == 0)
		{
			return;
		}
		Location? firstLocation = null;
		double firstLocationOnStationDetectRadius_m = 0;
		for (int i = 0; i < Rows.Length; i++)
		{
			TimetableRow row = Rows[i];
			if (row.Location.Latitude_deg is null || row.Location.Longitude_deg is null)
			{
				continue;
			}

			Location location = new(row.Location.Latitude_deg.Value, row.Location.Longitude_deg.Value);
			double onStationDetectRadius_m = row.Location.OnStationDetectRadius_m ?? StaLocationInfo.DefaultNearbyRadius_m;
			if (firstLocation is null)
			{
				firstLocation = location;
				firstLocationOnStationDetectRadius_m = onStationDetectRadius_m;
			}

			StringBuilder sb = new();
			sb.Append("Index: ").Append(i);
			sb.AppendLine();
			sb.Append("ID: ").Append(row.Id);
			sb.AppendLine();
			sb.Append("(lat:").Append(location.Latitude).Append(", lon:").Append(location.Longitude).Append(')');
			sb.AppendLine();
			sb.Append("Arr: ").Append(row.ArriveTime?.GetTimeString());
			sb.AppendLine();
			sb.Append("Dep: ").Append(row.DepartureTime?.GetTimeString());
			sb.AppendLine();
			sb.Append("Radius_m: ").Append(onStationDetectRadius_m);
			Pin pin = new()
			{
				Label = row.StationName,
				Address = sb.ToString(),
				Location = location,
				Type = PinType.Place,
			};
			map.Pins.Add(pin);

			Circle circle = new()
			{
				Center = location,
				Radius = Distance.FromMeters(onStationDetectRadius_m),
				StrokeColor = Colors.Red,
				FillColor = Colors.Red.WithAlpha(0.2f),
				StrokeWidth = 2,
			};
			map.MapElements.Add(circle);
		}
		if (firstLocation is not null)
		{
			double radius = Math.Max(firstLocationOnStationDetectRadius_m * 2, 500);
			map.MoveToRegion(MapSpan.FromCenterAndRadius(firstLocation, Distance.FromMeters(radius)));
		}
	}
}

#endif
