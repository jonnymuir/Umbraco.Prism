/**
 * Optional UX hint passed by the host to the editor.
 *
 * Never authoritative — `ServiceBlueprintSource.save` is the only enforcement point.
 * Used purely to grey out the Save button or stamp a "viewing as ${name}"
 * badge so authors get a clear signal before they try to save.
 */
export interface ServiceBlueprintAuthorContext {
  /** When `false`, the editor disables the Save button. Defaults to enabled. */
  canSave?: boolean;

  /** Optional display name for the author currently viewing the editor. */
  displayName?: string;
}
