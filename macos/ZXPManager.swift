// ZXP Manager — native macOS front end for Adobe's Unified Plugin Installer Agent (UPIA).
// Installs, lists and removes .zxp / .ccx extensions for Creative Cloud apps.
// Not affiliated with Adobe. Translations live in ../i18n/*.json (shared with the Windows app).

import SwiftUI
import AppKit
import UniformTypeIdentifiers

let upiaPath = "/Library/Application Support/Adobe/Adobe Desktop Common/RemoteComponents/UPI/UnifiedPluginInstallerAgent/UnifiedPluginInstallerAgent.app/Contents/MacOS/UnifiedPluginInstallerAgent"
let supportedExtensions = ["zxp", "ccx"]

// MARK: - Localization

/// English strings, used when a key is missing in the current language.
private let englishBundle: Bundle? = Bundle.main.path(forResource: "en", ofType: "lproj").flatMap { Bundle(path: $0) }

/// Localized string for a key (see i18n/en.json).
func L(_ key: String) -> String {
    let missing = "\u{1}"
    let value = Bundle.main.localizedString(forKey: key, value: missing, table: nil)
    if value != missing { return value }
    return englishBundle?.localizedString(forKey: key, value: key, table: nil) ?? key
}

/// Localized string with %@ / %d placeholders.
func LF(_ key: String, _ args: CVarArg...) -> String {
    String(format: L(key), locale: Locale.current, arguments: args)
}

// MARK: - Data

struct AdobeExtension: Identifiable, Hashable {
    let id = UUID()
    let name: String
    let version: String
    let enabled: Bool
}

struct HostApp: Identifiable {
    let id = UUID()
    let name: String
    let version: String?
    var extensions: [AdobeExtension]
}

struct HostTarget: Hashable {
    let code: String
    let range: String?
    var displayName: String { HostCodes.name(for: code) }
}

enum Outcome: Equatable {
    case pending, running, success, failure(String)
}

struct PackageInfo: Identifiable {
    let id = UUID()
    let url: URL
    var name: String
    var version: String?
    var bundleId: String?
    var hosts: [HostTarget] = []
    var readError: String?
    var installedVersion: String?
    var outcome: Outcome = .pending
}

// MARK: - Adobe host app codes

enum HostCodes {
    static let names: [String: String] = [
        "AEFT": "After Effects", "AEFX": "After Effects",
        "PHXS": "Photoshop", "PHSP": "Photoshop", "PS": "Photoshop",
        "ILST": "Illustrator", "AI": "Illustrator",
        "PPRO": "Premiere Pro",
        "IDSN": "InDesign", "ID": "InDesign",
        "AICY": "InCopy",
        "FLPR": "Animate",
        "AUDT": "Audition",
        "KBRG": "Bridge",
        "DRWV": "Dreamweaver",
        "AME": "Media Encoder",
        "LTRM": "Lightroom Classic",
        "XD": "XD",
        "PRLD": "Prelude",
        "RUSH": "Premiere Rush",
    ]

    static func name(for code: String) -> String {
        names[code.uppercased()] ?? code
    }

    /// "[13.0,99.9]" → "≥ 13.0", "[13.0,18.9]" → "13.0 – 18.9"
    static func formatRange(_ raw: String?) -> String? {
        guard let raw, !raw.isEmpty else { return nil }
        let trimmed = raw.trimmingCharacters(in: CharacterSet(charactersIn: "[]() "))
        let parts = trimmed.split(separator: ",").map { $0.trimmingCharacters(in: .whitespaces) }
        if parts.count == 2 {
            if parts[1].hasPrefix("99") { return "≥ " + parts[0] }
            return parts[0] + " – " + parts[1]
        }
        return "≥ " + trimmed
    }
}

// MARK: - Package reading

enum Shell {
    static func run(_ path: String, _ args: [String]) -> (status: Int32, data: Data) {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: path)
        process.arguments = args
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = FileHandle.nullDevice
        do { try process.run() } catch { return (-1, Data()) }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        return (process.terminationStatus, data)
    }
}

