import { UmbElementMixin as rt } from "@umbraco-cms/backoffice/element-api";
import { UMB_MODAL_MANAGER_CONTEXT as mt } from "@umbraco-cms/backoffice/modal";
import { UMB_AUTH_CONTEXT as nt } from "@umbraco-cms/backoffice/auth";
import { umbHttpClient as ot } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as at } from "@umbraco-cms/backoffice/resources";
const M = globalThis, z = M.ShadowRoot && (M.ShadyCSS === void 0 || M.ShadyCSS.nativeShadow) && "adoptedStyleSheets" in Document.prototype && "replace" in CSSStyleSheet.prototype, L = /* @__PURE__ */ Symbol(), J = /* @__PURE__ */ new WeakMap();
let ht = class {
  constructor(t, e, s) {
    if (this._$cssResult$ = !0, s !== L) throw Error("CSSResult is not constructable. Use `unsafeCSS` or `css` instead.");
    this.cssText = t, this.t = e;
  }
  get styleSheet() {
    let t = this.o;
    const e = this.t;
    if (z && t === void 0) {
      const s = e !== void 0 && e.length === 1;
      s && (t = J.get(e)), t === void 0 && ((this.o = t = new CSSStyleSheet()).replaceSync(this.cssText), s && J.set(e, t));
    }
    return t;
  }
  toString() {
    return this.cssText;
  }
};
const ft = (r) => new ht(typeof r == "string" ? r : r + "", void 0, L), lt = (r, ...t) => {
  const e = r.length === 1 ? r[0] : t.reduce((s, i, n) => s + ((o) => {
    if (o._$cssResult$ === !0) return o.cssText;
    if (typeof o == "number") return o;
    throw Error("Value passed to 'css' function must be a 'css' function result: " + o + ". Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.");
  })(i) + r[n + 1], r[0]);
  return new ht(e, r, L);
}, yt = (r, t) => {
  if (z) r.adoptedStyleSheets = t.map((e) => e instanceof CSSStyleSheet ? e : e.styleSheet);
  else for (const e of t) {
    const s = document.createElement("style"), i = M.litNonce;
    i !== void 0 && s.setAttribute("nonce", i), s.textContent = e.cssText, r.appendChild(s);
  }
}, K = z ? (r) => r : (r) => r instanceof CSSStyleSheet ? ((t) => {
  let e = "";
  for (const s of t.cssRules) e += s.cssText;
  return ft(e);
})(r) : r;
const { is: gt, defineProperty: At, getOwnPropertyDescriptor: bt, getOwnPropertyNames: vt, getOwnPropertySymbols: Et, getPrototypeOf: St } = Object, R = globalThis, X = R.trustedTypes, Ct = X ? X.emptyScript : "", wt = R.reactiveElementPolyfillSupport, C = (r, t) => r, U = { toAttribute(r, t) {
  switch (t) {
    case Boolean:
      r = r ? Ct : null;
      break;
    case Object:
    case Array:
      r = r == null ? r : JSON.stringify(r);
  }
  return r;
}, fromAttribute(r, t) {
  let e = r;
  switch (t) {
    case Boolean:
      e = r !== null;
      break;
    case Number:
      e = r === null ? null : Number(r);
      break;
    case Object:
    case Array:
      try {
        e = JSON.parse(r);
      } catch {
        e = null;
      }
  }
  return e;
} }, B = (r, t) => !gt(r, t), F = { attribute: !0, type: String, converter: U, reflect: !1, useDefault: !1, hasChanged: B };
Symbol.metadata ??= /* @__PURE__ */ Symbol("metadata"), R.litPropertyMetadata ??= /* @__PURE__ */ new WeakMap();
let g = class extends HTMLElement {
  static addInitializer(t) {
    this._$Ei(), (this.l ??= []).push(t);
  }
  static get observedAttributes() {
    return this.finalize(), this._$Eh && [...this._$Eh.keys()];
  }
  static createProperty(t, e = F) {
    if (e.state && (e.attribute = !1), this._$Ei(), this.prototype.hasOwnProperty(t) && ((e = Object.create(e)).wrapped = !0), this.elementProperties.set(t, e), !e.noAccessor) {
      const s = /* @__PURE__ */ Symbol(), i = this.getPropertyDescriptor(t, s, e);
      i !== void 0 && At(this.prototype, t, i);
    }
  }
  static getPropertyDescriptor(t, e, s) {
    const { get: i, set: n } = bt(this.prototype, t) ?? { get() {
      return this[e];
    }, set(o) {
      this[e] = o;
    } };
    return { get: i, set(o) {
      const h = i?.call(this);
      n?.call(this, o), this.requestUpdate(t, h, s);
    }, configurable: !0, enumerable: !0 };
  }
  static getPropertyOptions(t) {
    return this.elementProperties.get(t) ?? F;
  }
  static _$Ei() {
    if (this.hasOwnProperty(C("elementProperties"))) return;
    const t = St(this);
    t.finalize(), t.l !== void 0 && (this.l = [...t.l]), this.elementProperties = new Map(t.elementProperties);
  }
  static finalize() {
    if (this.hasOwnProperty(C("finalized"))) return;
    if (this.finalized = !0, this._$Ei(), this.hasOwnProperty(C("properties"))) {
      const e = this.properties, s = [...vt(e), ...Et(e)];
      for (const i of s) this.createProperty(i, e[i]);
    }
    const t = this[Symbol.metadata];
    if (t !== null) {
      const e = litPropertyMetadata.get(t);
      if (e !== void 0) for (const [s, i] of e) this.elementProperties.set(s, i);
    }
    this._$Eh = /* @__PURE__ */ new Map();
    for (const [e, s] of this.elementProperties) {
      const i = this._$Eu(e, s);
      i !== void 0 && this._$Eh.set(i, e);
    }
    this.elementStyles = this.finalizeStyles(this.styles);
  }
  static finalizeStyles(t) {
    const e = [];
    if (Array.isArray(t)) {
      const s = new Set(t.flat(1 / 0).reverse());
      for (const i of s) e.unshift(K(i));
    } else t !== void 0 && e.push(K(t));
    return e;
  }
  static _$Eu(t, e) {
    const s = e.attribute;
    return s === !1 ? void 0 : typeof s == "string" ? s : typeof t == "string" ? t.toLowerCase() : void 0;
  }
  constructor() {
    super(), this._$Ep = void 0, this.isUpdatePending = !1, this.hasUpdated = !1, this._$Em = null, this._$Ev();
  }
  _$Ev() {
    this._$ES = new Promise((t) => this.enableUpdating = t), this._$AL = /* @__PURE__ */ new Map(), this._$E_(), this.requestUpdate(), this.constructor.l?.forEach((t) => t(this));
  }
  addController(t) {
    (this._$EO ??= /* @__PURE__ */ new Set()).add(t), this.renderRoot !== void 0 && this.isConnected && t.hostConnected?.();
  }
  removeController(t) {
    this._$EO?.delete(t);
  }
  _$E_() {
    const t = /* @__PURE__ */ new Map(), e = this.constructor.elementProperties;
    for (const s of e.keys()) this.hasOwnProperty(s) && (t.set(s, this[s]), delete this[s]);
    t.size > 0 && (this._$Ep = t);
  }
  createRenderRoot() {
    const t = this.shadowRoot ?? this.attachShadow(this.constructor.shadowRootOptions);
    return yt(t, this.constructor.elementStyles), t;
  }
  connectedCallback() {
    this.renderRoot ??= this.createRenderRoot(), this.enableUpdating(!0), this._$EO?.forEach((t) => t.hostConnected?.());
  }
  enableUpdating(t) {
  }
  disconnectedCallback() {
    this._$EO?.forEach((t) => t.hostDisconnected?.());
  }
  attributeChangedCallback(t, e, s) {
    this._$AK(t, s);
  }
  _$ET(t, e) {
    const s = this.constructor.elementProperties.get(t), i = this.constructor._$Eu(t, s);
    if (i !== void 0 && s.reflect === !0) {
      const n = (s.converter?.toAttribute !== void 0 ? s.converter : U).toAttribute(e, s.type);
      this._$Em = t, n == null ? this.removeAttribute(i) : this.setAttribute(i, n), this._$Em = null;
    }
  }
  _$AK(t, e) {
    const s = this.constructor, i = s._$Eh.get(t);
    if (i !== void 0 && this._$Em !== i) {
      const n = s.getPropertyOptions(i), o = typeof n.converter == "function" ? { fromAttribute: n.converter } : n.converter?.fromAttribute !== void 0 ? n.converter : U;
      this._$Em = i;
      const h = o.fromAttribute(e, n.type);
      this[i] = h ?? this._$Ej?.get(i) ?? h, this._$Em = null;
    }
  }
  requestUpdate(t, e, s, i = !1, n) {
    if (t !== void 0) {
      const o = this.constructor;
      if (i === !1 && (n = this[t]), s ??= o.getPropertyOptions(t), !((s.hasChanged ?? B)(n, e) || s.useDefault && s.reflect && n === this._$Ej?.get(t) && !this.hasAttribute(o._$Eu(t, s)))) return;
      this.C(t, e, s);
    }
    this.isUpdatePending === !1 && (this._$ES = this._$EP());
  }
  C(t, e, { useDefault: s, reflect: i, wrapped: n }, o) {
    s && !(this._$Ej ??= /* @__PURE__ */ new Map()).has(t) && (this._$Ej.set(t, o ?? e ?? this[t]), n !== !0 || o !== void 0) || (this._$AL.has(t) || (this.hasUpdated || s || (e = void 0), this._$AL.set(t, e)), i === !0 && this._$Em !== t && (this._$Eq ??= /* @__PURE__ */ new Set()).add(t));
  }
  async _$EP() {
    this.isUpdatePending = !0;
    try {
      await this._$ES;
    } catch (e) {
      Promise.reject(e);
    }
    const t = this.scheduleUpdate();
    return t != null && await t, !this.isUpdatePending;
  }
  scheduleUpdate() {
    return this.performUpdate();
  }
  performUpdate() {
    if (!this.isUpdatePending) return;
    if (!this.hasUpdated) {
      if (this.renderRoot ??= this.createRenderRoot(), this._$Ep) {
        for (const [i, n] of this._$Ep) this[i] = n;
        this._$Ep = void 0;
      }
      const s = this.constructor.elementProperties;
      if (s.size > 0) for (const [i, n] of s) {
        const { wrapped: o } = n, h = this[i];
        o !== !0 || this._$AL.has(i) || h === void 0 || this.C(i, void 0, n, h);
      }
    }
    let t = !1;
    const e = this._$AL;
    try {
      t = this.shouldUpdate(e), t ? (this.willUpdate(e), this._$EO?.forEach((s) => s.hostUpdate?.()), this.update(e)) : this._$EM();
    } catch (s) {
      throw t = !1, this._$EM(), s;
    }
    t && this._$AE(e);
  }
  willUpdate(t) {
  }
  _$AE(t) {
    this._$EO?.forEach((e) => e.hostUpdated?.()), this.hasUpdated || (this.hasUpdated = !0, this.firstUpdated(t)), this.updated(t);
  }
  _$EM() {
    this._$AL = /* @__PURE__ */ new Map(), this.isUpdatePending = !1;
  }
  get updateComplete() {
    return this.getUpdateComplete();
  }
  getUpdateComplete() {
    return this._$ES;
  }
  shouldUpdate(t) {
    return !0;
  }
  update(t) {
    this._$Eq &&= this._$Eq.forEach((e) => this._$ET(e, this[e])), this._$EM();
  }
  updated(t) {
  }
  firstUpdated(t) {
  }
};
g.elementStyles = [], g.shadowRootOptions = { mode: "open" }, g[C("elementProperties")] = /* @__PURE__ */ new Map(), g[C("finalized")] = /* @__PURE__ */ new Map(), wt?.({ ReactiveElement: g }), (R.reactiveElementVersions ??= []).push("2.1.2");
const I = globalThis, G = (r) => r, H = I.trustedTypes, Q = H ? H.createPolicy("lit-html", { createHTML: (r) => r }) : void 0, ct = "$lit$", _ = `lit$${Math.random().toFixed(9).slice(2)}$`, ut = "?" + _, xt = `<${ut}>`, y = document, w = () => y.createComment(""), x = (r) => r === null || typeof r != "object" && typeof r != "function", q = Array.isArray, Pt = (r) => q(r) || typeof r?.[Symbol.iterator] == "function", D = `[ 	
\f\r]`, S = /<(?:(!--|\/[^a-zA-Z])|(\/?[a-zA-Z][^>\s]*)|(\/?$))/g, Y = /-->/g, tt = />/g, m = RegExp(`>|${D}(?:([^\\s"'>=/]+)(${D}*=${D}*(?:[^ 	
\f\r"'\`<>=]|("|')|))|$)`, "g"), et = /'/g, st = /"/g, dt = /^(?:script|style|textarea|title)$/i, Tt = (r) => (t, ...e) => ({ _$litType$: r, strings: t, values: e }), j = Tt(1), b = /* @__PURE__ */ Symbol.for("lit-noChange"), u = /* @__PURE__ */ Symbol.for("lit-nothing"), it = /* @__PURE__ */ new WeakMap(), f = y.createTreeWalker(y, 129);
function pt(r, t) {
  if (!q(r) || !r.hasOwnProperty("raw")) throw Error("invalid template strings array");
  return Q !== void 0 ? Q.createHTML(t) : t;
}
const Ot = (r, t) => {
  const e = r.length - 1, s = [];
  let i, n = t === 2 ? "<svg>" : t === 3 ? "<math>" : "", o = S;
  for (let h = 0; h < e; h++) {
    const a = r[h];
    let c, d, l = -1, p = 0;
    for (; p < a.length && (o.lastIndex = p, d = o.exec(a), d !== null); ) p = o.lastIndex, o === S ? d[1] === "!--" ? o = Y : d[1] !== void 0 ? o = tt : d[2] !== void 0 ? (dt.test(d[2]) && (i = RegExp("</" + d[2], "g")), o = m) : d[3] !== void 0 && (o = m) : o === m ? d[0] === ">" ? (o = i ?? S, l = -1) : d[1] === void 0 ? l = -2 : (l = o.lastIndex - d[2].length, c = d[1], o = d[3] === void 0 ? m : d[3] === '"' ? st : et) : o === st || o === et ? o = m : o === Y || o === tt ? o = S : (o = m, i = void 0);
    const $ = o === m && r[h + 1].startsWith("/>") ? " " : "";
    n += o === S ? a + xt : l >= 0 ? (s.push(c), a.slice(0, l) + ct + a.slice(l) + _ + $) : a + _ + (l === -2 ? h : $);
  }
  return [pt(r, n + (r[e] || "<?>") + (t === 2 ? "</svg>" : t === 3 ? "</math>" : "")), s];
};
class P {
  constructor({ strings: t, _$litType$: e }, s) {
    let i;
    this.parts = [];
    let n = 0, o = 0;
    const h = t.length - 1, a = this.parts, [c, d] = Ot(t, e);
    if (this.el = P.createElement(c, s), f.currentNode = this.el.content, e === 2 || e === 3) {
      const l = this.el.content.firstChild;
      l.replaceWith(...l.childNodes);
    }
    for (; (i = f.nextNode()) !== null && a.length < h; ) {
      if (i.nodeType === 1) {
        if (i.hasAttributes()) for (const l of i.getAttributeNames()) if (l.endsWith(ct)) {
          const p = d[o++], $ = i.getAttribute(l).split(_), O = /([.?@])?(.*)/.exec(p);
          a.push({ type: 1, index: n, name: O[2], strings: $, ctor: O[1] === "." ? Ut : O[1] === "?" ? Ht : O[1] === "@" ? Nt : k }), i.removeAttribute(l);
        } else l.startsWith(_) && (a.push({ type: 6, index: n }), i.removeAttribute(l));
        if (dt.test(i.tagName)) {
          const l = i.textContent.split(_), p = l.length - 1;
          if (p > 0) {
            i.textContent = H ? H.emptyScript : "";
            for (let $ = 0; $ < p; $++) i.append(l[$], w()), f.nextNode(), a.push({ type: 2, index: ++n });
            i.append(l[p], w());
          }
        }
      } else if (i.nodeType === 8) if (i.data === ut) a.push({ type: 2, index: n });
      else {
        let l = -1;
        for (; (l = i.data.indexOf(_, l + 1)) !== -1; ) a.push({ type: 7, index: n }), l += _.length - 1;
      }
      n++;
    }
  }
  static createElement(t, e) {
    const s = y.createElement("template");
    return s.innerHTML = t, s;
  }
}
function v(r, t, e = r, s) {
  if (t === b) return t;
  let i = s !== void 0 ? e._$Co?.[s] : e._$Cl;
  const n = x(t) ? void 0 : t._$litDirective$;
  return i?.constructor !== n && (i?._$AO?.(!1), n === void 0 ? i = void 0 : (i = new n(r), i._$AT(r, e, s)), s !== void 0 ? (e._$Co ??= [])[s] = i : e._$Cl = i), i !== void 0 && (t = v(r, i._$AS(r, t.values), i, s)), t;
}
class Mt {
  constructor(t, e) {
    this._$AV = [], this._$AN = void 0, this._$AD = t, this._$AM = e;
  }
  get parentNode() {
    return this._$AM.parentNode;
  }
  get _$AU() {
    return this._$AM._$AU;
  }
  u(t) {
    const { el: { content: e }, parts: s } = this._$AD, i = (t?.creationScope ?? y).importNode(e, !0);
    f.currentNode = i;
    let n = f.nextNode(), o = 0, h = 0, a = s[0];
    for (; a !== void 0; ) {
      if (o === a.index) {
        let c;
        a.type === 2 ? c = new T(n, n.nextSibling, this, t) : a.type === 1 ? c = new a.ctor(n, a.name, a.strings, this, t) : a.type === 6 && (c = new Rt(n, this, t)), this._$AV.push(c), a = s[++h];
      }
      o !== a?.index && (n = f.nextNode(), o++);
    }
    return f.currentNode = y, i;
  }
  p(t) {
    let e = 0;
    for (const s of this._$AV) s !== void 0 && (s.strings !== void 0 ? (s._$AI(t, s, e), e += s.strings.length - 2) : s._$AI(t[e])), e++;
  }
}
class T {
  get _$AU() {
    return this._$AM?._$AU ?? this._$Cv;
  }
  constructor(t, e, s, i) {
    this.type = 2, this._$AH = u, this._$AN = void 0, this._$AA = t, this._$AB = e, this._$AM = s, this.options = i, this._$Cv = i?.isConnected ?? !0;
  }
  get parentNode() {
    let t = this._$AA.parentNode;
    const e = this._$AM;
    return e !== void 0 && t?.nodeType === 11 && (t = e.parentNode), t;
  }
  get startNode() {
    return this._$AA;
  }
  get endNode() {
    return this._$AB;
  }
  _$AI(t, e = this) {
    t = v(this, t, e), x(t) ? t === u || t == null || t === "" ? (this._$AH !== u && this._$AR(), this._$AH = u) : t !== this._$AH && t !== b && this._(t) : t._$litType$ !== void 0 ? this.$(t) : t.nodeType !== void 0 ? this.T(t) : Pt(t) ? this.k(t) : this._(t);
  }
  O(t) {
    return this._$AA.parentNode.insertBefore(t, this._$AB);
  }
  T(t) {
    this._$AH !== t && (this._$AR(), this._$AH = this.O(t));
  }
  _(t) {
    this._$AH !== u && x(this._$AH) ? this._$AA.nextSibling.data = t : this.T(y.createTextNode(t)), this._$AH = t;
  }
  $(t) {
    const { values: e, _$litType$: s } = t, i = typeof s == "number" ? this._$AC(t) : (s.el === void 0 && (s.el = P.createElement(pt(s.h, s.h[0]), this.options)), s);
    if (this._$AH?._$AD === i) this._$AH.p(e);
    else {
      const n = new Mt(i, this), o = n.u(this.options);
      n.p(e), this.T(o), this._$AH = n;
    }
  }
  _$AC(t) {
    let e = it.get(t.strings);
    return e === void 0 && it.set(t.strings, e = new P(t)), e;
  }
  k(t) {
    q(this._$AH) || (this._$AH = [], this._$AR());
    const e = this._$AH;
    let s, i = 0;
    for (const n of t) i === e.length ? e.push(s = new T(this.O(w()), this.O(w()), this, this.options)) : s = e[i], s._$AI(n), i++;
    i < e.length && (this._$AR(s && s._$AB.nextSibling, i), e.length = i);
  }
  _$AR(t = this._$AA.nextSibling, e) {
    for (this._$AP?.(!1, !0, e); t !== this._$AB; ) {
      const s = G(t).nextSibling;
      G(t).remove(), t = s;
    }
  }
  setConnected(t) {
    this._$AM === void 0 && (this._$Cv = t, this._$AP?.(t));
  }
}
class k {
  get tagName() {
    return this.element.tagName;
  }
  get _$AU() {
    return this._$AM._$AU;
  }
  constructor(t, e, s, i, n) {
    this.type = 1, this._$AH = u, this._$AN = void 0, this.element = t, this.name = e, this._$AM = i, this.options = n, s.length > 2 || s[0] !== "" || s[1] !== "" ? (this._$AH = Array(s.length - 1).fill(new String()), this.strings = s) : this._$AH = u;
  }
  _$AI(t, e = this, s, i) {
    const n = this.strings;
    let o = !1;
    if (n === void 0) t = v(this, t, e, 0), o = !x(t) || t !== this._$AH && t !== b, o && (this._$AH = t);
    else {
      const h = t;
      let a, c;
      for (t = n[0], a = 0; a < n.length - 1; a++) c = v(this, h[s + a], e, a), c === b && (c = this._$AH[a]), o ||= !x(c) || c !== this._$AH[a], c === u ? t = u : t !== u && (t += (c ?? "") + n[a + 1]), this._$AH[a] = c;
    }
    o && !i && this.j(t);
  }
  j(t) {
    t === u ? this.element.removeAttribute(this.name) : this.element.setAttribute(this.name, t ?? "");
  }
}
class Ut extends k {
  constructor() {
    super(...arguments), this.type = 3;
  }
  j(t) {
    this.element[this.name] = t === u ? void 0 : t;
  }
}
class Ht extends k {
  constructor() {
    super(...arguments), this.type = 4;
  }
  j(t) {
    this.element.toggleAttribute(this.name, !!t && t !== u);
  }
}
class Nt extends k {
  constructor(t, e, s, i, n) {
    super(t, e, s, i, n), this.type = 5;
  }
  _$AI(t, e = this) {
    if ((t = v(this, t, e, 0) ?? u) === b) return;
    const s = this._$AH, i = t === u && s !== u || t.capture !== s.capture || t.once !== s.once || t.passive !== s.passive, n = t !== u && (s === u || i);
    i && this.element.removeEventListener(this.name, this, s), n && this.element.addEventListener(this.name, this, t), this._$AH = t;
  }
  handleEvent(t) {
    typeof this._$AH == "function" ? this._$AH.call(this.options?.host ?? this.element, t) : this._$AH.handleEvent(t);
  }
}
class Rt {
  constructor(t, e, s) {
    this.element = t, this.type = 6, this._$AN = void 0, this._$AM = e, this.options = s;
  }
  get _$AU() {
    return this._$AM._$AU;
  }
  _$AI(t) {
    v(this, t);
  }
}
const kt = I.litHtmlPolyfillSupport;
kt?.(P, T), (I.litHtmlVersions ??= []).push("3.3.2");
const Dt = (r, t, e) => {
  const s = e?.renderBefore ?? t;
  let i = s._$litPart$;
  if (i === void 0) {
    const n = e?.renderBefore ?? null;
    s._$litPart$ = i = new T(t.insertBefore(w(), n), n, void 0, e ?? {});
  }
  return i._$AI(r), i;
};
const V = globalThis;
class A extends g {
  constructor() {
    super(...arguments), this.renderOptions = { host: this }, this._$Do = void 0;
  }
  createRenderRoot() {
    const t = super.createRenderRoot();
    return this.renderOptions.renderBefore ??= t.firstChild, t;
  }
  update(t) {
    const e = this.render();
    this.hasUpdated || (this.renderOptions.isConnected = this.isConnected), super.update(t), this._$Do = Dt(e, this.renderRoot, this.renderOptions);
  }
  connectedCallback() {
    super.connectedCallback(), this._$Do?.setConnected(!0);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), this._$Do?.setConnected(!1);
  }
  render() {
    return b;
  }
}
A._$litElement$ = !0, A.finalized = !0, V.litElementHydrateSupport?.({ LitElement: A });
const jt = V.litElementPolyfillSupport;
jt?.({ LitElement: A });
(V.litElementVersions ??= []).push("4.2.2");
const $t = (r) => (t, e) => {
  e !== void 0 ? e.addInitializer(() => {
    customElements.define(r, t);
  }) : customElements.define(r, t);
};
const zt = { attribute: !0, type: String, converter: U, reflect: !1, hasChanged: B }, Lt = (r = zt, t, e) => {
  const { kind: s, metadata: i } = e;
  let n = globalThis.litPropertyMetadata.get(i);
  if (n === void 0 && globalThis.litPropertyMetadata.set(i, n = /* @__PURE__ */ new Map()), s === "setter" && ((r = Object.create(r)).wrapped = !0), n.set(e.name, r), s === "accessor") {
    const { name: o } = e;
    return { set(h) {
      const a = t.get.call(this);
      t.set.call(this, h), this.requestUpdate(o, a, r, !0, h);
    }, init(h) {
      return h !== void 0 && this.C(o, void 0, r, h), h;
    } };
  }
  if (s === "setter") {
    const { name: o } = e;
    return function(h) {
      const a = this[o];
      t.call(this, h), this.requestUpdate(o, a, r, !0, h);
    };
  }
  throw Error("Unsupported decorator location: " + s);
};
function Bt(r) {
  return (t, e) => typeof e == "object" ? Lt(r, t, e) : ((s, i, n) => {
    const o = i.hasOwnProperty(n);
    return i.constructor.createProperty(n, s), o ? Object.getOwnPropertyDescriptor(i, n) : void 0;
  })(r, t, e);
}
function W(r) {
  return Bt({ ...r, state: !0, attribute: !1 });
}
var It = Object.defineProperty, qt = Object.getOwnPropertyDescriptor, Z = (r, t, e, s) => {
  for (var i = s > 1 ? void 0 : s ? qt(t, e) : t, n = r.length - 1, o; n >= 0; n--)
    (o = r[n]) && (i = (s ? o(t, e, i) : o(i)) || i);
  return s && i && It(t, e, i), i;
};
let E = class extends rt(A) {
  constructor() {
    super(...arguments), this._name = "", this._hostname = "";
  }
  _handleClose() {
    this.modalContext?.submit();
  }
  async _handleSubmit() {
    const r = {
      name: this._name,
      hostname: this._hostname,
      themeColor: "#3544b1"
    };
    this.consumeContext(nt, async (t) => {
      if (!t) return;
      const e = await t.getLatestToken(), { error: s } = await at(
        this,
        ot.post({
          url: "/umbraco/management/api/v1/prism/tenants",
          body: r,
          headers: {
            Authorization: `Bearer ${e}`
          }
        })
      );
      if (s) {
        console.error("Save failed", s);
        return;
      }
      this.modalContext?.submit();
    });
  }
  render() {
    return j`
      <uui-dialog-layout headline="Create New Tenant">
        <div style="display:flex; flex-direction:column; gap: 16px;">
          <uui-label>Name</uui-label>
          <uui-input .value=${this._name} @input=${(r) => this._name = r.target.value}></uui-input>
          
          <uui-label>Hostname</uui-label>
          <uui-input .value=${this._hostname} @input=${(r) => this._hostname = r.target.value}></uui-input>
        </div>
        
        <uui-button slot="actions" @click=${this._handleClose}>Cancel</uui-button>
        <uui-button slot="actions" look="primary" color="positive" @click=${this._handleSubmit}>Create Tenant</uui-button>
      </uui-dialog-layout>
    `;
  }
};
E.styles = lt`
    :host {
      display: block;
      width: 500px;
      min-height: 800px;
      height: 100%;
      background-color: var(--uui-color-surface);
      padding: var(--uui-size-layout-1);
      box-sizing: border-box;
    }

    form {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-4);
    }
  `;
