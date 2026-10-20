import AuthenticationServices
import Capacitor

/// Opens the system sign-in sheet for prism-system-sign-in.js and returns the callback URL. The
/// callback is an https URL on the tenant's own site, which iOS hands only to this app because
/// the site's apple-app-site-association names it. The session is ephemeral, so no identity
/// provider session outlives the sign-in.
@objc(PrismSystemSignInPlugin)
public class PrismSystemSignInPlugin: CAPPlugin, CAPBridgedPlugin, ASWebAuthenticationPresentationContextProviding {
    public let identifier = "PrismSystemSignInPlugin"
    public let jsName = "PrismSystemSignIn"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(name: "authenticate", returnType: CAPPluginReturnPromise)
    ]
    private var session: ASWebAuthenticationSession?

    @objc func authenticate(_ call: CAPPluginCall) {
        guard let url = call.getString("url").flatMap(URL.init(string:)), PrismShell.isOwnSite(url),
              call.getString("callbackHost") == PrismShell.ownHost, let path = call.getString("callbackPath") else {
            call.reject("Sign-in must start and end on this app's own site.")
            return
        }

        PrismShell.log.info("Sign-in sheet opening")
        DispatchQueue.main.async {
            let session = ASWebAuthenticationSession(url: url, callback: .https(host: PrismShell.ownHost, path: path)) { [weak self] callbackURL, error in
                self?.session = nil
                if let callbackURL {
                    PrismShell.log.info("Sign-in sheet returned to the app")
                    call.resolve(["url": callbackURL.absoluteString])
                } else {
                    // An app the site does not name in apple-app-site-association fails here too,
                    // with an error that says so.
                    PrismShell.log.warning("Sign-in sheet ended without a callback: \(error?.localizedDescription ?? "unknown", privacy: .public)")
                    let cancelled = (error as? ASWebAuthenticationSessionError)?.code == .canceledLogin
                    call.reject(error?.localizedDescription ?? "Sign-in failed.", cancelled ? "CANCELLED" : "FAILED", error)
                }
            }
            session.prefersEphemeralWebBrowserSession = true
            session.presentationContextProvider = self
            self.session = session
            if !session.start() {
                self.session = nil
                call.reject("Sign-in could not start.", "FAILED")
            }
        }
    }

    public func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        bridge?.viewController?.view.window ?? ASPresentationAnchor()
    }
}