enum PackageReader {
    static func read(_ url: URL) -> PackageInfo {
        var info = PackageInfo(url: url, name: url.deletingPathExtension().lastPathComponent)
        switch url.pathExtension.lowercased() {
        case "zxp": readZXP(url, into: &info)
        case "ccx": readCCX(url, into: &info)
        default: info.readError = L("preview.unknownFormat")
        }
        return info
    }

    private static func usable(_ s: String?) -> String? {
        guard let s = s?.trimmingCharacters(in: .whitespacesAndNewlines),
              !s.isEmpty, !s.hasPrefix("%") else { return nil }  // "%key" = localized placeholder
        return s
    }

    private static func readZXP(_ url: URL, into info: inout PackageInfo) {
        let result = Shell.run("/usr/bin/unzip", ["-p", url.path, "CSXS/manifest.xml"])
        guard result.status == 0, !result.data.isEmpty,
              let doc = try? XMLDocument(data: result.data) else {
            info.readError = L("preview.readError")
            return
        }
        let root = doc.rootElement()
        let bundleName = root?.attribute(forName: "ExtensionBundleName")?.stringValue
        let menu = (try? doc.nodes(forXPath: "//DispatchInfo/UI/Menu"))?.first?.stringValue
        if let name = usable(bundleName) ?? usable(menu) { info.name = name }
        info.version = usable(root?.attribute(forName: "ExtensionBundleVersion")?.stringValue)
        info.bundleId = usable(root?.attribute(forName: "ExtensionBundleId")?.stringValue)

        let nodes = (try? doc.nodes(forXPath: "//HostList/Host")) ?? []
        var seen = Set<String>()
        for node in nodes {
            guard let el = node as? XMLElement,
                  let code = el.attribute(forName: "Name")?.stringValue else { continue }
            let target = HostTarget(code: code,
                                    range: HostCodes.formatRange(el.attribute(forName: "Version")?.stringValue))
            if seen.insert(target.displayName).inserted { info.hosts.append(target) }
        }
    }

    private static func readCCX(_ url: URL, into info: inout PackageInfo) {
        let result = Shell.run("/usr/bin/unzip", ["-p", url.path, "manifest.json"])
        guard result.status == 0,
              let obj = (try? JSONSerialization.jsonObject(with: result.data)) as? [String: Any] else {
            info.readError = L("preview.readError")
            return
        }
        if let name = obj["name"] as? String {
            info.name = name
        } else if let localized = obj["name"] as? [String: Any], let name = localized["default"] as? String {
            info.name = name
        }
        info.version = obj["version"] as? String
        info.bundleId = obj["id"] as? String

        var hosts: [[String: Any]] = []
        if let single = obj["host"] as? [String: Any] { hosts = [single] }
        else if let many = obj["host"] as? [[String: Any]] { hosts = many }
        for h in hosts {
            guard let app = h["app"] as? String else { continue }
            let min = (h["minVersion"] as? String).map { "≥ " + $0 }
            info.hosts.append(HostTarget(code: app, range: min))
        }
    }
}

// MARK: - UPIA

enum UPIA {
    /// Runs the UPIA with the given arguments; returns exit status and combined output.
    static func run(_ args: [String], timeout: TimeInterval = 180) async -> (status: Int32, output: String) {
        await withCheckedContinuation { cont in
            DispatchQueue.global(qos: .userInitiated).async {
                guard FileManager.default.isExecutableFile(atPath: upiaPath) else {
                    cont.resume(returning: (-1, LF("error.upiaNotFound", upiaPath)))
                    return
                }
                let process = Process()
                process.executableURL = URL(fileURLWithPath: upiaPath)
                process.arguments = args
                let pipe = Pipe()
                process.standardOutput = pipe
                process.standardError = pipe
                do {
                    try process.run()
                } catch {
                    cont.resume(returning: (-1, LF("error.upiaLaunch", error.localizedDescription)))
                    return
                }
                // The UPIA can hang if Creative Cloud is not signed in
                let watchdog = DispatchWorkItem { if process.isRunning { process.terminate() } }
                DispatchQueue.global().asyncAfter(deadline: .now() + timeout, execute: watchdog)

                let data = pipe.fileHandleForReading.readDataToEndOfFile()
                process.waitUntilExit()
                watchdog.cancel()

                var output = String(decoding: data, as: UTF8.self)
                if process.terminationReason == .uncaughtSignal {
                    output += "\n" + LF("error.upiaTimeout", Int32(timeout))
                }
                cont.resume(returning: (process.terminationStatus, output))
            }
        }
    }