Z([
  W()
], E.prototype, "_name", 2);
Z([
  W()
], E.prototype, "_hostname", 2);
E = Z([
  $t("prism-create-tenant-modal")
], E);
var Vt = Object.defineProperty, Wt = Object.getOwnPropertyDescriptor, _t = (r, t, e, s) => {
  for (var i = s > 1 ? void 0 : s ? Wt(t, e) : t, n = r.length - 1, o; n >= 0; n--)
    (o = r[n]) && (i = (s ? o(t, e, i) : o(i)) || i);
  return s && i && Vt(t, e, i), i;
};
console.log("Modal element loaded:", E);
let N = class extends rt(A) {
  constructor() {
    super(...arguments), this._tenants = [];
  }
  async _openCreateModal() {
    this.consumeContext(mt, (r) => {
      if (!r) return;
      r.open(this, "Prism.CreateTenantModal").onSubmit().then(() => {
        this._fetchTenants();
      }).catch(() => {
      });
    });
  }
  async connectedCallback() {
    super.connectedCallback(), this._fetchTenants();
  }
  async _fetchTenants() {
    this.consumeContext(nt, async (r) => {
      if (!r) return;
      const t = await r.getLatestToken(), { data: e, error: s } = await at(
        this,
        ot.get({
          url: "/umbraco/management/api/v1/prism/tenants",
          headers: {
            Authorization: `Bearer ${t}`
          }
        })
      );
      if (s) {
        console.error("Prism API Error", s);
        return;
      }
      this._tenants = e ?? [];
    });
  }
  render() {
    return j`
      <div style="padding: 20px;">
        <uui-box headline="Prism Multi-Tenant Manager">
          <uui-button look="primary" color="positive" @click=${this._openCreateModal} style="margin-bottom: 20px;">
            Add New Tenant
          </uui-button>
          <uui-button look="placeholder" @click=${this._fetchTenants} style="width:100%; margin-bottom: 20px;">
            Refresh Tenants
          </uui-button>
          <uui-table>
            <uui-table-head>
              <uui-table-head-cell>Name</uui-table-head-cell>
              <uui-table-head-cell>Hostname</uui-table-head-cell>
              <uui-table-head-cell>Color</uui-table-head-cell>
            </uui-table-head>
            ${this._tenants.map((r) => j`
              <uui-table-row>
                <uui-table-cell>${r.name}</uui-table-cell>
                <uui-table-cell>${r.hostname}</uui-table-cell>
                <uui-table-cell>
                    <div style="background:${r.themeColor}; width:20px; height:20px; border-radius:4px;"></div>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>
        </uui-box>
      </div>
    `;
  }
};
N.styles = lt`
    :host {
      display: block;
      color: var(--uui-color-text-main);
    }
  `;
_t([
  W()
], N.prototype, "_tenants", 2);
N = _t([
  $t("prism-dashboard")
], N);
export {
  N as PrismDashboardElement
};
//# sourceMappingURL=prism-dashboard.js.map
