import Capacitor
import WebKit

/// Removes the identity provider's own session cookies from the shared cookie store on sign-out
/// (see prism-biometric-signout.js), since the provider's pages cannot be asked to do it.
@objc(PrismIdentityCookiePlugin)
public class PrismIdentityCookiePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "PrismIdentityCookiePlugin"
    public let jsName = "PrismIdentityCookiePlugin"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(#selector(clearCookies))
    ]

    @objc func clearCookies(_ call: CAPPluginCall) {
        let hosts = call.getArray("hosts", String.self) ?? []
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