    static func succeeded(_ output: String) -> Bool {
        output.localizedCaseInsensitiveContains("Successful")
    }

    static func lastLine(_ output: String) -> String {
        output.components(separatedBy: .newlines)
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .last { !$0.isEmpty } ?? L("error.unknown")
    }

    /// Parses the output of `--list all`.
    static func parseList(_ text: String) -> [HostApp] {
        let header = try! NSRegularExpression(
            pattern: #"^\s*\d+\s+extensions?\s+installed\s+for\s+(.+?)(?:\s+\(ver\s+([^)]+)\))?\s*$"#)
        let row = try! NSRegularExpression(
            pattern: #"^\s*(Enabled|Disabled)\s{2,}(.+?)\s{2,}(\S+)\s*$"#)

        var apps: [HostApp] = []
        for line in text.components(separatedBy: .newlines) {
            let range = NSRange(line.startIndex..., in: line)
            if let m = header.firstMatch(in: line, range: range),
               let nameRange = Range(m.range(at: 1), in: line) {
                let version = Range(m.range(at: 2), in: line).map { String(line[$0]) }
                apps.append(HostApp(name: String(line[nameRange]), version: version, extensions: []))
            } else if let m = row.firstMatch(in: line, range: range), !apps.isEmpty,
                      let s = Range(m.range(at: 1), in: line),
                      let n = Range(m.range(at: 2), in: line),
                      let v = Range(m.range(at: 3), in: line) {
                apps[apps.count - 1].extensions.append(
                    AdobeExtension(name: String(line[n]), version: String(line[v]), enabled: line[s] == "Enabled"))
            }
        }
        return apps
    }
}

// MARK: - Model

@MainActor
final class Model: ObservableObject {
    static let shared = Model()

    @Published var apps: [HostApp] = []
    @Published var busy = false
    @Published var log = ""
    @Published var showEmptyApps = false

    // UI state lives here instead of @State: the @State macro needs full Xcode
    // and does not compile with the Command Line Tools alone.
    @Published var selection: AdobeExtension.ID?
    @Published var pendingRemoval: AdobeExtension?
    @Published var dropTargeted = false
    @Published var importing = false

    // Preview of the packages about to be installed
    @Published var previewItems: [PackageInfo] = []
    @Published var showingPreview = false

    var previewStarted: Bool { previewItems.contains { $0.outcome != .pending } }
    var previewFinished: Bool {
        !previewItems.isEmpty && previewItems.allSatisfy { $0.outcome != .pending && $0.outcome != .running }
    }

    func refresh() async {
        busy = true
        let result = await UPIA.run(["--list", "all"])
        apps = UPIA.parseList(result.output)
        if apps.isEmpty { appendLog(L("log.listTitle"), result.output) }
        busy = false
    }

    /// Reads the packages and opens the preview (entry point for drag & drop, "+" and the Dock).
    func prepare(_ urls: [URL]) async {
        let valid = urls.filter { supportedExtensions.contains($0.pathExtension.lowercased()) }
        for skipped in urls where !valid.contains(skipped) {
            appendLog(L("log.ignored"), LF("log.ignoredDetail", skipped.lastPathComponent))
        }
        guard !valid.isEmpty else { return }
        guard !busy, !showingPreview else {
            appendLog(L("log.wait"), L("log.waitDetail"))
            return
        }

        var items = await Task.detached { valid.map { PackageReader.read($0) } }.value

        let installed = apps.flatMap { $0.extensions }
        for i in items.indices {
            let name = items[i].name
            if let match = installed.first(where: { $0.name.caseInsensitiveCompare(name) == .orderedSame }) {
                items[i].installedVersion = match.version
            }
        }
        previewItems = items
        showingPreview = true
    }

    func installPreviewed() async {
        guard !busy else { return }
        busy = true
        for i in previewItems.indices {
            previewItems[i].outcome = .running
            let item = previewItems[i]
            let result = await UPIA.run(["--install", item.url.path])
            if UPIA.succeeded(result.output) {
                appendLog(LF("log.installed", item.name))
                previewItems[i].outcome = .success
            } else {
                // Adobe's own message is only shown when something goes wrong
                appendLog(LF("log.installFailed", item.name), result.output)
                previewItems[i].outcome = .failure(UPIA.lastLine(result.output))
            }
        }
        busy = false
        await refresh()
    }

    func closePreview() {
        showingPreview = false
        previewItems = []
    }

    func remove(_ ext: AdobeExtension) async {
        guard !busy else { return }
        busy = true
        let result = await UPIA.run(["--remove", ext.name])
        if UPIA.succeeded(result.output) {
            appendLog(LF("log.removed", ext.name))
        } else {
            appendLog(LF("log.removeFailed", ext.name), result.output)
        }
        busy = false
        await refresh()
    }

    /// Adds a line to the details panel; `text`, when given, is shown below it.
    func appendLog(_ title: String, _ text: String? = nil) {
        guard let text else {
            log += "▸ \(title)\n\n"
            return
        }
        let body = text.trimmingCharacters(in: .whitespacesAndNewlines)
        log += "▸ \(title)\n\(body.isEmpty ? L("log.noOutput") : body)\n\n"
    }
}

// MARK: - Preview

struct PackageCard: View {
    let item: PackageInfo
    let apps: [HostApp]

    private func isInstalled(_ host: HostTarget) -> Bool {
        apps.contains { $0.name.localizedCaseInsensitiveContains(host.displayName) }
    }

    @ViewBuilder
    private var outcomeBadge: some View {
        switch item.outcome {
        case .pending:
            EmptyView()
        case .running:
            ProgressView().controlSize(.small)
        case .success:
            Label(L("preview.status.installed"), systemImage: "checkmark.circle.fill")
                .font(.caption).foregroundStyle(Color.green)
        case .failure:
            Label(L("preview.status.error"), systemImage: "xmark.octagon.fill")
                .font(.caption).foregroundStyle(Color.red)
        }
    }

    private func hostRow(_ host: HostTarget) -> some View {
        let installed = isInstalled(host)
        let range = host.range.map { " (" + $0 + ")" } ?? ""
        return HStack(spacing: 6) {
            Image(systemName: installed ? "app.badge.checkmark" : "app.dashed")
                .foregroundStyle(installed ? Color.green : Color.orange)
            Text(host.displayName + range)
            if !installed {
                Text(L("preview.notInstalled")).foregroundStyle(Color.orange)
            }
        }
        .font(.callout)
    }

    private var header: some View {
        HStack(alignment: .firstTextBaseline) {
            Text(item.name).font(.headline)
            if let version = item.version {
                Text(version).monospacedDigit().foregroundStyle(.secondary)
            }
            Spacer()
            outcomeBadge
        }
    }

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "puzzlepiece.extension.fill")
                .font(.title)
                .foregroundStyle(Color.accentColor)
            VStack(alignment: .leading, spacing: 5) {
                header
                Text(item.bundleId ?? item.url.lastPathComponent)
                    .font(.caption).foregroundStyle(.secondary)
                if let error = item.readError {
                    Label(error, systemImage: "exclamationmark.triangle")
                        .font(.caption).foregroundStyle(Color.orange)
                }
                if !item.hosts.isEmpty {
                    Text(L("preview.installsIn")).font(.caption).foregroundStyle(.secondary).padding(.top, 2)
                    ForEach(item.hosts, id: \.self) { host in
                        hostRow(host)
                    }
                }
                if let installed = item.installedVersion, item.outcome == .pending {
                    Label(LF("preview.alreadyInstalled", installed),
                          systemImage: "arrow.triangle.2.circlepath")
                        .font(.caption).foregroundStyle(.secondary).padding(.top, 2)
                }
                if case .failure(let message) = item.outcome {
                    Text(message).font(.caption).foregroundStyle(Color.red).textSelection(.enabled)
                }
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 10).fill(Color.secondary.opacity(0.1)))
    }
}

