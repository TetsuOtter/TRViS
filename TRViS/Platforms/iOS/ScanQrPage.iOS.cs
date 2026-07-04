using AVFoundation;
using CoreFoundation;
using Foundation;
using ObjCRuntime;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using UIKit;

using TRViS.Services;

namespace TRViS.RootPages;

public partial class ScanQrPage
{
	private LegacyIosQrScannerSession? _legacyIosScanner;

	/// <summary>
	/// Starts the iOS 12-compatible scanner only after ScanQrPage is visible.
	/// The implementation deliberately contains no managed NSObject subclasses:
	/// those are registered by the static iOS registrar during app startup even
	/// when their instances are created lazily.
	/// </summary>
	[SupportedOSPlatform("ios12.2")]
	[UnsupportedOSPlatform("ios15.1")]
	private async Task<bool> StartLegacyIosScannerAsync()
	{
		AVAuthorizationStatus status =
			AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video);

		if (status == AVAuthorizationStatus.NotDetermined)
		{
			bool granted =
				await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video);
			if (!granted)
				return false;
		}
		else if (status != AVAuthorizationStatus.Authorized)
		{
			return false;
		}

		UIView nativeHost = await GetNativeCameraHostAsync();

		if (_legacyIosScanner is null)
		{
			_legacyIosScanner = new LegacyIosQrScannerSession(
				nativeHost,
				value => MainThread.BeginInvokeOnMainThread(
					async () => await TryHandleCandidateAsync(value)));
			CameraHost.SizeChanged += OnLegacyCameraHostSizeChanged;
		}
		_legacyIosScanner.Start();
		return true;
	}

	/// <summary>
	/// On iOS 12 the CameraHost ContentView's Handler is sometimes not yet
	/// created when OnAppearing runs (the native view hasn't finished attaching
	/// to the window), so <c>CameraHost.Handler</c> can still be null here. Wait
	/// for HandlerChanged instead of assuming it is already available.
	/// </summary>
	private async Task<UIView> GetNativeCameraHostAsync()
	{
		if (CameraHost.Handler?.PlatformView is UIView existingHost)
			return existingHost;

		TaskCompletionSource<UIView> tcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		void OnHandlerChanged(object? sender, EventArgs e)
		{
			if (CameraHost.Handler?.PlatformView is UIView view)
				tcs.TrySetResult(view);
		}

		CameraHost.HandlerChanged += OnHandlerChanged;
		try
		{
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
			using CancellationTokenRegistration registration = cts.Token.Register(() =>
				tcs.TrySetException(
					new InvalidOperationException("The native camera host is not available.")));

			return await tcs.Task;
		}
		finally
		{
			CameraHost.HandlerChanged -= OnHandlerChanged;
		}
	}

	private void StopLegacyIosScanner()
		=> _legacyIosScanner?.Stop();

	private Task DisposeLegacyIosScannerAsync()
	{
		if (_legacyIosScanner is null)
			return Task.CompletedTask;

		CameraHost.SizeChanged -= OnLegacyCameraHostSizeChanged;
		_legacyIosScanner.Dispose();
		_legacyIosScanner = null;
		return Task.CompletedTask;
	}

	private void OnLegacyCameraHostSizeChanged(object? sender, EventArgs e)
		=> _legacyIosScanner?.UpdatePreviewFrame();

	private void ToggleLegacyIosTorch()
	{
		bool enabled = _legacyIosScanner?.ToggleTorch() ?? false;
		logger.Trace("Legacy iOS torch toggled -> {0}", enabled);
	}
}

