import { UmbElementMixin as lt } from "@umbraco-cms/backoffice/element-api";
import { UMB_MODAL_MANAGER_CONTEXT as J } from "@umbraco-cms/backoffice/modal";
import { UMB_AUTH_CONTEXT as L } from "@umbraco-cms/backoffice/auth";
import { umbHttpClient as B } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as K } from "@umbraco-cms/backoffice/resources";
const H = globalThis, q = H.ShadowRoot && (H.ShadyCSS === void 0 || H.ShadyCSS.nativeShadow) && "adoptedStyleSheets" in Document.prototype && "replace" in CSSStyleSheet.prototype, V = /* @__PURE__ */ Symbol(), X = /* @__PURE__ */ new WeakMap();
let ut = class {
  constructor(t, e, i) {
    if (this._$cssResult$ = !0, i !== V) throw Error("CSSResult is not constructable. Use `unsafeCSS` or `css` instead.");
    this.cssText = t, this.t = e;
  }
  get styleSheet() {
    let t = this.o;
    const e = this.t;
    if (q && t === void 0) {
      const i = e !== void 0 && e.length === 1;
      i && (t = X.get(e)), t === void 0 && ((this.o = t = new CSSStyleSheet()).replaceSync(this.cssText), i && X.set(e, t));
    }
    return t;
  }
  toString() {
    return this.cssText;
  }
};
const bt = (s) => new ut(typeof s == "string" ? s : s + "", void 0, V), ht = (s, ...t) => {
  const e = s.length === 1 ? s[0] : t.reduce((i, n, r) => i + ((o) => {
    if (o._$cssResult$ === !0) return o.cssText;
    if (typeof o == "number") return o;
    throw Error("Value passed to 'css' function must be a 'css' function result: " + o + ". Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.");
  })(n) + s[r + 1], s[0]);
  return new ut(e, s, V);
}, yt = (s, t) => {
  if (q) s.adoptedStyleSheets = t.map((e) => e instanceof CSSStyleSheet ? e : e.styleSheet);
  else for (const e of t) {
    const i = document.createElement("style"), n = H.litNonce;
    n !== void 0 && i.setAttribute("nonce", n), i.textContent = e.cssText, s.appendChild(i);
  }
}, Q = q ? (s) => s : (s) => s instanceof CSSStyleSheet ? ((t) => {
  let e = "";
  for (const i of t.cssRules) e += i.cssText;
  return bt(e);
})(s) : s;
const { is: gt, defineProperty: vt, getOwnPropertyDescriptor: At, getOwnPropertyNames: Et, getOwnPropertySymbols: Ct, getPrototypeOf: wt } = Object, R = globalThis, Y = R.trustedTypes, St = Y ? Y.emptyScript : "", Tt = R.reactiveElementPolyfillSupport, x = (s, t) => s, I = { toAttribute(s, t) {
  switch (t) {
    case Boolean:
      s = s ? St : null;
      break;
    case Object:
    case Array:
      s = s == null ? s : JSON.stringify(s);
  }
  return s;
}, fromAttribute(s, t) {
  let e = s;
  switch (t) {
    case Boolean:
      e = s !== null;
      break;
    case Number:
      e = s === null ? null : Number(s);
      break;
    case Object:
    case Array:
      try {
        e = JSON.parse(s);
      } catch {
        e = null;
      }
  }
  return e;
} }, W = (s, t) => !gt(s, t), tt = { attribute: !0, type: String, converter: I, reflect: !1, useDefault: !1, hasChanged: W };
Symbol.metadata ??= /* @__PURE__ */ Symbol("metadata"), R.litPropertyMetadata ??= /* @__PURE__ */ new WeakMap();
let E = class extends HTMLElement {
  static addInitializer(t) {
    this._$Ei(), (this.l ??= []).push(t);
  }
  static get observedAttributes() {
    return this.finalize(), this._$Eh && [...this._$Eh.keys()];
  }
  static createProperty(t, e = tt) {
    if (e.state && (e.attribute = !1), this._$Ei(), this.prototype.hasOwnProperty(t) && ((e = Object.create(e)).wrapped = !0), this.elementProperties.set(t, e), !e.noAccessor) {
      const i = /* @__PURE__ */ Symbol(), n = this.getPropertyDescriptor(t, i, e);
      n !== void 0 && vt(this.prototype, t, n);
    }
  }
  static getPropertyDescriptor(t, e, i) {
    const { get: n, set: r } = At(this.prototype, t) ?? { get() {
      return this[e];
    }, set(o) {
      this[e] = o;
    } };
    return { get: n, set(o) {
      const l = n?.call(this);
      r?.call(this, o), this.requestUpdate(t, l, i);
    }, configurable: !0, enumerable: !0 };
  }
  static getPropertyOptions(t) {
    return this.elementProperties.get(t) ?? tt;
  }
  static _$Ei() {
    if (this.hasOwnProperty(x("elementProperties"))) return;
    const t = wt(this);
    t.finalize(), t.l !== void 0 && (this.l = [...t.l]), this.elementProperties = new Map(t.elementProperties);
  }
  static finalize() {
    if (this.hasOwnProperty(x("finalized"))) return;
    if (this.finalized = !0, this._$Ei(), this.hasOwnProperty(x("properties"))) {
      const e = this.properties, i = [...Et(e), ...Ct(e)];
      for (const n of i) this.createProperty(n, e[n]);
    }
    const t = this[Symbol.metadata];
    if (t !== null) {
      const e = litPropertyMetadata.get(t);
      if (e !== void 0) for (const [i, n] of e) this.elementProperties.set(i, n);
    }
    this._$Eh = /* @__PURE__ */ new Map();
    for (const [e, i] of this.elementProperties) {
      const n = this._$Eu(e, i);
      n !== void 0 && this._$Eh.set(n, e);
    }
    this.elementStyles = this.finalizeStyles(this.styles);
  }
  static finalizeStyles(t) {
    const e = [];
    if (Array.isArray(t)) {
      const i = new Set(t.flat(1 / 0).reverse());
      for (const n of i) e.unshift(Q(n));
    } else t !== void 0 && e.push(Q(t));
    return e;
  }
  static _$Eu(t, e) {
    const i = e.attribute;
    return i === !1 ? void 0 : typeof i == "string" ? i : typeof t == "string" ? t.toLowerCase() : void 0;
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
    for (const i of e.keys()) this.hasOwnProperty(i) && (t.set(i, this[i]), delete this[i]);
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
  attributeChangedCallback(t, e, i) {
    this._$AK(t, i);
  }
  _$ET(t, e) {
    const i = this.constructor.elementProperties.get(t), n = this.constructor._$Eu(t, i);
    if (n !== void 0 && i.reflect === !0) {
      const r = (i.converter?.toAttribute !== void 0 ? i.converter : I).toAttribute(e, i.type);
      this._$Em = t, r == null ? this.removeAttribute(n) : this.setAttribute(n, r), this._$Em = null;
    }
  }
  _$AK(t, e) {
    const i = this.constructor, n = i._$Eh.get(t);
    if (n !== void 0 && this._$Em !== n) {
      const r = i.getPropertyOptions(n), o = typeof r.converter == "function" ? { fromAttribute: r.converter } : r.converter?.fromAttribute !== void 0 ? r.converter : I;
      this._$Em = n;
      const l = o.fromAttribute(e, r.type);
      this[n] = l ?? this._$Ej?.get(n) ?? l, this._$Em = null;
    }
  }
  requestUpdate(t, e, i, n = !1, r) {
    if (t !== void 0) {
      const o = this.constructor;
      if (n === !1 && (r = this[t]), i ??= o.getPropertyOptions(t), !((i.hasChanged ?? W)(r, e) || i.useDefault && i.reflect && r === this._$Ej?.get(t) && !this.hasAttribute(o._$Eu(t, i)))) return;
      this.C(t, e, i);
    }
    this.isUpdatePending === !1 && (this._$ES = this._$EP());
  }
  C(t, e, { useDefault: i, reflect: n, wrapped: r }, o) {
    i && !(this._$Ej ??= /* @__PURE__ */ new Map()).has(t) && (this._$Ej.set(t, o ?? e ?? this[t]), r !== !0 || o !== void 0) || (this._$AL.has(t) || (this.hasUpdated || i || (e = void 0), this._$AL.set(t, e)), n === !0 && this._$Em !== t && (this._$Eq ??= /* @__PURE__ */ new Set()).add(t));
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
        for (const [n, r] of this._$Ep) this[n] = r;
        this._$Ep = void 0;
      }
      const i = this.constructor.elementProperties;
      if (i.size > 0) for (const [n, r] of i) {
        const { wrapped: o } = r, l = this[n];
        o !== !0 || this._$AL.has(n) || l === void 0 || this.C(n, void 0, r, l);
      }
    }
    let t = !1;
    const e = this._$AL;
    try {
      t = this.shouldUpdate(e), t ? (this.willUpdate(e), this._$EO?.forEach((i) => i.hostUpdate?.()), this.update(e)) : this._$EM();
    } catch (i) {
      throw t = !1, this._$EM(), i;
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
E.elementStyles = [], E.shadowRootOptions = { mode: "open" }, E[x("elementProperties")] = /* @__PURE__ */ new Map(), E[x("finalized")] = /* @__PURE__ */ new Map(), Tt?.({ ReactiveElement: E }), (R.reactiveElementVersions ??= []).push("2.1.2");
const G = globalThis, et = (s) => s, k = G.trustedTypes, it = k ? k.createPolicy("lit-html", { createHTML: (s) => s }) : void 0, ct = "$lit$", f = `lit$${Math.random().toFixed(9).slice(2)}$`, dt = "?" + f, xt = `<${dt}>`, A = document, P = () => A.createComment(""), N = (s) => s === null || typeof s != "object" && typeof s != "function", Z = Array.isArray, Pt = (s) => Z(s) || typeof s?.[Symbol.iterator] == "function", j = `[ 	
\f\r]`, T = /<(?:(!--|\/[^a-zA-Z])|(\/?[a-zA-Z][^>\s]*)|(\/?$))/g, st = /-->/g, nt = />/g, g = RegExp(`>|${j}(?:([^\\s"'>=/]+)(${j}*=${j}*(?:[^ 	
\f\r"'\`<>=]|("|')|))|$)`, "g"), rt = /'/g, ot = /"/g, pt = /^(?:script|style|textarea|title)$/i, Nt = (s) => (t, ...e) => ({ _$litType$: s, strings: t, values: e }), b = Nt(1), w = /* @__PURE__ */ Symbol.for("lit-noChange"), c = /* @__PURE__ */ Symbol.for("lit-nothing"), at = /* @__PURE__ */ new WeakMap(), v = A.createTreeWalker(A, 129);
function mt(s, t) {
  if (!Z(s) || !s.hasOwnProperty("raw")) throw Error("invalid template strings array");
  return it !== void 0 ? it.createHTML(t) : t;
}
const Ot = (s, t) => {
  const e = s.length - 1, i = [];
  let n, r = t === 2 ? "<svg>" : t === 3 ? "<math>" : "", o = T;
  for (let l = 0; l < e; l++) {
    const a = s[l];
    let h, d, u = -1, m = 0;
    for (; m < a.length && (o.lastIndex = m, d = o.exec(a), d !== null); ) m = o.lastIndex, o === T ? d[1] === "!--" ? o = st : d[1] !== void 0 ? o = nt : d[2] !== void 0 ? (pt.test(d[2]) && (n = RegExp("</" + d[2], "g")), o = g) : d[3] !== void 0 && (o = g) : o === g ? d[0] === ">" ? (o = n ?? T, u = -1) : d[1] === void 0 ? u = -2 : (u = o.lastIndex - d[2].length, h = d[1], o = d[3] === void 0 ? g : d[3] === '"' ? ot : rt) : o === ot || o === rt ? o = g : o === st || o === nt ? o = T : (o = g, n = void 0);
    const _ = o === g && s[l + 1].startsWith("/>") ? " " : "";
    r += o === T ? a + xt : u >= 0 ? (i.push(h), a.slice(0, u) + ct + a.slice(u) + f + _) : a + f + (u === -2 ? l : _);
  }
  return [mt(s, r + (s[e] || "<?>") + (t === 2 ? "</svg>" : t === 3 ? "</math>" : "")), i];
};
class O {
  constructor({ strings: t, _$litType$: e }, i) {
    let n;
    this.parts = [];
    let r = 0, o = 0;
    const l = t.length - 1, a = this.parts, [h, d] = Ot(t, e);
    if (this.el = O.createElement(h, i), v.currentNode = this.el.content, e === 2 || e === 3) {
      const u = this.el.content.firstChild;
      u.replaceWith(...u.childNodes);
    }
    for (; (n = v.nextNode()) !== null && a.length < l; ) {
      if (n.nodeType === 1) {
        if (n.hasAttributes()) for (const u of n.getAttributeNames()) if (u.endsWith(ct)) {
          const m = d[o++], _ = n.getAttribute(u).split(f), U = /([.?@])?(.*)/.exec(m);
          a.push({ type: 1, index: r, name: U[2], strings: _, ctor: U[1] === "." ? Ut : U[1] === "?" ? Ht : U[1] === "@" ? It : z }), n.removeAttribute(u);
        } else u.startsWith(f) && (a.push({ type: 6, index: r }), n.removeAttribute(u));
        if (pt.test(n.tagName)) {
          const u = n.textContent.split(f), m = u.length - 1;
          if (m > 0) {
            n.textContent = k ? k.emptyScript : "";
            for (let _ = 0; _ < m; _++) n.append(u[_], P()), v.nextNode(), a.push({ type: 2, index: ++r });
            n.append(u[m], P());
          }
        }
      } else if (n.nodeType === 8) if (n.data === dt) a.push({ type: 2, index: r });
      else {
        let u = -1;
        for (; (u = n.data.indexOf(f, u + 1)) !== -1; ) a.push({ type: 7, index: r }), u += f.length - 1;
      }
      r++;
    }
  }
  static createElement(t, e) {
    const i = A.createElement("template");
    return i.innerHTML = t, i;
  }
}
function S(s, t, e = s, i) {
  if (t === w) return t;
  let n = i !== void 0 ? e._$Co?.[i] : e._$Cl;
  const r = N(t) ? void 0 : t._$litDirective$;
  return n?.constructor !== r && (n?._$AO?.(!1), r === void 0 ? n = void 0 : (n = new r(s), n._$AT(s, e, i)), i !== void 0 ? (e._$Co ??= [])[i] = n : e._$Cl = n), n !== void 0 && (t = S(s, n._$AS(s, t.values), n, i)), t;
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
    const { el: { content: e }, parts: i } = this._$AD, n = (t?.creationScope ?? A).importNode(e, !0);
    v.currentNode = n;
    let r = v.nextNode(), o = 0, l = 0, a = i[0];
    for (; a !== void 0; ) {
      if (o === a.index) {
        let h;
        a.type === 2 ? h = new M(r, r.nextSibling, this, t) : a.type === 1 ? h = new a.ctor(r, a.name, a.strings, this, t) : a.type === 6 && (h = new kt(r, this, t)), this._$AV.push(h), a = i[++l];
      }
      o !== a?.index && (r = v.nextNode(), o++);
    }
    return v.currentNode = A, n;
  }
  p(t) {
    let e = 0;
    for (const i of this._$AV) i !== void 0 && (i.strings !== void 0 ? (i._$AI(t, i, e), e += i.strings.length - 2) : i._$AI(t[e])), e++;
  }
}
class M {
  get _$AU() {
    return this._$AM?._$AU ?? this._$Cv;
  }
  constructor(t, e, i, n) {
    this.type = 2, this._$AH = c, this._$AN = void 0, this._$AA = t, this._$AB = e, this._$AM = i, this.options = n, this._$Cv = n?.isConnected ?? !0;
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
    t = S(this, t, e), N(t) ? t === c || t == null || t === "" ? (this._$AH !== c && this._$AR(), this._$AH = c) : t !== this._$AH && t !== w && this._(t) : t._$litType$ !== void 0 ? this.$(t) : t.nodeType !== void 0 ? this.T(t) : Pt(t) ? this.k(t) : this._(t);
  }
  O(t) {
    return this._$AA.parentNode.insertBefore(t, this._$AB);
  }
  T(t) {
    this._$AH !== t && (this._$AR(), this._$AH = this.O(t));
  }
  _(t) {
    this._$AH !== c && N(this._$AH) ? this._$AA.nextSibling.data = t : this.T(A.createTextNode(t)), this._$AH = t;
  }
  $(t) {
    const { values: e, _$litType$: i } = t, n = typeof i == "number" ? this._$AC(t) : (i.el === void 0 && (i.el = O.createElement(mt(i.h, i.h[0]), this.options)), i);
    if (this._$AH?._$AD === n) this._$AH.p(e);
    else {
      const r = new Mt(n, this), o = r.u(this.options);
      r.p(e), this.T(o), this._$AH = r;
    }
  }
  _$AC(t) {
    let e = at.get(t.strings);
    return e === void 0 && at.set(t.strings, e = new O(t)), e;
  }
  k(t) {
    Z(this._$AH) || (this._$AH = [], this._$AR());
    const e = this._$AH;
    let i, n = 0;
    for (const r of t) n === e.length ? e.push(i = new M(this.O(P()), this.O(P()), this, this.options)) : i = e[n], i._$AI(r), n++;
    n < e.length && (this._$AR(i && i._$AB.nextSibling, n), e.length = n);
  }
  _$AR(t = this._$AA.nextSibling, e) {
    for (this._$AP?.(!1, !0, e); t !== this._$AB; ) {
      const i = et(t).nextSibling;
      et(t).remove(), t = i;
    }
  }
  setConnected(t) {
    this._$AM === void 0 && (this._$Cv = t, this._$AP?.(t));
  }
}
class z {
  get tagName() {
    return this.element.tagName;
  }
  get _$AU() {
    return this._$AM._$AU;
  }
  constructor(t, e, i, n, r) {
    this.type = 1, this._$AH = c, this._$AN = void 0, this.element = t, this.name = e, this._$AM = n, this.options = r, i.length > 2 || i[0] !== "" || i[1] !== "" ? (this._$AH = Array(i.length - 1).fill(new String()), this.strings = i) : this._$AH = c;
  }
  _$AI(t, e = this, i, n) {
    const r = this.strings;
    let o = !1;
    if (r === void 0) t = S(this, t, e, 0), o = !N(t) || t !== this._$AH && t !== w, o && (this._$AH = t);
    else {
      const l = t;
      let a, h;
      for (t = r[0], a = 0; a < r.length - 1; a++) h = S(this, l[i + a], e, a), h === w && (h = this._$AH[a]), o ||= !N(h) || h !== this._$AH[a], h === c ? t = c : t !== c && (t += (h ?? "") + r[a + 1]), this._$AH[a] = h;
    }
    o && !n && this.j(t);
  }
  j(t) {
    t === c ? this.element.removeAttribute(this.name) : this.element.setAttribute(this.name, t ?? "");
  }
}
class Ut extends z {
  constructor() {
    super(...arguments), this.type = 3;
  }
  j(t) {
    this.element[this.name] = t === c ? void 0 : t;
  }
}
class Ht extends z {
  constructor() {
    super(...arguments), this.type = 4;
  }
  j(t) {
    this.element.toggleAttribute(this.name, !!t && t !== c);
  }
}
class It extends z {
  constructor(t, e, i, n, r) {
    super(t, e, i, n, r), this.type = 5;
  }
  _$AI(t, e = this) {
    if ((t = S(this, t, e, 0) ?? c) === w) return;
    const i = this._$AH, n = t === c && i !== c || t.capture !== i.capture || t.once !== i.once || t.passive !== i.passive, r = t !== c && (i === c || n);
    n && this.element.removeEventListener(this.name, this, i), r && this.element.addEventListener(this.name, this, t), this._$AH = t;
  }
  handleEvent(t) {
    typeof this._$AH == "function" ? this._$AH.call(this.options?.host ?? this.element, t) : this._$AH.handleEvent(t);
  }
}
class kt {
  constructor(t, e, i) {
    this.element = t, this.type = 6, this._$AN = void 0, this._$AM = e, this.options = i;
  }
  get _$AU() {
    return this._$AM._$AU;
  }
  _$AI(t) {
    S(this, t);
  }
}
const Dt = G.litHtmlPolyfillSupport;
Dt?.(O, M), (G.litHtmlVersions ??= []).push("3.3.2");
const Rt = (s, t, e) => {
  const i = e?.renderBefore ?? t;
  let n = i._$litPart$;
  if (n === void 0) {
    const r = e?.renderBefore ?? null;
    i._$litPart$ = n = new M(t.insertBefore(P(), r), r, void 0, e ?? {});
  }
  return n._$AI(s), n;
};
const F = globalThis;
class C extends E {
  constructor() {
    super(...arguments), this.renderOptions = { host: this }, this._$Do = void 0;
  }
  createRenderRoot() {
    const t = super.createRenderRoot();
    return this.renderOptions.renderBefore ??= t.firstChild, t;
  }
  update(t) {
    const e = this.render();
    this.hasUpdated || (this.renderOptions.isConnected = this.isConnected), super.update(t), this._$Do = Rt(e, this.renderRoot, this.renderOptions);
  }
  connectedCallback() {
    super.connectedCallback(), this._$Do?.setConnected(!0);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), this._$Do?.setConnected(!1);
  }
  render() {
    return w;
  }
}
C._$litElement$ = !0, C.finalized = !0, F.litElementHydrateSupport?.({ LitElement: C });
const zt = F.litElementPolyfillSupport;
zt?.({ LitElement: C });
(F.litElementVersions ??= []).push("4.2.2");
const $t = (s) => (t, e) => {
  e !== void 0 ? e.addInitializer(() => {
    customElements.define(s, t);
  }) : customElements.define(s, t);
};
const jt = { attribute: !0, type: String, converter: I, reflect: !1, hasChanged: W }, Lt = (s = jt, t, e) => {
  const { kind: i, metadata: n } = e;
  let r = globalThis.litPropertyMetadata.get(n);
  if (r === void 0 && globalThis.litPropertyMetadata.set(n, r = /* @__PURE__ */ new Map()), i === "setter" && ((s = Object.create(s)).wrapped = !0), r.set(e.name, s), i === "accessor") {
    const { name: o } = e;
    return { set(l) {
      const a = t.get.call(this);
      t.set.call(this, l), this.requestUpdate(o, a, s, !0, l);
    }, init(l) {
      return l !== void 0 && this.C(o, void 0, s, l), l;
    } };
  }
  if (i === "setter") {
    const { name: o } = e;
    return function(l) {
      const a = this[o];
      t.call(this, l), this.requestUpdate(o, a, s, !0, l);
    };
  }
  throw Error("Unsupported decorator location: " + i);
};
function _t(s) {
  return (t, e) => typeof e == "object" ? Lt(s, t, e) : ((i, n, r) => {
    const o = n.hasOwnProperty(r);
    return n.constructor.createProperty(r, i), o ? Object.getOwnPropertyDescriptor(n, r) : void 0;
  })(s, t, e);
}
function y(s) {
  return _t({ ...s, state: !0, attribute: !1 });
}
var Bt = Object.defineProperty, Kt = Object.getOwnPropertyDescriptor, $ = (s, t, e, i) => {
  for (var n = i > 1 ? void 0 : i ? Kt(t, e) : t, r = s.length - 1, o; r >= 0; r--)
    (o = s[r]) && (n = (i ? o(t, e, n) : o(n)) || n);
  return i && n && Bt(t, e, n), n;
};
let p = class extends lt(C) {
  constructor() {
    super(...arguments), this._activeTab = "general", this._id = null, this._name = "", this._hostname = "", this._entraTenantId = "", this._entraClientId = "", this._secretKeyName = "";
  }
  /**
   * Lifecycle method that runs when the element is added to the DOM.
   * We use this to populate the form if we are editing an existing tenant.
   */
  connectedCallback() {
    if (super.connectedCallback(), this.data?.tenant) {
      const s = this.data.tenant;
      this._id = s.id, this._name = s.name ?? "", this._hostname = s.hostname ?? "", this._entraTenantId = s.entraTenantId ?? "", this._entraClientId = s.entraClientId ?? "", this._secretKeyName = s.secretKeyName ?? "";
    }
  }
  async _handleSubmit() {
    if (!this._name || !this._hostname) {
      this._activeTab = "general";
      return;
    }
    const s = {
      id: this._id,
      name: this._name,
      hostname: this._hostname,
      themeColor: "#3544b1",
      // Defaulting for now, could be a color picker later
      entraTenantId: this._entraTenantId,
      entraClientId: this._entraClientId,
      secretKeyName: this._secretKeyName
    };
    this.consumeContext(L, async (t) => {
      if (!t) return;
      const e = await t.getLatestToken(), i = this._id !== null, n = i ? `/umbraco/management/api/v1/prism/tenants/${this._id}` : "/umbraco/management/api/v1/prism/tenants", { error: r } = await K(
        this,
        B[i ? "put" : "post"]({
          url: n,
          body: s,
          headers: { Authorization: `Bearer ${e}` }
        })
      );
      r ? console.error("Failed to save tenant", r) : this.modalContext?.submit();
    });
  }
  _renderGeneralTab() {
    return b`
      <div role="tabpanel" id="general-panel" aria-labelledby="general-tab" class="tab-content">
        <uui-box>
          <div class="field">
            <uui-label for="tenant-name">Tenant Name</uui-label>
            <uui-input 
              id="tenant-name" 
              label="Tenant Name" 
              .value=${this._name} 
              @input=${(s) => this._name = s.target.value}
              required>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="hostname">Hostname</uui-label>
            <uui-input 
              id="hostname" 
              label="Hostname" 
              placeholder="e.g. tenant-a.com" 
              .value=${this._hostname} 
              @input=${(s) => this._hostname = s.target.value}
              required>
            </uui-input>
          </div>
        </uui-box>
      </div>
    `;
  }
  _renderIdentityTab() {
    return b`
      <div role="tabpanel" id="identity-panel" aria-labelledby="identity-tab" class="tab-content">
        <uui-box>
          <p class="description">Configure Microsoft Entra ID integration. Branding is managed in the Azure Portal.</p>
          
          <div class="field">
            <uui-label for="tenant-id">Directory (Tenant) ID</uui-label>
            <uui-input 
              id="tenant-id" 
              label="Directory ID" 
              .value=${this._entraTenantId} 
              @input=${(s) => this._entraTenantId = s.target.value}>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="client-id">Application (Client) ID</uui-label>
            <uui-input 
              id="client-id" 
              label="Client ID" 
              .value=${this._entraClientId} 
              @input=${(s) => this._entraClientId = s.target.value}>
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="secret-name">Key Vault Secret Name</uui-label>
            <uui-input 
              id="secret-name" 
              label="Secret Name" 
              .value=${this._secretKeyName} 
              @input=${(s) => this._secretKeyName = s.target.value}>
            </uui-input>
            <small id="secret-hint">Must match the secret identifier in your configured Azure Key Vault.</small>
          </div>
        </uui-box>
      </div>
    `;
  }
  render() {
    const s = this._id !== null;
    return b`
      <uui-dialog-layout headline="${s ? "Edit" : "Register New"} Tenant">
        
        <uui-tab-group>
          <uui-tab 
            label="General" 
            ?active=${this._activeTab === "general"} 
            @click=${() => this._activeTab = "general"}>
            General
          </uui-tab>
          <uui-tab 
            label="Identity" 
            ?active=${this._activeTab === "identity"} 
            @click=${() => this._activeTab = "identity"}>
            Identity
          </uui-tab>
        </uui-tab-group>

        <div class="container">
          ${this._activeTab === "general" ? this._renderGeneralTab() : this._renderIdentityTab()}
        </div>
        
        <uui-button slot="actions" @click=${() => this.modalContext?.reject()}>Cancel</uui-button>
        <uui-button 
            slot="actions" 
            look="primary" 
            color="positive" 
            @click=${this._handleSubmit}>
            ${s ? "Update Tenant" : "Create Tenant"}
        </uui-button>
      </uui-dialog-layout>
    `;
  }
};
p.styles = ht`
    :host {
      display: block;
      width: 700px;
      height: 100%;
      min-height: 550px;
      background-color: var(--uui-color-surface);
    }
    .container { 
      min-height: 350px;
    }
    .field { 
      display: flex;
      flex-direction: column;
      margin-bottom: var(--uui-size-space-5); 
    }
    uui-label { 
      margin-bottom: var(--uui-size-space-2); 
      font-weight: bold; 
    }
    uui-input { width: 100%; }
    .description { 
      color: var(--uui-color-text-alt); 
      margin-bottom: var(--uui-size-space-5); 
      font-size: 0.9rem;
    }
    small { 
      margin-top: var(--uui-size-space-2); 
      color: var(--uui-color-text-alt); 
    }
  `;