struct PreviewSheet: View {
    @ObservedObject var model: Model

    private var title: String {
        if model.previewFinished { return L("preview.title.done") }
        let n = model.previewItems.count
        return n == 1 ? L("preview.title.one") : LF("preview.title.many", Int32(n))
    }

    private var buttons: some View {
        HStack {
            if model.busy {
                ProgressView().controlSize(.small)
                Text(L("preview.progress")).foregroundStyle(.secondary)
            }
            Spacer()
            if model.previewFinished {
                Button(L("button.close")) { model.closePreview() }
                    .keyboardShortcut(.defaultAction)
            } else {
                Button(L("button.cancel")) { model.closePreview() }
                    .keyboardShortcut(.cancelAction)
                    .disabled(model.previewStarted)
                Button(L("button.install")) { Task { await model.installPreviewed() } }
                    .keyboardShortcut(.defaultAction)
                    .disabled(model.previewStarted)
            }
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(title).font(.title3).bold()
            ScrollView {
                VStack(spacing: 10) {
                    ForEach(model.previewItems) { item in
                        PackageCard(item: item, apps: model.apps)
                    }
                }
            }
            .frame(maxHeight: 380)
            buttons
        }
        .padding(20)
        .frame(width: 500)
        .interactiveDismissDisabled(model.busy)
    }
}

