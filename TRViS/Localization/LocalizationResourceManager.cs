using System.ComponentModel;
using System.Globalization;

using TRViS.MyAppCustomizables;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.Localization;

/// <summary>
/// 実行時に言語を切り替えられるローカライズの中核。
///
/// XAML からは <see cref="TranslateExtension"/> 経由でインデクサ
/// (<c>this[key]</c>) にバインドし、言語変更時に
/// <c>PropertyChanged("Item")</c> を発火してラベルを更新する。
/// (MAUI のバインディングは <c>"Item[]"</c> という WPF 流の通知名を
/// 解釈せず、ブラケットを含む名前は <c>"Item[&lt;key&gt;]"</c> と完全一致
/// しない限り無視する。ブラケット無しの <c>"Item"</c> は IndexerName と
/// 一致し、全インデクサバインドが再評価される。)
///
/// Shell の FlyoutItem / ContentPage の Title はバインディングの
/// 再評価が不安定なため、<see cref="CultureChanged"/> を購読して
/// コードビハインドで明示的に再設定する。
/// </summary>
public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// resx のマニフェスト名 (RootNamespace + フォルダ + ファイル名)。
	// 衛星アセンブリ (ja/TRViS.resources.dll) はカルチャ指定で解決される。
	static readonly System.Resources.ResourceManager resmgr
		= new("TRViS.Resources.Strings.AppResources", typeof(LocalizationResourceManager).Assembly);

	public static LocalizationResourceManager Current { get; } = new();

	private LocalizationResourceManager()
	{
		_culture = ResolveCulture(AppLanguage.System);
	}

	private CultureInfo _culture;
	public CultureInfo CurrentCulture => _culture;

	private AppLanguage _currentLanguage = AppLanguage.System;
	public AppLanguage CurrentLanguage => _currentLanguage;

	public string this[string key]
		=> resmgr.GetString(key, _culture) ?? key;

	public string Get(string key)
		=> resmgr.GetString(key, _culture) ?? key;

	public string Format(string key, params object?[] args)
		=> string.Format(_culture, Get(key), args);

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// 言語変更時に発火。Shell / ページの Title など、バインディング
	/// 再評価に頼れない箇所が購読して明示的に再描画する。
	/// </summary>
	public event EventHandler? CultureChanged;

	private static CultureInfo ResolveCulture(AppLanguage language)
		=> language switch
		{
			AppLanguage.Japanese => new CultureInfo("ja"),
			AppLanguage.English => new CultureInfo("en"),
			// System: 端末のUI言語が日本語なら ja、それ以外は中立 (英語) リソース。
			_ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja"
				? new CultureInfo("ja")
				: new CultureInfo("en"),
		};

	/// <summary>
	/// 言語を適用する。値が変わったときだけ通知を発火する。
	/// </summary>
	public void SetLanguage(AppLanguage language)
	{
		CultureInfo newCulture = ResolveCulture(language);
		bool cultureChanged = !Equals(newCulture.Name, _culture.Name);

		_currentLanguage = language;
		_culture = newCulture;

		CultureInfo.CurrentCulture = newCulture;
		CultureInfo.CurrentUICulture = newCulture;
		CultureInfo.DefaultThreadCurrentCulture = newCulture;
		CultureInfo.DefaultThreadCurrentUICulture = newCulture;

		if (!cultureChanged)
			return;

		logger.Info("UI language changed: {0} (culture: {1})", language, newCulture.Name);
		// "Item[]" (WPF 流の「全インデクサ変更」) は MAUI のバインディングが
		// 解釈しない。ブラケット無しの "Item" なら IndexerName と一致し、
		// {loc:Translate} の全インデクサバインドが再評価される。
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
		CultureChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// 起動時に保存済みの言語設定を同期で読み取って適用する。
	/// AppShell 生成より前に App コンストラクタから呼ぶことで、
	/// 初期描画から正しい言語で表示される。設定ファイルが無い・
	/// 壊れている場合は端末設定にフォールバックする。
	/// </summary>
	public void InitializeFromSettings()
	{
		try
		{
			// iOS / Mac Catalyst: OS (AppleLanguages / iOS設定) を真実とする。
			// .NET の CurrentUICulture は起動時に AppleLanguages から導出済みなので
			// System として解決すれば OS 設定にそのまま追従する。JSON は参照しない。
			if (AppLanguageOsBridge.IsSupported)
			{
				SetLanguage(AppLanguage.System);
				return;
			}

			string path = Path.Combine(
				DirectoryPathProvider.InternalFilesDirectory.FullName,
				MyAppCustomizables.SettingFileStructure.SettingFileName);

			if (!File.Exists(path))
			{
				SetLanguage(AppLanguage.System);
				return;
			}

			string json = File.ReadAllText(path);
			(MyAppCustomizables.SettingFileStructure setting, string? err)
				= MyAppCustomizables.SettingFileStructure.LoadFromJson(json);
			if (err is not null)
				logger.Warn("InitializeFromSettings: setting load reported '{0}', using parsed/default", err);

			SetLanguage(setting.AppLanguage);
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "InitializeFromSettings failed; falling back to system language");
			SetLanguage(AppLanguage.System);
		}
	}
}