/// <summary>
/// iOS 12 QR backend without application-defined NSObject subclasses.
/// A tiny Objective-C delegate class is created only when this scanner opens,
/// so it never enters .NET iOS's startup-time static registrar.
/// </summary>
[SupportedOSPlatform("ios12.2")]
[UnsupportedOSPlatform("ios15.1")]
internal sealed class LegacyIosQrScannerSession : IDisposable
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly UIView _host;
	private readonly Action<string> _detected;
	private readonly DispatchQueue _captureQueue =
		new("dev.t0r.trvis.legacy-qr-capture");

	private AVCaptureSession? _session;
	private AVCaptureDevice? _camera;
	private AVCaptureMetadataOutput? _metadataOutput;
	private UIView? _preview;
	private AVCaptureVideoPreviewLayer? _previewLayer;
	private IntPtr _delegateHandle;
	private bool _configured;
	private bool _disposed;
	private bool _torchOn;
	private NSTimer? _frameConvergenceTimer;

	internal LegacyIosQrScannerSession(UIView host, Action<string> detected)
	{
		_host = host;
		_detected = detected;
	}

	internal void Start()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_configured)
			Configure();

		AVCaptureSession session = _session
			?? throw new InvalidOperationException("The capture session was not configured.");
		if (!session.Running)
			_captureQueue.DispatchAsync(session.StartRunning);
	}

	internal void Stop()
	{
		AVCaptureSession? session = _session;
		if (session?.Running == true)
			_captureQueue.DispatchAsync(session.StopRunning);
	}

	internal bool ToggleTorch()
	{
		AVCaptureDevice? camera = _camera;
		if (camera is null || !camera.HasTorch)
			return false;

		NSError? error;
		if (!camera.LockForConfiguration(out error))
		{
			logger.Warn("Could not lock camera for torch configuration: {0}", error);
			return _torchOn;
		}

		try
		{
			_torchOn = !_torchOn;
			camera.TorchMode = _torchOn
				? AVCaptureTorchMode.On
				: AVCaptureTorchMode.Off;
			return _torchOn;
		}
		finally
		{
			camera.UnlockForConfiguration();
		}
	}

	private void Configure()
	{
		using AVCaptureDeviceDiscoverySession discovery =
			AVCaptureDeviceDiscoverySession.Create(
				[AVCaptureDeviceType.BuiltInWideAngleCamera],
				AVMediaTypes.Video,
				AVCaptureDevicePosition.Back);
		_camera = discovery.Devices.FirstOrDefault()
			?? throw new InvalidOperationException("No rear camera is available.");

		AVCaptureDeviceInput input = AVCaptureDeviceInput.FromDevice(
			_camera,
			out NSError? inputError)
			?? throw new InvalidOperationException(
				$"Could not create camera input: {inputError?.LocalizedDescription}");

		var session = new AVCaptureSession();
		var output = new AVCaptureMetadataOutput();

		if (!session.CanAddInput(input))
			throw new InvalidOperationException("Could not add camera input.");
		session.AddInput(input);

		if (!session.CanAddOutput(output))
			throw new InvalidOperationException("Could not add QR metadata output.");
		session.AddOutput(output);

		_delegateHandle = LegacyQrMetadataDelegate.Create(this);
		LegacyQrMetadataDelegate.SetOutputDelegate(
			output,
			_delegateHandle,
			_captureQueue);
		output.MetadataObjectTypes = AVMetadataObjectType.QRCode;

		var preview = new UIView
		{
			Frame = _host.Bounds,
			AutoresizingMask =
				UIViewAutoresizing.FlexibleWidth |
				UIViewAutoresizing.FlexibleHeight,
		};
		var previewLayer = new AVCaptureVideoPreviewLayer(session)
		{
			Frame = preview.Bounds,
			VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
		};
		preview.Layer.AddSublayer(previewLayer);
		_host.InsertSubview(preview, 0);

		_session = session;
		_metadataOutput = output;
		_preview = preview;
		_previewLayer = previewLayer;
		_configured = true;
		UpdatePreviewFrame();
		StartFrameConvergencePolling();
	}

	/// <summary>
	/// On the "camera permission already granted" fast path, OnAppearing can
	/// reach here before MAUI's AbsoluteLayout has arranged CameraHost to its
	/// final size, so <c>_host.Bounds</c> is still (0,0,0,0) here. The
	/// CameraHost.SizeChanged subscription is set up before this call, but the
	/// arrange pass that follows does not reliably re-raise it once the
	/// handler already exists, leaving the preview permanently zero-sized (no
	/// visible video). Poll for a short window until the host reports a real
	/// size, re-applying the preview frame each tick; this is independent of
	/// exactly when/whether SizeChanged fires.
	/// </summary>
	private void StartFrameConvergencePolling()
	{
		int tickCount = 0;
		_frameConvergenceTimer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromMilliseconds(100), timer =>
		{
			tickCount++;
			UpdatePreviewFrame();

			bool hasSize = _host.Bounds.Width > 0 && _host.Bounds.Height > 0;
			if (hasSize || tickCount >= 20 || _disposed)
			{
				timer.Invalidate();
				_frameConvergenceTimer = null;
			}
		});
	}

	internal void UpdatePreviewFrame()
	{
		if (_preview is null || _previewLayer is null)
			return;

		_preview.Frame = _host.Bounds;
		_previewLayer.Frame = _preview.Bounds;

		AVCaptureConnection? connection = _previewLayer.Connection;
		if (connection?.SupportsVideoOrientation != true)
			return;

		connection.VideoOrientation = UIDevice.CurrentDevice.Orientation switch
		{
			UIDeviceOrientation.PortraitUpsideDown => AVCaptureVideoOrientation.PortraitUpsideDown,
			// Device and capture landscape directions are intentionally mirrored.
			UIDeviceOrientation.LandscapeLeft => AVCaptureVideoOrientation.LandscapeRight,
			UIDeviceOrientation.LandscapeRight => AVCaptureVideoOrientation.LandscapeLeft,
			_ => AVCaptureVideoOrientation.Portrait,
		};
	}

	internal void OnMetadataObjects(AVMetadataObject[] metadataObjects)
	{
		foreach (AVMetadataObject metadataObject in metadataObjects)
		{
			if (metadataObject is AVMetadataMachineReadableCodeObject code &&
				code.Type == AVMetadataObjectType.QRCode &&
				!string.IsNullOrWhiteSpace(code.StringValue))
			{
				_detected(code.StringValue);
				return;
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;

		_frameConvergenceTimer?.Invalidate();
		_frameConvergenceTimer = null;

		// Tear down the UI-side objects synchronously — this only detaches the
		// preview from the screen and does not need the camera to have stopped.
		_preview?.RemoveFromSuperview();
		_previewLayer?.Dispose();
		_preview?.Dispose();

		// AVCaptureSession.StopRunning() can block for several seconds on some
		// hardware (observed ~9s on an iPad Air 2 running iOS 12.5.8). Waiting
		// for it synchronously here stalls whatever the caller does next (e.g.
		// the AppLink navigation after a successful scan), so hand the actual
		// stop -> detach delegate -> release native objects sequence off to the
		// capture queue and let it finish in the background instead of blocking
		// the caller. Order matches the original synchronous teardown.
		AVCaptureSession? session = _session;
		AVCaptureMetadataOutput? metadataOutput = _metadataOutput;
		AVCaptureDevice? camera = _camera;
		IntPtr delegateHandle = _delegateHandle;
		DispatchQueue captureQueue = _captureQueue;
		_delegateHandle = IntPtr.Zero;
		captureQueue.DispatchAsync(() =>
		{
			// Runs on a background GCD queue with nothing above it to catch an
			// unhandled throw (the original DispatchSync let exceptions
			// propagate to the caller's try/catch); report explicitly instead.
			try
			{
				if (session?.Running == true)
					session.StopRunning();

				if (metadataOutput is not null)
					LegacyQrMetadataDelegate.SetOutputDelegate(metadataOutput, IntPtr.Zero, null);
				if (delegateHandle != IntPtr.Zero)
					LegacyQrMetadataDelegate.Destroy(delegateHandle);

				metadataOutput?.Dispose();
				session?.Dispose();
				camera?.Dispose();
				captureQueue.Dispose();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Background camera teardown failed");
				InstanceManager.CrashlyticsWrapper.Log(ex, "LegacyIosQrScannerSession.Dispose (background teardown)");
			}
		});
	}
}

/// <summary>
/// Runtime-created Objective-C implementation of
/// AVCaptureMetadataOutputObjectsDelegate. This is intentionally not an
/// NSObject-derived managed class, so the static registrar never sees it.
/// </summary>
[SupportedOSPlatform("ios12.2")]
[UnsupportedOSPlatform("ios15.1")]
internal static class LegacyQrMetadataDelegate
{
	private const string DelegateClassName = "TRViSLegacyQrMetadataDelegate";
	private const string CallbackSelectorName =
		"captureOutput:didOutputMetadataObjects:fromConnection:";
	private const string SetDelegateSelectorName =
		"setMetadataObjectsDelegate:queue:";

	private static readonly object classLock = new();
	private static readonly ConcurrentDictionary<IntPtr, WeakReference<LegacyIosQrScannerSession>>
		sessions = new();
	private static readonly MetadataCallback metadataCallback = OnMetadata;
	private static IntPtr delegateClass;

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void MetadataCallback(
		IntPtr self,
		IntPtr command,
		IntPtr output,
		IntPtr metadataObjects,
		IntPtr connection);

	internal static IntPtr Create(LegacyIosQrScannerSession session)
	{
		IntPtr nativeClass = GetOrCreateClass();
		IntPtr instance = IntPtr_objc_msgSend(
			IntPtr_objc_msgSend(nativeClass, Selector.GetHandle("alloc")),
			Selector.GetHandle("init"));
		if (instance == IntPtr.Zero)
			throw new InvalidOperationException("Could not create the native QR delegate.");

		sessions[instance] = new WeakReference<LegacyIosQrScannerSession>(session);
		return instance;
	}

	internal static void Destroy(IntPtr instance)
	{
		sessions.TryRemove(instance, out _);
		objc_release(instance);
	}

	internal static void SetOutputDelegate(
		AVCaptureMetadataOutput output,
		IntPtr delegateHandle,
		DispatchQueue? queue)
	{
		void_objc_msgSend_IntPtr_IntPtr(
			output.Handle,
			Selector.GetHandle(SetDelegateSelectorName),
			delegateHandle,
			queue?.Handle ?? IntPtr.Zero);
	}

	private static IntPtr GetOrCreateClass()
	{
		if (delegateClass != IntPtr.Zero)
			return delegateClass;

		lock (classLock)
		{
			if (delegateClass != IntPtr.Zero)
				return delegateClass;

			IntPtr existingClass = objc_getClass(DelegateClassName);
			if (existingClass != IntPtr.Zero)
			{
				delegateClass = existingClass;
				return delegateClass;
			}

			IntPtr nsObjectClass = objc_getClass("NSObject");
			IntPtr newClass = objc_allocateClassPair(
				nsObjectClass,
				DelegateClassName,
				IntPtr.Zero);
			if (newClass == IntPtr.Zero)
				throw new InvalidOperationException("Could not allocate the native QR delegate class.");

			IntPtr protocol = objc_getProtocol("AVCaptureMetadataOutputObjectsDelegate");
			if (protocol == IntPtr.Zero || !class_addProtocol(newClass, protocol))
				throw new InvalidOperationException("Could not add the QR metadata protocol.");

			IntPtr callbackPointer = Marshal.GetFunctionPointerForDelegate(metadataCallback);
			if (!class_addMethod(
				newClass,
				Selector.GetHandle(CallbackSelectorName),
				callbackPointer,
				"v@:@@@"))
			{
				throw new InvalidOperationException("Could not add the QR metadata callback.");
			}

			objc_registerClassPair(newClass);
			delegateClass = newClass;
			return delegateClass;
		}
	}

	[MonoPInvokeCallback(typeof(MetadataCallback))]
	private static void OnMetadata(
		IntPtr self,
		IntPtr command,
		IntPtr output,
		IntPtr metadataObjects,
		IntPtr connection)
	{
		try
		{
			if (!sessions.TryGetValue(self, out var weakSession) ||
				!weakSession.TryGetTarget(out LegacyIosQrScannerSession? session))
			{
				return;
			}

			AVMetadataObject?[]? objects =
				NSArray.ArrayFromHandle<AVMetadataObject>((NativeHandle)metadataObjects);
			if (objects is null)
				return;

			session.OnMetadataObjects(objects.OfType<AVMetadataObject>().ToArray());
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "Native iOS QR metadata callback failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "LegacyQrMetadataDelegate.OnMetadata");
		}
	}

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private const string ObjectiveCLibrary = "/usr/lib/libobjc.dylib";

	[DllImport(ObjectiveCLibrary)]
	private static extern IntPtr objc_getClass(string name);

	[DllImport(ObjectiveCLibrary)]
	private static extern IntPtr objc_getProtocol(string name);

	[DllImport(ObjectiveCLibrary)]
	private static extern IntPtr objc_allocateClassPair(
		IntPtr superclass,
		string name,
		IntPtr extraBytes);

	[DllImport(ObjectiveCLibrary)]
	private static extern void objc_registerClassPair(IntPtr cls);

	[DllImport(ObjectiveCLibrary)]
	[return: MarshalAs(UnmanagedType.I1)]
	private static extern bool class_addProtocol(IntPtr cls, IntPtr protocol);

	[DllImport(ObjectiveCLibrary)]
	[return: MarshalAs(UnmanagedType.I1)]
	private static extern bool class_addMethod(
		IntPtr cls,
		IntPtr selector,
		IntPtr implementation,
		string types);

	[DllImport(ObjectiveCLibrary)]
	private static extern void objc_release(IntPtr value);

	[DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
	private static extern IntPtr IntPtr_objc_msgSend(
		IntPtr receiver,
		IntPtr selector);

	[DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
	private static extern void void_objc_msgSend_IntPtr_IntPtr(
		IntPtr receiver,
		IntPtr selector,
		IntPtr firstArgument,
		IntPtr secondArgument);
}
