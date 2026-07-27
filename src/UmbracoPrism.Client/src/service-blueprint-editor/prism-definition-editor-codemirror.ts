/**
 * @internal CodeMirror 6 setup for the Definition tab. Lives in its own module
 * so `prism-definition-editor.ts` can dynamically import it — keeping CM6 out
 * of the main editor bundle until an author opens the Definition tab.
 */

import { EditorState, Compartment, StateEffect, StateField } from '@codemirror/state';
import {
  EditorView,
  keymap,
  highlightActiveLine,
  highlightActiveLineGutter,
  drawSelection,
  lineNumbers,
  highlightSpecialChars,
} from '@codemirror/view';
import {
  defaultKeymap,
  history,
  historyKeymap,
  indentWithTab,
} from '@codemirror/commands';
import { search, searchKeymap } from '@codemirror/search';
import {
  bracketMatching,
  defaultHighlightStyle,
  syntaxHighlighting,
  indentOnInput,
} from '@codemirror/language';
import { json } from '@codemirror/lang-json';
import {
  setDiagnostics,
  type Diagnostic,
  lintGutter,
} from '@codemirror/lint';

export const setDiagnosticsEffect = StateEffect.define<Diagnostic[]>();

const readOnlyCompartment = new Compartment();

const diagnosticsField = StateField.define<Diagnostic[]>({
  create: () => [],
  update(prev, tr) {
    let next = prev;
    for (const effect of tr.effects) {
      if (effect.is(setDiagnosticsEffect)) {
        next = effect.value;
      }
    }
    return next;
  },
});

export interface CreateDefinitionViewOptions {
  parent: HTMLElement;
  doc: string;
  readOnly: boolean;
  onChange: (value: string) => void;
}

export function createDefinitionView({
  parent,
  doc,
  readOnly,
  onChange,
}: CreateDefinitionViewOptions): EditorView {
  const updateListener = EditorView.updateListener.of(update => {
    if (update.docChanged) {
      onChange(update.state.doc.toString());
    }
    // When the diagnostics field changes, push them through the linter API so
    // CM6 can render gutter markers and tooltips.
    for (const tr of update.transactions) {
      for (const effect of tr.effects) {
        if (effect.is(setDiagnosticsEffect)) {
          update.view.dispatch(setDiagnostics(update.state, effect.value));
        }
      }
    }
  });

  const state = EditorState.create({
    doc,
    extensions: [
      lineNumbers(),
      highlightActiveLine(),
      highlightActiveLineGutter(),
      highlightSpecialChars(),
      drawSelection(),
      indentOnInput(),
      bracketMatching(),
      syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
      json(),
      lintGutter(),
      diagnosticsField,
      search({ top: true }),
      keymap.of([...defaultKeymap, ...historyKeymap, ...searchKeymap, indentWithTab]),
      history(),
      readOnlyCompartment.of(EditorState.readOnly.of(readOnly)),
      EditorView.contentAttributes.of({
        'aria-label': 'Service blueprint definition JSON editor',
        'data-prism-definition-editor-input': 'true',
        'spellcheck': 'false',
      }),
      updateListener,
    ],
  });

  return new EditorView({ state, parent });
}

// Re-export for the host so it can dispatch the read-only compartment update.
export const setReadOnlyDispatch = (view: EditorView, readOnly: boolean): void => {
  view.dispatch({
    effects: readOnlyCompartment.reconfigure(EditorState.readOnly.of(readOnly)),
  });
};