// MARK: - Main window

struct ContentView: View {
    @ObservedObject var model = Model.shared

    private var visibleApps: [HostApp] {
        model.showEmptyApps ? model.apps : model.apps.filter { !$0.extensions.isEmpty }
    }

    private var selectedExtension: AdobeExtension? {
        let all: [AdobeExtension] = model.apps.flatMap { $0.extensions }
        return all.first { $0.id == model.selection }
    }

    private func sectionTitle(_ app: HostApp) -> String {
        if let version = app.version {
            return app.name + " — " + version
        }
        return app.name
    }

    private var removalBinding: Binding<Bool> {
        Binding(get: { model.pendingRemoval != nil },
                set: { if !$0 { model.pendingRemoval = nil } })
    }

    private var allowedTypes: [UTType] {
        supportedExtensions.compactMap { UTType(filenameExtension: $0) }
    }

    /// Describes what clicking the eye toggle will do.
    private var emptyAppsToggleText: String {
        model.showEmptyApps ? L("toolbar.hideEmpty") : L("toolbar.showEmpty")
    }

    private func extensionRow(_ ext: AdobeExtension) -> some View {
        HStack {
            Image(systemName: ext.enabled ? "checkmark.circle.fill" : "pause.circle")
                .foregroundStyle(ext.enabled ? Color.green : Color.secondary)
            Text(ext.name)
            Spacer()
            Text(ext.version).monospacedDigit().foregroundStyle(.secondary)
        }
    }

    private var extensionList: some View {
        List(selection: $model.selection) {
            ForEach(visibleApps) { app in
                Section(sectionTitle(app)) {
                    if app.extensions.isEmpty {
                        Text(L("list.noExtensions")).foregroundStyle(.secondary)
                    }
                    ForEach(app.extensions) { ext in
                        extensionRow(ext).tag(ext.id)
                    }
                }
            }
        }
        .overlay {
            if visibleApps.isEmpty && !model.busy {
                VStack(spacing: 8) {
                    Image(systemName: "puzzlepiece.extension").font(.largeTitle)
                    Text(L("list.empty.title"))
                    Text(L("list.empty.hint"))
                        .font(.callout).foregroundStyle(.secondary)
                }
            }
        }
    }

