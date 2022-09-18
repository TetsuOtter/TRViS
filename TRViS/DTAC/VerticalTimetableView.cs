using System.Collections.Specialized;
using System.ComponentModel;
using DependencyPropertyGenerator;

using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<IEnumerable<TimetableRow>>("TimetableRowList")]
[DependencyProperty<ColumnDefinitionCollection>("TimetableColumnDefinitions")]
public partial class VerticalTimetableView : Grid
{
	static readonly GridLength RowHeight = new(60);

	Dictionary<TimetableRow, VerticalTimetableRow> ModelUIRelation { get; set; } = new();

	partial void OnTimetableRowListChanged(IEnumerable<TimetableRow>? oldValue, IEnumerable<TimetableRow>? newValue)
	{
		if (oldValue is INotifyCollectionChanged vOld)
			vOld.CollectionChanged -= TimetableRowList_CollectionChanged;

		if (newValue is INotifyCollectionChanged v)
			v.CollectionChanged += TimetableRowList_CollectionChanged;

		SetRowViews();
	}

	private void TimetableRowList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		=> SetRowViews();

	void SetRowViews()
	{
		int? newCount = TimetableRowList?.Count();

		SetRowDefinitions(newCount);

		if (newCount is null || newCount <= 0)
		{
			ModelUIRelation.Clear();
			Children.Clear();
			return;
		}

		// TODO: Add / Remove等のイベントに応じて、最適な処理のみを実行するように書き直す
		// `newCount is null`チェックにより、TimetableRowListがNULLでないことは自明である
		Dictionary<TimetableRow, VerticalTimetableRow> NewModelUIRelation = new();
		int i = 0;
		foreach (var rowData in TimetableRowList!)
		{
			// ModelUIRelationには、最終的に「現時点でのTimetableRowListに存在しないView」のみが残る
			if (!ModelUIRelation.Remove(rowData, out VerticalTimetableRow? rowView) || rowView is null)
			{
				rowView = new();
				rowView.SetBinding(VerticalTimetableRow.ColumnDefinitionsProperty, new Binding()
				{
					Source = this,
					Path = nameof(TimetableColumnDefinitions)
				});
				rowView.BindingContext = rowData;

				Children.Add(rowView);
			}

			Grid.SetRow(rowView, i);

			NewModelUIRelation.Add(rowData, rowView);

			i++;
		}

		foreach (var rowViewToRemove in ModelUIRelation.Values)
			Children.Remove(rowViewToRemove);

		ModelUIRelation = NewModelUIRelation;
	}

	void SetRowDefinitions(int? newCount)
	{
		int currentCount = RowDefinitions.Count;
		HeightRequest = newCount * RowHeight.Value ?? 0;

		if (newCount is null)
			RowDefinitions.Clear();
		else if (currentCount < newCount)
		{
			for (int i = RowDefinitions.Count; i < newCount; i++)
				RowDefinitions.Add(new(RowHeight));
		}
		else if (newCount < currentCount)
		{
			for (int i = RowDefinitions.Count - 1; i >= newCount; i++)
				RowDefinitions.RemoveAt(i);
		}
	}
}
