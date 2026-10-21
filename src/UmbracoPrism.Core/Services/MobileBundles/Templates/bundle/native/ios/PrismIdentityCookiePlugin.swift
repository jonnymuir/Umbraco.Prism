import Capacitor
import WebKit

/// Removes the identity provider's own session cookies from the shared cookie store on sign-out
/// (see prism-biometric-signout.js), since the provider's pages cannot be asked to do it. The
/// site's own cookies are never touched: the sign-out request still needs them, and locally the
/// identity provider can share the site's host name on another port.
@objc(PrismIdentityCookiePlugin)
public class PrismIdentityCookiePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "PrismIdentityCookiePlugin"
    public let jsName = "PrismIdentityCookiePlugin"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(#selector(clearCookies))
    ]

    @objc func clearCookies(_ call: CAPPluginCall) {
        // A cookie belongs to a host name whatever the port, so the port is dropped first.
        let hosts = (call.getArray("hosts", String.self) ?? [])
            .compactMap { URL(string: "https://" + $0)?.host?.lowercased() }
            .filter { $0 != PrismShell.origin.host?.lowercased() }
        guard !hosts.isEmpty else {
            call.resolve(["cleared": 0])
            return
        }

        let store = WKWebsiteDataStore.default()
        store.fetchDataRecords(ofTypes: [WKWebsiteDataTypeCookies]) { records in
            let matching = records.filter { record in
                hosts.contains { host in record.displayName == host || record.displayName.hasSuffix("." + host) }
            }
            guard !matching.isEmpty else {
                DispatchQueue.main.async { call.resolve(["cleared": 0]) }
                return
            }
            store.removeData(ofTypes: [WKWebsiteDataTypeCookies], for: matching) {
                DispatchQueue.main.async { call.resolve(["cleared": matching.count]) }
            }
        }
    }
}
