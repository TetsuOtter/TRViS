# Screenshot-regression baselines

Committed reference images for `ScreenshotRegressionTests`. Each CI iOS run
recaptures every screen and pixel-diffs it against the baseline here; a
drift larger than the tolerance fails the build.

## Layout

```
Screenshots/<deviceClass>/<theme>/<screen>.png
```

* `deviceClass` — `iphone` (iPhone 16) · `ipad-mini-a17` (iPad mini A17 Pro)
  · `ipad-mini-5` (review-only, **not** part of the pixel gate).
* `theme` — `light` · `dark`.
* `screen` — `startHome-start`, `privacyPolicy`, `connectServer`,
  `selectFile`, `thirdPartyLicenses`, `startHome-home`, `dtac-timetable`,
  `dtac-hako`, `horizontalTimetable`, `settings`.

  > The DTAC 行路添付 (WorkAffix) tab is **not** captured: it is hard-coded
  > `IsEnabled="False"` in `ViewHost.xaml` (placeholder for an
  > unimplemented feature) with no dynamic enabler, so tapping it is a
  > no-op that would only re-shoot the previous tab.

Only `iphone` and `ipad-mini-a17` baselines block the build. `ipad-mini-5`
is captured for the Apple-guideline review but excluded from the gate
because its macos-26 / iPadOS-17 WDA path is too flaky to be a blocking
signal (it is `continue-on-error` in `ui-test.yml`).

Every screen is gated **in full** — there is no per-region masking. Each
captured viewport is comparable end-to-end because the only intrinsically
non-deterministic elements are pinned to fixed values by `UI_TEST` seams in
the app (see "Determinism" below).

## Regenerating

```sh
./update-screenshots.sh                 # iphone + ipad-mini-a17
./update-screenshots.sh iphone          # one device class
```

This runs only `ScreenshotRegressionTests` with `SCREENSHOT_UPDATE=1`,
which overwrites every baseline instead of diffing. Review `git diff` on
this directory and commit the frames you accept.

## Determinism

The captured pixels are made reproducible across CI runners by `UI_TEST`
compile-time seams in the app plus a simulator override:

* **Clock** — `StartHome.TestFreezeClockButton` pins the app clock to
  09:41:00 so every time-of-day label is fixed.
* **Theme** — `TestForceLight/DarkThemeButton` forces the requested
  light/dark theme regardless of the simulator's system setting.
* **Log file path** — the Settings (EasterEgg) page's *Log File Path* card
  normally prints an absolute path embedding the simulator's Device UUID
  *and* the app's per-install GUID, both of which differ on every runner.
  An `#if UI_TEST` seam in `EasterEggPage.xaml.cs` substitutes the fixed
  placeholder `/UITEST/TRViS.InternalFiles/logs` so the whole Settings
  screen — including the deterministic PDF-engine status line — stays
  pixel-comparable. Production builds compile this out and show the real
  path.
* **Status bar** — `run-ui-tests.sh`'s `simctl status_bar override` fixes
  the carrier/time/battery indicators.

Do not hand-edit these PNGs.
