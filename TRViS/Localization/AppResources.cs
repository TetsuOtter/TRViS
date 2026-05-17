namespace TRViS.Localization;

/// <summary>
/// コードビハインド / ViewModel からローカライズ文字列を取得する強い型付け
/// アクセサ。XAML からは <see cref="TranslateExtension"/> を使う。
///
/// 各プロパティは現在の言語の値を返す。書式付き文字列は
/// <c>string.Format(AppResources.Xxx_Format, args)</c> で使う
/// (現在カルチャは <see cref="LocalizationResourceManager.SetLanguage"/> が設定済み)。
/// </summary>
public static class AppResources
{
	static string Get(string key) => LocalizationResourceManager.Current[key];

	public static string Common_OK => Get(nameof(Common_OK));
	public static string Common_Cancel => Get(nameof(Common_Cancel));
	public static string Common_Yes => Get(nameof(Common_Yes));
	public static string Common_No => Get(nameof(Common_No));
	public static string Common_Close => Get(nameof(Common_Close));
	public static string Common_Continue => Get(nameof(Common_Continue));
	public static string Common_Stop => Get(nameof(Common_Stop));
	public static string Common_Error => Get(nameof(Common_Error));
	public static string Common_Success => Get(nameof(Common_Success));

	public static string Shell_Home => Get(nameof(Shell_Home));
	public static string Shell_ThirdPartyLicenses => Get(nameof(Shell_ThirdPartyLicenses));
	public static string Shell_Settings => Get(nameof(Shell_Settings));
	public static string Shell_FirebaseSetting => Get(nameof(Shell_FirebaseSetting));
	public static string Shell_PrivacyPolicy => Get(nameof(Shell_PrivacyPolicy));
	public static string Shell_PrivacyPolicyOnline => Get(nameof(Shell_PrivacyPolicyOnline));

	public static string StartHome_PrivacyPolicy => Get(nameof(StartHome_PrivacyPolicy));
	public static string StartHome_ThirdPartyLicenses => Get(nameof(StartHome_ThirdPartyLicenses));
	public static string StartHome_ConnectServer => Get(nameof(StartHome_ConnectServer));
	public static string StartHome_SelectFile => Get(nameof(StartHome_SelectFile));
	public static string StartHome_PrivacyReconfirmTitle => Get(nameof(StartHome_PrivacyReconfirmTitle));
	public static string StartHome_PrivacyReconfirmBody => Get(nameof(StartHome_PrivacyReconfirmBody));
	public static string StartHome_LoadDemo => Get(nameof(StartHome_LoadDemo));
	public static string StartHome_SampleLoadFailedFormat => Get(nameof(StartHome_SampleLoadFailedFormat));

	public static string Home_LoadedData => Get(nameof(Home_LoadedData));
	public static string Home_Reconnect => Get(nameof(Home_Reconnect));
	public static string Home_Reconnecting => Get(nameof(Home_Reconnecting));
	public static string Home_WorkGroup => Get(nameof(Home_WorkGroup));
	public static string Home_Work => Get(nameof(Home_Work));
	public static string Home_Change => Get(nameof(Home_Change));
	public static string Home_Disconnect => Get(nameof(Home_Disconnect));
	public static string Home_Open => Get(nameof(Home_Open));
	public static string Home_LoaderType_Demo => Get(nameof(Home_LoaderType_Demo));
	public static string Home_LoaderType_Json => Get(nameof(Home_LoaderType_Json));
	public static string Home_LoaderType_Sqlite => Get(nameof(Home_LoaderType_Sqlite));
	public static string Home_LoaderType_ServerDisconnected => Get(nameof(Home_LoaderType_ServerDisconnected));
	public static string Home_LoaderType_ServerConnected => Get(nameof(Home_LoaderType_ServerConnected));
	public static string Home_DiagramFormat => Get(nameof(Home_DiagramFormat));
	public static string Home_WorkCountFormat => Get(nameof(Home_WorkCountFormat));
	public static string Home_AffectDateFormat => Get(nameof(Home_AffectDateFormat));
	public static string Home_TrainCountFormat => Get(nameof(Home_TrainCountFormat));
	public static string Home_NotSelectedTitle => Get(nameof(Home_NotSelectedTitle));
	public static string Home_NotSelectedBody => Get(nameof(Home_NotSelectedBody));
	public static string Home_ConfirmTitle => Get(nameof(Home_ConfirmTitle));
	public static string Home_ConfirmCloseBody => Get(nameof(Home_ConfirmCloseBody));

