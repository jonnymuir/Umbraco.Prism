export type ServiceBlueprintShortcutMatcher = {
  key: string;
  ctrlOrMeta?: boolean;
  shift?: boolean;
  alt?: boolean;
};

export type ServiceBlueprintShortcutDefinition = {
  id: string;
  command: string;
  context: string;
  description: string;
  labels: string[];
  ariaKeys?: string;
  matchers: ServiceBlueprintShortcutMatcher[];
};

export type ServiceBlueprintShortcutGroup = {
  id: string;
  title: string;
  shortcuts: ServiceBlueprintShortcutDefinition[];
};

export const SERVICE_BLUEPRINT_SHORTCUT_GROUPS: ServiceBlueprintShortcutGroup[] = [
  {
    id: 'editor',
    title: 'Editor-wide',
    shortcuts: [
      {
        id: 'save',
        command: 'Save serviceBlueprint',
        context: 'Anywhere in the editor',
        description: 'Saves the current serviceBlueprint when validation allows it.',
        labels: ['Ctrl/Cmd+S'],
        ariaKeys: 'Control+S Meta+S',
        matchers: [{ key: 's', ctrlOrMeta: true }],
      },
      {
        id: 'undo',
        command: 'Undo last change',
        context: 'Anywhere in the editor',
        description: 'Steps back through the editor history.',
        labels: ['Ctrl/Cmd+Z'],
        ariaKeys: 'Control+Z Meta+Z',
        matchers: [{ key: 'z', ctrlOrMeta: true }],
      },
      {
        id: 'redo',
        command: 'Redo change',
        context: 'Anywhere in the editor',
        description: 'Replays the next available change in history.',
        labels: ['Ctrl/Cmd+Y', 'Ctrl/Cmd+Shift+Z'],
        ariaKeys: 'Control+Y Meta+Y Control+Shift+Z Meta+Shift+Z',
        matchers: [
          { key: 'y', ctrlOrMeta: true },
          { key: 'z', ctrlOrMeta: true, shift: true },
        ],
      },
      {
        id: 'copy',
        command: 'Copy selected stage or action',
        context: 'Selected stage or action',
        description: 'Copies the current stage or action into the editor clipboard.',
        labels: ['Ctrl/Cmd+C'],
        ariaKeys: 'Control+C Meta+C',
        matchers: [{ key: 'c', ctrlOrMeta: true }],
      },
      {
        id: 'paste',
        command: 'Paste copied stage or action',
        context: 'Selected stage or route',
        description: 'Pastes the current clipboard item into the selected destination.',
        labels: ['Ctrl/Cmd+V'],
        ariaKeys: 'Control+V Meta+V',
        matchers: [{ key: 'v', ctrlOrMeta: true }],
      },
      {
        id: 'help',
        command: 'Open help and shortcuts',
        context: 'Anywhere in the editor',
        description: 'Opens the shortcut reference without leaving the service blueprint.',
        labels: ['F1'],
        ariaKeys: 'F1',
        matchers: [{ key: 'f1' }],
      },
    ],
  },
  {
    id: 'workspace',
    title: 'Graph and list workspace',
    shortcuts: [
      {
        id: 'select-item',
        command: 'Select the focused item',
        context: 'Graph nodes, route chips, or list rows',
        description: 'Keeps the current item selected without moving focus away.',
        labels: ['Enter', 'Space'],
        matchers: [{ key: 'enter' }, { key: ' ' }],
      },
      {
        id: 'open-inspector',
        command: 'Open the inspector',
        context: 'Focused stage, route, gateway, or list row',
        description: 'Moves into the inspector for deeper editing.',
        labels: ['E'],
        matchers: [{ key: 'e' }],
      },
      {
        id: 'workspace-menu',
        command: 'Open the context menu',
        context: 'Focused stage, route, gateway, or list row',
        description: 'Opens the focused item actions from the keyboard.',
        labels: ['Shift+F10'],
        matchers: [{ key: 'f10', shift: true }],
      },
      {
        id: 'delete-stage-transition',
        command: 'Delete the selected stage or route',
        context: 'Focused stage, route, or list row',
        description: 'Opens delete confirmation or removes the focused route.',
        labels: ['Delete', 'Backspace'],
        matchers: [{ key: 'delete' }, { key: 'backspace' }],
      },
      {
        id: 'navigate-list',
        command: 'Move between list rows',
        context: 'List workspace',
        description: 'Moves focus through visible rows without leaving the table.',
        labels: ['↑', '↓', 'Home', 'End'],
        matchers: [{ key: 'arrowup' }, { key: 'arrowdown' }, { key: 'home' }, { key: 'end' }],
      },
      {
        id: 'reorder-stage',
        command: 'Reorder a stage',
        context: 'List workspace',
        description: 'Moves the focused stage earlier or later in the service blueprint.',
        labels: ['Alt+↑', 'Alt+↓'],
        matchers: [
          { key: 'arrowup', alt: true },
          { key: 'arrowdown', alt: true },
        ],
      },
    ],
  },
  {
    id: 'actions',
    title: 'Action editor',
    shortcuts: [
      {
        id: 'reorder-action',
        command: 'Reorder an action',
        context: 'Focused action card',
        description: 'Moves the current action up or down inside its list.',
        labels: ['Alt+↑', 'Alt+↓'],
        matchers: [
          { key: 'arrowup', alt: true },
          { key: 'arrowdown', alt: true },
        ],
      },
      {
        id: 'delete-action',
        command: 'Delete an action',
        context: 'Focused action card',
        description: 'Opens the delete confirmation for the focused action.',
        labels: ['Delete', 'Backspace'],
        matchers: [{ key: 'delete' }, { key: 'backspace' }],
      },
      {
        id: 'reorder-form-field',
        command: 'Reorder a form field',
        context: 'Focused form field row',
        description: 'Moves the current form field earlier or later in the schema.',
        labels: ['Alt+↑', 'Alt+↓'],
        matchers: [
          { key: 'arrowup', alt: true },
          { key: 'arrowdown', alt: true },
        ],
      },
    ],
  },
];

export const SERVICE_BLUEPRINT_SHORTCUTS = SERVICE_BLUEPRINT_SHORTCUT_GROUPS.flatMap(group => group.shortcuts);

export function findServiceBlueprintShortcut(id: string): ServiceBlueprintShortcutDefinition | undefined {
  return SERVICE_BLUEPRINT_SHORTCUTS.find(shortcut => shortcut.id === id);
}

export function matchesShortcut(event: KeyboardEvent, shortcut: ServiceBlueprintShortcutDefinition): boolean {
  const key = event.key.length === 1 ? event.key.toLowerCase() : event.key.toLowerCase();
  return shortcut.matchers.some(matcher =>
    key === matcher.key.toLowerCase()
    && Boolean(matcher.ctrlOrMeta) === Boolean(event.ctrlKey || event.metaKey)
    && Boolean(matcher.shift) === Boolean(event.shiftKey)
    && Boolean(matcher.alt) === Boolean(event.altKey)
  );
}
