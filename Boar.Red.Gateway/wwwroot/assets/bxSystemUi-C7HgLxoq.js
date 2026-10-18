import{B as o}from"./bxRect-CmNzrDr2.js";import{B as i}from"./bxUiConstants-FYGHusxE.js";var a;(s=>{(d=>{class n{static getScreenBounds(){return new o(0,0,window.innerWidth,window.innerHeight)}static getSystemStatusBarFrame(){return o.Zero}static getSystemMenuButtonFrame(){const e=i.TopNavigationBarHeight-i.Spacing.Normalx2,t=(i.TopNavigationBarHeight-e)/2;return new o(window.innerWidth,t,0,e)}static _systemBottomUnsafeAreaFrame;static getSystemBottomUnsafeAreaFrame(){if(!n._systemBottomUnsafeAreaFrame){let e=0;const t=document.createElement("div");t.setAttribute("aria-hidden","true"),t.style.cssText=`
				position: fixed;
				left: 0;
				bottom: 0;
				width: 0;
				height: 0;
				visibility: hidden;
				pointer-events: none;
			`,t.style.setProperty("padding-bottom","env(safe-area-inset-bottom, 0px)","important"),(document.body??document.documentElement).appendChild(t);try{const r=window.getComputedStyle(t).paddingBottom,m=Number.parseFloat(r);Number.isFinite(m)&&(e=Math.min(Math.max(m,0),window.innerHeight))}finally{t.remove()}n._systemBottomUnsafeAreaFrame=new o(0,window.innerHeight-e,window.innerWidth,e)}return n._systemBottomUnsafeAreaFrame}}d.BxSystemUi=n})(s.BxWebUi||(s.BxWebUi={}))})(a||(a={}));const c=a.BxWebUi.BxSystemUi;export{c as B};