	public static string ConnectServer_Title => Get(nameof(ConnectServer_Title));
	public static string ConnectServer_HistoryHint => Get(nameof(ConnectServer_HistoryHint));
	public static string ConnectServer_BackToHistory => Get(nameof(ConnectServer_BackToHistory));
	public static string ConnectServer_UrlLabel => Get(nameof(ConnectServer_UrlLabel));
	public static string ConnectServer_UrlHint => Get(nameof(ConnectServer_UrlHint));
	public static string ConnectServer_SaveUrlLabel => Get(nameof(ConnectServer_SaveUrlLabel));
	public static string ConnectServer_SaveUrlHint => Get(nameof(ConnectServer_SaveUrlHint));
	public static string ConnectServer_Connect => Get(nameof(ConnectServer_Connect));
	public static string ConnectServer_NewConnection => Get(nameof(ConnectServer_NewConnection));
	public static string ConnectServer_AlertCannotConnectTitle => Get(nameof(ConnectServer_AlertCannotConnectTitle));
	public static string ConnectServer_AlertEnterUrl => Get(nameof(ConnectServer_AlertEnterUrl));
	public static string ConnectServer_AlertInvalidUrl => Get(nameof(ConnectServer_AlertInvalidUrl));
	public static string ConnectServer_AlertLoadFailedFormat => Get(nameof(ConnectServer_AlertLoadFailedFormat));

	public static string SelectFile_Title => Get(nameof(SelectFile_Title));
	public static string SelectFile_FileListHint => Get(nameof(SelectFile_FileListHint));
	public static string SelectFile_EmptyMessage => Get(nameof(SelectFile_EmptyMessage));
	public static string SelectFile_EmptyDetail => Get(nameof(SelectFile_EmptyDetail));
	public static string SelectFile_Browse => Get(nameof(SelectFile_Browse));
	public static string SelectFile_OpenStorage => Get(nameof(SelectFile_OpenStorage));
	public static string SelectFile_UpFolder => Get(nameof(SelectFile_UpFolder));
	public static string SelectFile_FolderEmpty => Get(nameof(SelectFile_FolderEmpty));
	public static string SelectFile_FolderCountFormat => Get(nameof(SelectFile_FolderCountFormat));
	public static string SelectFile_FileCountFormat => Get(nameof(SelectFile_FileCountFormat));
	public static string SelectFile_AlertCannotLoadTitle => Get(nameof(SelectFile_AlertCannotLoadTitle));
	public static string SelectFile_AlertUnsupportedFormat => Get(nameof(SelectFile_AlertUnsupportedFormat));
	public static string SelectFile_AlertLoadFailedFormat => Get(nameof(SelectFile_AlertLoadFailedFormat));
	public static string SelectFile_AlertCannotOpenFileTitle => Get(nameof(SelectFile_AlertCannotOpenFileTitle));
	public static string SelectFile_AlertCannotOpenFolderTitle => Get(nameof(SelectFile_AlertCannotOpenFolderTitle));

	public static string Privacy_Title => Get(nameof(Privacy_Title));
	public static string Privacy_LogCollectionTitle => Get(nameof(Privacy_LogCollectionTitle));
	public static string Privacy_DescBody => Get(nameof(Privacy_DescBody));
	public static string Privacy_CrashRequiredNotice => Get(nameof(Privacy_CrashRequiredNotice));
	public static string Privacy_AllowAnalytics => Get(nameof(Privacy_AllowAnalytics));
	public static string Privacy_RestoreSaved => Get(nameof(Privacy_RestoreSaved));
	public static string Privacy_AgreeAndSave => Get(nameof(Privacy_AgreeAndSave));
	public static string Privacy_Save => Get(nameof(Privacy_Save));

	public static string Firebase_Title => Get(nameof(Firebase_Title));
	public static string Firebase_AlertSavedBodyFormat => Get(nameof(Firebase_AlertSavedBodyFormat));

