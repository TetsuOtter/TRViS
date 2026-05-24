using System.ComponentModel;

using TRViS.IO.Models;
using TRViS.OriginalTimetable.Controls;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable.Resources;

// V1 Modern Classic - タブレット幅 (>=768pt) レイアウト。
// 共有 OriginalTimetableViewModel をモデルとして使い、
// ActiveTrain.Rows / VM の markers/memos/curIdxOverride を読みながら描画。
//
// 注: TRViS.DTAC.Logic の VerticalTimetableViewPresenter は location/run の
// state machine 専用で、ここで必要な静的リスト描画には適合しないため流用せず、
// ActiveTrain.Rows を直接列挙する。
internal sealed class V1TabletLayout : ContentView
{
	private const int TabletColMin_PunCol_W = 48;       // 分
	private const int TabletColMin_TimeCol_W = 100;     // 着 / 発
	private const int TabletColMin_PlatCol_W = 64;      // 線
	private const int TabletColMin_LimitCol_W = 56;     // 制限

	private readonly OriginalTimetableViewModel _vm;
	private readonly Grid _root;
	private readonly Grid _header;       // 列車番号など
	private readonly Grid _listHeader;   // 列ヘッダ (分/停車場/着/発/線/制限)
	private readonly VerticalStackLayout _rowsHost;
	private readonly ScrollView _scroll;
	private readonly MemoSheet _memoSheet;

	// 現在描画中の TrainData。Rows null の場合は描画スキップ。
	private TrainData? _renderedTrain;

	public V1TabletLayout()
		: this(InstanceManager.OriginalTimetableViewModel) { }

