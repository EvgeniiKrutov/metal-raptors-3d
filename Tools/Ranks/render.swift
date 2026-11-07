import AppKit
import WebKit

final class Renderer: NSObject, WKNavigationDelegate {
    var queue: [(URL, URL)]
    let size: Int
    let web: WKWebView
    let window: NSWindow

    init(queue: [(URL, URL)], size: Int) {
        self.queue = queue
        self.size = size
        let frame = NSRect(x: 0, y: 0, width: size, height: size)
        web = WKWebView(frame: frame, configuration: WKWebViewConfiguration())
        web.setValue(false, forKey: "drawsBackground")
        window = NSWindow(contentRect: frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.isOpaque = false
        window.backgroundColor = .clear
        window.contentView = web
        super.init()
        web.navigationDelegate = self
    }

    func next() {
        guard let (source, _) = queue.first else { exit(0) }
        let svg = (try? String(contentsOf: source, encoding: .utf8)) ?? ""
        let html = "<html><head><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}"
            + "svg{display:block;width:\(size)px;height:\(size)px}</style></head><body>\(svg)</body></html>"
        web.loadHTMLString(html, baseURL: nil)
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { self.capture() }
    }

    func capture() {
        let config = WKSnapshotConfiguration()
        config.rect = NSRect(x: 0, y: 0, width: size, height: size)
        web.takeSnapshot(with: config) { image, _ in
            let (_, target) = self.queue.removeFirst()
            if let image = image { self.write(image, to: target) }
            self.next()
        }
    }

    func write(_ image: NSImage, to url: URL) {
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size,
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true,
                                         isPlanar: false, colorSpaceName: .deviceRGB,
                                         bytesPerRow: 0, bitsPerPixel: 0) else { return }
        rep.size = NSSize(width: size, height: size)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        NSGraphicsContext.current?.imageInterpolation = .high
        image.draw(in: NSRect(x: 0, y: 0, width: size, height: size))
        NSGraphicsContext.restoreGraphicsState()
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
        print("wrote \(url.lastPathComponent)")
    }
}

let args = CommandLine.arguments
let input = URL(fileURLWithPath: args[1])
let output = URL(fileURLWithPath: args[2])
let size = Int(args[3]) ?? 512
let only = args.count > 4 ? args[4] : nil

let files = (try? FileManager.default.contentsOfDirectory(at: input, includingPropertiesForKeys: nil)) ?? []
let jobs = files.filter { $0.pathExtension == "svg" }
    .filter { only == nil || $0.lastPathComponent.hasPrefix(only!) }
    .sorted { $0.lastPathComponent < $1.lastPathComponent }
    .map { ($0, output.appendingPathComponent($0.deletingPathExtension().lastPathComponent + ".png")) }

let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let renderer = Renderer(queue: jobs, size: size)
renderer.next()
app.run()