	public static string ThirdParty_Title => Get(nameof(ThirdParty_Title));
	public static string ThirdParty_Close => Get(nameof(ThirdParty_Close));
	public static string ThirdParty_LicenseExpressionFormat => Get(nameof(ThirdParty_LicenseExpressionFormat));
	public static string ThirdParty_OpenLicense => Get(nameof(ThirdParty_OpenLicense));
	public static string ThirdParty_NoLicenseInfo => Get(nameof(ThirdParty_NoLicenseInfo));
	public static string ThirdParty_AlertCannotLoadTitle => Get(nameof(ThirdParty_AlertCannotLoadTitle));
	public static string ThirdParty_AlertCannotLoadBodyFormat => Get(nameof(ThirdParty_AlertCannotLoadBodyFormat));
	public static string ThirdParty_AlertInvalidUrlTitle => Get(nameof(ThirdParty_AlertInvalidUrlTitle));
	public static string ThirdParty_AlertInvalidUrlFormat => Get(nameof(ThirdParty_AlertInvalidUrlFormat));
	public static string ThirdParty_AlertCannotOpenUrlTitle => Get(nameof(ThirdParty_AlertCannotOpenUrlTitle));

	public static string Settings_Title => Get(nameof(Settings_Title));
	public static string Settings_ReloadSaved => Get(nameof(Settings_ReloadSaved));
	public static string Settings_Save => Get(nameof(Settings_Save));
	public static string Settings_LocationInterval => Get(nameof(Settings_LocationInterval));
	public static string Settings_TimeProgression => Get(nameof(Settings_TimeProgression));
	public static string Settings_TimeProgression_1x => Get(nameof(Settings_TimeProgression_1x));
	public static string Settings_TimeProgression_30x => Get(nameof(Settings_TimeProgression_30x));
	public static string Settings_TimeProgression_60x => Get(nameof(Settings_TimeProgression_60x));
	public static string Settings_Advanced => Get(nameof(Settings_Advanced));
	public static string Settings_ShowMapLandscape => Get(nameof(Settings_ShowMapLandscape));
	public static string Settings_KeepScreenOn => Get(nameof(Settings_KeepScreenOn));
	public static string Settings_HorizontalTimetableButtonLabel => Get(nameof(Settings_HorizontalTimetableButtonLabel));
	public static string Settings_HTBL_Horizontal => Get(nameof(Settings_HTBL_Horizontal));
	public static string Settings_HTBL_Train => Get(nameof(Settings_HTBL_Train));
	public static string Settings_HTBL_ETrain => Get(nameof(Settings_HTBL_ETrain));
	public static string Settings_AppTheme => Get(nameof(Settings_AppTheme));
	public static string Settings_Theme_System => Get(nameof(Settings_Theme_System));
	public static string Settings_Theme_Light => Get(nameof(Settings_Theme_Light));
	public static string Settings_Theme_Dark => Get(nameof(Settings_Theme_Dark));
	public static string Settings_PdfEngine => Get(nameof(Settings_PdfEngine));
	public static string Settings_PdfEngineHint => Get(nameof(Settings_PdfEngineHint));
	public static string Settings_PdfEngineDisplayFormat => Get(nameof(Settings_PdfEngineDisplayFormat));
	public static string Settings_PdfRender_Svg => Get(nameof(Settings_PdfRender_Svg));
	public static string Settings_PdfRender_Canvas => Get(nameof(Settings_PdfRender_Canvas));
	public static string Settings_PdfEngineCurrentFormat => Get(nameof(Settings_PdfEngineCurrentFormat));
	public static string Settings_PdfEngineCurrentFallbackFormat => Get(nameof(Settings_PdfEngineCurrentFallbackFormat));
	public static string Settings_LogFilePath => Get(nameof(Settings_LogFilePath));
	public static string Settings_Language => Get(nameof(Settings_Language));
	public static string Settings_Language_System => Get(nameof(Settings_Language_System));
	public static string Settings_Language_Japanese => Get(nameof(Settings_Language_Japanese));
	public static string Settings_Language_English => Get(nameof(Settings_Language_English));
	public static string Settings_AlertReloadFailedFormat => Get(nameof(Settings_AlertReloadFailedFormat));
	public static string Settings_AlertSavedSuccess => Get(nameof(Settings_AlertSavedSuccess));
	public static string Settings_AlertSaveFailedFormat => Get(nameof(Settings_AlertSaveFailedFormat));
	public static string Settings_AlertLoadSettingFailedTitle => Get(nameof(Settings_AlertLoadSettingFailedTitle));
	public static string Settings_AlertInvalidLocationIntervalTitle => Get(nameof(Settings_AlertInvalidLocationIntervalTitle));
	public static string Settings_AlertInvalidLocationIntervalFormat => Get(nameof(Settings_AlertInvalidLocationIntervalFormat));

