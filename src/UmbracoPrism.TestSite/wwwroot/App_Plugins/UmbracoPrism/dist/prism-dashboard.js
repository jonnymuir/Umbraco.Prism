import { UmbElementMixin as at } from "@umbraco-cms/backoffice/element-api";
import { UMB_MODAL_MANAGER_CONTEXT as _t } from "@umbraco-cms/backoffice/modal";
import { UMB_AUTH_CONTEXT as L } from "@umbraco-cms/backoffice/auth";
import { umbHttpClient as B } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as q } from "@umbraco-cms/backoffice/resources";
const H = globalThis, K = H.ShadowRoot && (H.ShadyCSS === void 0 || H.ShadyCSS.nativeShadow) && "adoptedStyleSheets" in Document.prototype && "replace" in CSSStyleSheet.prototype, V = /* @__PURE__ */ Symbol(), X = /* @__PURE__ */ new WeakMap();
let lt = class {
  constructor(t, e, i) {
    if (this._$cssResult$ = !0, i !== V) throw Error("CSSResult is not constructable. Use `unsafeCSS` or `css` instead.");
    this.cssText = t, this.t = e;
  }
  get styleSheet() {
    let t = this.o;
    const e = this.t;
    if (K && t === void 0) {
      const i = e !== void 0 && e.length === 1;
      i && (t = X.get(e)), t === void 0 && ((this.o = t = new CSSStyleSheet()).replaceSync(this.cssText), i && X.set(e, t));
    }
    return t;
  }
  toString() {
    return this.cssText;
  }
};
const ft = (n) => new lt(typeof n == "string" ? n : n + "", void 0, V), ht = (n, ...t) => {
  const e = n.length === 1 ? n[0] : t.reduce((i, s, r) => i + ((o) => {
    if (o._$cssResult$ === !0) return o.cssText;
    if (typeof o == "number") return o;
    throw Error("Value passed to 'css' function must be a 'css' function result: " + o + ". Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.");
  })(s) + n[r + 1], n[0]);
  return new lt(e, n, V);
}, bt = (n, t) => {
  if (K) n.adoptedStyleSheets = t.map((e) => e instanceof CSSStyleSheet ? e : e.styleSheet);
  else for (const e of t) {
    const i = document.createElement("style"), s = H.litNonce;
    s !== void 0 && i.setAttribute("nonce", s), i.textContent = e.cssText, n.appendChild(i);
  }
}, F = K ? (n) => n : (n) => n instanceof CSSStyleSheet ? ((t) => {
  let e = "";
  for (const i of t.cssRules) e += i.cssText;
  return ft(e);
})(n) : n;
const { is: yt, defineProperty: gt, getOwnPropertyDescriptor: vt, getOwnPropertyNames: At, getOwnPropertySymbols: Et, getPrototypeOf: wt } = Object, R = globalThis, Q = R.trustedTypes, Ct = Q ? Q.emptyScript : "", St = R.reactiveElementPolyfillSupport, x = (n, t) => n, I = { toAttribute(n, t) {
  switch (t) {
    case Boolean:
      n = n ? Ct : null;
      break;
    case Object:
    case Array:
      n = n == null ? n : JSON.stringify(n);
  }
  return n;
}, fromAttribute(n, t) {
  let e = n;
  switch (t) {
    case Boolean:
      e = n !== null;
      break;
    case Number:
      e = n === null ? null : Number(n);
      break;
    case Object:
    case Array:
      try {
        e = JSON.parse(n);
      } catch {
        e = null;
      }
  }
  return e;
} }, W = (n, t) => !yt(n, t), Y = { attribute: !0, type: String, converter: I, reflect: !1, useDefault: !1, hasChanged: W };
Symbol.metadata ??= /* @__PURE__ */ Symbol("metadata"), R.litPropertyMetadata ??= /* @__PURE__ */ new WeakMap();
let E = class extends HTMLElement {
  static addInitializer(t) {
    this._$Ei(), (this.l ??= []).push(t);
  }
  static get observedAttributes() {
    return this.finalize(), this._$Eh && [...this._$Eh.keys()];
  }
  static createProperty(t, e = Y) {
    if (e.state && (e.attribute = !1), this._$Ei(), this.prototype.hasOwnProperty(t) && ((e = Object.create(e)).wrapped = !0), this.elementProperties.set(t, e), !e.noAccessor) {
      const i = /* @__PURE__ */ Symbol(), s = this.getPropertyDescriptor(t, i, e);
      s !== void 0 && gt(this.prototype, t, s);
    }
  }
  static getPropertyDescriptor(t, e, i) {
    const { get: s, set: r } = vt(this.prototype, t) ?? { get() {
      return this[e];
    }, set(o) {
      this[e] = o;
    } };
    return { get: s, set(o) {
      const l = s?.call(this);
      r?.call(this, o), this.requestUpdate(t, l, i);
    }, configurable: !0, enumerable: !0 };
  }
  static getPropertyOptions(t) {
    return this.elementProperties.get(t) ?? Y;
  }
  static _$Ei() {
    if (this.hasOwnProperty(x("elementProperties"))) return;
    const t = wt(this);
    t.finalize(), t.l !== void 0 && (this.l = [...t.l]), this.elementProperties = new Map(t.elementProperties);
  }
  static finalize() {
    if (this.hasOwnProperty(x("finalized"))) return;
    if (this.finalized = !0, this._$Ei(), this.hasOwnProperty(x("properties"))) {
      const e = this.properties, i = [...At(e), ...Et(e)];
      for (const s of i) this.createProperty(s, e[s]);
    }
    const t = this[Symbol.metadata];
    if (t !== null) {
      const e = litPropertyMetadata.get(t);
      if (e !== void 0) for (const [i, s] of e) this.elementProperties.set(i, s);
    }
    this._$Eh = /* @__PURE__ */ new Map();
    for (const [e, i] of this.elementProperties) {
      const s = this._$Eu(e, i);
      s !== void 0 && this._$Eh.set(s, e);
    }
    this.elementStyles = this.finalizeStyles(this.styles);
  }
  static finalizeStyles(t) {
    const e = [];
    if (Array.isArray(t)) {
      const i = new Set(t.flat(1 / 0).reverse());
      for (const s of i) e.unshift(F(s));
    } else t !== void 0 && e.push(F(t));
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
    return bt(t, this.constructor.elementStyles), t;
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
    const i = this.constructor.elementProperties.get(t), s = this.constructor._$Eu(t, i);
    if (s !== void 0 && i.reflect === !0) {
      const r = (i.converter?.toAttribute !== void 0 ? i.converter : I).toAttribute(e, i.type);
      this._$Em = t, r == null ? this.removeAttribute(s) : this.setAttribute(s, r), this._$Em = null;
    }
  }
  _$AK(t, e) {
    const i = this.constructor, s = i._$Eh.get(t);
    if (s !== void 0 && this._$Em !== s) {
      const r = i.getPropertyOptions(s), o = typeof r.converter == "function" ? { fromAttribute: r.converter } : r.converter?.fromAttribute !== void 0 ? r.converter : I;
      this._$Em = s;
      const l = o.fromAttribute(e, r.type);
      this[s] = l ?? this._$Ej?.get(s) ?? l, this._$Em = null;
    }
  }
  requestUpdate(t, e, i, s = !1, r) {
    if (t !== void 0) {
      const o = this.constructor;
      if (s === !1 && (r = this[t]), i ??= o.getPropertyOptions(t), !((i.hasChanged ?? W)(r, e) || i.useDefault && i.reflect && r === this._$Ej?.get(t) && !this.hasAttribute(o._$Eu(t, i)))) return;
      this.C(t, e, i);
    }
    this.isUpdatePending === !1 && (this._$ES = this._$EP());
  }
  C(t, e, { useDefault: i, reflect: s, wrapped: r }, o) {
    i && !(this._$Ej ??= /* @__PURE__ */ new Map()).has(t) && (this._$Ej.set(t, o ?? e ?? this[t]), r !== !0 || o !== void 0) || (this._$AL.has(t) || (this.hasUpdated || i || (e = void 0), this._$AL.set(t, e)), s === !0 && this._$Em !== t && (this._$Eq ??= /* @__PURE__ */ new Set()).add(t));
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
        for (const [s, r] of this._$Ep) this[s] = r;
        this._$Ep = void 0;
      }
      const i = this.constructor.elementProperties;
      if (i.size > 0) for (const [s, r] of i) {
        const { wrapped: o } = r, l = this[s];
        o !== !0 || this._$AL.has(s) || l === void 0 || this.C(s, void 0, r, l);
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
E.elementStyles = [], E.shadowRootOptions = { mode: "open" }, E[x("elementProperties")] = /* @__PURE__ */ new Map(), E[x("finalized")] = /* @__PURE__ */ new Map(), St?.({ ReactiveElement: E }), (R.reactiveElementVersions ??= []).push("2.1.2");
const G = globalThis, tt = (n) => n, k = G.trustedTypes, et = k ? k.createPolicy("lit-html", { createHTML: (n) => n }) : void 0, ut = "$lit$", _ = `lit$${Math.random().toFixed(9).slice(2)}$`, ct = "?" + _, Tt = `<${ct}>`, g = document, P = () => g.createComment(""), N = (n) => n === null || typeof n != "object" && typeof n != "function", Z = Array.isArray, xt = (n) => Z(n) || typeof n?.[Symbol.iterator] == "function", j = `[ 	
\f\r]`, T = /<(?:(!--|\/[^a-zA-Z])|(\/?[a-zA-Z][^>\s]*)|(\/?$))/g, it = /-->/g, st = />/g, b = RegExp(`>|${j}(?:([^\\s"'>=/]+)(${j}*=${j}*(?:[^ 	
\f\r"'\`<>=]|("|')|))|$)`, "g"), nt = /'/g, rt = /"/g, dt = /^(?:script|style|textarea|title)$/i, Pt = (n) => (t, ...e) => ({ _$litType$: n, strings: t, values: e }), f = Pt(1), C = /* @__PURE__ */ Symbol.for("lit-noChange"), c = /* @__PURE__ */ Symbol.for("lit-nothing"), ot = /* @__PURE__ */ new WeakMap(), y = g.createTreeWalker(g, 129);
function pt(n, t) {
  if (!Z(n) || !n.hasOwnProperty("raw")) throw Error("invalid template strings array");
  return et !== void 0 ? et.createHTML(t) : t;
}
const Nt = (n, t) => {
  const e = n.length - 1, i = [];
  let s, r = t === 2 ? "<svg>" : t === 3 ? "<math>" : "", o = T;
  for (let l = 0; l < e; l++) {
    const a = n[l];
    let u, d, h = -1, $ = 0;
    for (; $ < a.length && (o.lastIndex = $, d = o.exec(a), d !== null); ) $ = o.lastIndex, o === T ? d[1] === "!--" ? o = it : d[1] !== void 0 ? o = st : d[2] !== void 0 ? (dt.test(d[2]) && (s = RegExp("</" + d[2], "g")), o = b) : d[3] !== void 0 && (o = b) : o === b ? d[0] === ">" ? (o = s ?? T, h = -1) : d[1] === void 0 ? h = -2 : (h = o.lastIndex - d[2].length, u = d[1], o = d[3] === void 0 ? b : d[3] === '"' ? rt : nt) : o === rt || o === nt ? o = b : o === it || o === st ? o = T : (o = b, s = void 0);
    const m = o === b && n[l + 1].startsWith("/>") ? " " : "";
    r += o === T ? a + Tt : h >= 0 ? (i.push(u), a.slice(0, h) + ut + a.slice(h) + _ + m) : a + _ + (h === -2 ? l : m);
  }
  return [pt(n, r + (n[e] || "<?>") + (t === 2 ? "</svg>" : t === 3 ? "</math>" : "")), i];
};
class O {
  constructor({ strings: t, _$litType$: e }, i) {
    let s;
    this.parts = [];
    let r = 0, o = 0;
    const l = t.length - 1, a = this.parts, [u, d] = Nt(t, e);
    if (this.el = O.createElement(u, i), y.currentNode = this.el.content, e === 2 || e === 3) {
      const h = this.el.content.firstChild;
      h.replaceWith(...h.childNodes);
    }
    for (; (s = y.nextNode()) !== null && a.length < l; ) {
      if (s.nodeType === 1) {
        if (s.hasAttributes()) for (const h of s.getAttributeNames()) if (h.endsWith(ut)) {
          const $ = d[o++], m = s.getAttribute(h).split(_), U = /([.?@])?(.*)/.exec($);
          a.push({ type: 1, index: r, name: U[2], strings: m, ctor: U[1] === "." ? Mt : U[1] === "?" ? Ut : U[1] === "@" ? Ht : z }), s.removeAttribute(h);
        } else h.startsWith(_) && (a.push({ type: 6, index: r }), s.removeAttribute(h));
        if (dt.test(s.tagName)) {
          const h = s.textContent.split(_), $ = h.length - 1;
          if ($ > 0) {
            s.textContent = k ? k.emptyScript : "";
            for (let m = 0; m < $; m++) s.append(h[m], P()), y.nextNode(), a.push({ type: 2, index: ++r });
            s.append(h[$], P());
          }
        }
      } else if (s.nodeType === 8) if (s.data === ct) a.push({ type: 2, index: r });
      else {
        let h = -1;
        for (; (h = s.data.indexOf(_, h + 1)) !== -1; ) a.push({ type: 7, index: r }), h += _.length - 1;
      }
      r++;
    }
  }
  static createElement(t, e) {
    const i = g.createElement("template");
    return i.innerHTML = t, i;
  }
}
function S(n, t, e = n, i) {
  if (t === C) return t;
  let s = i !== void 0 ? e._$Co?.[i] : e._$Cl;
  const r = N(t) ? void 0 : t._$litDirective$;
  return s?.constructor !== r && (s?._$AO?.(!1), r === void 0 ? s = void 0 : (s = new r(n), s._$AT(n, e, i)), i !== void 0 ? (e._$Co ??= [])[i] = s : e._$Cl = s), s !== void 0 && (t = S(n, s._$AS(n, t.values), s, i)), t;
}
class Ot {
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
    const { el: { content: e }, parts: i } = this._$AD, s = (t?.creationScope ?? g).importNode(e, !0);
    y.currentNode = s;
    let r = y.nextNode(), o = 0, l = 0, a = i[0];
    for (; a !== void 0; ) {
      if (o === a.index) {
        let u;
        a.type === 2 ? u = new M(r, r.nextSibling, this, t) : a.type === 1 ? u = new a.ctor(r, a.name, a.strings, this, t) : a.type === 6 && (u = new It(r, this, t)), this._$AV.push(u), a = i[++l];
      }
      o !== a?.index && (r = y.nextNode(), o++);
    }
    return y.currentNode = g, s;
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
  constructor(t, e, i, s) {
    this.type = 2, this._$AH = c, this._$AN = void 0, this._$AA = t, this._$AB = e, this._$AM = i, this.options = s, this._$Cv = s?.isConnected ?? !0;
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
    t = S(this, t, e), N(t) ? t === c || t == null || t === "" ? (this._$AH !== c && this._$AR(), this._$AH = c) : t !== this._$AH && t !== C && this._(t) : t._$litType$ !== void 0 ? this.$(t) : t.nodeType !== void 0 ? this.T(t) : xt(t) ? this.k(t) : this._(t);
  }
  O(t) {
    return this._$AA.parentNode.insertBefore(t, this._$AB);
  }
  T(t) {
    this._$AH !== t && (this._$AR(), this._$AH = this.O(t));
  }
  _(t) {
    this._$AH !== c && N(this._$AH) ? this._$AA.nextSibling.data = t : this.T(g.createTextNode(t)), this._$AH = t;
  }
  $(t) {
    const { values: e, _$litType$: i } = t, s = typeof i == "number" ? this._$AC(t) : (i.el === void 0 && (i.el = O.createElement(pt(i.h, i.h[0]), this.options)), i);
    if (this._$AH?._$AD === s) this._$AH.p(e);
    else {
      const r = new Ot(s, this), o = r.u(this.options);
      r.p(e), this.T(o), this._$AH = r;
    }
  }
  _$AC(t) {
    let e = ot.get(t.strings);
    return e === void 0 && ot.set(t.strings, e = new O(t)), e;
  }
  k(t) {
    Z(this._$AH) || (this._$AH = [], this._$AR());
    const e = this._$AH;
    let i, s = 0;
    for (const r of t) s === e.length ? e.push(i = new M(this.O(P()), this.O(P()), this, this.options)) : i = e[s], i._$AI(r), s++;
    s < e.length && (this._$AR(i && i._$AB.nextSibling, s), e.length = s);
  }
  _$AR(t = this._$AA.nextSibling, e) {
    for (this._$AP?.(!1, !0, e); t !== this._$AB; ) {
      const i = tt(t).nextSibling;
      tt(t).remove(), t = i;
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
  constructor(t, e, i, s, r) {
    this.type = 1, this._$AH = c, this._$AN = void 0, this.element = t, this.name = e, this._$AM = s, this.options = r, i.length > 2 || i[0] !== "" || i[1] !== "" ? (this._$AH = Array(i.length - 1).fill(new String()), this.strings = i) : this._$AH = c;
  }
  _$AI(t, e = this, i, s) {
    const r = this.strings;
    let o = !1;
    if (r === void 0) t = S(this, t, e, 0), o = !N(t) || t !== this._$AH && t !== C, o && (this._$AH = t);
    else {
      const l = t;
      let a, u;
      for (t = r[0], a = 0; a < r.length - 1; a++) u = S(this, l[i + a], e, a), u === C && (u = this._$AH[a]), o ||= !N(u) || u !== this._$AH[a], u === c ? t = c : t !== c && (t += (u ?? "") + r[a + 1]), this._$AH[a] = u;
    }
    o && !s && this.j(t);
  }
  j(t) {
    t === c ? this.element.removeAttribute(this.name) : this.element.setAttribute(this.name, t ?? "");
  }
}
class Mt extends z {
  constructor() {
    super(...arguments), this.type = 3;
  }
  j(t) {
    this.element[this.name] = t === c ? void 0 : t;
  }
}
class Ut extends z {
  constructor() {
    super(...arguments), this.type = 4;
  }
  j(t) {
    this.element.toggleAttribute(this.name, !!t && t !== c);
  }
}
class Ht extends z {
  constructor(t, e, i, s, r) {
    super(t, e, i, s, r), this.type = 5;
  }
  _$AI(t, e = this) {
    if ((t = S(this, t, e, 0) ?? c) === C) return;
    const i = this._$AH, s = t === c && i !== c || t.capture !== i.capture || t.once !== i.once || t.passive !== i.passive, r = t !== c && (i === c || s);
    s && this.element.removeEventListener(this.name, this, i), r && this.element.addEventListener(this.name, this, t), this._$AH = t;
  }
  handleEvent(t) {
    typeof this._$AH == "function" ? this._$AH.call(this.options?.host ?? this.element, t) : this._$AH.handleEvent(t);
  }
}
class It {
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
const kt = G.litHtmlPolyfillSupport;
kt?.(O, M), (G.litHtmlVersions ??= []).push("3.3.2");
const Dt = (n, t, e) => {
  const i = e?.renderBefore ?? t;
  let s = i._$litPart$;
  if (s === void 0) {
    const r = e?.renderBefore ?? null;
    i._$litPart$ = s = new M(t.insertBefore(P(), r), r, void 0, e ?? {});
  }
  return s._$AI(n), s;
};
const J = globalThis;
class w extends E {
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
    return C;
  }
}
w._$litElement$ = !0, w.finalized = !0, J.litElementHydrateSupport?.({ LitElement: w });
const Rt = J.litElementPolyfillSupport;
Rt?.({ LitElement: w });
(J.litElementVersions ??= []).push("4.2.2");
const $t = (n) => (t, e) => {
  e !== void 0 ? e.addInitializer(() => {
    customElements.define(n, t);
  }) : customElements.define(n, t);
};
const zt = { attribute: !0, type: String, converter: I, reflect: !1, hasChanged: W }, jt = (n = zt, t, e) => {
  const { kind: i, metadata: s } = e;
  let r = globalThis.litPropertyMetadata.get(s);
  if (r === void 0 && globalThis.litPropertyMetadata.set(s, r = /* @__PURE__ */ new Map()), i === "setter" && ((n = Object.create(n)).wrapped = !0), r.set(e.name, n), i === "accessor") {
    const { name: o } = e;
    return { set(l) {
      const a = t.get.call(this);
      t.set.call(this, l), this.requestUpdate(o, a, n, !0, l);
    }, init(l) {
      return l !== void 0 && this.C(o, void 0, n, l), l;
    } };
  }
  if (i === "setter") {
    const { name: o } = e;
    return function(l) {
      const a = this[o];
      t.call(this, l), this.requestUpdate(o, a, n, !0, l);
    };
  }
  throw Error("Unsupported decorator location: " + i);
};
function Lt(n) {
  return (t, e) => typeof e == "object" ? jt(n, t, e) : ((i, s, r) => {
    const o = s.hasOwnProperty(r);
    return s.constructor.createProperty(r, i), o ? Object.getOwnPropertyDescriptor(s, r) : void 0;
  })(n, t, e);
}
function v(n) {
  return Lt({ ...n, state: !0, attribute: !1 });
}
var Bt = Object.defineProperty, qt = Object.getOwnPropertyDescriptor, A = (n, t, e, i) => {
  for (var s = i > 1 ? void 0 : i ? qt(t, e) : t, r = n.length - 1, o; r >= 0; r--)
    (o = n[r]) && (s = (i ? o(t, e, s) : o(s)) || s);
  return i && s && Bt(t, e, s), s;
};
let p = class extends at(w) {
  constructor() {
    super(...arguments), this._activeTab = "general", this._name = "", this._hostname = "", this._entraTenantId = "", this._entraClientId = "", this._secretKeyName = "";
  }
  async _handleSubmit() {
    if (!this._name || !this._hostname) {
      this._activeTab = "general";
      return;
    }
    const n = {
      name: this._name,
      hostname: this._hostname,
      themeColor: "#3544b1",
      entraTenantId: this._entraTenantId,
      entraClientId: this._entraClientId,
      secretKeyName: this._secretKeyName
    };
    this.consumeContext(L, async (t) => {
      if (!t) return;
      const e = await t.getLatestToken(), { error: i } = await q(
        this,
        B.post({
          url: "/umbraco/management/api/v1/prism/tenants",
          body: n,
          headers: { Authorization: `Bearer ${e}` }
        })
      );
      i || this.modalContext?.submit();
    });
  }
  _renderGeneralTab() {
    return f`
      <div role="tabpanel" id="general-panel" aria-labelledby="general-tab" class="tab-content">
        <uui-box>
          <div class="field">
            <uui-label for="tenant-name">Tenant Name</uui-label>
            <uui-input 
              id="tenant-name" 
              label="Tenant Name" 
              .value=${this._name} 
              @input=${(n) => this._name = n.target.value}
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
              @input=${(n) => this._hostname = n.target.value}
              required>
            </uui-input>
          </div>
        </uui-box>
      </div>
    `;
  }
  _renderIdentityTab() {
    return f`
      <div role="tabpanel" id="identity-panel" aria-labelledby="identity-tab" class="tab-content">
        <uui-box>
          <p class="description">Configure Microsoft Entra ID integration. Branding is managed in the Azure Portal.</p>
          
          <div class="field">
            <uui-label for="tenant-id">Directory (Tenant) ID</uui-label>
            <uui-input 
              id="tenant-id" 
              label="Directory ID" 
              .value=${this._entraTenantId} 
              @input=${(n) => this._entraTenantId = n.target.value}>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="client-id">Application (Client) ID</uui-label>
            <uui-input 
              id="client-id" 
              label="Client ID" 
              .value=${this._entraClientId} 
              @input=${(n) => this._entraClientId = n.target.value}>
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="secret-name">Key Vault Secret Name</uui-label>
            <uui-input 
              id="secret-name" 
              label="Secret Name" 
              .value=${this._secretKeyName} 
              @input=${(n) => this._secretKeyName = n.target.value}>
            </uui-input>
            <small id="secret-hint">Must match the secret identifier in your configured Azure Key Vault.</small>
          </div>
        </uui-box>
      </div>
    `;
  }
  render() {
    return f`
      <uui-dialog-layout headline="New Tenant Registration">
        
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
        <uui-button slot="actions" look="primary" color="positive" @click=${this._handleSubmit}>Create Tenant</uui-button>
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
    }

    small { 
      display: block; 
      margin-top: var(--uui-size-space-2); 
      color: var(--uui-color-text-alt); 
    }
  `;
A([
  v()
], p.prototype, "_activeTab", 2);
A([
  v()
], p.prototype, "_name", 2);
A([
  v()
], p.prototype, "_hostname", 2);
A([
  v()
], p.prototype, "_entraTenantId", 2);
A([
  v()
], p.prototype, "_entraClientId", 2);
A([
  v()
], p.prototype, "_secretKeyName", 2);
p = A([
  $t("prism-create-tenant-modal")
], p);
var Kt = Object.defineProperty, Vt = Object.getOwnPropertyDescriptor, mt = (n, t, e, i) => {
  for (var s = i > 1 ? void 0 : i ? Vt(t, e) : t, r = n.length - 1, o; r >= 0; r--)
    (o = n[r]) && (s = (i ? o(t, e, s) : o(s)) || s);
  return i && s && Kt(t, e, s), s;
};
console.log("Modal element loaded:", p);
let D = class extends at(w) {
  constructor() {
    super(...arguments), this._tenants = [];
  }
  async _openCreateModal() {
    this.consumeContext(_t, (n) => {
      if (!n) return;
      n.open(this, "Prism.CreateTenantModal", {
        type: "sidebar",
        size: "small"
      }).onSubmit().then(() => {
        this._fetchTenants();
      }).catch(() => {
      });
    });
  }
  async connectedCallback() {
    super.connectedCallback(), this._fetchTenants();
  }
  async _fetchTenants() {
    this.consumeContext(L, async (n) => {
      if (!n) return;
      const t = await n.getLatestToken(), { data: e, error: i } = await q(
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
  async _deleteTenant(n) {
    confirm("Are you sure you want to delete this tenant?") && this.consumeContext(L, async (t) => {
      if (t === void 0) return;
      const e = await t.getLatestToken(), { error: i } = await q(
        this,
        B.delete({
          url: `/umbraco/management/api/v1/prism/tenants/${n}`,
          headers: { Authorization: `Bearer ${e}` }
        })
      );
      i || this._fetchTenants();
    });
  }
  render() {
    return f`
      <div class="dashboard-container">
        <uui-box headline="Prism Multi-Tenant Manager">
          
          <div slot="header-actions">
             <uui-button look="primary" color="positive" @click=${this._openCreateModal}>
                Add New Tenant
             </uui-button>
          </div>

          <uui-table>
            <uui-table-column style="width: 25%"></uui-table-column>
            <uui-table-column style="width: 25%"></uui-table-column>
            <uui-table-column style="width: 10%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>
            <uui-table-column style="width: 10%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Name</uui-table-head-cell>
              <uui-table-head-cell>Hostname</uui-table-head-cell>
              <uui-table-head-cell>Color</uui-table-head-cell>
              <uui-table-head-cell>Entra Client ID</uui-table-head-cell>
              <uui-table-head-cell>Actions</uui-table-head-cell>
            </uui-table-head>

            ${this._tenants.map((n) => f`
              <uui-table-row>
                <uui-table-cell>${n.name}</uui-table-cell>
                <uui-table-cell><code>${n.hostname}</code></uui-table-cell>
                <uui-table-cell>
                    <div class="color-swatch" style="background:${n.themeColor}"></div>
                </uui-table-cell>
                <uui-table-cell>
                    ${n.entraClientId ? f`<uui-tag look="primary" color="positive">${n.entraClientId.substring(0, 8)}...</uui-tag>` : f`<uui-tag look="secondary">Not Set</uui-tag>`}
                </uui-table-cell>
                <uui-table-cell>
                    <uui-button color="danger" look="outline" @click=${() => this._deleteTenant(n.id)}>
                        <uui-icon name="delete"></uui-icon> Delete
                    </uui-button>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>

          ${this._tenants.length === 0 ? f`
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
  `;
mt([
  v()
], D.prototype, "_tenants", 2);
D = mt([
  $t("prism-dashboard")
], D);
export {
  D as PrismDashboardElement
};
//# sourceMappingURL=prism-dashboard.js.map
