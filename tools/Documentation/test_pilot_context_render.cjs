// DOM substitute, not browser automation or a visual-layout check.
const assert = require('node:assert/strict');
const vm = require('node:vm');
class Element {
  constructor(tag='div'){this.tagName=tag;this.children=[];this._text='';this.hidden=false;this.dataset={};}
  set textContent(value){this._text=String(value);this.children=[];}
  get textContent(){return this._text+this.children.map(c=>c.textContent||'').join('');}
  append(...nodes){this.children.push(...nodes);}
  replaceChildren(...nodes){this.children=nodes;this._text='';}
}
(async()=>{
  const url=process.argv[2]||'http://127.0.0.1:8781';
  const session=await (await fetch(url+'/api/session')).json();
  const js=await (await fetch(url+'/app.js')).text();
  new vm.Script(js); // Syntax-check the entire delivered script.
  const elements=new Map();
  const get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id);};
  const answerBox=new Element();
  const context=vm.createContext({document:{getElementById:get,createElement:t=>new Element(t),
    querySelector:()=>answerBox,querySelectorAll:()=>[]},
    appendNameRole:()=>{}, actualSession:session});
  vm.runInContext(js.slice(0,js.indexOf('\nfunction renderInputs()'))+'\nsession=actualSession;',context);
  vm.runInContext(js.slice(js.indexOf('function renderAuthoredAnswer(row)')),context);
  let events=0, backgrounds=0, answerContexts=0;
  const descendants=node=>[node,...node.children.flatMap(descendants)];
  for(const row of session.rows){
    context.view=row.presentation;
    vm.runInContext('renderPresentation(view)',context);
    assert.equal(get('inputFacts').children.length,row.presentation.events.length);
    assert.equal(get('inputBackground').children.length,row.presentation.characterContext.length);
    assert.equal(get('inputUnknown').children.length,row.presentation.unknownFacts.length);
    assert(!get('inputBackground').textContent.includes('현행 정책 미평가'));
    assert(!get('inputBackground').textContent.includes('수집 당시:'));
    assert(!get('inputFacts').children.some(c=>/인물 기본정보/.test(c.textContent)));
    events+=get('inputFacts').children.length;backgrounds+=get('inputBackground').children.length;
    context.row=row;
    vm.runInContext('renderAuthoredAnswer(row)',context);
    for(const node of descendants(answerBox)){
      if(node.children[0]?.textContent==='인물 기본정보·배경 (사건 아님)'){
        assert(!node.children.some(c=>c.textContent.startsWith('중요도')));
        answerContexts++;
      }
    }
  }
  assert(answerContexts>0);
  console.log(JSON.stringify({requests:session.rows.length,events,backgrounds,answerContexts,domRendering:'PASS',visualLayout:'NOT_RUN'}));
})().catch(error=>{console.error(error);process.exitCode=1;});