	public static string SettingFile_LoadFailedFormat => Get(nameof(SettingFile_LoadFailedFormat));
	public static string SettingFile_LoadFailedNull => Get(nameof(SettingFile_LoadFailedNull));
	public static string SettingFile_EmptyJson => Get(nameof(SettingFile_EmptyJson));

	public static string ShowAlert_UnknownErrorFormat => Get(nameof(ShowAlert_UnknownErrorFormat));

	public static string AppLink_CannotOpenFileTitle => Get(nameof(AppLink_CannotOpenFileTitle));
	public static string AppLink_IdentifyFailedFormat => Get(nameof(AppLink_IdentifyFailedFormat));
	public static string AppLink_OpenExternalFileTitle => Get(nameof(AppLink_OpenExternalFileTitle));
	public static string AppLink_OpenExternalFileFormat => Get(nameof(AppLink_OpenExternalFileFormat));
	public static string AppLink_LocalPathInvalid => Get(nameof(AppLink_LocalPathInvalid));
	public static string AppLink_TimeoutTitle => Get(nameof(AppLink_TimeoutTitle));
	public static string AppLink_TimeoutBody => Get(nameof(AppLink_TimeoutBody));
	public static string AppLink_OpenAppLinkFailedFormat => Get(nameof(AppLink_OpenAppLinkFailedFormat));
	public static string AppLink_ExternalLocationServiceTitle => Get(nameof(AppLink_ExternalLocationServiceTitle));
	public static string AppLink_ExternalLocationServiceBodyFormat => Get(nameof(AppLink_ExternalLocationServiceBodyFormat));
	public static string AppLink_CannotSetExternalLocationTitle => Get(nameof(AppLink_CannotSetExternalLocationTitle));
	public static string AppLink_SetNetworkSyncFailedFormat => Get(nameof(AppLink_SetNetworkSyncFailedFormat));
	public static string AppLink_FileLoadCompleteBody => Get(nameof(AppLink_FileLoadCompleteBody));
	public static string AppLink_WebSocketUrlMissing => Get(nameof(AppLink_WebSocketUrlMissing));
	public static string AppLink_WebSocketConnectedBody => Get(nameof(AppLink_WebSocketConnectedBody));
	public static string AppLink_CannotConnectWebSocketTitle => Get(nameof(AppLink_CannotConnectWebSocketTitle));
	public static string AppLink_WebSocketConnectFailedFormat => Get(nameof(AppLink_WebSocketConnectFailedFormat));
	public static string AppLink_LocalFileOutsideFolder => Get(nameof(AppLink_LocalFileOutsideFolder));
	public static string AppLink_FileNotFoundFormat => Get(nameof(AppLink_FileNotFoundFormat));
	public static string AppLink_PathResolveFailedFormat => Get(nameof(AppLink_PathResolveFailedFormat));
	public static string AppLink_MaybeDifferentNetworkTitle => Get(nameof(AppLink_MaybeDifferentNetworkTitle));
	public static string AppLink_MaybeDifferentNetworkBodyFormat => Get(nameof(AppLink_MaybeDifferentNetworkBodyFormat));
	public static string AppLink_ThisDeviceFormat => Get(nameof(AppLink_ThisDeviceFormat));
	public static string AppLink_EmptyFileBody => Get(nameof(AppLink_EmptyFileBody));
	public static string AppLink_ContinueDownloadTitle => Get(nameof(AppLink_ContinueDownloadTitle));
	public static string AppLink_UnknownSizeBody => Get(nameof(AppLink_UnknownSizeBody));
	public static string AppLink_FileSizeFormat => Get(nameof(AppLink_FileSizeFormat));
}
