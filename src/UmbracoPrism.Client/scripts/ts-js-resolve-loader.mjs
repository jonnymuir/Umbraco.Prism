// Node's built-in TypeScript type-stripping (used by these scripts, no ts-node/tsx
// dependency) strips types but does not remap ".js" import specifiers to sibling
// ".ts" files — the convention every .ts file in this project uses when importing
// another .ts file, anticipating a real tsc/bundler compile step. Without this hook,
// any script that imports a non-leaf .ts module (one with its own relative imports)
// fails with ERR_MODULE_NOT_FOUND. Register via --import with register-ts-js-resolve.mjs.
export async function resolve(specifier, context, nextResolve) {
  if (specifier.endsWith('.js') && /\.tsx?$/.test(context.parentURL ?? '')) {
    try {
      return await nextResolve(specifier.replace(/\.js$/, '.ts'), context);
    } catch {
      // No .ts sibling — fall through to normal resolution (e.g. a real .js dependency).
    }
  }
  return nextResolve(specifier, context);
}