	public V1TabletLayout(OriginalTimetableViewModel vm)
	{
		_vm = vm;
		BindingContext = vm;

		_header = BuildTrainHeader();
		_listHeader = BuildListHeader();
		_rowsHost = new VerticalStackLayout { Spacing = 0 };

		_scroll = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Spacing = 0,
				Children = { _listHeader, _rowsHost },
			},
		};

		_root = new Grid
		{
			RowDefinitions =
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star),
			},
			Padding = new Thickness(14, 12),
			RowSpacing = 0,
		};
		_root.Add(_header, 0, 0);
		_root.Add(_scroll, 0, 1);

		_memoSheet = new MemoSheet(vm);
		var overlay = new Grid();
		overlay.Children.Add(_root);
		overlay.Children.Add(_memoSheet);

		Content = overlay;

		_vm.PropertyChanged += OnVmPropertyChanged;
		Rebuild();
	}

	~V1TabletLayout()
	{
		// TODO: 幅切替で新インスタンスが作られる度に singleton VM の購読が残る。
		// 次スライスで Page.OnDisappearing 経由の明示的な解除に置き換える。
		_vm.PropertyChanged -= OnVmPropertyChanged;
	}

	private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// ActiveTrain か versions のいずれかが変わったら再描画。
		switch (e.PropertyName)
		{
			case nameof(OriginalTimetableViewModel.ActiveTrain):
			case nameof(OriginalTimetableViewModel.MarkersVersion):
			case nameof(OriginalTimetableViewModel.MemosVersion):
			case nameof(OriginalTimetableViewModel.NoteOpenVersion):
			case nameof(OriginalTimetableViewModel.CurIdxVersion):
			case nameof(OriginalTimetableViewModel.ShowPasses):
				MainThread.BeginInvokeOnMainThread(Rebuild);
				break;
		}
	}

	private Grid BuildTrainHeader()
	{
		var typeChip = new Border
		{
			Padding = new Thickness(10, 4),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			Content = new Label
			{
				FontSize = 17,
				FontAttributes = FontAttributes.Bold,
				VerticalTextAlignment = TextAlignment.Center,
			},
		};
		typeChip.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_Accent_Light"],
			(Color)Application.Current.Resources["OT_Accent_Dark"]);

		// typeChip の中の Label に SpeedType をバインド (今はプレースホルダ - Rebuild で設定)
		var trainNumberLabel = new Label
		{
			FontSize = 30,
			FontAttributes = FontAttributes.Bold,
		};
		trainNumberLabel.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);

		var carCountLabel = new Label
		{
			FontSize = 16,
			HorizontalTextAlignment = TextAlignment.End,
		};
		carCountLabel.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);

		var maxSpeedLabel = new Label
		{
			FontSize = 16,
			HorizontalTextAlignment = TextAlignment.End,
		};
		maxSpeedLabel.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_Fg_Light"],
			(Color)Application.Current.Resources["OT_Fg_Dark"]);

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Auto),
			},
			ColumnSpacing = 12,
			Padding = new Thickness(0, 10, 0, 12),
		};
		grid.Add(typeChip, 0, 0);
		grid.Add(trainNumberLabel, 1, 0);
		grid.Add(carCountLabel, 2, 0);
		grid.Add(maxSpeedLabel, 3, 0);

		// 識別用にタグ付け
		typeChip.AutomationId = "OriginalTimetable.V1.Header.TypeChip";
		trainNumberLabel.AutomationId = "OriginalTimetable.V1.Header.TrainNumber";
		carCountLabel.AutomationId = "OriginalTimetable.V1.Header.CarCount";
		maxSpeedLabel.AutomationId = "OriginalTimetable.V1.Header.MaxSpeed";
		return grid;
	}

	private Grid BuildListHeader()
	{
		var grid = new Grid
		{
			ColumnDefinitions = MakeColumnDefinitions(),
			Padding = new Thickness(0, 6),
		};
		grid.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_BgSoft_Light"],
			(Color)Application.Current.Resources["OT_BgSoft_Dark"]);

		Label TH(string text, TextAlignment align)
		{
			var lbl = new Label
			{
				Text = text,
				FontSize = 13,
				FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = align,
				VerticalTextAlignment = TextAlignment.Center,
			};
			lbl.SetAppThemeColor(Label.TextColorProperty,
				(Color)Application.Current!.Resources["OT_Muted_Light"],
				(Color)Application.Current.Resources["OT_Muted_Dark"]);
			return lbl;
		}

		grid.Add(TH("分", TextAlignment.Center), 0, 0);
		grid.Add(TH("停車場", TextAlignment.Start), 1, 0);
		grid.Add(TH("着", TextAlignment.Center), 2, 0);
		grid.Add(TH("発", TextAlignment.Center), 3, 0);
		grid.Add(TH("線", TextAlignment.Center), 4, 0);
		grid.Add(TH("制限", TextAlignment.Center), 5, 0);
		return grid;
	}

	private ColumnDefinitionCollection MakeColumnDefinitions() => new()
	{
		new ColumnDefinition(new GridLength(TabletColMin_PunCol_W)),
		new ColumnDefinition(GridLength.Star),
		new ColumnDefinition(new GridLength(TabletColMin_TimeCol_W)),
		new ColumnDefinition(new GridLength(TabletColMin_TimeCol_W)),
		new ColumnDefinition(new GridLength(TabletColMin_PlatCol_W)),
		new ColumnDefinition(new GridLength(TabletColMin_LimitCol_W)),
	};

	private void Rebuild()
	{
		var train = _vm.ActiveTrain;
		_renderedTrain = train;

		// header の値更新
		UpdateTrainHeader(train);

		_rowsHost.Children.Clear();

		if (train?.Rows is not { Length: > 0 } rows)
			return;

		string trainId = train.Id;
		int curIdx = _vm.GetCurIdxOverride(trainId) ?? 0;

		// セクション境界判定用: 直前の RunOutLimit と現在の RunInLimit の不一致で
		// 区間切替とみなす。data.jsx の maxSpeeds[].fromIdx は DB には存在しないので
		// 派生計算で代用する。
		int? prevRunOutLimit = null;

		for (int i = 0; i < rows.Length; i++)
		{
			var row = rows[i];
			if (!_vm.ShowPasses && row.IsPass)
			{
				// 行を出さない場合でも prevRunOutLimit は更新しておく (連続性)
				prevRunOutLimit = row.RunOutLimit ?? prevRunOutLimit;
				continue;
			}

			// 区間切替: 最初の行は出さない (prototype と一致)
			if (i > 0 && row.RunInLimit is int curRunIn && prevRunOutLimit is int prev && curRunIn != prev)
			{
				_rowsHost.Children.Add(new SectionBreakHeader
				{
					SpeedKmh = curRunIn,
					SpeedClass = train.SpeedType,
				});
			}

			_rowsHost.Children.Add(BuildRow(train, row, i, curIdx));
			prevRunOutLimit = row.RunOutLimit ?? prevRunOutLimit;
		}
	}

	private void UpdateTrainHeader(TrainData? train)
	{
		var typeChip = (Border)_header.Children[0];
		var typeLabel = (Label)typeChip.Content!;
		var trainNumber = (Label)_header.Children[1];
		var carCount = (Label)_header.Children[2];
		var maxSpeed = (Label)_header.Children[3];

		typeLabel.Text = train?.SpeedType ?? "";
		typeLabel.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources["OT_AccentFg_Light"],
			(Color)Application.Current.Resources["OT_AccentFg_Dark"]);

		trainNumber.Text = train?.TrainNumber ?? "—";
		carCount.Text = train?.CarCount is int c ? $"{c}両" : "";
		maxSpeed.Text = train?.MaxSpeed is string ms && !string.IsNullOrEmpty(ms) ? $"{ms} km/h" : "";
	}

	private View BuildRow(TrainData train, TimetableRow row, int idx, int curIdx)
	{
		bool isCurrent = idx == curIdx;
		bool isPassed = idx < curIdx;
		bool isPass = row.IsPass;
		string trainId = train.Id;
		string rowId = row.Id;

		var marker = _vm.GetMarker(trainId, rowId);
		string memo = _vm.GetMemo(trainId, rowId);
		bool noteOpen = _vm.IsNoteOpen(trainId, rowId);
		bool hasNoteOrMemo = !string.IsNullOrWhiteSpace(row.Remarks) || !string.IsNullOrWhiteSpace(memo);

		// 行の中身グリッド
		var inner = new Grid
		{
			ColumnDefinitions = MakeColumnDefinitions(),
			Padding = new Thickness(0, 8),
			ColumnSpacing = 4,
		};

		var bgKey = isCurrent ? "OT_RowCurrent" : (idx % 2 == 1 ? "OT_RowAlt" : "OT_Bg");
		inner.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources[$"{bgKey}_Light"],
			(Color)Application.Current.Resources[$"{bgKey}_Dark"]);
		inner.Opacity = isPassed ? 0.45 : 1.0;

		// 分 (DriveTime)
		string runText = row.DriveTimeMM is int mm
			? $"{mm}:{(row.DriveTimeSS ?? 0):D2}"
			: "";
		inner.Add(MakeCenterCell(runText, muted: true, fontSize: 16), 0, 0);

		// 停車場
		inner.Add(BuildNameCell(row, marker, memo, hasNoteOrMemo, trainId, rowId, isCurrent), 1, 0);

		// 着 (Arrive)
		inner.Add(MakeCenterCell(FormatTime(row.ArriveTime, isPass), fontSize: 22, mono: true, muted: isPass), 2, 0);
		// 発 (Departure)
		inner.Add(MakeCenterCell(FormatTime(row.DepartureTime, isPass), fontSize: 22, mono: true, bold: true, muted: isPass), 3, 0);
		// 線 (Track)
		inner.Add(BuildTrackCell(row.TrackName), 4, 0);
		// 制限 (RunInLimit + RunOutLimit を併記)
		string limitText = (row.RunInLimit, row.RunOutLimit) switch
		{
			(int a, int b) when a == b => a.ToString(),
			(int a, int b) => $"{a}/{b}",
			(int a, null) => a.ToString(),
			(null, int b) => b.ToString(),
			_ => "",
		};
		inner.Add(MakeCenterCell(limitText, muted: true, fontSize: 16), 5, 0);

		// 行ラッパー: SwipeRow に Tap = Advance / Trailing = マーカー・メモ
		var swipeRow = new SwipeRow
		{
			RowContent = inner,
			TrailingActions = BuildTrailingActions(trainId, rowId, swipeRowAnchor: inner),
			AutomationId = $"OriginalTimetable.V1.Row.{idx}",
		};
		int idxCapture = idx;
		swipeRow.Tapped += (_, _) =>
		{
			if (_renderedTrain is null)
				return;
			int max = (_renderedTrain.Rows?.Length ?? 1) - 1;
			_vm.Advance(_renderedTrain.Id, max);
		};

		// 記事 (note) フォールド
		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(swipeRow);
		if (hasNoteOrMemo)
		{
			string noteText = string.IsNullOrWhiteSpace(memo)
				? row.Remarks ?? string.Empty
				: string.IsNullOrWhiteSpace(row.Remarks) ? memo : $"{row.Remarks}\n{memo}";

			var fold = new NoteFold
			{
				Text = noteText,
				IsOpen = noteOpen,
				AutomationId = $"OriginalTimetable.V1.Row.{idx}.Note",
			};
			fold.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(NoteFold.IsOpen))
				{
					// ユーザの折り畳み操作を VM に通知。
					_vm.ToggleNote(trainId, rowId);
				}
			};
			rowStack.Children.Add(fold);
		}

		// 行下罫
		var border = new Border
		{
			StrokeThickness = 0,
			Padding = 0,
			Content = rowStack,
		};
		return border;
	}

	private static Label MakeCenterCell(string text, bool muted = false, bool bold = false, bool mono = false, double fontSize = 18)
	{
		var lbl = new Label
		{
			Text = text,
			FontSize = fontSize,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
			FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
		};
		if (mono)
			lbl.FontFamily = "Courier New";
		lbl.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources[muted ? "OT_Muted_Light" : "OT_Fg_Light"],
			(Color)Application.Current.Resources[muted ? "OT_Muted_Dark" : "OT_Fg_Dark"]);
		return lbl;
	}

	private View BuildNameCell(TimetableRow row, MarkerKind marker, string memo, bool hasNoteOrMemo, string trainId, string rowId, bool isCurrent)
	{
		var name = new Label
		{
			Text = row.StationName,
			FontSize = 24,
			FontAttributes = FontAttributes.Bold,
			VerticalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		name.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current!.Resources[row.IsPass ? "OT_Muted_Light" : "OT_Fg_Light"],
			(Color)Application.Current.Resources[row.IsPass ? "OT_Muted_Dark" : "OT_Fg_Dark"]);

		var stack = new HorizontalStackLayout
		{
			Spacing = 8,
			Padding = new Thickness(8, 0, 0, 0),
			VerticalOptions = LayoutOptions.Center,
		};

		// 現在駅マーカー (左の縦バー)
		if (isCurrent)
		{
			var bar = new BoxView { WidthRequest = 6, HeightRequest = 22, CornerRadius = 2 };
			bar.SetAppThemeColor(BoxView.ColorProperty,
				(Color)Application.Current!.Resources["OT_Accent_Light"],
				(Color)Application.Current.Resources["OT_Accent_Dark"]);
			stack.Children.Add(bar);
		}
		stack.Children.Add(name);

		if (marker != MarkerKind.None)
			stack.Children.Add(MakeMarkerBadge(marker));

		if (!string.IsNullOrWhiteSpace(memo))
			stack.Children.Add(MakeMemoDot());

		if (hasNoteOrMemo)
			stack.Children.Add(MakeNoteToggleChip(trainId, rowId));

		return stack;
	}

	private static View MakeMarkerBadge(MarkerKind kind)
	{
		string iconChar = kind switch
		{
			MarkerKind.Flag => MaterialIcons.Flag,
			MarkerKind.Caution => MaterialIcons.Warning,
			MarkerKind.Star => MaterialIcons.Star,
			_ => string.Empty,
		};
		var (bgKey, fgKey) = kind switch
		{
			MarkerKind.Flag => ("OT_MarkerFlagBg", "OT_MarkerFlagFg"),
			MarkerKind.Caution => ("OT_MarkerCautionBg", "OT_MarkerCautionFg"),
			MarkerKind.Star => ("OT_MarkerStarBg", "OT_MarkerStarFg"),
			_ => ("OT_BgSoft_Light", "OT_Fg_Light"),
		};
		var badge = new Border
		{
			HeightRequest = 20,
			WidthRequest = 20,
			Padding = 0,
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
			BackgroundColor = (Color)Application.Current!.Resources[bgKey],
			Content = new Label
			{
				Text = iconChar,
				FontFamily = "MaterialIconsRegular",
				FontSize = 12,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
				TextColor = (Color)Application.Current.Resources[fgKey],
			},
		};
		return badge;
	}

	private static View MakeMemoDot()
	{
		var dot = new Border
		{
			HeightRequest = 14,
			WidthRequest = 14,
			Padding = 0,
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 },
			Content = new Label
			{
				Text = MaterialIcons.EditNote,
				FontFamily = "MaterialIconsRegular",
				FontSize = 10,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
			},
		};
		dot.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_Accent_Light"],
			(Color)Application.Current.Resources["OT_Accent_Dark"]);
		((Label)dot.Content!).SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current.Resources["OT_AccentFg_Light"],
			(Color)Application.Current.Resources["OT_AccentFg_Dark"]);
		return dot;
	}

	private View MakeNoteToggleChip(string trainId, string rowId)
	{
		bool open = _vm.IsNoteOpen(trainId, rowId);
		var chip = new Border
		{
			Padding = new Thickness(8, 4),
			StrokeThickness = 0.5,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
		};
		chip.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources[open ? "OT_AccentSoft_Light" : "OT_BgSoft_Light"],
			(Color)Application.Current.Resources[open ? "OT_AccentSoft_Dark" : "OT_BgSoft_Dark"]);
		chip.SetAppThemeColor(Border.StrokeProperty,
			(Color)Application.Current.Resources["OT_Rule_Light"],
			(Color)Application.Current.Resources["OT_Rule_Dark"]);

		var label = new Label
		{
			Text = "記事",
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
		};
		label.SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current.Resources[open ? "OT_AccentFgStrong_Light" : "OT_Muted_Light"],
			(Color)Application.Current.Resources[open ? "OT_AccentFgStrong_Dark" : "OT_Muted_Dark"]);
		chip.Content = label;

		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => _vm.ToggleNote(trainId, rowId);
		chip.GestureRecognizers.Add(tap);
		return chip;
	}

	private View BuildTrackCell(string? track)
	{
		if (string.IsNullOrWhiteSpace(track))
			return new Label { Text = "" };
		var b = new Border
		{
			Padding = new Thickness(8, 3),
			StrokeThickness = 0.5,
			MinimumWidthRequest = 36,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			Content = new Label
			{
				Text = track,
				FontSize = 16,
				FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = TextAlignment.Center,
			},
		};
		b.SetAppThemeColor(BackgroundColorProperty,
			(Color)Application.Current!.Resources["OT_PlatBg_Light"],
			(Color)Application.Current.Resources["OT_PlatBg_Dark"]);
		b.SetAppThemeColor(Border.StrokeProperty,
			(Color)Application.Current.Resources["OT_RuleStrong_Light"],
			(Color)Application.Current.Resources["OT_RuleStrong_Dark"]);
		((Label)b.Content!).SetAppThemeColor(Label.TextColorProperty,
			(Color)Application.Current.Resources["OT_PlatFg_Light"],
			(Color)Application.Current.Resources["OT_PlatFg_Dark"]);
		return b;
	}

	private static string FormatTime(TimeData? time, bool isPass)
	{
		if (time is null)
			return isPass ? "↓" : "—";
		// hm のみ (秒は省略)
		string h = time.Hour?.ToString("D2") ?? "";
		string m = time.Minute?.ToString("D2") ?? "";
		if (h.Length > 0 || m.Length > 0)
			return $"{h}:{m}";
		return time.Text ?? "";
	}

	private IList<View> BuildTrailingActions(string trainId, string rowId, View swipeRowAnchor)
	{
		// マーカー (Accent), メモ (Accent), クリア (Muted)
		Button MakeAction(string label, string bgKey, string fgKey, EventHandler onClick)
		{
			var b = new Button
			{
				Text = label,
				WidthRequest = 88,
				MinimumHeightRequest = 44,
				FontSize = 13,
				FontAttributes = FontAttributes.Bold,
			};
			b.SetAppThemeColor(Button.BackgroundColorProperty,
				(Color)Application.Current!.Resources[$"{bgKey}_Light"],
				(Color)Application.Current.Resources[$"{bgKey}_Dark"]);
			b.SetAppThemeColor(Button.TextColorProperty,
				(Color)Application.Current.Resources[$"{fgKey}_Light"],
				(Color)Application.Current.Resources[$"{fgKey}_Dark"]);
			b.Clicked += onClick;
			return b;
		}

		var marker = MakeAction("マーカー", "OT_WarnBorder", "OT_AccentFg", async (_, _) =>
		{
			var pop = new MarkerPopover(_vm);
			await pop.ShowAsync(swipeRowAnchor, trainId, rowId);
		});

		var memo = MakeAction("メモ", "OT_Accent", "OT_AccentFg", (_, _) =>
		{
			string stationName = ResolveStationName(rowId) ?? "";
			_memoSheet.Open(trainId, rowId, stationName);
		});

		var clear = MakeAction("クリア", "OT_Muted", "OT_AccentFg", (_, _) =>
		{
			_vm.ClearMarker(trainId, rowId);
			_vm.SetMemo(trainId, rowId, null);
		});

		return new List<View> { marker, memo, clear };
	}

	private string? ResolveStationName(string rowId)
	{
		if (_renderedTrain?.Rows is null)
			return null;
		foreach (var r in _renderedTrain.Rows)
			if (r.Id == rowId)
				return r.StationName;
		return null;
	}
}