$([
  _t({ type: Object })
], p.prototype, "data", 2);
$([
  y()
], p.prototype, "_activeTab", 2);
$([
  y()
], p.prototype, "_id", 2);
$([
  y()
], p.prototype, "_name", 2);
$([
  y()
], p.prototype, "_hostname", 2);
$([
  y()
], p.prototype, "_entraTenantId", 2);
$([
  y()
], p.prototype, "_entraClientId", 2);
$([
  y()
], p.prototype, "_secretKeyName", 2);
p = $([
  $t("prism-create-tenant-modal")
], p);
var qt = Object.defineProperty, Vt = Object.getOwnPropertyDescriptor, ft = (s, t, e, i) => {
  for (var n = i > 1 ? void 0 : i ? Vt(t, e) : t, r = s.length - 1, o; r >= 0; r--)
    (o = s[r]) && (n = (i ? o(t, e, n) : o(n)) || n);
  return i && n && qt(t, e, n), n;
};
console.log("Modal element loaded:", p);
let D = class extends lt(C) {
  constructor() {
    super(...arguments), this._tenants = [];
  }
  async connectedCallback() {
    super.connectedCallback(), this._fetchTenants();
  }
  /**
   * Fetches the list of tenants from the Management API
   */
  async _fetchTenants() {
    this.consumeContext(L, async (s) => {
      if (!s) return;
      const t = await s.getLatestToken(), { data: e, error: i } = await K(
        this,
        B.get({
          url: "/umbraco/management/api/v1/prism/tenants",
          headers: {
            Authorization: `Bearer ${t}`
          }
        })
      );
      if (i) {
        console.error("Prism API Error", i);
        return;
      }
      this._tenants = e ?? [];
    });
  }
  /**
   * Opens the modal in "Create" mode (no tenant data passed)
   */
  async _openCreateModal() {
    this.consumeContext(J, (s) => {
      if (!s) return;
      s.open(this, "Prism.CreateTenantModal", {
        type: "sidebar",
        size: "small"
      }).onSubmit().then(() => {
        this._fetchTenants();
      }).catch(() => {
      });
    });
  }
  /**
   * Opens the modal in "Edit" mode (passes the tenant object)
   */
  async _editTenant(s) {
    this.consumeContext(J, (t) => {
      if (!t) return;
      t.open(this, "Prism.CreateTenantModal", {
        type: "sidebar",
        size: "small",
        data: { tenant: s }
        // Passing existing data triggers Edit mode in the modal
      }).onSubmit().then(() => {
        this._fetchTenants();
      });
    });
  }
  /**
   * Deletes a tenant by ID
   */
  async _deleteTenant(s) {
    confirm("Are you sure you want to delete this tenant? This cannot be undone.") && this.consumeContext(L, async (t) => {
      if (t === void 0) return;
      const e = await t.getLatestToken(), { error: i } = await K(
        this,
        B.delete({
          url: `/umbraco/management/api/v1/prism/tenants/${s}`,
          headers: { Authorization: `Bearer ${e}` }
        })
      );
      i || this._fetchTenants();
    });
  }
  render() {
    return b`
      <div class="dashboard-container">
        <uui-box headline="Prism Multi-Tenant Manager">
          
          <div slot="header-actions">
             <uui-button look="primary" color="positive" @click=${this._openCreateModal}>
                <uui-icon name="add"></uui-icon> Add New Tenant
             </uui-button>
          </div>

          <uui-table>
            <uui-table-column style="width: 20%"></uui-table-column>
            <uui-table-column style="width: 25%"></uui-table-column>
            <uui-table-column style="width: 10%"></uui-table-column>
            <uui-table-column style="width: 25%"></uui-table-column>
            <uui-table-column style="width: 20%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Name</uui-table-head-cell>
              <uui-table-head-cell>Hostname</uui-table-head-cell>
              <uui-table-head-cell>Color</uui-table-head-cell>
              <uui-table-head-cell>Entra Client ID</uui-table-head-cell>
              <uui-table-head-cell>Actions</uui-table-head-cell>
            </uui-table-head>

            ${this._tenants.map((s) => b`
              <uui-table-row>
                <uui-table-cell><strong>${s.name}</strong></uui-table-cell>
                <uui-table-cell><code>${s.hostname}</code></uui-table-cell>
                <uui-table-cell>
                    <div class="color-swatch" style="background:${s.themeColor}"></div>
                </uui-table-cell>
                <uui-table-cell>
                    ${s.entraClientId ? b`<uui-tag look="primary" color="positive">${s.entraClientId.substring(0, 8)}...</uui-tag>` : b`<uui-tag look="secondary">Not Configured</uui-tag>`}
                </uui-table-cell>
                <uui-table-cell>
                    <uui-button-group>
                        <uui-button look="outline" label="Edit" @click=${() => this._editTenant(s)}>
                            <uui-icon name="edit"></uui-icon>
                        </uui-button>
                        <uui-button color="danger" look="outline" label="Delete" @click=${() => this._deleteTenant(s.id)}>
                            <uui-icon name="delete"></uui-icon>
                        </uui-button>
                    </uui-button-group>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>

          ${this._tenants.length === 0 ? b`
            <p class="empty-state">No tenants found. Click "Add New Tenant" to get started.</p>
          ` : ""}

        </uui-box>
      </div>
    `;
  }
};
D.styles = ht`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    .dashboard-container {
      max-width: 1200px;
      margin: 0 auto;
    }

    .color-swatch {
      width: 24px;
      height: 24px;
      border-radius: 4px;
      border: 1px solid var(--uui-color-divider);
    }

    .empty-state {
      text-align: center;
      padding: 40px;
      color: var(--uui-color-text-alt);
    }

    uui-table-head-cell {
      font-weight: bold;
    }

    uui-button-group {
      display: flex;
    }

    code {
      background: var(--uui-color-surface-alt);
      padding: 2px 4px;
      border-radius: 4px;
    }
  `;
ft([
  y()
], D.prototype, "_tenants", 2);
D = ft([
  $t("prism-dashboard")
], D);
export {
  D as PrismDashboardElement
};
//# sourceMappingURL=prism-dashboard.js.map
