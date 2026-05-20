import Cocoa

@main
class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        let contentRect = NSRect(x: 0, y: 0, width: 480, height: 240)
        let window = NSWindow(
            contentRect: contentRect,
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.center()
        window.title = "TRViS UITests Host (Catalyst)"

        let label = NSTextField(labelWithString:
            "TRViS UITests Host\nXCUITest drives dev.t0r.trvis (Mac Catalyst)")
        label.alignment = .center
        label.translatesAutoresizingMaskIntoConstraints = false
        label.maximumNumberOfLines = 0

        let view = NSView(frame: contentRect)
        view.addSubview(label)
        NSLayoutConstraint.activate([
            label.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            label.centerYAnchor.constraint(equalTo: view.centerYAnchor),
        ])
        window.contentView = view
        window.makeKeyAndOrderFront(nil)
        self.window = window
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }
}
