namespace TRViS.IO.Models;

public record Work(
	string Id,
	string WorkGroupId,
	string Name,
	DateOnly? AffectDate = null,
	int? AffixContentType = null,
	byte[]? AffixContent = null,
	string? Remarks = null,
	bool? HasETrainTimetable = null,
	int? ETrainTimetableContentType = null,
	byte[]? ETrainTimetableContent = null,
	// 「施行日」に日付ではない任意の文字列を表示したい場合のテキスト。
	// 設定されていればこちらが優先表示される (UI 側のレンダリング規約)。
	// JSON 上の AffectDate が日付として解釈できない場合、ConvertWork が自動で埋める。
	string? AffectDateText = null
) : IHasRemarksProperty
{
}
