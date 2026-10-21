import ActivityKit
import SwiftUI
import WidgetKit

@main
struct PrismJourneyStatusBundle: WidgetBundle {
    var body: some Widget {
        PrismJourneyStatusWidget()
    }
}

/// A journey's Live Activity, on the lock screen and in the Dynamic Island. Drawn in the system's
/// own style, which keeps its contrast in light and dark mode.
struct PrismJourneyStatusWidget: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: PrismJourneyStatusAttributes.self) { context in
            PrismJourneyStatusCard(attributes: context.attributes, state: context.state)
                .padding(16)
                .widgetURL(context.attributes.url)
        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.bottom) {
                    PrismJourneyStatusCard(attributes: context.attributes, state: context.state)
                }
            } compactLeading: {
                Image(systemName: "hourglass").foregroundStyle(.tint)
            } compactTrailing: {
                if let step = context.state.step, let steps = context.state.steps {
                    Text("\(step)/\(steps)").accessibilityLabel("Step \(step) of \(steps)")
                }
            } minimal: {
                Image(systemName: "hourglass").foregroundStyle(.tint)
            }
            .widgetURL(context.attributes.url)
        }
    }
}

struct PrismJourneyStatusCard: View {
    let attributes: PrismJourneyStatusAttributes
    let state: PrismJourneyStatusAttributes.ContentState

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(attributes.serviceName).font(.subheadline).foregroundStyle(.secondary)
            Text(state.status).font(.headline)
            if let step = state.step, let steps = state.steps, steps > 0 {
                ProgressView(value: Double(step), total: Double(steps))
                Text("Step \(step) of \(steps)").font(.caption).foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
    }
}