    /// Always-visible strip that tells the user files can be dropped here (click = file picker).
    private var dropHint: some View {
        HStack(spacing: 8) {
            Image(systemName: "arrow.down.doc")
            Text(L("list.empty.hint"))
        }
        .font(.callout)
        .foregroundStyle(.secondary)
        .frame(maxWidth: .infinity)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .strokeBorder(Color.secondary.opacity(0.5), style: StrokeStyle(lineWidth: 1.5, dash: [6, 4]))
        )
        .contentShape(Rectangle())
        .onTapGesture { if !model.busy { model.importing = true } }
        .help(L("toolbar.install"))
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
    }

    /// Feedback shown while a file is being dragged over the window.
    private var dropOverlay: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 12)
                .fill(Color.accentColor.opacity(0.12))
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 3, dash: [10, 6]))
            VStack(spacing: 10) {
                Image(systemName: "arrow.down.doc.fill").font(.system(size: 44))
                Text(L("drop.release")).font(.title3).bold()
            }
            .foregroundStyle(Color.accentColor)
        }
        .padding(8)
        .allowsHitTesting(false)
    }

    /// List + hint strip: the whole area accepts dropped files.
    private var dropArea: some View {
        VStack(spacing: 0) {
            extensionList
            if !visibleApps.isEmpty {
                dropHint
            }
        }
        .overlay {
            if model.dropTargeted { dropOverlay }
        }
        .onDrop(of: [.fileURL], isTargeted: $model.dropTargeted, perform: handleDrop)
    }

    private var logPanel: some View {
        ScrollView {
            Text(model.log.isEmpty ? L("log.placeholder") : model.log)
                .font(.system(.caption, design: .monospaced))
                .foregroundStyle(model.log.isEmpty ? .secondary : .primary)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(8)
        }
        .frame(height: 130)
    }

    var body: some View {
        VStack(spacing: 0) {
            dropArea
            Divider()
            logPanel
        }
        .frame(minWidth: 560, minHeight: 440)
        .toolbar {
            ToolbarItemGroup {
                if model.busy { ProgressView().controlSize(.small) }
                Button { model.importing = true } label: {
                    Label(L("toolbar.install"), systemImage: "plus")
                }
                .help(L("toolbar.install"))
                .disabled(model.busy)
                Button { model.pendingRemoval = selectedExtension } label: {
                    Label(L("toolbar.remove"), systemImage: "trash")
                }
                .help(L("toolbar.remove"))
                .disabled(selectedExtension == nil || model.busy)
                Button { Task { await model.refresh() } } label: {
                    Label(L("toolbar.refresh"), systemImage: "arrow.clockwise")
                }
                .help(L("toolbar.refresh"))
                .disabled(model.busy)
                Toggle(isOn: $model.showEmptyApps) {
                    Label(emptyAppsToggleText, systemImage: "eye")
                }
                .help(emptyAppsToggleText)
            }
        }
        .fileImporter(isPresented: $model.importing, allowedContentTypes: allowedTypes, allowsMultipleSelection: true) { result in
            if case .success(let urls) = result {
                Task { await model.prepare(urls) }
            }
        }
        .sheet(isPresented: $model.showingPreview) {
            PreviewSheet(model: model)
        }
        .confirmationDialog(
            L("remove.confirm.title"),
            isPresented: removalBinding,
            presenting: model.pendingRemoval
        ) { ext in
            Button(LF("remove.confirm.button", ext.name), role: .destructive) {
                Task { await model.remove(ext) }
            }
        } message: { _ in
            Text(L("remove.confirm.message"))
        }
        .task { await model.refresh() }
    }

    private func handleDrop(_ providers: [NSItemProvider]) -> Bool {
        let group = DispatchGroup()
        let lock = NSLock()
        var urls: [URL] = []
        for provider in providers where provider.canLoadObject(ofClass: URL.self) {
            group.enter()
            _ = provider.loadObject(ofClass: URL.self) { url, _ in
                if let url {
                    lock.lock(); urls.append(url); lock.unlock()
                }
                group.leave()
            }
        }
        group.notify(queue: .main) {
            Task { await Model.shared.prepare(urls) }
        }
        return true
    }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate {
    // Files dropped on the Dock icon or opened with "Open With"
    func application(_ application: NSApplication, open urls: [URL]) {
        Task { @MainActor in await Model.shared.prepare(urls) }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@main
struct ZXPManagerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    init() {
        // Show tooltips after 500 ms (macOS default is ~1.5 s), like browsers and Windows.
        // register(defaults:) only sets the fallback: a value set by the user with
        // `defaults write com.oddmoth.zxpmanager NSInitialToolTipDelay` still wins.
        UserDefaults.standard.register(defaults: ["NSInitialToolTipDelay": 500])
    }

    var body: some Scene {
        Window("ZXP Manager", id: "main") {
            ContentView()
        }
    }
}
